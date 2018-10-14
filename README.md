[![Build Status](https://travis-ci.com/NachoColl/dotnet-cf4dotnet.svg?branch=master)](https://travis-ci.com/NachoColl/dotnet-cf4dotnet)

Use **Cloudformation4dotNET** (cf4dotNet) to dynamically create the AWS Cloudformation templates you need to deploy your code on AWS. 

The idea is to use the command on your deployment pipelines, so you only have to work on the code side, without worrying about the related Cloudformation updates and AWS resources versioning.

You can check for some notes on a real example using this tool at [linkedin.com](https://www.linkedin.com/pulse/building-cicd-pipeline-aws-lambdas-net-core-nacho-coll/).

# How-To

Install the [tool templates](https://github.com/NachoColl/dotnet-cf4dotnet-templates),

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
```

and create a new ```cf4dotnet``` project:

```
dotnet new cf4dotnet
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

Test the project to check everything is ok and build it as you will do for pushing your code to AWS, for example:

```bash
dotnet publish ./src -o ../artifact --framework netcoreapp2.1 -c Release
```

### Running cf4dotNET tool

To create the Cloudformation templates that you need to deploy your code on AWS, install the tool,

```bash
dotnet tool install --global NachoColl.Cloudformation4dotNET --version 0.0.33
```
and run  ```dotnet-cf4dotnet``` indicating the next parameters:

```bash
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name> -c 2-accounts
```

This command will use ```sam.yml``` and ```samx.yml``` base templates (those files are not modified) to add your code related resources (check the source code [here](./src/tool/Injection.cs) - sorry, I also need to write spaghetti code time to time -). 

To get and example, if you run the command on the provided project template (cf4dotnet),

```bash
dotnet cf4dotnet api E:\Git\public\Cloudformation4dotNET\dotnet-cf4dotnet\demo\artifact\MyProject.dll
```
you get the next [sam-base.yml](./demo/sam-base.yml) and [sam-prod.yml](./demo/sam-prod.yml) cloudformation templates.

#### cf4dotNET options

```bash
Usage: cf4dotNet api [arguments] [options]

Arguments:
  source  Your dotnet dll source file full path (e.g. E:/src/my-dotnet-api.dll).

Options:
  -? | -h | --help                      Show help information
  -e|--environment <test/staging/prod>  Environment (default: 'prod').
  -c|--configuration <2-accounts>       Accounts configuration (default: '2-accounts').
  -b|--build <build-version>            Build version number used to create incremental templates (default: '1').
  -o|--ouput <output-path>              Cloudformation templates will get created here (default: './').
```

This initial version is configured to work for 3 environments that will get deployed using 2 accounts: 1 account for ```test```, and 1 account for ```staging``` and ```prod``` - you can check some notes here [https://www.linkedin.com/pulse/building-cicd-pipeline-aws-lambdas-net-core-nacho-coll/](https://www.linkedin.com/pulse/building-cicd-pipeline-aws-lambdas-net-core-nacho-coll/) -.

# Version Notes

This is an initial 0.0.x version that fits my deployment needs! I will check for issues and add new features as soon as I need them. Please feel free to push/ask for improvements, questions or whatever. 

