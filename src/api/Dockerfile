# ── Build stage ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Restore — copy only project files first for layer caching
COPY api/WhiskeyAndSmokes.Api.csproj api/
COPY ServiceDefaults/WhiskeyAndSmokes.ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore api/WhiskeyAndSmokes.Api.csproj

# Build + publish
COPY api/ api/
COPY ServiceDefaults/ ServiceDefaults/
RUN dotnet publish api/WhiskeyAndSmokes.Api.csproj -c Release -o /app

# ── Runtime stage (chiseled/distroless — ~30MB, no shell, non-root) ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WhiskeyAndSmokes.Api.dll"]
