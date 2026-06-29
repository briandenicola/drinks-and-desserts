# ── Build stage ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Restore — copy only project files first for layer caching
COPY api/WhiskeyAndSmokes.Api.csproj api/
COPY ServiceDefaults/WhiskeyAndSmokes.ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore api/WhiskeyAndSmokes.Api.csproj

# Publish as self-contained single file
COPY api/ api/
COPY ServiceDefaults/ ServiceDefaults/
RUN dotnet publish api/WhiskeyAndSmokes.Api.csproj \
    -c Release \
    -o /app \
    --self-contained \
    -r linux-x64 \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true

# ── Runtime stage (chiseled/distroless — no .NET runtime needed) ──
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .

# Run as non-root user (chiseled images include UID 1654)
USER 1654

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["./WhiskeyAndSmokes.Api"]
