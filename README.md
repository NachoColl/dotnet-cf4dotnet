# Description

Use **Cloudformation4dotNET** (cf4dotNet) to automatically create the  cloudformation templates you need to deploy your dotNET code on AWS. 

The idea is that you use it on your deployment pipelines, so you only have to work on the code side, without worrying about the related CF templates.

# How to use it

To start, **install the Cloudformation4dotNET templates**,

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates.DotNetNew
```

and create a new project example:

```
dotnet new cf4dotnet
```

This will create a project including the next files:

- ```MyApi.cs```, a simple API Gateway functions class example,

```csharp
 /* A simple function to check if our API is up and running. */
[Cloudformation4dotNET.APIGateway.APIGatewayResourceProperties(PathPart:"utils/status")]
public APIGatewayProxyResponse CheckStatus(APIGatewayProxyRequest Request, ILambdaContext context) => new APIGatewayProxyResponse
{
    StatusCode = 200,
    Headers =  new Dictionary<string,string>(){{"Content-Type","text/plain"}},
    Body = String.Format("Running lambda version {0} {1}.", context.FunctionVersion, JsonConvert.SerializeObject(Request?.StageVariables))
};
```

- the related ```MyApi.csproj``` project file, 

- and two cloudformation templates, ```sam.yml``` and ```samx.yml```, that will be used as your base cloudformation templates.

Build your project as you will do for publishing your code on AWS, for example:

```shell
dotnet publish -o ./artifact --framework netcoreapp2.0 -c Release
```

### Running cf4dotNET tool

Once your code builds, to get the Cloudformation templates you'll need to deploy, run ```dotnet-cf4dotnet``` as follows:

```shell
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name>
```

This command will use ```sam.yml``` and ```samx.yml``` base templates (those files are not modified) to add your code related resources (check the source code [injection.cs](./src/injection.cs)). For example, if you run the command on the provided project template,

```shell
dotnet cf4dotnet api E:\Git\public\dotnet-cf4dotnet\test\artifact\MyApi.dll -b 1 -e test
```
you will get the next [sam-base.yml](./test/sam-base.yml) and [sam-test.yml](./test/sam-test.yml) cloudformation templates.


 
# Note

This is an initial 0.0.x version of the tool that fits for my personal deployment needs! I will add new features as soon as I need them. Please feel free to push/ask for improvements, issues or whatever. 

How I use it? 

* I work on my API Gateway functions code,
* and push code to GitHub, trigering [travis](https://travis-ci.com) pipeline that mainly:
    * builds and tests the code,
    * sends the code artifact to S3,
    * runs cf4dotnet to create the required CF templates (using  $TRAVIS_BUILD_NUMBER as the build-version I use to version my Lambdas), and fnially,
    * deploy the CF templates to my AWS account.

Hope you get ideas on how to build your own pipes ;)