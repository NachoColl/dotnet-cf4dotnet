[![Build Status](https://travis-ci.com/NachoColl/dotnet-cf4dotnet.svg?branch=master)](https://travis-ci.com/NachoColl/dotnet-cf4dotnet)

Use **Cloudformation4dotNET** (cf4dotNet) to dynamically create the AWS Cloudformation templates your code needs to get deployed on AWS. 

The idea is to use the command on your deployment pipelines, so you only have to work on the code side, without worrying about the related Cloudformation updates and AWS resources versioning.

You can check for some notes on a real example using this tool at [linkedin.com](https://www.linkedin.com/pulse/building-cicd-pipeline-aws-lambdas-net-core-nacho-coll/).

# How-To

Install the [tool templates](https://github.com/NachoColl/dotnet-cf4dotnet-templates), so you don't have to start from scratch:

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates
```

Create a new ```cf4dotnet``` project,

```
dotnet new cf4dotnet
```

and a new project will get generated including the next files:

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

- the related ```MyProject.csproj``` project file, 

- and two cloudformation templates, ```sam.yml``` and ```samx.yml```, that are used as your project base cloudformation templates.

Test the project to check everything is ok and build it as you will do for pushing your code to AWS, for example:

```bash
dotnet publish -o ./artifact --framework netcoreapp2.1 -c Release
```

### Running cf4dotNET tool

To create the Cloudformation templates that you need to deploy your code on AWS, install the tool,

```bash
dotnet tool install --global NachoColl.Cloudformation4dotNET --version 0.0.28
```
and run  ```dotnet-cf4dotnet``` indicating the next parameters:

```bash
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name> -c 2-accounts
```

This command will use ```sam.yml``` and ```samx.yml``` base templates (those files are not modified) to add your code related resources (check the source code [here](./src/Injection.cs) - sorry, I also need to write spaghetti code time to time -). 

To get and example, if you run the command on the provided project template,

```bash
dotnet cf4dotnet api E:\Git\public\dotnet-cf4dotnet\test\artifact\MyApi.dll -b 1 -e prod
```
the next [sam-base.yml](./test/sam-base.yml) and [sam-prod.yml](./test/sam-prod.yml) cloudformation templates will get created and ready to use for deployment.

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

How I use it? 

I only work on API/Lambdas code, push changes and let [travis](https://travis-ci.com) pipeline deploy the cloudformation updates to my AWS account. 

```bash
sudo: required
language: csharp
mono: none
dotnet: 2.1
services:
  - docker

branches:
  except:
    # this is required to avoid building tags.
    - /^v?\d+\.\d+\.\d+(\w*\W*)*$/
    # dev brach should not be published on the cloud.
    - dev
install:
  # install awscli for deploying
  - pip install awscli --upgrade --user
  # install dynamodb docker/tables
  - bash $TRAVIS_BUILD_DIR/test/init.travis.sh
  # intall newman for testing
  - npm install -g newman
  - npm install -g newman-reporter-html
script:
  # unit test before continuing
  - dotnet test $TRAVIS_BUILD_DIR/test/nwayapi.Tests.csproj -c Debug -v n 
deploy:
  - provider: script
    skip_cleanup: true
    script: bash $TRAVIS_BUILD_DIR/deploy/.deploy.sh test 
    on: test
  - provider: script
    skip_cleanup: true
    script: bash $TRAVIS_BUILD_DIR/deploy/.deploy.sh staging 
    on: staging
  - provider: script
    skip_cleanup: true
    script: bash $TRAVIS_BUILD_DIR/deploy/.deploy.sh prod 
    on: master
```

The related ```.deploy.sh``` code is quite simple and mainly:

* builds and tests the code,
* sends the code artifact to S3,
* runs ```cf4dotnet``` to create the required CF templates (using $TRAVIS_BUILD_NUMBER as the build-version I use to version my Lambdas), and finally,
* deploy the CF templates to my AWS account.

Hope you get ideas on how to build your own pipes ;)