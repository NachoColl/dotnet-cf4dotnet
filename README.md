[![Build Status](https://travis-ci.com/NachoColl/dotnet-cf4dotnet.svg?branch=master)](https://travis-ci.com/NachoColl/dotnet-cf4dotnet)

Use **Cloudformation4dotNET** (cf4dotNet) to create the [AWS Cloudformation](https://aws.amazon.com/cloudformation/) templates you need to deploy your code on AWS. 

The idea is to only have to work on the code side and let ```cf4dotnet``` build the required Cloudformation updates and AWS resources on your deployment pipeline.

## TL;DR

```bash

# create a cf4dotnet demo project (1 API Gateway Lambda and 1 standalone Lambda )
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
dotnet new cf4dotnet -n MyDemoProject -as MyDemoAssemblyName -t MyAWSTagCode

# build your code and check everything is ok
dotnet publish ./src -o ../artifact --framework netcoreapp2.1 -c Release

# install cf4dotnet tool
dotnet tool install --global NachoColl.Cloudformation4dotNET --version 0.0.33

# get the required AWS Cloudformation templates to deploy your code on AWS
dotnet cf4dotnet api E:\<your-project-path>\artifact\MyDemoAssemblyName.dll -e prod

```

You get [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) ready to go templates.


# How It Works

**Cloudformation4dotNET** uses reflection to check your code for functions that you want to deploy on AWS. To make it work, you only have to mark your AWS functions with the provided function-property class (e.g. ```Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties```).

```csharp
[Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties("utils/status", EnableCORS=true, TimeoutInSeconds=2)]
public APIGatewayProxyResponse MyAWSLambdaFunction(...) { 
  ...
}
```

### Cloudformation4dotNET 'dotnet new' templates

To check how the tool works, I recommend that you install the available ['dotnet new' templates](https://github.com/NachoColl/dotnet-cf4dotnet-templates),

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
        [Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties("utils/status", EnableCORS=true, TimeoutInSeconds=2)]
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
        [Cloudformation4dotNET.Lambda.LambdaResourceProperties(TimeoutInSeconds=2)]
        public void Echo(string Input, ILambdaContext Context) => Context?.Logger?.Log(Input.ToUpper());
        
    }
}
```

- and two cloudformation templates, ```sam.yml``` and ```samx.yml```, that are used as your project base cloudformation templates.


### How to run 

To get your code cloudformation templates install and run [dotnet-cf4dotnet](https://www.nuget.org/packages/NachoColl.Cloudformation4dotNET/) indicating your *code file*, the *environment name* and the *version number* (version number is used to create new AWS Lambda versions):

```bash
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name>
```

If you run the command on the provided project demo template files (```dotnet new cf4dotnet```),

```bash
dotnet cf4dotnet api E:\Git\public\Cloudformation4dotNET\dotnet-cf4dotnet\demo\artifact\MyDemoAssemblyName.dll
```
you get the next [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) cloudformation templates ready to get deployed on AWS:

```bash
# deploy base template
echo "deploying base template ..."
aws cloudformation deploy --profile deploy --template-file $CF_BASE_TEMPLATE --stack-name $CF_BASE_STACKNAME --parameter-overrides ArtifactS3Bucket=$ARTIFACT_S3_BUCKET  ArtifactS3BucketKey=$ARTIFACT_S3_KEY --tags appcode=$TAG_CODE --no-fail-on-empty-changeset 

# deploy environment template
echo "deploying $ENVIRONMENT template ..."
aws cloudformation deploy --profile deploy --template-file $CF_ENVIRONMENT_TEMPLATE --stack-name $CF_ENVIRONMENT_STACKNAME --tags appcode=$TAG_CODE --no-fail-on-empty-changeset 
```

# Version Notes

This is an initial 0.0.x version that fits my deployment needs! I will check for issues and add new features as soon as I need them. Please feel free to push/ask for improvements, questions or whatever. 

