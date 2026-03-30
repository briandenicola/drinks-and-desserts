# Whiskey & Smokes 🥃💨

Track your whiskey, wine, cocktails & cigars. Snap a photo at the bar, let AI do the rest, refine later.

## Architecture

- **Frontend**: Vue 3 + TypeScript + TailwindCSS (mobile-first PWA)
- **Backend**: .NET 10 Web API
- **Orchestration**: .NET Aspire AppHost (OpenTelemetry dashboard)
- **Database**: Azure CosmosDB (prod) / LiteDB (local dev)
- **Storage**: Azure Blob Storage (prod) / local filesystem (local dev)
- **AI**: Azure AI Foundry Agent Service (image analysis & item categorization)
- **Observability**: OpenTelemetry → Azure Application Insights + Aspire Dashboard
- **Auth**: JWT (local dev) / Azure Entra ID (prod)
- **Infra**: Terraform → Azure Container Apps
- **CI/CD**: GitHub Actions

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Task](https://taskfile.dev) (task runner)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (for AI Foundry provisioning)
- [Terraform](https://developer.hashicorp.com/terraform/install)
- An Azure subscription with permissions to create Cognitive Services resources

## Local Development Setup

### 1. Clone and install dependencies

```bash
git clone <repo-url> && cd whiskey-and-smokes
```

### 2. Sign in to Azure

The only Azure dependency for local dev is AI Foundry (for AI agent features).
Authentication uses your Azure CLI credentials via `DefaultAzureCredential`.

```bash
az login
```

### 3. Provision Azure AI Foundry

This creates a resource group, an Azure OpenAI (Foundry) account, a model deployment,
and grants your Azure user the **Cognitive Services OpenAI User** role.

```bash
task local:up
```

Verify the endpoint was created:

```bash
task local:output
```

### 4. Run the application

The API starts with LiteDB (file-based database) and local filesystem storage — no
CosmosDB or Blob Storage emulators needed. AI Foundry and Application Insights endpoints
are automatically injected from your Terraform state.

```bash
task app:run
```

This starts both the Aspire AppHost (API + OpenTelemetry dashboard) and the Vue dev server:

- **API**: http://localhost:5062 (.NET 10, OpenAPI at `/openapi/v1.json`)
- **Web**: http://localhost:5173 (Vue 3 + Vite dev server with hot reload)
- **Aspire Dashboard**: http://localhost:18888 (traces, metrics, logs)

To run services individually:

```bash
task app:run:apphost  # API via Aspire (with dashboard)
task app:run:api      # API standalone (no Aspire)
task app:run:web      # Frontend only
```

### 5. Build

```bash
task app:build        # Full .NET solution (API + AppHost + ServiceDefaults)
task app:build:web    # Vue frontend only (npm ci + vite build)
```

### 6. Test

```bash
task app:test         # All tests
task app:test:api     # .NET tests (dotnet test)
task app:test:web     # Vue TypeScript type checking (vue-tsc)
```

### 7. Docker Compose (optional)

Run services as containers, including CosmosDB emulator and Azurite.

```bash
task app:docker:up    # Build and start containers
task app:docker:logs  # Tail logs
task app:docker:down  # Stop and remove containers
```

### 8. Tear down Azure resources

When you're done, destroy the AI Foundry resources to avoid charges:

```bash
task local:down
```

## Observability

The application uses OpenTelemetry for distributed tracing, metrics, and structured logging.

### Exporters

| Exporter | Env Var | Purpose |
|----------|---------|---------|
| OTLP | `OTEL_EXPORTER_OTLP_ENDPOINT` | Aspire Dashboard (local dev) |
| Azure Monitor | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Application Insights (prod + optional local) |

Both exporters can run simultaneously. The Aspire AppHost auto-configures the OTLP endpoint.
Application Insights is auto-injected from Terraform when available.

### Custom trace sources

All key operations produce traces under these ActivitySources:
- `WhiskeyAndSmokes.Api` — general operations
- `WhiskeyAndSmokes.Api.Auth` — authentication & authorization
- `WhiskeyAndSmokes.Api.Captures` — photo capture workflow
- `WhiskeyAndSmokes.Api.Agent` — AI Foundry agent interactions
- `WhiskeyAndSmokes.Api.Storage` — CosmosDB & blob storage
- `WhiskeyAndSmokes.Api.Admin` — admin operations

### Runtime log level configuration

Log levels are configurable at runtime from the **Admin Panel → Logging** tab.
Changes take effect immediately without restart and are persisted to the database.

## Admin Features

Access the admin panel at `/admin` (requires admin role — the first registered user is
automatically promoted to admin).

| Feature | Description |
|---------|-------------|
| **User Management** | List users, toggle roles, reset passwords, delete accounts |
| **AI Prompts** | Edit the instructions sent to the AI agent for image analysis |
| **Logging** | Configure per-category log levels at runtime |

## How Local Dev Works

| Concern | Local | Production |
|---------|-------|------------|
| Database | LiteDB (file-based, zero config) | Azure CosmosDB |
| Blob Storage | Local filesystem (`uploads/`) | Azure Blob Storage |
| AI Agent | Azure AI Foundry (`DefaultAzureCredential`) | Azure AI Foundry (Managed Identity) |
| Observability | Aspire Dashboard (OTLP) | Azure Application Insights |
| Auth | JWT with dev secret | Azure Entra ID |

The API auto-detects the environment: when `CosmosDb:Endpoint` and `CosmosDb:ConnectionString`
are both empty and `ASPNETCORE_ENVIRONMENT=Development`, it falls back to LiteDB and local
filesystem storage. AI features require the Foundry endpoint to be configured.

## Available Tasks

Run `task --list` to see all available tasks.

### App Tasks (`task app:*`)
| Task | Description |
|------|-------------|
| `app:build` | Builds the full .NET solution |
| `app:build:web` | Builds the Vue frontend |
| `app:run` | Runs Aspire AppHost + Vue frontend |
| `app:run:apphost` | Runs the Aspire AppHost (API + dashboard) |
| `app:run:api` | Runs the .NET API standalone |
| `app:run:web` | Runs the Vue dev server |
| `app:test` | Runs all tests |
| `app:test:api` | Runs .NET API tests |
| `app:test:web` | Runs Vue type checking |
| `app:docker:up` | Starts all services via Docker Compose |
| `app:docker:down` | Stops Docker Compose services |
| `app:docker:logs` | Tails Docker Compose logs |

### Local Infrastructure (`task local:*`)
| Task | Description |
|------|-------------|
| `local:up` | Provisions Azure AI Foundry for local dev |
| `local:apply` | Applies Terraform changes |
| `local:output` | Shows Terraform outputs (AI Foundry endpoint, App Insights) |
| `local:down` | Destroys local Azure resources |

### Azure Infrastructure (`task azure:*`)
| Task | Description |
|------|-------------|
| `azure:up` | Creates full Azure environment |
| `azure:plan` | Plans Terraform changes |
| `azure:output` | Shows Terraform outputs |
| `azure:down` | Destroys all Azure resources |

## Project Structure

```
├── src/
│   ├── api/                    # .NET 10 Web API (WhiskeyAndSmokes.Api)
│   ├── AppHost/                # .NET Aspire orchestrator
│   ├── ServiceDefaults/        # Shared OpenTelemetry, health checks, resilience
│   ├── web/                    # Vue 3 Frontend
│   └── WhiskeyAndSmokes.sln    # Solution file
├── infrastructure/
│   ├── local/                  # Terraform — local dev (AI Foundry only)
│   ├── azure/                  # Terraform — full Azure environment
│   ├── app/                    # Terraform — Container Apps deployment
│   └── modules/                # Shared Terraform modules
├── tasks/
│   ├── Taskfile.app.yml        # Build, run, test tasks
│   ├── Taskfile.local.yml      # Local infrastructure tasks
│   ├── Taskfile.azure.yml      # Azure infrastructure tasks
│   └── docker-compose.yml      # Docker Compose for containerized dev
├── .github/workflows/          # CI/CD
├── Taskfile.yml                # Root task runner config
└── README.md
```
