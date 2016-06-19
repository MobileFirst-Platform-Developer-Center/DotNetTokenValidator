using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading.Tasks;

namespace DotNetTokenValidator.Inspector
{
    public class MyCustomBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            //Console.WriteLine("In {0}.{1}", this.GetType().Name, MethodBase.GetCurrentMethod().Name);
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            //Console.WriteLine("In {0}.{1}", this.GetType().Name, MethodBase.GetCurrentMethod().Name);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            //Console.WriteLine("In {0}.{1}", this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new MyInspector());
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            //Console.WriteLine("In {0}.{1}", this.GetType().Name, MethodBase.GetCurrentMethod().Name);
        }
    }

    public class MyCustomBehaviorExtension : BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(MyCustomBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new MyCustomBehavior();
        }
    }
}
