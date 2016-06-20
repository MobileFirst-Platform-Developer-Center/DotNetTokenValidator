using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Remoting.Contexts;
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
        private static string azServerBaseURL = "http://9.148.225.70:9080/mfp/api";
        private static string scope = "abc";
        private static string myToken = null;

        //***************************************
        // sendRequest
        //***************************************
        private HttpWebResponse sendRequest(Dictionary<string, string> postParameters, string endPoint, string authHeader)
        {
            string postData = "";
            foreach (string key in postParameters.Keys)
            {
                postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new System.Uri(azServerBaseURL + "/az/v1/" + endPoint));
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

        //***************************************
        // getToken
        //***************************************
        private string getToken()
        {
            Console.WriteLine("getToken()");
            string returnVal = null;

            string strResponse = null;
            string myUserName = "externalResource"; // Confidential Client Username
            string myPassword = "abcd!234";  // Confidential Client Password

            string Base64Credentials = Convert.ToBase64String(
               System.Text.ASCIIEncoding.ASCII.GetBytes(
                        string.Format("{0}:{1}", myUserName, myPassword)
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
            }
            catch (WebException ex)
            {
                returnVal = string.Format("Could not get data. {0}", ex);
            }

            // EXtract the access_token from the response
            try
            {
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

        //***************************************
        // introspectClientRequest
        //***************************************
        private HttpWebResponse introspectClientRequest(string clientToken)
        {
            // Prepare Post Data
            Dictionary<string, string> postParameters = new Dictionary<string, string> { };
            postParameters.Add("token", clientToken);

            return sendRequest(postParameters, "introspection", "Bearer " + myToken);
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

        //***************************************
        // preProcess
        //***************************************
        private void preProcess(OutgoingWebResponseContext response, string authenticationHeader)
        {
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

        //***************************************
        // postProcess
        //***************************************
        private void postProcess(OutgoingWebResponseContext response, HttpWebResponse currentResponse, string scope)
        {
            // Check Conflict response (409)
            if (currentResponse.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("postProcess()->Conflict response (409)");
                response.StatusCode = HttpStatusCode.Conflict;
                response.Headers.Add(currentResponse.Headers);
                flushResponse();
            }

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
            if (!azResp.scope.Contains(scope))
            {
                Console.WriteLine("postProcess()->response doesn't include the requested scope");
                response.StatusCode = HttpStatusCode.Forbidden;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"insufficient_scope\", scope=\"" + scope + "\"");
                flushResponse();
            }
        }

        //**********************************************************
        // AfterReceiveRequest (Filter implementation)
        //**********************************************************
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            string authHeader = null;
            string authHeaderWithoutBearer = null;

            var httpRequest = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];
            authHeader = httpRequest.Headers[HttpRequestHeader.Authorization];

            OutgoingWebResponseContext response = WebOperationContext.Current.OutgoingResponse;

            preProcess(response, authHeader);

            // Get token as the resource filter from mfp auth server
            if (myToken == null)
            {
                myToken = getToken();
            }

            // Extract the Authorization header "Bearer <token>"
            try
            {
                authHeaderWithoutBearer = authHeader.Substring("Bearer ".Length);
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine("Could not extract the Bearer from authHeader using substring. {0}", ex);
            }

            // Check client auth header against mfp auth server using the token I received in previous step
            HttpWebResponse currentResponse = introspectClientRequest(authHeaderWithoutBearer);

            postProcess(response, currentResponse, scope);

            return null;
        }

        //**********************************************************
        // BeforeSendReply (Filter implementation)
        //**********************************************************
        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            
        }
    }
}
