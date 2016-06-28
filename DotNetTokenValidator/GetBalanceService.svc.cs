//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Net;
//using System.ServiceModel.Web;
//using System.Text;
//using System.Web;

using System;

namespace DotNetTokenValidator
{
    public class GetBalanceService : IGetBalanceService
    {
 
        //***************************************
        // getBalance
        //***************************************
        public string getBalance()
        {
            Console.WriteLine("getBalance()");
            return "19938.80";
        }
    }
}
