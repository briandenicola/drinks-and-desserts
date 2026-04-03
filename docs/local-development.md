# Local Development

The only Azure dependency for local development is **Azure AI Foundry** (for the multi-agent vision pipeline). All other services use local alternatives.

| Concern | Local | Production |
|---------|-------|------------|
| Database | LiteDB (file-based, zero config) | Azure CosmosDB |
| Blob Storage | Local filesystem (`uploads/`) | Azure Blob Storage |
| AI Vision | gpt-4o via Azure AI Foundry | gpt-4o via Azure AI Foundry |
| AI Reasoning | gpt-5-mini via Azure AI Foundry | gpt-5-mini via Azure AI Foundry |
| Observability | Aspire Dashboard (OTLP) | Azure Application Insights |
| Auth | JWT with dev secret | Azure Entra ID + JWT |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Task](https://taskfile.dev) (task runner)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Terraform](https://developer.hashicorp.com/terraform/install)
- An Azure subscription with permissions to create Cognitive Services resources

## Getting Started

### 1. Clone and install dependencies

```bash
git clone <repo-url> && cd whiskey-and-smokes
```

### 2. Sign in to Azure

Authentication uses your Azure CLI credentials via `DefaultAzureCredential`.

```bash
az login
```

### 3. Provision Azure AI Foundry

Creates a resource group, an Azure AI Foundry account, deploys **gpt-4o** (vision) and **gpt-5-mini** (reasoning) models, and grants your Azure user the **Cognitive Services OpenAI User** role.

```bash
task local:up
```

### 4. Initialize Foundry Agents

Creates (or recreates) the three agents in your Foundry project with prompts from `src/AgentInitiator/Prompts/`:

```bash
task local:agents
```

### 5. Run the application

The API starts with LiteDB and local filesystem storage — no CosmosDB or Blob Storage emulators needed. AI Foundry and Application Insights endpoints are automatically injected from your Terraform state.

```bash
task test:run
```

This starts both the Aspire AppHost (API + OpenTelemetry dashboard) and the Vue dev server:

| Service | URL | Notes |
|---------|-----|-------|
| API | http://localhost:5062 | .NET 10, OpenAPI at `/openapi/v1.json` |
| Web | http://localhost:5173 | Vue 3 + Vite with hot reload |
| Aspire Dashboard | http://localhost:18888 | Traces, metrics, logs |

To run services individually:

```bash
task test:apphost  # API via Aspire (with dashboard)
task test:api      # API standalone (no Aspire)
task test:web      # Frontend only
```

### 6. Build

```bash
task test:build        # Full .NET solution + Vue frontend
task test:build:web    # Vue frontend only
```

### 7. Tear down Azure resources

When you're done, destroy the AI Foundry resources to avoid charges:

```bash
task local:down
```

## Available Tasks

Run `task --list` to see all tasks. Key ones for local dev:

### Test/Dev Tasks (`task test:*`)

| Task | Description |
|------|-------------|
| `test:build` | Builds the full .NET solution and Vue frontend |
| `test:build:web` | Builds the Vue frontend only |
| `test:run` | Runs Aspire AppHost + Vue frontend + Docker services |
| `test:apphost` | Runs the Aspire AppHost (API + dashboard) |
| `test:api` | Runs the .NET API standalone |
| `test:web` | Runs the Vue dev server |
| `test:services:up` | Starts Docker Compose services (Aspire dashboard, emulators) |
| `test:services:down` | Stops Docker Compose services |

### Local Infrastructure (`task local:*`)

| Task | Description |
|------|-------------|
| `local:up` | Provisions Azure AI Foundry for local dev |
| `local:apply` | Applies Terraform changes |
| `local:agents` | Creates/recreates agents in AI Foundry |
| `local:output` | Shows Terraform outputs (endpoint, App Insights) |
| `local:down` | Destroys local Azure resources |

## Observability

The application uses OpenTelemetry for distributed tracing, metrics, and structured logging.

### Exporters

| Exporter | Env Var | Purpose |
|----------|---------|---------|
| OTLP | `OTEL_EXPORTER_OTLP_ENDPOINT` | Aspire Dashboard (local dev) |
| Azure Monitor | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights (optional locally) |

Both exporters can run simultaneously. The Aspire AppHost auto-configures OTLP. Application Insights is injected from Terraform when available.

### Custom Trace Sources

| Source | Purpose |
|--------|---------|
| `WhiskeyAndSmokes.Api` | General operations |
| `WhiskeyAndSmokes.Api.Auth` | Authentication & authorization |
| `WhiskeyAndSmokes.Api.Captures` | Photo capture workflow |
| `WhiskeyAndSmokes.Api.Agent` | AI agent interactions |
| `WhiskeyAndSmokes.Api.Workflow` | Multi-agent workflow orchestration |
| `WhiskeyAndSmokes.Api.Storage` | CosmosDB & blob storage |
| `WhiskeyAndSmokes.Api.Admin` | Admin operations |

### Runtime Log Levels

Log levels are configurable at runtime from the **Admin Panel → Logging** tab. Changes take effect immediately without restart and are persisted to the database.

## Troubleshooting

### SSL certificate errors connecting to Foundry

If you're behind a TLS-inspecting proxy (corporate network, Zscaler, etc.), you may see `UntrustedRoot` errors. The Taskfile sets `SSL_CERT_DIR=/etc/ssl/certs` to trust system-installed CAs. If that doesn't work:

1. Export your proxy's root CA as a `.crt` file
2. Copy it to `/usr/local/share/ca-certificates/`
3. Run `sudo update-ca-certificates`

### Aspire dashboard not accessible

The dashboard runs on port 18888. If running standalone (`task test:api`), there is no Aspire dashboard — use Application Insights or attach a local OTLP collector.

### Foundry agents not found

Run `task local:agents` to create the agents. Agent version must be `"1"` (not `"latest"`).

## JWT Secret

In local development, a dev secret is auto-generated. In production, the application **will not start** unless `Jwt__Secret` is configured via environment variable. The secret must be at least 32 characters.

## External API (iOS Shortcuts)

The API exposes `POST /api/external/capture` for external integrations. To test locally:

1. Start the app with `task test:run`
2. Register a user and log in
3. Go to Profile and create an API key
4. Test with curl:

```bash
curl -X POST http://localhost:5062/api/external/capture \
  -H "X-API-Key: ws_your_key_here" \
  -F "images=@photo.jpg" \
  -F "note=Great old fashioned at the speakeasy"
```

Accepted image formats: JPEG, PNG, GIF, WebP, HEIC (max 15MB per file).

## Item Types

The application supports six item types:

| Type | Description |
|------|-------------|
| `whiskey` | Bourbon, scotch, rye, etc. |
| `wine` | All varietals and regions |
| `cocktail` | Mixed drinks and cocktails |
| `cigar` | Premium cigars |
| `venue` | Bars, lounges, restaurants |
| `custom` | Anything that doesn't fit the other types |

Types are editable after AI detection — the user can always change the assigned type.
