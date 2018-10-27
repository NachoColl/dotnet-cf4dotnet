using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Newtonsoft.Json;

namespace MyDemoProject {

    public class Lambdas
    {       
        /* A function that will get Lambda resources created (only) */
        [Cloudformation4dotNET.Lambda.LambdaResourceProperties(TimeoutInSeconds=5)]
        public void Echo(Object Input, ILambdaContext Context) => Context?.Logger?.Log(JsonConvert.SerializeObject(Input));
    }
}
