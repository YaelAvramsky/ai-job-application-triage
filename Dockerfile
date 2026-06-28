# Stage 1: build
# The SDK image restores packages and publishes the release binary.
# Copying .sln and .csproj files before the full source means the
# restore layer is cached as long as project dependencies don't change.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project manifests (enables layer-cached restore)
COPY *.sln .
COPY *.csproj .
COPY ai-job-application-triage.Tests/*.csproj ./ai-job-application-triage.Tests/
RUN dotnet restore

# Copy full source and publish the main app only (test project is not published)
COPY . .
RUN dotnet publish ai-job-application-triage.csproj -c Release -o /app/publish

# Stage 2: lean runtime image (~220 MB vs ~800 MB for the SDK image)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ai-job-application-triage.dll"]
