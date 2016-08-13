// Copyright 2016 IBM Corp.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Web;

namespace DotNetTokenValidator
{
    public class MyInspector : IDispatchMessageInspector
    {
        private const string azServerBaseURL = "http://MFP-SERVER-URL:9080/mfp/api/az/v1/";
        private const string scope = "accessRestricted";
        private static string filterIntrospectionToken = null;
        private const string filterUserName = "CONFIDENTIAL-CLIENT-USERNAME";
        private const string filterPassword = "CONFIDENTIAL-CLIENT-SECRET";

        //*************************************************************************************
        // sendRequest
        // - a helper method that makes a post request to MFP server.
        //*************************************************************************************
        private HttpWebResponse sendRequest(Dictionary<string, string> postParameters, string endPoint, string authHeaderValue)
        {
            string postData = string.Empty;
            foreach (string key in postParameters.Keys)
            {
                postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new System.Uri(azServerBaseURL + endPoint));
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add(HttpRequestHeader.Authorization, authHeaderValue);

            // Attach Post Data
            byte[] data = Encoding.ASCII.GetBytes(postData);
            request.ContentLength = data.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(data, 0, data.Length);
            dataStream.Close();

            return (HttpWebResponse)request.GetResponse();
        }

        //****************************************************************************************
        // getToken
        // - This method is responsible for obtaining an access token for the message inspector
        //   from MFP Authentication Server.
        //****************************************************************************************
        private string getIntrospectionToken()
        {
            Console.WriteLine("getToken()");

            string returnVal = null;
            string strResponse = null;

            string Base64Credentials = Convert.ToBase64String(
                System.Text.ASCIIEncoding.ASCII.GetBytes(
                    string.Format("{0}:{1}", filterUserName, filterPassword)
                )
            );

            // Prepare Post Data
            Dictionary<string, string> postParameters = new Dictionary<string, string> { };
            postParameters.Add("grant_type", "client_credentials");
            postParameters.Add("scope", "authorization.introspect");

            try
            {
                HttpWebResponse resp = sendRequest(postParameters, "token", "Basic " + Base64Credentials);
                Stream dataStream = resp.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                strResponse = reader.ReadToEnd();

                JToken token = JObject.Parse(strResponse);
                returnVal = (string)token.SelectToken("access_token");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return returnVal;
        }

        //*************************************************************************************
        // introspectClientRequest
        // - This method is responsible for sending the client token to MFP Auth Server
        //   using the message inspector token in the request header
        //*************************************************************************************
        private HttpWebResponse IntrospectClientToken(string clientToken)
        {
            Console.WriteLine("IntrospectClientToken()");

            // Prepare Post Data
            Dictionary<string, string> postParameters = new Dictionary<string, string> { };
            postParameters.Add("token", clientToken);

            return sendRequest(postParameters, "introspection", "Bearer " + filterIntrospectionToken);
        }

        //*************************************************************************************
        // ReturnErrorResponse
        // - A helper method that receives an HttpStatusCode and a WebHeaderCollection
        //   and handles the response submission to the client application.
        //   it also ends the current request.
        //*************************************************************************************
        private void ReturnErrorResponse(HttpStatusCode httpStatusCode, WebHeaderCollection headers)
        {
            OutgoingWebResponseContext outgoingResponse = WebOperationContext.Current.OutgoingResponse;
            outgoingResponse.StatusCode = httpStatusCode;
            outgoingResponse.Headers.Add(headers);
            HttpContext.Current.Response.Flush();
            HttpContext.Current.Response.SuppressContent = true; //Prevent sending content - only headers will be sent
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        //*************************************************************************************
        // GetClientTokenFromHeader
        // - This method contains the initial checks of the client request:
        //   1. If the authentication header is empty
        //   2. If the authentication hader does not start with "Bearer "
        //*************************************************************************************
        private string GetClientTokenFromHeader(Message request)
        {
            Console.WriteLine("GetClientTokenFromHeader()");
            string token = null;
            string authHeader = null;

            var httpRequest = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];
            authHeader = httpRequest.Headers[HttpRequestHeader.Authorization];

            if ((string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer", StringComparison.CurrentCulture)))
            {
                Console.WriteLine("Client Authorization Header is empty or does not start with Bearer");
                WebHeaderCollection webHeaderCollection = new WebHeaderCollection();
                webHeaderCollection.Add(HttpResponseHeader.WwwAuthenticate, "Bearer");
                ReturnErrorResponse(HttpStatusCode.Unauthorized, webHeaderCollection);
            }

            try
            {
                token = authHeader.Substring("Bearer ".Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return token;
        }

        //*************************************************************************************
        // postProcess
        //   First we make sure that we received 200 OK response from MFP Auth Server.
        //   If we received 409 response (Conflict) we forward the response to the client application.
        //   After that we examine the response:
        //   2. that active==true
        //   3. that scope contains the right scope
        //*************************************************************************************
        private void postProcess(HttpWebResponse introspectionResponse)
        {
            Console.WriteLine("postProcess()");

            if (introspectionResponse.StatusCode != HttpStatusCode.OK) // Make sure that HttpStatusCode = 200 ok (before checking active==true & scope)
            {
                if (introspectionResponse.StatusCode == HttpStatusCode.Unauthorized) // We have a real problem since we already obtained a new token
                {
                    throw new WebFaultException<string>("Authentication did not succeed, Please try again...", HttpStatusCode.BadRequest);
                }
                else if (introspectionResponse.StatusCode == HttpStatusCode.Conflict) // Check Conflict response (409)
                {
                    ReturnErrorResponse(HttpStatusCode.Conflict, introspectionResponse.Headers);
                }
                else
                {
                    throw new WebFaultException<string>("Authentication did not succeed, Please try again...", HttpStatusCode.BadRequest);
                }
            }
            else
            {
                AzResponse azResp = new AzResponse(introspectionResponse); // Create an object from the response
                WebHeaderCollection webHeaderCollection = new WebHeaderCollection();

                if (!azResp.isActive)
                {
                    Console.WriteLine("postProcess()->active==false");
                    webHeaderCollection.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"invalid_token\"");
                    ReturnErrorResponse(HttpStatusCode.Unauthorized, webHeaderCollection);
                }
                else if (!azResp.scope.Contains(scope))
                {
                    Console.WriteLine("postProcess()->response doesn't include the requested scope");
                    webHeaderCollection.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"insufficient_scope\", scope=\"" + scope + "\"");
                    ReturnErrorResponse(HttpStatusCode.Forbidden, webHeaderCollection);
                }
            }
        }

        //*************************************************************************************
        // validateRequest
        // - This is the heart of the message inspector. It is called from
        //   AfterReceiveRequest() and initialize the validation process.
        //*************************************************************************************
        private void validateRequest(Message request)
        {
            Console.WriteLine("\nvalidateRequest()");

            // Extract the clientToken out of the request, check it is not empty and that it starts with "Bearer"
            string clientToken = GetClientTokenFromHeader(request);


            if (filterIntrospectionToken == null)
            {
                filterIntrospectionToken = getIntrospectionToken(); // Get token as the resource filter from mfp auth server
            }

            // Check client auth header against mfp auth server using the token I received in previous step
            HttpWebResponse introspectionResponse = IntrospectClientToken(clientToken);

            // Check if introspectionToken has expired (401)
            // - if so we should obtain a new token and resend the client request
            if (introspectionResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                filterIntrospectionToken = getIntrospectionToken();
                introspectionResponse = IntrospectClientToken(clientToken);
            }

            // Check that the MFP Authrorization server response is valid and includes the requested scope
            postProcess(introspectionResponse);
        }

        //**********************************************************
        // AfterReceiveRequest (Filter implementation Method)
        //**********************************************************
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            validateRequest(request);
            return null;
        }

        //**********************************************************
        // BeforeSendReply (Filter implementation Method)
        //**********************************************************
        public void BeforeSendReply(ref Message reply, object correlationState)
        {

        }
    }
}
