using System;

namespace Cloudformation4dotNET.Lambda
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class LambdaResourceProperties : Attribute {

        public int TimeoutInSeconds { get; set;}  = 20;
        
    }
}
