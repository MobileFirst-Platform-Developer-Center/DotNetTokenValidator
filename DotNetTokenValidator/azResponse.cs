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
using System.Net;
using System.Text;

namespace DotNetTokenValidator
{
    public class AzResponse
    {
        public string username { get; set; }
        public string scope { get; set; }
        public string client_id { get; set; }
        public bool isActive { get; set; }
        
        public AzResponse(HttpWebResponse input)
        {
            string strInput;

            // Convert input to string
            var encoding = ASCIIEncoding.ASCII;
            using (var reader = new System.IO.StreamReader(input.GetResponseStream(), encoding))
            {
                strInput = reader.ReadToEnd();
            }

            try
            {
                JToken token = JObject.Parse(strInput);
                username = (string)token.SelectToken("username");
                scope = (string)token.SelectToken("scope");
                client_id = (string)token.SelectToken("client_id");
                isActive = (bool)token.SelectToken("active");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
