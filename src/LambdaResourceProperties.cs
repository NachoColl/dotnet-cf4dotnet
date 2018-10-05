using System;

namespace AWSCloudFormationGenerator.Lambda
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class LambdaResourceProperties : Attribute {

        public int TimeoutInSeconds { get; set;}  = 20;
        
    }
}
