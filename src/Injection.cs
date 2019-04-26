using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using Microsoft.Extensions.CommandLineUtils;

namespace Cloudformation4dotNET
{
    class Injection
    {
        internal class ResourceProperties : APIGateway.APIGatewayResourceProperties
        {
            public string MethodClassPath;
            public string MethodName;

            public ResourceProperties(string PathPart) : base(PathPart)
            {
            }
        }

        static void Main(string[] args)
        {
            try
            {

                var app = new CommandLineApplication();
                app.Name = "cf4dotNet";
                app.Command("api", config =>
                {
                    config.Description = "Creates an API Gateway Cloudformation template from your dotNet code.";
                    config.HelpOption("-? | -h | --help"); //show help on --help

                    var dllSourceFile = config.Argument("source", "Your dotnet dll source file full path (e.g. E:/src/my-dotnet-api.dll).", false);
                    var environments = (new List<string> { "dev", "test", "qa", "staging", "prod" });

                    // optionals
                    var apiName = config.Option("-n|--name <api-name>", "API Name (default: 'api').", CommandOptionType.SingleValue);
                    var apiRate = config.Option("-r|--rate <api-rate>", "API Rate Limit (default: '100').", CommandOptionType.SingleValue);
                    var apiBurst = config.Option("-rb|--burst <api-rate-burst>", "API Rate Burst (default: '200').", CommandOptionType.SingleValue);

                    var environmentKey = config.Option("-e|--environment <dev/test/qa/staging/prod>", "Environment (default: 'prod').", CommandOptionType.SingleValue);
                    var accountsConfiguration = config.Option("-c|--configuration <2-accounts>", "Accounts configuration (default: '2-accounts').", CommandOptionType.SingleValue);
                    var buildVersion = config.Option("-b|--build <build-version>", "Build version number used to create incremental resources (default: '1').", CommandOptionType.SingleValue);
                    var outputPah = config.Option("-o|--ouput <output-path>", "Cloudformation templates will get created here (default: '.').", CommandOptionType.SingleValue);
                    var lambdasPrefix = config.Option("-p|--prefix <functions-prefix>", "Llambdas prefix code (default: 'myapi-').", CommandOptionType.SingleValue);

                    config.OnExecute(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(dllSourceFile.Value))
                        {
                            if ((accountsConfiguration.HasValue() && environmentKey.Values[0] != "2-accounts") ||
                                (environmentKey.HasValue() && !environments.Contains(environmentKey.Values[0])))
                            {
                                app.ShowHelp();
                                return -1;
                            }
                            else
                                return api(dllSourceFile.Value,
                                        environmentKey.HasValue() ? environmentKey.Values[0] : "prod",
                                        buildVersion.HasValue() ? int.Parse(buildVersion.Values[0]) : 1,
                                        outputPah.HasValue() ? outputPah.Values[0] : "./",
                                        lambdasPrefix.HasValue() ? lambdasPrefix.Values[0] : "myapi-",
                                        apiName.HasValue() ? apiName.Values[0] : "api",
                                        apiRate.HasValue() ? apiRate.Values[0] : "100",
                                        apiBurst.HasValue() ? apiBurst.Values[0] : "200");
                        }
                        else
                        {
                            app.ShowHelp();
                            return -1;
                        }
                    });
                });

                //give people help with --help
                app.HelpOption("-? | -h | --help");
                var result = app.Execute(args);
                Environment.Exit(result);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(-1);
            }
        }

        static int api(string dllSourceFile, string environmentKey = "prod", int buildVersion = 1, string outputPah = "./", string prefix = "myapi-", string name = "api", string rate = "100", string burst = "200")
        {

            try
            {

                /*
                    We will create new templates from:
                 */
                string samFile = string.Format("{0}/sam.yml", outputPah);
                string samBaseFile = string.Format("{0}/sam-base.yml", outputPah);

                /* Load your code assembly */
                AppDomain.CurrentDomain.AssemblyResolve += CheckLoaded;
                Assembly assembly = Assembly.LoadFrom(dllSourceFile);
                string assemblyName = GetAssemblyName(dllSourceFile);

                // Build the cloudformation API Gateway related resources string to inject (including lambdas).
                List<ResourceProperties> APIFunctionsList = GetAPIFunctions(assembly);
                string cloudformationAPIResources = GetCloudformationAPIResourcesString(assemblyName, APIFunctionsList, environmentKey, prefix, name, rate, burst);

                // Build the cloudformation Lambdas related resources string to inject.
                List<ResourceProperties> LambdaFunctionsList = GetLambdaFunctions(assembly, assemblyName);
                string cloudformationLambdaResources = GetCloudformationLambdaResourcesString(assemblyName, LambdaFunctionsList, environmentKey, prefix);

                string source = System.IO.File.ReadAllText(samFile);
                if (File.Exists(samBaseFile)) File.Delete(samBaseFile);
                using (FileStream fs = System.IO.File.Create(samBaseFile))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(source.Replace("@INJECT", " INJECTED CODE:\n\n" + cloudformationAPIResources + cloudformationLambdaResources + IndentText(1, "# END of injected code")));
                    fs.Write(info, 0, info.Length);
                }


                /*
                    Inject lambdas versions.
                */

                string samXFile = string.Format("{0}/samx.yml", outputPah);
                string samEnvironmentFile = string.Format("{0}/sam-{1}.yml", outputPah, environmentKey);

                // Build the cloudformation lambda's versions resources string to inject.      
                string cloudformationLambdasVersionsResources =
                    AppendTitle("Lambdas versions") +
                    GetCloudformationLambdasVersionsResourcesString(APIFunctionsList, environmentKey, buildVersion, prefix) +
                    GetCloudformationLambdasVersionsResourcesString(LambdaFunctionsList, environmentKey, buildVersion, prefix);

                string sourceX = System.IO.File.ReadAllText(samXFile);
                if (File.Exists(samEnvironmentFile)) File.Delete(samEnvironmentFile);
                using (FileStream fs = System.IO.File.Create(samEnvironmentFile))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(sourceX.Replace("@INJECT", " INJECTED CODE:\n\n" + cloudformationLambdasVersionsResources + IndentText(1, "# END of injected code")));
                    fs.Write(info, 0, info.Length);
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected error running 'cf4dotnet api':");
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        static List<ResourceProperties> GetAPIFunctions(Assembly Assembly)
        {
            List<ResourceProperties> functionsList = new List<ResourceProperties>();
            foreach (Type type in Assembly.GetTypes())
            {
                foreach (MethodInfo methodInfo in type.GetMethods()
                            .Where(m => !typeof(object)
                            .GetMethods()
                            .Select(me => me.Name)
                            .Contains(m.Name)))
                {

                    APIGateway.APIGatewayResourceProperties apiGatewayProperties =
                        (APIGateway.APIGatewayResourceProperties)methodInfo.GetCustomAttribute(typeof(APIGateway.APIGatewayResourceProperties));
                    if (apiGatewayProperties != null)
                        functionsList.Add(new ResourceProperties(apiGatewayProperties?.PathPart ?? methodInfo.Name) { MethodClassPath = methodInfo.DeclaringType.FullName, MethodName = methodInfo.Name, APIKeyRequired = apiGatewayProperties.APIKeyRequired, Autorizer = apiGatewayProperties.Autorizer, EnableCORS = apiGatewayProperties.EnableCORS, TimeoutInSeconds = apiGatewayProperties.TimeoutInSeconds });
                }
            }
            return functionsList;
        }

        static List<ResourceProperties> GetLambdaFunctions(Assembly Assembly, string AssemblyName)
        {
            List<ResourceProperties> functionsList = new List<ResourceProperties>();
            foreach (Type type in Assembly.GetTypes())
            {
                foreach (MethodInfo methodInfo in type.GetMethods()
                            .Where(m => !typeof(object)
                            .GetMethods()
                            .Select(me => me.Name)
                            .Contains(m.Name)))
                {

                    Lambda.LambdaResourceProperties lambdaProperties =
                        (Lambda.LambdaResourceProperties)methodInfo.GetCustomAttribute(typeof(Lambda.LambdaResourceProperties));
                    if (lambdaProperties != null && !(lambdaProperties is APIGateway.APIGatewayResourceProperties))
                        functionsList.Add(new ResourceProperties(null) { MethodClassPath = methodInfo.DeclaringType.FullName, MethodName = methodInfo.Name, TimeoutInSeconds = lambdaProperties.TimeoutInSeconds, VPCSecurityGroupIdsParameterName = lambdaProperties.VPCSecurityGroupIdsParameterName, VPCSubnetIdsParameterName = lambdaProperties.VPCSubnetIdsParameterName });
                }
            }
            return functionsList;
        }

        static string GetCloudformationAPIResourcesString(string AssemblyName, List<ResourceProperties> functions, string Environment, string NamePrefix, string ApiName, string ApiRate, string ApiBurst)
        {

            StringBuilder cloudformationResources = new StringBuilder();

            cloudformationResources.Append(AppendTitle("API Gateway"));

            cloudformationResources.AppendLine(IndentText(1, "myAPI:"));
            cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::RestApi"));
            cloudformationResources.AppendLine(IndentText(2, "Properties:"));
            cloudformationResources.AppendLine(IndentText(3, string.Format("Name: {0}-{1}", ApiName, Environment)));
            cloudformationResources.AppendLine(IndentText(3, "EndpointConfiguration:"));
            cloudformationResources.AppendLine(IndentText(4, "Types:"));
            cloudformationResources.AppendLine(IndentText(5, "- REGIONAL"));

            cloudformationResources.AppendLine();

            cloudformationResources.AppendLine(IndentText(1, "myAPIUsagePlan:"));
            cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::UsagePlan"));
            cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
            cloudformationResources.AppendLine(IndentText(3, "- myAPI"));
            cloudformationResources.AppendLine(IndentText(3, string.Format("- {0}", FirstCharToUpper(Environment))));
            cloudformationResources.AppendLine(IndentText(2, "Properties:"));
            cloudformationResources.AppendLine(IndentText(3, "ApiStages:"));
            cloudformationResources.AppendLine(IndentText(4, "- ApiId: !Ref myAPI"));
            cloudformationResources.AppendLine(IndentText(4, string.Format("  Stage: !Ref {0}", FirstCharToUpper(Environment))));
            cloudformationResources.AppendLine(IndentText(3, string.Format("Description: {0} usage plan.", FirstCharToUpper(Environment))));
            cloudformationResources.AppendLine(IndentText(3, "Throttle:"));
            cloudformationResources.AppendLine(IndentText(4, string.Format("BurstLimit: {0}", ApiRate)));
            cloudformationResources.AppendLine(IndentText(4, string.Format("RateLimit: {0}", ApiBurst)));
            cloudformationResources.AppendLine(IndentText(3, string.Format("UsagePlanName: {0}-{1}-usageplan", ApiName, Environment)));

            cloudformationResources.AppendLine();

            // create the authorizers
            cloudformationResources.Append(AppendTitle("API Gateway authorizers"));

            List<string> authorizers = new List<string>();
            foreach (ResourceProperties function in functions)
            {
                if (function.Autorizer.Length > 0)
                {
                    if (!authorizers.Contains(function.Autorizer))
                        authorizers.Add(function.Autorizer);
                }
            }

            foreach (string authorizer in authorizers)
            {
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Authorizer:", ReplaceNonAlphanumeric(authorizer))));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Authorizer"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, "Type: COGNITO_USER_POOLS"));
                cloudformationResources.AppendLine(IndentText(3, "Name: " + authorizer));
                cloudformationResources.AppendLine(IndentText(3, "IdentitySource:  method.request.header.Authorization"));
                cloudformationResources.AppendLine(IndentText(3, "ProviderARNs:"));
                cloudformationResources.AppendLine(IndentText(4, "- !Sub \"arn:aws:cognito-idp:${AWS::Region}:${AWS::AccountId}:userpool/" + authorizer + "\""));
                cloudformationResources.AppendLine(IndentText(3, "RestApiId:"));
                cloudformationResources.AppendLine(IndentText(4, "!Ref myAPI"));
                cloudformationResources.AppendLine();
            }

            // create the root paths  
            cloudformationResources.Append(AppendTitle("API Gateway root paths"));
            List<string> rootPaths = new List<string>();
            foreach (ResourceProperties function in functions)
            {
                string[] pathParts = function.PathPart.Split("/");
                if (!rootPaths.Contains(pathParts[0]))
                {
                    rootPaths.Add(pathParts[0]);
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIResource:", ReplaceNonAlphanumeric(pathParts.Count() == 1 ? function.MethodName : pathParts[0]))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Resource"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, "ParentId: !GetAtt myAPI.RootResourceId"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("PathPart: {0}", pathParts[0])));
                    cloudformationResources.AppendLine();
                }
            }

            cloudformationResources.Append(AppendTitle("API Gateway methods"));

            foreach (ResourceProperties function in functions)
            {

                cloudformationResources.AppendLine(IndentText(1, "# " + function.MethodName));

                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Function:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Serverless::Function"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: {0}{1}", NamePrefix, function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Handler: {0}::{1}::{2} ", AssemblyName, function.MethodClassPath, function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, "Role: !GetAtt myAPILambdaExecutionRole.Arn"));
                cloudformationResources.AppendLine(IndentText(3, "Timeout: " + function.TimeoutInSeconds));
                // VPC Settings
                if (function.VPCSecurityGroupIdsParameterName.Length > 0 && function.VPCSubnetIdsParameterName.Length > 0)
                {
                    cloudformationResources.AppendLine(IndentText(3, "VpcConfig:"));
                    cloudformationResources.AppendLine(IndentText(4, String.Format("SecurityGroupIds: !Ref {0} ", function.VPCSecurityGroupIdsParameterName)));
                    cloudformationResources.AppendLine(IndentText(4, String.Format("SubnetIds: !Ref {0} ", function.VPCSubnetIdsParameterName)));
                }

                cloudformationResources.AppendLine();

                // to create 2nd level paths (e.g. utils/ip/)
                // TODO: allow >2 levels!
                string[] pathParts = function.PathPart.Split("/");
                if (pathParts.Count() > 1)
                {
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIResource:", ReplaceNonAlphanumeric(pathParts[0] + pathParts[1]))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Resource"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, "ParentId: !Ref " + String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts[0]))));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("PathPart: {0}", pathParts[1])));
                    cloudformationResources.AppendLine();
                }

                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIMethod:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Method"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("ResourceId: !Ref {0}", String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts.Count() == 1 ? function.MethodName : pathParts[0] + pathParts[1])))));

                if (function.APIKeyRequired)
                    cloudformationResources.AppendLine(IndentText(3, "ApiKeyRequired: true"));

                cloudformationResources.AppendLine(IndentText(3, "HttpMethod: POST"));

                if (function.Autorizer.Length > 0)
                {
                    cloudformationResources.AppendLine(IndentText(3, "AuthorizationType: COGNITO_USER_POOLS"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("AuthorizerId: !Ref {0}Authorizer", ReplaceNonAlphanumeric(function.Autorizer))));
                }
                else
                    cloudformationResources.AppendLine(IndentText(3, "AuthorizationType: NONE"));

                cloudformationResources.AppendLine(IndentText(3, "Integration:"));
                cloudformationResources.AppendLine(IndentText(4, "Type: AWS_PROXY"));
                cloudformationResources.AppendLine(IndentText(4, "IntegrationHttpMethod: POST"));
                cloudformationResources.AppendLine(IndentText(4, "Uri: !Sub \"arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${" + functionCFResourceName + "Function.Arn}:${!stageVariables.lambdaAlias}/invocations\""));
                cloudformationResources.AppendLine(IndentText(4, "Credentials: !GetAtt myAPILambdaExecutionRole.Arn"));

                /* already added when using lambda proxy integration
                if (function.Autorizer.Length > 0){
                    cloudformationResources.AppendLine(IndentText(4, "PassthroughBehavior: WHEN_NO_TEMPLATES")); 
                    cloudformationResources.AppendLine(IndentText(4, "RequestTemplates:"));
                    cloudformationResources.AppendLine(IndentText(5, "application/json: |")); 
                    cloudformationResources.AppendLine(IndentText(5, "    {"));
                    cloudformationResources.AppendLine(IndentText(5, "    \"cognito\": {\"sub\" : \"$context.authorizer.claims.sub\",\"email\" : \"$context.authorizer.claims.email\"},"));
                    cloudformationResources.AppendLine(IndentText(5, "    \"body\": $input.json('$')"));
                    cloudformationResources.AppendLine(IndentText(5, "    }"));
                }
                */

                cloudformationResources.AppendLine();

                if (function.EnableCORS)
                {
                    #region ENABLE CORS
                    cloudformationResources.AppendLine(IndentText(1, "# enabling OPTIONS for " + function.MethodName));
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIMethodOPTIONS:", functionCFResourceName)));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Method"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("ResourceId: !Ref {0}", String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts.Count() == 1 ? function.MethodName : pathParts[0] + pathParts[1])))));
                    cloudformationResources.AppendLine(IndentText(3, "HttpMethod: OPTIONS"));
                    cloudformationResources.AppendLine(IndentText(3, "AuthorizationType: NONE"));
                    cloudformationResources.AppendLine(IndentText(3, "Integration:"));
                    cloudformationResources.AppendLine(IndentText(4, "Type: MOCK"));
                    cloudformationResources.AppendLine(IndentText(4, "IntegrationResponses:"));
                    cloudformationResources.AppendLine(IndentText(5, "-  ResponseParameters:"));
                    cloudformationResources.AppendLine(IndentText(6, "  method.response.header.Access-Control-Allow-Headers: \"'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token,IdentityPoolId,IdentityId'\""));
                    cloudformationResources.AppendLine(IndentText(6, "  method.response.header.Access-Control-Allow-Methods: \"'POST,OPTIONS'\""));
                    cloudformationResources.AppendLine(IndentText(6, "  method.response.header.Access-Control-Allow-Origin: \"'*'\""));
                    cloudformationResources.AppendLine(IndentText(5, "   ResponseTemplates:"));
                    cloudformationResources.AppendLine(IndentText(6, "  application/json: ''"));
                    cloudformationResources.AppendLine(IndentText(5, "   StatusCode: '200'"));
                    cloudformationResources.AppendLine(IndentText(4, "PassthroughBehavior: NEVER"));
                    cloudformationResources.AppendLine(IndentText(4, "RequestTemplates:"));
                    cloudformationResources.AppendLine(IndentText(5, "application/json: '{\"statusCode\": 200}'"));
                    cloudformationResources.AppendLine(IndentText(3, "MethodResponses:"));
                    cloudformationResources.AppendLine(IndentText(4, "-  ResponseModels:"));
                    cloudformationResources.AppendLine(IndentText(5, "  application/json: Empty"));
                    cloudformationResources.AppendLine(IndentText(4, "   ResponseParameters:"));
                    cloudformationResources.AppendLine(IndentText(5, "  method.response.header.Access-Control-Allow-Headers: true"));
                    cloudformationResources.AppendLine(IndentText(5, "  method.response.header.Access-Control-Allow-Methods: true"));
                    cloudformationResources.AppendLine(IndentText(5, "  method.response.header.Access-Control-Allow-Origin: true"));
                    cloudformationResources.AppendLine(IndentText(4, "   StatusCode: '200'"));
                    cloudformationResources.AppendLine();
                    #endregion
                }

            }

            cloudformationResources.Append(AppendTitle("API Gateway stages"));

            switch (Environment.ToLower())
            {

                default:

                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}:", FirstCharToUpper(Environment))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, string.Format("StageName: {0}", Environment)));
                    cloudformationResources.AppendLine(IndentText(3, string.Format("Description: API {0}", FirstCharToUpper(Environment))));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("DeploymentId: !Ref {0}Deployment", FirstCharToUpper(Environment))));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, String.Format("lambdaAlias: {0}", Environment)));

                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Deployment:", FirstCharToUpper(Environment))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach (ResourceProperties function in functions)
                    {
                        string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                        cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }
                    cloudformationResources.AppendLine();
                    break;

                case "staging": // STAGING and PROD are contained into the same environment.
                case "prod":


                    // StagingDeployment
                    cloudformationResources.AppendLine(IndentText(1, "Staging:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "StageName: staging"));
                    cloudformationResources.AppendLine(IndentText(3, "Description: API Staging"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, "DeploymentId: !Ref StagingDeployment"));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, "lambdaAlias: staging"));

                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "StagingDeployment:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach (ResourceProperties function in functions)
                    {
                        string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                        cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }

                    // ProdDeployment
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "Prod:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "StageName: prod"));
                    cloudformationResources.AppendLine(IndentText(3, "Description: API Production"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(3, "DeploymentId: !Ref ProdDeployment"));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, "lambdaAlias: prod"));

                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "ProdDeployment:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref myAPI"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach (ResourceProperties function in functions)
                    {
                        string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                        cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }
                    cloudformationResources.AppendLine();
                    break;

            }

            return cloudformationResources.ToString();
        }

        static string GetCloudformationLambdaResourcesString(string AssemblyName, List<ResourceProperties> functions, string Environment, string NamePrefix)
        {
            // the cloudformation deployment resrouces.
            StringBuilder cloudformationResources = new StringBuilder();
            cloudformationResources.Append(AppendTitle("Standalone Lambdas"));

            foreach (ResourceProperties function in functions)
            {

                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);

                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Function:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Serverless::Function"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: {0}{1}", NamePrefix, function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Handler: {0}::{1}::{2} ", AssemblyName, function.MethodClassPath, function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, "Role: !GetAtt myAPILambdaExecutionRole.Arn"));
                cloudformationResources.AppendLine(IndentText(3, "Timeout: " + function.TimeoutInSeconds));
                // VPC Settings
                if (function.VPCSecurityGroupIdsParameterName.Length > 0 && function.VPCSubnetIdsParameterName.Length > 0)
                {
                    cloudformationResources.AppendLine(IndentText(3, "VpcConfig:"));
                    cloudformationResources.AppendLine(IndentText(4, String.Format("SecurityGroupIds: !Ref {0} ", function.VPCSecurityGroupIdsParameterName)));
                    cloudformationResources.AppendLine(IndentText(4, String.Format("SubnetIds: !Ref {0} ", function.VPCSubnetIdsParameterName)));
                }
                cloudformationResources.AppendLine();

            }

            return cloudformationResources.ToString();
        }

        static string GetCloudformationLambdasVersionsResourcesString(List<ResourceProperties> functions, string Environment, int BuildVersion, string NamePrefix)
        {

            StringBuilder cloudformationResources = new StringBuilder();

            // staging and prod use the same AWS ACCOUNT.
            int environmentVersion = Environment.Equals("staging") ? BuildVersion + (BuildVersion - 1) : BuildVersion * 2;

            foreach (ResourceProperties function in functions)
            {

                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);

                cloudformationResources.AppendLine(IndentText(1, String.Format("# {0}Version{1} lambda resources (version + alias)", functionCFResourceName, environmentVersion)));

                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Version{1}:", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Lambda::Version"));
                cloudformationResources.AppendLine(IndentText(2, "DeletionPolicy: Retain"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: {0}{1}", NamePrefix, function.MethodName)));

                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Alias:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Lambda::Alias"));
                cloudformationResources.AppendLine(IndentText(2, "DeletionPolicy: Retain"));
                cloudformationResources.AppendLine(IndentText(2, String.Format("DependsOn: {0}Version{1}", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: {0}{1}", NamePrefix, function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionVersion: !GetAtt {0}Version{1}.Version", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Name: {0}", Environment)));
                cloudformationResources.AppendLine();

            }

            return cloudformationResources.ToString();
        }


        #region UTILS

        static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        static string GetAssemblyName(string FullPathDll) => Path.GetFileNameWithoutExtension(FullPathDll);

        static string IndentText(int Level, string Text) => String.Concat(new string(' ', Level * 2), Text);

        static bool IsOdd(int value) => value % 2 != 0;

        static string ReplaceNonAlphanumeric(string Text) => new Regex("[^a-zA-Z0-9]").Replace(Text, "");

        static string AppendTitle(string Title) => new StringBuilder().AppendLine().AppendLine(IndentText(1, "############################################")).AppendLine(IndentText(1, $"# {Title}")).AppendLine(IndentText(1, "############################################")).AppendLine().ToString();

        private static Assembly CheckLoaded(object sender, ResolveEventArgs args)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.FullName.Equals(args.Name))
                {
                    return a;
                }
            }
            return null;
        }
        #endregion

    }
}
