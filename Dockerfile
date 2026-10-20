FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY TandisWebApp.sln ./
COPY TandisWebApp/TandisWebApp.csproj TandisWebApp/
COPY TandisWebApp/packages.lock.json TandisWebApp/
COPY tests/TandisWebApp.Checks/TandisWebApp.Checks.csproj tests/TandisWebApp.Checks/
COPY tests/TandisWebApp.Checks/packages.lock.json tests/TandisWebApp.Checks/

RUN dotnet restore TandisWebApp.sln --locked-mode --ignore-failed-sources -p:NuGetAudit=false
COPY . .
RUN dotnet publish TandisWebApp/TandisWebApp.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TandisWebApp.dll"]
