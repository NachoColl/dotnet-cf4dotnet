using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Xunit;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using MyDemoProject;

namespace MyTests {
    
    public class LambdaTests {
        Lambdas myLambdas= new Lambdas ();

        TestLambdaContext context = new TestLambdaContext (){ Logger=new TestLambdaLogger() };

        [Fact]
        public void Echo_SimpleTest () {

            myLambdas.Echo(Input: "Hello World!", Context: context);       
            Assert.Contains("Hello World!", ((TestLambdaLogger)context.Logger).Buffer.ToString());
         
        }

    }
}