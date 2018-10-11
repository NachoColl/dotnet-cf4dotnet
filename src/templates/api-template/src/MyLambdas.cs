using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Newtonsoft.Json;

namespace MyAPI {

    public class Lambdas
    {
        
        /* A function that will get Lambda resources created (only) */
        [Cloudformation4dotNET.Lambda.LambdaResourceProperties(TimeoutInSeconds=20)]
        public void Echo(string Input, ILambdaContext Context) => 
        #if DEBUG 
            /* for local testing */
            Context.Logger.Log(Input.ToUpper()); 
        #else  
            LambdaLogger.Log(Input.ToUpper());
        #endif       
    }
}