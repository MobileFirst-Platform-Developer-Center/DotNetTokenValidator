using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main()
        {
            GetBalanceServiceClient client = new GetBalanceServiceClient();

            // Use the 'client' variable to call operations on the service.
            Console.WriteLine("Balance is : "+client.GetBalance());
            // Always close the client.
            client.Close();
        }
    }
}
