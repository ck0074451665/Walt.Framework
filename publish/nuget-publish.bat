set path=%path%;%cd%
cd .\..\Walt.Framework.Log
dotnet restore
dotnet build
set versionNumber=1.3.3
nuget pack . -Build -Prop Configuration=Release   -Version %versionNumber% -OutputDirectory .\..\output

cd .\..\output
nuget  push  .\Walt.Framework.Log.%versionNumber%.nupkg -ApiKey 123456 -Source http://localhost/nuget


cd .\..\publish


cd .\..\Walt.Framework.Configuration
dotnet restore
dotnet build
set versionNumber=1.2.3
nuget pack . -Build -Prop Configuration=Release   -Version %versionNumber% -OutputDirectory .\..\output

cd .\..\output
nuget  push  .\Walt.Framework.Configuration.%versionNumber%.nupkg -ApiKey 123456 -Source http://localhost/nuget


cd .\..\publish


cd .\..\Walt.Framework.Service
dotnet restore
dotnet build
set versionNumber=1.8.4
nuget pack . -Build -Prop Configuration=Release   -Version %versionNumber% -OutputDirectory .\..\output

cd .\..\output
nuget  push  .\Walt.Framework.Service.%versionNumber%.nupkg -ApiKey 123456 -Source http://localhost/nuget


cd .\..\publish


cd .\..\Walt.Framework.Quartz
dotnet restore
dotnet build
set versionNumber=1.0.1
nuget pack . -Build -Prop Configuration=Release   -Version %versionNumber% -OutputDirectory .\..\output

cd .\..\output
nuget  push  .\Walt.Framework.Quartz.%versionNumber%.nupkg -ApiKey 123456 -Source http://localhost/nuget


cd .\..\publish


