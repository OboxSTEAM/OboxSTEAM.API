# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OboxSteam.API/OboxSteam.API.csproj", "OboxSteam.API/"]
COPY ["OboxSteam.Application/OboxSteam.Application.csproj", "OboxSteam.Application/"]
COPY ["OboxSteam.Domain/OboxSteam.Domain.csproj", "OboxSteam.Domain/"]
COPY ["OboxSteam.Infrastructure/OboxSteam.Infrastructure.csproj", "OboxSteam.Infrastructure/"]
RUN dotnet restore "./OboxSteam.API/OboxSteam.API.csproj"
COPY . .
WORKDIR "/src/OboxSteam.API"
RUN dotnet build "./OboxSteam.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OboxSteam.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OboxSteam.API.dll"]