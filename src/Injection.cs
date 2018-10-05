using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;


namespace AWSCloudFormationGenerator
{
    class Injection
    {
        internal class AWSAPIMethodInfo{
            public string MethodName;
            public string MethodPath;
            public int TimeoutInSeconds = 40;
            public bool EnableCORS = false;
        }

        // args[0]: assembly file full path
        // args[1]: cloudformation files path
        // args[2]: environment (test / staging / prod)
        // args[3]: build version
        static int Main(string[] args)
        {

            try{

                /*
                    Inject resources to BASE template.
                 */
                string samFile = string.Format("{0}/sam.yml", args[1]);
                string samBaseFile = string.Format("{0}/sam-base.yml", args[1]);
                string environment = args[2];

                // Build the cloudformation API Gateway related resources string to inject (including lambdas).
                List<AWSAPIMethodInfo> APIFunctionsList = GetAPIFunctions(args[0]);
                string cloudformationAPIResources = GetCloudformationAPIResourcesString(APIFunctionsList, environment);
 
                // Build the cloudformation Lambdas related resources string to inject.
                List<AWSAPIMethodInfo> LambdaFunctionsList = GetLambdaFunctions(args[0]);
                string cloudformationLambdaResources = GetCloudformationLambdaResourcesString(LambdaFunctionsList, environment);

                string source = System.IO.File.ReadAllText(samFile);   
                if (File.Exists(samBaseFile)) File.Delete(samBaseFile);
                using (FileStream fs = System.IO.File.Create(samBaseFile))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(source.Replace("@INJECT"," INJECTED CODE:" + cloudformationAPIResources + cloudformationLambdaResources + IndentText(1,"# END of injected code")));
                    fs.Write(info, 0, info.Length);
                }


                /*
                    Inject lambdas versions.
                */
                
                int buildVersion = Int32.Parse(args[3]);
                
                string samXFile = string.Format("{0}/samx.yml", args[1]);
                string samEnvironmentFile = string.Format("{0}/sam-{1}.yml", args[1], environment);
             
                // Build the cloudformation lambda's versions resources string to inject.      
                string cloudformationLambdasVersionsResources = 
                    GetCloudformationLambdasVersionsResourcesString(APIFunctionsList, environment, buildVersion) +
                    GetCloudformationLambdasVersionsResourcesString(LambdaFunctionsList, environment, buildVersion);

                string sourceX = System.IO.File.ReadAllText(samXFile);   
                if (File.Exists(samEnvironmentFile)) File.Delete(samEnvironmentFile);
                using (FileStream fs = System.IO.File.Create(samEnvironmentFile))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(sourceX.Replace("@INJECT"," INJECTED CODE:" + cloudformationLambdasVersionsResources + IndentText(1,"# END of injected code")));
                    fs.Write(info, 0, info.Length);
                }

                return 0;
            } catch(Exception e){
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        static List<AWSAPIMethodInfo> GetAPIFunctions(string AssemblyFile){
            List<AWSAPIMethodInfo> functionsList = new List<AWSAPIMethodInfo>();
            Assembly testAssembly = Assembly.LoadFile(AssemblyFile);
            foreach (Type type in testAssembly.GetTypes()) 
            {   
                foreach (MethodInfo methodInfo in type.GetMethods()
                            .Where(m => !typeof(object)
                            .GetMethods()
                            .Select(me => me.Name)
                            .Contains(m.Name))){

                    APIGateway.APIGatewayResourceProperties apiGatewayPath = 
                        (APIGateway.APIGatewayResourceProperties) methodInfo.GetCustomAttribute(typeof (APIGateway.APIGatewayResourceProperties));
                    if (apiGatewayPath!=null)
                        functionsList.Add(new AWSAPIMethodInfo(){ MethodName = methodInfo.Name, MethodPath = apiGatewayPath?.PathPart ?? methodInfo.Name, EnableCORS = apiGatewayPath.EnableCORS});
                }
            }
            return functionsList;
        }

        static List<AWSAPIMethodInfo> GetLambdaFunctions(string AssemblyFile){
            List<AWSAPIMethodInfo> functionsList = new List<AWSAPIMethodInfo>();
            Assembly testAssembly = Assembly.LoadFile(AssemblyFile);
            foreach (Type type in testAssembly.GetTypes()) 
            {   
                foreach (MethodInfo methodInfo in type.GetMethods()
                            .Where(m => !typeof(object)
                            .GetMethods()
                            .Select(me => me.Name)
                            .Contains(m.Name))){

                    Lambda.LambdaResourceProperties lambdaProperties = 
                        (Lambda.LambdaResourceProperties) methodInfo.GetCustomAttribute(typeof (Lambda.LambdaResourceProperties));
                    if (lambdaProperties!=null)  
                        functionsList.Add(new AWSAPIMethodInfo(){ MethodName = methodInfo.Name, MethodPath = null, TimeoutInSeconds = lambdaProperties.TimeoutInSeconds});
                }
            }
            return functionsList;
        }

        static string GetCloudformationAPIResourcesString(List<AWSAPIMethodInfo> functions, string Environment){
            // the cloudformation deployment resrouces.
            StringBuilder cloudformationResources = new StringBuilder();
            cloudformationResources.AppendLine();

            // create the root paths
            cloudformationResources.AppendLine();
            cloudformationResources.AppendLine(IndentText(1,"# root API resources"));
            cloudformationResources.AppendLine();
        
            List<string> rootPaths = new List<string>();
            foreach(AWSAPIMethodInfo function in functions){
                string[] pathParts = function.MethodPath.Split("/");
                if (!rootPaths.Contains(pathParts[0])) {
                    rootPaths.Add(pathParts[0]);  
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIResource:", ReplaceNonAlphanumeric(pathParts.Count()==1 ? function.MethodName : pathParts[0]))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Resource"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, "ParentId: !GetAtt nWAYApi.RootResourceId"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("PathPart: {0}", pathParts[0])));
                    cloudformationResources.AppendLine();
                }          
            }
       
            foreach(AWSAPIMethodInfo function in functions){            
                
                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1,"# " + function.MethodName));
                cloudformationResources.AppendLine();

                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Function:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Serverless::Function"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: nway-{0}", function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Handler: nwayapi::nWAY.API::{0} ", function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, "Role: !Sub \"arn:aws:iam::${AWS::AccountId}:role/nway-lambdas\""));
                cloudformationResources.AppendLine(IndentText(3, "Timeout: " + function.TimeoutInSeconds));
                cloudformationResources.AppendLine();

                // to create 2nd level paths (e.g. utils/ip/)
                string[] pathParts = function.MethodPath.Split("/");   
                if (pathParts.Count()>1){     
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIResource:", ReplaceNonAlphanumeric(pathParts[1]))));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Resource"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, "ParentId: !Ref " + String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts[0]))));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("PathPart: {0}", pathParts[1])));
                    cloudformationResources.AppendLine();  
                }

                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIMethod:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Method"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("ResourceId: !Ref {0}", String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts.Count()==1 ? function.MethodName : pathParts[pathParts.Count()-1])))));
                cloudformationResources.AppendLine(IndentText(3, "HttpMethod: POST"));
                cloudformationResources.AppendLine(IndentText(3, "AuthorizationType: NONE"));
                cloudformationResources.AppendLine(IndentText(3, "Integration:"));
                cloudformationResources.AppendLine(IndentText(4, "Type: AWS_PROXY"));
                cloudformationResources.AppendLine(IndentText(4, "IntegrationHttpMethod: POST"));
                cloudformationResources.AppendLine(IndentText(4, "Uri: !Sub \"arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${" + functionCFResourceName + "Function.Arn}:${!stageVariables.lambdaAlias}/invocations\""));
                cloudformationResources.AppendLine(IndentText(4, "Credentials: !Sub \"arn:aws:iam::${AWS::AccountId}:role/nway-lambdas\""));
                cloudformationResources.AppendLine();


                if (function.EnableCORS){
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1,"# enabling OPTIONS for " + function.MethodName));
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, String.Format("{0}APIMethodOPTIONS:", functionCFResourceName)));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Method"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, String.Format("ResourceId: !Ref {0}", String.Format("{0}APIResource", ReplaceNonAlphanumeric(pathParts.Count()==1 ? function.MethodName : pathParts[pathParts.Count()-1])))));
                    cloudformationResources.AppendLine(IndentText(3, "HttpMethod: OPTIONS"));
                    cloudformationResources.AppendLine(IndentText(3, "AuthorizationType: NONE"));
                    cloudformationResources.AppendLine(IndentText(3, "Integration:"));
                    cloudformationResources.AppendLine(IndentText(4, "Type: MOCK"));
                    cloudformationResources.AppendLine(IndentText(4, "IntegrationResponses:"));
                    cloudformationResources.AppendLine(IndentText(5,"-  ResponseParameters:"));
                    cloudformationResources.AppendLine(IndentText(6,"  method.response.header.Access-Control-Allow-Headers: \"'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token'\""));
                    cloudformationResources.AppendLine(IndentText(6,"  method.response.header.Access-Control-Allow-Methods: \"'POST,OPTIONS'\""));
                    cloudformationResources.AppendLine(IndentText(6,"  method.response.header.Access-Control-Allow-Origin: \"'*'\""));
                    cloudformationResources.AppendLine(IndentText(5,"   ResponseTemplates:"));
                    cloudformationResources.AppendLine(IndentText(6,"  application/json: ''"));
                    cloudformationResources.AppendLine(IndentText(5,"   StatusCode: '200'"));
                    cloudformationResources.AppendLine(IndentText(4,"PassthroughBehavior: NEVER"));
                    cloudformationResources.AppendLine(IndentText(4,"RequestTemplates:"));
                    cloudformationResources.AppendLine(IndentText(5,"application/json: '{\"statusCode\": 200}'"));
                    cloudformationResources.AppendLine(IndentText(3,"MethodResponses:"));
                    cloudformationResources.AppendLine(IndentText(4,"-  ResponseModels:"));
                    cloudformationResources.AppendLine(IndentText(5,"  application/json: Empty"));
                    cloudformationResources.AppendLine(IndentText(4,"   ResponseParameters:"));
                    cloudformationResources.AppendLine(IndentText(5,"  method.response.header.Access-Control-Allow-Headers: true"));
                    cloudformationResources.AppendLine(IndentText(5,"  method.response.header.Access-Control-Allow-Methods: true"));
                    cloudformationResources.AppendLine(IndentText(5,"  method.response.header.Access-Control-Allow-Origin: true"));
                    cloudformationResources.AppendLine(IndentText(4,"   StatusCode: '200'"));
                    cloudformationResources.AppendLine();
                }
             
            }

            switch (Environment){
                case "test": // TEST environment just content the test environmnet ;)

                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "Test:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "StageName: test"));
                    cloudformationResources.AppendLine(IndentText(3, "Description: API Test"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, "DeploymentId: !Ref TestDeployment"));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, "lambdaAlias: test"));
                
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "TestDeployment:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach(AWSAPIMethodInfo function in functions){
                            string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                            cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }
                    cloudformationResources.AppendLine();
                    break;
                default: // STAGING and PROD are contained into the same environment.
                

                    // StagingDeployment
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "Staging:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "StageName: staging"));
                    cloudformationResources.AppendLine(IndentText(3, "Description: API Staging"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, "DeploymentId: !Ref StagingDeployment"));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, "lambdaAlias: staging"));

                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "StagingDeployment:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach(AWSAPIMethodInfo function in functions){
                        string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                        cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }
                    cloudformationResources.AppendLine();

                    // ProdDeployment
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "Test:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Stage"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn: nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "StageName: prod"));
                    cloudformationResources.AppendLine(IndentText(3, "Description: API Production"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(3, "DeploymentId: !Ref ProdDeployment"));
                    cloudformationResources.AppendLine(IndentText(3, "Variables:"));
                    cloudformationResources.AppendLine(IndentText(4, "lambdaAlias: prod"));
                           
                    cloudformationResources.AppendLine();
                    cloudformationResources.AppendLine(IndentText(1, "ProdDeployment:"));
                    cloudformationResources.AppendLine(IndentText(2, "Type: AWS::ApiGateway::Deployment"));
                    cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                    cloudformationResources.AppendLine(IndentText(3, "RestApiId: !Ref nWAYApi"));
                    cloudformationResources.AppendLine(IndentText(2, "DependsOn:"));
                    foreach(AWSAPIMethodInfo function in functions){
                        string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);
                        cloudformationResources.AppendLine(IndentText(3, String.Format("- {0}APIMethod", functionCFResourceName)));
                    }
                    cloudformationResources.AppendLine();
                    break;

            }
           
            return cloudformationResources.ToString();
        }

         static string GetCloudformationLambdaResourcesString(List<AWSAPIMethodInfo> functions, string Environment){
            // the cloudformation deployment resrouces.
            StringBuilder cloudformationResources = new StringBuilder();
            cloudformationResources.AppendLine();

            foreach(AWSAPIMethodInfo function in functions){
                
                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);

                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Function:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Serverless::Function"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: nway-{0}", function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Handler: nwayapi::nWAY.Lambdas.Handlers::{0} ", function.MethodName)));
                
                cloudformationResources.AppendLine(IndentText(3, "Role: !Sub \"arn:aws:iam::${AWS::AccountId}:role/nway-lambdas\""));
                cloudformationResources.AppendLine(IndentText(3, "Timeout: " + function.TimeoutInSeconds));
                cloudformationResources.AppendLine();

            
             
            }
          
            return cloudformationResources.ToString();
        }

        static string GetCloudformationLambdasVersionsResourcesString(List<AWSAPIMethodInfo> functions, string Environment, int BuildVersion){

            StringBuilder cloudformationResources = new StringBuilder();
            cloudformationResources.AppendLine();

            // staging and prod use the same AWS ACCOUNT.
            int environmentVersion=Environment.Equals("staging") ? BuildVersion + (BuildVersion - 1) : BuildVersion * 2;

            foreach(AWSAPIMethodInfo function in functions){

                string functionCFResourceName = ReplaceNonAlphanumeric(function.MethodName);

                cloudformationResources.AppendLine(IndentText(1, String.Format("# {0}Version{1} related resources.", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine();
                            
                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Version{1}:", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Lambda::Version"));
                cloudformationResources.AppendLine(IndentText(2, "DeletionPolicy: Retain"));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: nway-{0}", function.MethodName))); 
                cloudformationResources.AppendLine();
                
                cloudformationResources.AppendLine();
                cloudformationResources.AppendLine(IndentText(1, String.Format("{0}Alias:", functionCFResourceName)));
                cloudformationResources.AppendLine(IndentText(2, "Type: AWS::Lambda::Alias"));
                cloudformationResources.AppendLine(IndentText(2, "DeletionPolicy: Retain"));
                cloudformationResources.AppendLine(IndentText(2, String.Format("DependsOn: {0}Version{1}", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(2, "Properties:"));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionName: nway-{0}", function.MethodName)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("FunctionVersion: !GetAtt {0}Version{1}.Version", functionCFResourceName, environmentVersion)));
                cloudformationResources.AppendLine(IndentText(3, String.Format("Name: {0}", Environment)));
                cloudformationResources.AppendLine();
                
            }

            return cloudformationResources.ToString();
        }


        #region UTILS
        static string IndentText(int Level, string Text) => String.Concat(new string(' ', Level * 2), Text);

        static bool IsOdd(int value) => value % 2 != 0;

        static string ReplaceNonAlphanumeric(string Text) => new Regex("[^a-zA-Z0-9]").Replace(Text,"");
        #endregion

    }
}
