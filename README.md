[![Build Status](https://travis-ci.com/NachoColl/dotnet-cf4dotnet.svg?branch=master)](https://travis-ci.com/NachoColl/dotnet-cf4dotnet)

Use **Cloudformation4dotNET** (cf4dotNet) to dynamically create the AWS Cloudformation templates you need to deploy your code on AWS. 

The idea is to use the command on your deployment pipelines, so you only have to work on the code side, without worrying about the related Cloudformation updates and AWS resources versioning.

# How To

Install the [tool templates](https://github.com/NachoColl/dotnet-cf4dotnet-templates),

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
```

and create a new ```cf4dotnet``` project (check ```dotnet new cf4dotnet -h``` for the available parameters):

```
dotnet new cf4dotnet -n MyDemoProject -as MyDemoAssemblyName -t MyAWSTagCode
```

A new ```MyProject.csproj``` will get generated including the next files:

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

Test the project to check everything is ok and build it as you will do for pushing your code to AWS:

```bash
dotnet publish ./src -o ../artifact --framework netcoreapp2.1 -c Release
```

### Installing cf4dotNET global tool

To install the cf4dotNET just run the next command:

```bash
dotnet tool install --global NachoColl.Cloudformation4dotNET --version 0.0.33
```

### Getting your code AWS Cloudformation templates

To get your code templates you mainly need to run  ```dotnet-cf4dotnet``` indicating your code file, the environment name you want to deploy the code and the code versino number your deploying :

```bash
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name> -c 2-accounts
```

This command will check your <your-code-dll-file> and use the base cloudformation files (```sam.yml``` and ```samx.yml```) to build the templates your need. For example, if you run the command on the provided project demo template files (```dotnet new cf4dotnet```),

```bash
dotnet cf4dotnet api E:\Git\public\Cloudformation4dotNET\dotnet-cf4dotnet\demo\artifact\MyDemoAssemblyName.dll
```
you get the next [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) cloudformation templates.

You can now use those files to deploy your code on your pipeline:

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

