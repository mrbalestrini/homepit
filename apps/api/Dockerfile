FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props HomePit.sln global.json ./
COPY src/HomePit.Domain/HomePit.Domain.csproj src/HomePit.Domain/
COPY src/HomePit.Application/HomePit.Application.csproj src/HomePit.Application/
COPY src/HomePit.Infrastructure/HomePit.Infrastructure.csproj src/HomePit.Infrastructure/
COPY src/HomePit.Api/HomePit.Api.csproj src/HomePit.Api/
COPY tests/HomePit.Api.Tests/HomePit.Api.Tests.csproj tests/HomePit.Api.Tests/
COPY tests/HomePit.IntegrationTests/HomePit.IntegrationTests.csproj tests/HomePit.IntegrationTests/
RUN dotnet restore HomePit.sln

COPY . .
RUN dotnet publish src/HomePit.Api/HomePit.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomePit.Api.dll"]
