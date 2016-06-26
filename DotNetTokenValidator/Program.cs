using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace DotNetTokenValidator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create the ServiceHost.
            using (ServiceHost host = new ServiceHost(typeof(GetBalanceService)))
            {
                // Enable metadata publishing.
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;

                Console.WriteLine("The service is ready at {0}", host.BaseAddresses[0]);
                host.Open();

                Console.WriteLine("Press <Enter> to stop the service.");
                Console.ReadLine();

                // Close the ServiceHost.
                host.Close();
            }
        }

    }
}
