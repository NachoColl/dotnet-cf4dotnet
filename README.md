# Description

Cloudformation4dotNET (cf4dotNet) is a tool you can use to automatically create the required cloudformation templates from your dotNET code. The idea is that you use it on your deployment pipelines, so you finally just work on the code side, without having to worry on manually updating the CF templates.

# How to use it

### Install the template: 

```
dotnet new -i NachoColl.Cloudformation4dotNET.Templates.DotNetNew
```

### Create a new API Gateway project:

```
dotnet new cf4dotnet
```

This will create a simple project including the next files:

- ```MyApi.cs``` as a simple API Gateway function example,

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

Check everything is corrrectly setup.

```
dotnet build
```


### Execute cf4dotNET tool

To dynamically create your code AWS Cloudformation templates, use the next command:

```
dotnet-cf4dotnet <your-code-dll-file>
```



