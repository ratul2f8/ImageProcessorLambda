### How to deploy the lambda function

Install tools:
`dotnet new install Amazon.Lambda.Templates`
`dotnet tool install --global Amazon.Lambda.Tools`

Login To CLI: 
`aws configure`

Go the lambda function project where .csproj file is located and deploy from cli:
`dotnet lambda deploy-function`

After that Go to the AWS Console and add triggers for S3