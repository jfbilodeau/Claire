FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /app

COPY . ./
RUN dotnet publish -c Release -o out

# Path: Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app

COPY --from=build-env /app/Claire/bin/Release/net8.0/ .

ENTRYPOINT ["dotnet", "Claire.dll"]