using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Newtonsoft.Json;

namespace demo {

    public class Lambdas
    {
        
        /* A function that will get Lambda resources created (only) */
        [Cloudformation4dotNET.Lambda.LambdaResourceProperties(TimeoutInSeconds=2)]
        public void Echo(string Input, ILambdaContext Context) => Context?.Logger?.Log(Input.ToUpper());
    }
}
