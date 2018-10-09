using System;

namespace Cloudformation4dotNET.APIGateway
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class APIGatewayResourceProperties : Lambda.LambdaResourceProperties
    {

        public string PathPart { get ; set; } = ""; 

        public bool EnableCORS {get; set;} = false;

        public APIGatewayResourceProperties(string PathPart){
            this.PathPart = PathPart;
        }
    }
}
