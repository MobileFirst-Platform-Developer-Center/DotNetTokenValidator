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
