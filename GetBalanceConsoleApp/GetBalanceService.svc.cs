using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using System.Text;
using System.Web;

namespace GetBalanceConsoleApp
{
    public class GetBalanceService : IGetBalanceService
    {
        private string azServerBaseURL = "http://9.148.225.228:9080/mfp/api";
        private string myToken = null;

        public object HttpContext { get; internal set; }

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

        //***************************************
        // preProcess
        //***************************************
        private Boolean preProcess(OutgoingWebResponseContext response, string authHeader)
        {
            // No Authorization header
            if (authHeader == null || authHeader == "")
            {
                response.StatusCode = HttpStatusCode.Unauthorized;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer");
                //returnVal = false;
                return false;
            }

            // Authorization header does not start with "Bearer"
            if (!authHeader.StartsWith("Bearer", StringComparison.CurrentCulture))
            {
                response.StatusCode = HttpStatusCode.Unauthorized;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer");
                return false;
            }
            return true;
        }

        //***************************************
        // postProcess
        //***************************************
        private Boolean postProcess(OutgoingWebResponseContext response, HttpWebResponse currentResponse, string scope)
        {
            // Check Conflict response (409)
            if (currentResponse.StatusCode == HttpStatusCode.Conflict)
            {
                response.StatusCode = HttpStatusCode.Conflict;
                response.Headers.Add(currentResponse.Headers);
                return false;
            }

            // Create an object from the response
            azResponse azResp = new azResponse(currentResponse);

            // Check if active == false
            if (!azResp.isActive)
            {
                response.StatusCode = HttpStatusCode.Unauthorized;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"invalid_token\"");
                return false;
            }

            // Check scope
            if (!azResp.scope.Contains(scope))
            {
                response.StatusCode = HttpStatusCode.Forbidden;
                response.Headers.Add(HttpResponseHeader.WwwAuthenticate, "Bearer error=\"insufficient_scope\", scope=\"" + scope + "\"");
                return false;
            }
            return true;
        }

        //***************************************
        // isValidRequest
        //***************************************
        private Boolean isValidRequest(IncomingWebRequestContext request, OutgoingWebResponseContext response, string scope)
        {
            string authHeader = request.Headers[HttpRequestHeader.Authorization];
            string authHeaderWithoutBearer = null;

            // Auth header exist + starts with "Bearer"
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
                Debug.WriteLine("Could not extract the Bearer from authHeader using substring. {0}", ex);
                return false;
            }

            // Check client auth header against mfp auth server using the token I received in previous step
            HttpWebResponse currentResponse = introspectClientRequest(authHeaderWithoutBearer);

            // Check Conflict response (409) + active==true + scope
            postProcess(response, currentResponse, scope);

            return true;
        }

        //***************************************
        // GetBalance
        //***************************************
        public string GetBalance(string id)
        {
            string scope = "abc";

            IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
            OutgoingWebResponseContext response = WebOperationContext.Current.OutgoingResponse;
            if (isValidRequest(request, response, scope))
            {
                return "balance: 7.814";
            }
            return "You are not authorized!";
        }
    }
}
