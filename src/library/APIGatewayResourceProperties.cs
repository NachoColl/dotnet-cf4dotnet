using System;

namespace Cloudformation4dotNET.APIGateway
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class APIGatewayResourceProperties : Cloudformation4dotNET.Lambda.LambdaResourceProperties
    {

        public string PathPart { get ; set; } = ""; 

        public bool EnableCORS {get; set;} = false;

        public bool APIKeyRequired {get; set;} = false;

        public string Autorizer { get; set; } = "";

        public APIGatewayResourceProperties(string PathPart){
            this.PathPart = PathPart;
        }
    }
}
