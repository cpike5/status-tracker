# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for restore
# Copying these first allows Docker to cache the restore layer — rebuild only invalidates
# this cache when .csproj or .sln files change, not on every source change.
COPY StatusTracker.sln .
COPY src/StatusTracker/StatusTracker.csproj src/StatusTracker/
COPY tests/StatusTracker.Tests/StatusTracker.Tests.csproj tests/StatusTracker.Tests/

# Restore dependencies
RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/StatusTracker/StatusTracker.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
# Use the smaller aspnet runtime image (no SDK) for the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user to avoid running the process as root inside the container
RUN groupadd -r appuser && useradd -r -g appuser -d /app -s /sbin/nologin appuser

COPY --from=build /app/publish .

# Drop privileges
USER appuser

# Kestrel default port (overrides internal 5000 default; mapped to host via docker-compose)
EXPOSE 8080

ENTRYPOINT ["dotnet", "StatusTracker.dll"]
