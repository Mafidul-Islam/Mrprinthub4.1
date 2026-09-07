# =============================================================================
# MR Print Hub Cloud - Production Multi-Stage Dockerfile (Koyeb / Cloud PaaS)
# =============================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Central Package Management and Directory build properties
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["NuGet.Config", "./"]

# Copy project definition files for layer caching
COPY ["src/MRPrintHub.Core/MRPrintHub.Core.csproj", "src/MRPrintHub.Core/"]
COPY ["src/MRPrintHub.Security/MRPrintHub.Security.csproj", "src/MRPrintHub.Security/"]
COPY ["src/MRPrintHub.Cloud/MRPrintHub.Cloud.csproj", "src/MRPrintHub.Cloud/"]

# Restore NuGet packages
RUN dotnet restore "src/MRPrintHub.Cloud/MRPrintHub.Cloud.csproj"

# Copy source trees for dependent projects
COPY src/MRPrintHub.Core/ src/MRPrintHub.Core/
COPY src/MRPrintHub.Security/ src/MRPrintHub.Security/
COPY src/MRPrintHub.Cloud/ src/MRPrintHub.Cloud/

# Build and Publish Release artifacts
WORKDIR "/src/src/MRPrintHub.Cloud"
RUN dotnet publish "MRPrintHub.Cloud.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Set default port (overridden dynamically at runtime by Koyeb via $PORT)
ENV ASPNETCORE_HTTP_PORTS=8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "MRPrintHub.Cloud.dll"]
