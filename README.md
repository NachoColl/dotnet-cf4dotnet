[![Build Status](https://travis-ci.com/NachoColl/dotnet-cf4dotnet.svg?branch=master)](https://travis-ci.com/NachoColl/dotnet-cf4dotnet)

Use **Cloudformation4dotNET** (cf4dotNet) to create the [AWS Cloudformation](https://aws.amazon.com/cloudformation/) templates you need to push your code on AWS (the idea is to only have to work on the code side and run ```cf4dotnet``` on your deployment pipeline).


### TL;DR

```bash

# create a cf4dotnet demo project (1 API Gateway Lambda and 1 standalone Lambda )
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
dotnet new cf4dotnet -n MyDemoProject -as MyDemoAssemblyName -t MyAWSTagCode

# build your code and check everything is ok
dotnet publish ./src -o ../artifact --framework netcoreapp2.1 -c Release

# install cf4dotnet tool
dotnet tool install --global NachoColl.Cloudformation4dotNET --version 1.0.*

# get the required AWS Cloudformation templates to deploy your code on AWS
dotnet cf4dotnet api E:\<your-project-path>\artifact\MyDemoAssemblyName.dll -e prod

```

You get [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) to deploy your code on AWS.


### How It Works

**Cloudformation4dotNET** uses reflection to check for functions that you want to deploy on AWS and outputs the required resources definition. 

For example, if you mark a function as follows:

```csharp
[Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties("utils/status", EnableCORS=true, TimeoutInSeconds=5)]
public APIGatewayProxyResponse CheckStatusFunction(...) { 
  ...
}
```
you get the related resources output:

```bash
...

CheckStatusFunction:
    Type: AWS::Serverless::Function
    Properties:
      FunctionName: myapi-CheckStatus
      Handler: MyDemoAssemblyName::MyDemoProject.APIGateway::CheckStatus 
      Role: !Ref myAPILambdaExecutionRole
      Timeout: 5

statusAPIResource:
    Type: AWS::ApiGateway::Resource
    Properties:
      RestApiId: !Ref myAPI
      ParentId: !Ref utilsAPIResource
      PathPart: status

CheckStatusAPIMethod:
    Type: AWS::ApiGateway::Method
    Properties:
      RestApiId: !Ref myAPI
      ResourceId: !Ref statusAPIResource
      HttpMethod: POST
      AuthorizationType: NONE
      Integration:
        Type: AWS_PROXY
        IntegrationHttpMethod: POST
        Uri: !Sub "arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${CheckStatusFunction.Arn}:${!stageVariables.lambdaAlias}/invocations"
        Credentials: !GetAtt myAPILambdaExecutionRole.Arn

...
```

To check how it works, I recommend that you install the available ['dotnet new' templates](https://github.com/NachoColl/dotnet-cf4dotnet-templates),

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
```

and call ```dotnet new cf4dotnet``` to get a ready-to-go project that will contain the next files:

- ```MyApi.cs```, a simple [AWS API Gateway](https://aws.amazon.com/api-gateway/) functions class,

```csharp
namespace MyAPI
{
    public class APIGateway
    {

        /* A function that will get APIGateway + Lambda resources created. */
        [Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties("utils/status", APIKeyRequired=true,  EnableCORS=true, TimeoutInSeconds=2)]
        public APIGatewayProxyResponse CheckStatus(APIGatewayProxyRequest Request, ILambdaContext context) => new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Headers =  new Dictionary<string,string>(){{"Content-Type","text/plain"}},
            Body = String.Format("Running lambda version {0} {1}.", context.FunctionVersion, JsonConvert.SerializeObject(Request?.StageVariables))
        };

    }
}
```

- ```MyLambdas.cs```, to code standalone [Lambdas](https://aws.amazon.com/lambda/),

```csharp
namespace MyAPI {

    public class Lambdas
    {
        
        /* A function that will get Lambda resources created (only) */
        [Cloudformation4dotNET.Lambda.LambdaResourceProperties(TimeoutInSeconds=20)]
        public void Echo(Object Input, ILambdaContext Context) => Context?.Logger?.Log(JsonConvert.SerializeObject(Input));
        
    }
}
```

- and two cloudformation templates, ```sam.yml``` and ```samx.yml```, that are used as your project base cloudformation templates.


#### How to run cf4dotnet

Once your code is ready, install and run [dotnet-cf4dotnet](https://www.nuget.org/packages/NachoColl.Cloudformation4dotNET/) indicating your *code file*, the *environment name* and the *version number* (version number is used to create new AWS Lambda versions):

```bash
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name>
```

As an example, if you run the command on the provided template project,

```bash
dotnet cf4dotnet api E:\Git\public\Cloudformation4dotNET\dotnet-cf4dotnet\demo\artifact\MyDemoAssemblyName.dll
```
you get the next [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) cloudformation templates ready to get deployed on AWS:


### Version Notes

This is an initial 1.0.x version that fits my deployment needs! I will check for issues and add new features as soon as I need them. Please feel free to push/ask for improvements, questions or whatever. 

#### Limits

While you can use ```cf4dotnet``` to automatically build your dotNET code required AWS Cloudformation templates, take note you will only be able to:

##### APIGateway

- set and use a 2 level path for your API Gateway resources, e.g. ```contact/get``` or ```utils/status```
- set if an API Key is required
- enable CORS, that will add the next rules:
```xml
method.response.header.Access-Control-Allow-Headers: "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token'"
method.response.header.Access-Control-Allow-Methods: "'POST,OPTIONS'"
method.response.header.Access-Control-Allow-Origin: "'*'"
```
- set COGNITO_USER_POOLS as authorizer, that will also add the next mapping template to the integration request:
```cs
{
 "cognito":{
    "sub" : "$context.authorizer.claims.sub",
	"email" : "$context.authorizer.claims.email"
    },
 "body" : $input.json('$')
}
```

##### Lambda 

- set the related AWS Lambdas timeout.


