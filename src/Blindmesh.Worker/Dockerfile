FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props NuGet.config ./
COPY src/ src/

RUN dotnet restore src/Blindmesh.Worker/Blindmesh.Worker.csproj
RUN dotnet publish src/Blindmesh.Worker/Blindmesh.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Blindmesh.Worker.dll"]
