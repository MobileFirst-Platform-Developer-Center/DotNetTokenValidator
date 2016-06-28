using Newtonsoft.Json.Linq;
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
        private const string azServerBaseURL = "http://9.148.225.196:9080/mfp/api/az/v1/";
        private const string scope = "accessRestricted";
        private static string filterIntrospectionToken = null;
        private const string filterUserName = "externalResource"; // Confidential Client Username
        private const string filterPassword = "abcd!234";  // Confidential Client Secret

        //*************************************************************************************
        // sendRequest
        // - a helper method that makes a post request to MFP server.
        //   it is being used by the getToken() and introspectClientRequest() methods
        //*************************************************************************************
        private HttpWebResponse sendRequest(Dictionary<string, string> postParameters, string endPoint, string authHeader)
        {
            string postData = "";
            foreach (string key in postParameters.Keys)
            {
                postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
            }

            // ********************** Put /az/v1 as class member
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new System.Uri(azServerBaseURL + endPoint));
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add(HttpRequestHeader.Authorization, authHeader);

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
        private string getToken()
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

            // EXtract the access_token from the response
            try
            {
                HttpWebResponse resp = sendRequest(postParameters, "token", "Basic " + Base64Credentials);
                Stream dataStream = resp.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                strResponse = reader.ReadToEnd();

                JToken token = JObject.Parse(strResponse);
                returnVal = (string)token.SelectToken("access_token");
            }
            catch (WebException authHeaderScopeExc)
            {
                Debug.WriteLine("Could not get data. {0}", authHeaderScopeExc);
            }
            catch (Newtonsoft.Json.JsonReaderException authHeaderScopeJSONExc)
            {
                Debug.WriteLine("Could not get data. {0}", authHeaderScopeJSONExc);
            }

            return returnVal;
        }

        //*************************************************************************************
        // introspectClientRequest
        // - This method is responsible for sending the client token to MFP Auth Server
        //   using the message inspector token in the request header 
        //*************************************************************************************
        private HttpWebResponse introspectClientRequest(string clientToken)
        {
            // Prepare Post Data
            Dictionary<string, string> postParameters = new Dictionary<string, string> { };
            postParameters.Add("token", clientToken);

            return sendRequest(postParameters, "introspection", "Bearer " + filterIntrospectionToken);
        }

        //******************************************************
        // flushResponse
        // - This is a helper method for sending headers
        //   back to the client application.
        //******************************************************
        private void flushResponse()
        {
            HttpContext.Current.Response.Flush();
            HttpContext.Current.Response.SuppressContent = true; //Prevent sending content - only headers will be sent
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        //*************************************************************************************
        // preProcess
        // - This method contains the initial checks of the client request. 
        //   1. If the authentication header is empty
        //   2. If the authentication hader does not start with "Bearer"
        //*************************************************************************************
        private void preProcess(OutgoingWebResponseContext response, string authenticationHeader)
        {
            Console.WriteLine("preProcess");
            // No Authorization header
            if (string.IsNullOrEmpty(authenticationHeader))
            {
                Console.WriteLine("preProcess()->authHeader is empty");
                response.StatusCode = HttpStatusCode.Unauthorized;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer");
                flushResponse();
            }

            // Authorization header does not start with "Bearer"
            else if (!authenticationHeader.StartsWith("Bearer", StringComparison.CurrentCulture))
            {
                Console.WriteLine("preProcess()->authHeader not starting with Bearer");
                response.StatusCode = HttpStatusCode.Unauthorized;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer");
                flushResponse();
            }
        }

        //*************************************************************************************
        // postProcess
        // - This method performs the final checks of the client request. It is being called 
        //   after the inspector received a token from MFP server and submitted a request
        //   to the introspection endpoint. This method checks the following:
        //   First it makes sure that we did not receive a Conflict response (409), 
        //   then it examines 2 elements from the response:
        //   1. that active==true
        //   2. that scope contains the right scope
        //*************************************************************************************
        private void postProcess(OutgoingWebResponseContext response, HttpWebResponse currentResponse, string scope, Message request)
        {
            Console.WriteLine("postProcess");
            // Check Conflict response (409)
            if (currentResponse.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("postProcess()->Conflict response (409)");
                response.StatusCode = HttpStatusCode.Conflict;
                response.Headers.Add(currentResponse.Headers);
                flushResponse();
            }

            // Check if filterToken has expired (401) - if so we should obtain a new token and run validateRequest() again
            else if (currentResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                filterIntrospectionToken = null;
                validateRequest(request);
                return; // stops the current instance of validateRequest
            }

            // Make sure that HttpStatusCode = 200 ok (before checking active==true & scope)
            else if (currentResponse.StatusCode == HttpStatusCode.OK)
            {
                // Create an object from the response
                azResponse azResp = new azResponse(currentResponse);

                // Check if active == false
                if (!azResp.isActive)
                {
                    Console.WriteLine("postProcess()->active==false");
                    response.StatusCode = HttpStatusCode.Unauthorized;
                    response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"invalid_token\"");
                    flushResponse();
                }

                // Check scope
                else if (!azResp.scope.Contains(scope))
                {
                    Console.WriteLine("postProcess()->response doesn't include the requested scope");
                    response.StatusCode = HttpStatusCode.Forbidden;
                    response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"insufficient_scope\", scope=\"" + scope + "\"");
                    flushResponse();
                }
            }
            else
            {
                throw new WebFaultException<string>("Authentication did not succeed, Please try again...", HttpStatusCode.BadRequest);
            }            
        }

        //*************************************************************************************
        // validateRequest
        // - This is the heart of the message inspector. It is called from 
        //   AfterReceiveRequest() and initialize the validation process.
        //*************************************************************************************
        private void validateRequest(Message request)
        {
            Console.WriteLine("validateRequest");
            string authHeader = null;
            string authHeaderWithoutBearer = null;

            var httpRequest = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];
            authHeader = httpRequest.Headers[HttpRequestHeader.Authorization];

            OutgoingWebResponseContext response = WebOperationContext.Current.OutgoingResponse;

            preProcess(response, authHeader);

            // Get token as the resource filter from mfp auth server
            if (filterIntrospectionToken == null)
            {
                filterIntrospectionToken = getToken();
            }

            // Extract the Authorization header "Bearer <token>"
            try
            {
                authHeaderWithoutBearer = authHeader.Substring("Bearer ".Length);
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine("Could not extract the Bearer from authHeader using substring. {0}", ex);
                preProcess(response, authHeader);
            }

            // Check client auth header against mfp auth server using the token I received in previous step
            HttpWebResponse currentResponse = introspectClientRequest(authHeaderWithoutBearer);

            postProcess(response, currentResponse, scope, request);
        }

        //**********************************************************
        // AfterReceiveRequest (Message Inspector Implementation Method)
        //**********************************************************
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            validateRequest(request);
            return null;
        }

        //**********************************************************
        // BeforeSendReply (Message Inspector Implementation Method)
        //**********************************************************
        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            
        }
    }
}
