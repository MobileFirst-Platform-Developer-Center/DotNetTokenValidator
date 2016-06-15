using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace DotNetTokenValidator
{
    public class azResponse
    {
        public string username { get; set; }
        public string scope { get; set; }
        public string client_id { get; set; }
        public Boolean isActive { get; set; }

        public azResponse(HttpWebResponse input)
        {
            string strInput;

            // Convert input to string
            var encoding = ASCIIEncoding.ASCII;
            using (var reader = new System.IO.StreamReader(input.GetResponseStream(), encoding))
            {
                strInput = reader.ReadToEnd();
            }
            Debug.WriteLine("[azResponse] strInput. {0}", strInput);

            // Assign values
            username = getPropertyValueAsString(strInput, "username");
            scope = getPropertyValueAsString(strInput, "scope");
            client_id = getPropertyValueAsString(strInput, "client_id");
            isActive = getPropertyValueAsBool(strInput, "active");
        }

        private string getPropertyValueAsString(string jsonString, string propertyName)
        {
            string returnVal = null;
            try
            {
                JToken token = JObject.Parse(jsonString);
                returnVal = (string)token.SelectToken(propertyName);
            }
            catch (WebException authHeaderScopeExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeExc);
            }
            catch (JsonReaderException authHeaderScopeJSONExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeJSONExc);
            }
            return returnVal;
        }

        private Boolean getPropertyValueAsBool(string jsonString, string propertyName)
        {
            Boolean returnVal = false;
            try
            {
                JToken token = JObject.Parse(jsonString);
                returnVal = (Boolean)token.SelectToken(propertyName);
            }
            catch (WebException authHeaderScopeExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeExc);
            }
            catch (JsonReaderException authHeaderScopeJSONExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeJSONExc);
            }
            return returnVal;
        }

        /*private Object getPropertyValue(string jsonString, string propertyName, string objType)
        {
            Object returnVal;
            if (objType == "Boolean") {returnVal = false;}
            else {returnVal = null;}
            
            try
            {
                JToken token = JObject.Parse(jsonString);
                returnVal = (Object)token.SelectToken(propertyName);
            }
            catch (WebException authHeaderScopeExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeExc);
            }
            catch (JsonReaderException authHeaderScopeJSONExc)
            {
                Debug.WriteLine("[azResponse->getPropertyValue] Could not parse data. {0}", authHeaderScopeJSONExc);
            }
            return returnVal;
        }*/
    }
}
