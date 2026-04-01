# Local Docker Deployment

Run Whiskey & Smokes locally with Docker Compose. Data is stored in LiteDB (files) with a Docker volume for persistence. The only external dependency is Azure AI Foundry for the AI pipeline.

## Prerequisites

- Docker and Docker Compose
- Azure AI Foundry project (for the AI agent pipeline)
- Azure CLI (for `az login` — Foundry auth uses DefaultAzureCredential)

## Quick Start

```bash
# 1. Copy and configure environment variables
cp tasks/.env.example tasks/.env
# Edit tasks/.env — set JWT_SECRET and AI_FOUNDRY_PROJECT_ENDPOINT

# 2. Log in to Azure (for Foundry access from the container)
az login

# 3. Build and start
docker compose --file tasks/docker-compose.local.prod.yml up --build -d

# 4. Open the app
open http://localhost:8080
```

The first user to register becomes the admin.

## Configuration

All configuration is in `tasks/.env`:

| Variable | Required | Description |
|----------|----------|-------------|
| `JWT_SECRET` | Yes | JWT signing key (min 32 characters) |
| `AI_FOUNDRY_PROJECT_ENDPOINT` | Yes | Azure AI Foundry project endpoint URL |
| `WEB_PORT` | No | Web UI port (default: 8080) |
| `ENTRA_TENANT_ID` | No | Entra ID tenant for Microsoft sign-in |
| `ENTRA_CLIENT_ID` | No | Entra ID app registration client ID |

## Data Persistence

Data is stored in a Docker volume (`app-data`):

- **Database**: LiteDB at `/data/whiskey-and-smokes.db`
- **Uploads**: Photo files at `/data/uploads/`

To back up your data:
```bash
docker compose cp api:/data ./backup
```

To restore:
```bash
docker compose cp ./backup/. api:/data
```

## Azure AI Foundry Authentication

The API container uses `DefaultAzureCredential` to authenticate with Foundry. For local Docker, this picks up credentials from:

1. **Environment variables** — Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET` in `tasks/.env` for a service principal
2. **Azure CLI** — If running on the same machine with `az login` active, mount the Azure CLI cache:

```yaml
# Add to tasks/docker-compose.local.prod.yml api service volumes:
volumes:
  - app-data:/data
  - ~/.azure:/root/.azure:ro
```

## Stopping and Cleanup

```bash
# Stop (preserves data)
docker compose --file tasks/docker-compose.local.prod.yml down

# Stop and remove data
docker compose --file tasks/docker-compose.local.prod.yml down -v
```

## Without AI Foundry

If `AI_FOUNDRY_PROJECT_ENDPOINT` is empty, the app falls back to keyword-based local extraction. Photos won't be analyzed by AI, but manual entry still works.
