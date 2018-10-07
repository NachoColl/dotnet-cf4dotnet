# Description

Cloudformation4dotNET (cf4dotNet) is a tool you can use to automatically create the required cloudformation templates from your dotNET code. The idea is that you use it on your deployment pipelines, so you finally work on the code side, without having to worry about CF templates manuals.

# How to use it

To start using this tool, **install the required templates**,

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates.DotNetNew
```

and **create a simple project example**:

```
dotnet new cf4dotnet
```

This will create a c-sharp project including the next files:

- ```MyApi.cs``` as a simple API Gateway functions class example,

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

- the related ```MyApi.csproj``` project file with the required initial dependencies,

- ```sam.yml``` and ```samx.yml``` as the base cloudformation templates that will be used to inject your code-related resources.

### How to use cf4dotNET tool

First, build your project,

```
dotnet build
```

To dynamically create your code AWS Cloudformation templates, ```dotnet-cf4dotnet``` indicating the next available options:

```
dotnet-cf4dotnet <your-code-dll-file> -o <output-path> -b <build-version-number> -e <environment-name>
```
To get an idea on how you can use it, I personally call this command on every push I make on Git that should get deployed to AWS. I build the code, test it, and then dynamically create the related Cloudformation template using a new <build-version> (I use $TRAVIS_BUILD_NUMBER in my case).

# Note

This is an initial 0.0.x version of the tool that mainly fits for my personal deployment needs!

Feel free to push/ask for improvements, issues or whatever.


