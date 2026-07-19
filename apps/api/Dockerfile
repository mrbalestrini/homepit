FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props OrganizaClub.sln global.json ./
COPY src/OrganizaClub.Domain/OrganizaClub.Domain.csproj src/OrganizaClub.Domain/
COPY src/OrganizaClub.Application/OrganizaClub.Application.csproj src/OrganizaClub.Application/
COPY src/OrganizaClub.Infrastructure/OrganizaClub.Infrastructure.csproj src/OrganizaClub.Infrastructure/
COPY src/OrganizaClub.Api/OrganizaClub.Api.csproj src/OrganizaClub.Api/
COPY tests/OrganizaClub.Api.Tests/OrganizaClub.Api.Tests.csproj tests/OrganizaClub.Api.Tests/
COPY tests/OrganizaClub.IntegrationTests/OrganizaClub.IntegrationTests.csproj tests/OrganizaClub.IntegrationTests/
RUN dotnet restore OrganizaClub.sln

COPY . .
RUN dotnet publish src/OrganizaClub.Api/OrganizaClub.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrganizaClub.Api.dll"]
