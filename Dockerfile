# Stage 1: Build
# Use the full SDK image only for the build stage; it is discarded in the final image.
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy the project file for restore.
# Copying this first allows Docker to cache the restore layer — this layer is only
# invalidated when the .csproj changes, not on every source change.
COPY src/StatusTracker/StatusTracker.csproj src/StatusTracker/

# Restore dependencies into a separate layer to maximise cache reuse.
RUN dotnet restore src/StatusTracker/StatusTracker.csproj

# Copy the remaining source and publish in Release configuration.
# --no-restore skips a redundant restore since the layer above already ran it.
COPY . .
RUN dotnet publish src/StatusTracker/StatusTracker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
# Alpine-based aspnet image is ~50 MB smaller than the Debian variant.
# curl is installed here (not present by default on Alpine) for the HEALTHCHECK below.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Metadata labels on the final image only (LABEL is not valid before FROM).
LABEL org.opencontainers.image.description="Status Tracker — self-hosted status page for monitoring HTTP endpoints" \
      org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:9.0-alpine"

WORKDIR /app

RUN apk add --no-cache curl tzdata \
    # Create a locked-down non-root user; /sbin/nologin prevents interactive logins.
    && addgroup -S appgroup \
    && adduser -S -G appgroup -h /app -s /sbin/nologin appuser

COPY --from=build /app/publish .

# Tell Kestrel to bind on all interfaces at port 8080 (avoids the internal 5000 default).
ENV ASPNETCORE_URLS=http://+:8080

# Drop privileges before starting the process.
USER appuser

# Expose the Kestrel port. Actual host mapping is defined in docker-compose.yml.
EXPOSE 8080

# Liveness probe: hit the ASP.NET Core health-check endpoint.
# --fail causes curl to exit non-zero on HTTP errors (4xx/5xx).
# Interval/timeout/retries chosen to allow a slow cold start (EF migrations, DB connect).
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "StatusTracker.dll"]
