.DEFAULT_GOAL := test

restore:
	dotnet restore TandisWebApp.sln --locked-mode --ignore-failed-sources -p:NuGetAudit=false

build: restore
	dotnet build TandisWebApp.sln --no-restore --configuration Release

test: restore
	dotnet run --project tests/TandisWebApp.Checks/TandisWebApp.Checks.csproj --no-restore --configuration Release

run:
	dotnet run --project TandisWebApp/TandisWebApp.csproj --no-restore

publish: restore
	dotnet publish TandisWebApp/TandisWebApp.csproj --no-restore --configuration Release --output ./publish
