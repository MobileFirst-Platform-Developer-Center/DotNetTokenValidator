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


﻿using Newtonsoft.Json;
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
            //mfp_application - build json
            //mfp_device - build json
            //mfp_user - build json
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
    }
}
