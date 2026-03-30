# SipPuff 🥃💨

Track your whiskey, wine, cocktails & cigars. Snap a photo at the bar, let AI do the rest, refine later.

## Architecture

- **Frontend**: Vue 3 + TypeScript + TailwindCSS (mobile-first PWA)
- **Backend**: .NET 10 Web API
- **Database**: Azure CosmosDB (prod) / LiteDB (local dev)
- **Storage**: Azure Blob Storage (prod) / local filesystem (local dev)
- **AI**: Azure AI Foundry Agent Service
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

The only Azure dependency for local dev is AI Foundry (for the AI agent features).
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
CosmosDB or Blob Storage emulators needed. The AI Foundry endpoint is automatically
injected from your Terraform state.

```bash
task app:run
```

This starts both services concurrently:
- **API**: http://localhost:5062 (.NET 10, Swagger at `/openapi/v1.json`)
- **Web**: http://localhost:5173 (Vue 3 + Vite dev server with hot reload)

To run them individually:

```bash
task app:run:api    # API only — auto-configures AiFoundry__Endpoint from Terraform
task app:run:web    # Frontend only
```

### 5. Build

```bash
task app:build          # Both API and web
task app:build:api      # .NET API only
task app:build:web      # Vue frontend only (npm ci + vite build)
```

### 6. Test

```bash
task app:test           # All tests
task app:test:api       # .NET tests (dotnet test)
task app:test:web       # Vue TypeScript type checking (vue-tsc)
```

### 7. Docker Compose (optional)

Run both services as containers. This builds Docker images and runs them together.

```bash
task app:docker:up      # Build and start containers
task app:docker:logs    # Tail logs
task app:docker:down    # Stop and remove containers
```

- **API container**: http://localhost:5062
- **Web container**: http://localhost:8080

### 8. Tear down Azure resources

When you're done, destroy the AI Foundry resources to avoid charges:

```bash
task local:down
```

## How Local Dev Works

| Concern | Local | Production |
|---------|-------|------------|
| Database | LiteDB (file-based, zero config) | Azure CosmosDB |
| Blob Storage | Local filesystem (`uploads/`) | Azure Blob Storage |
| AI Agent | Azure AI Foundry (`DefaultAzureCredential`) | Azure AI Foundry (Managed Identity) |
| Auth | JWT with dev secret | Azure Entra ID |

The API auto-detects the environment: when `CosmosDb:Endpoint` and `CosmosDb:ConnectionString`
are both empty and `ASPNETCORE_ENVIRONMENT=Development`, it falls back to LiteDB and local
filesystem storage. AI features require the Foundry endpoint to be configured.

## Available Tasks

Run `task --list` to see all available tasks.

### App Tasks (`task app:*`)
| Task | Description |
|------|-------------|
| `task app:build` | Builds both API and web frontend |
| `task app:build:api` | Builds the .NET API |
| `task app:build:web` | Builds the Vue frontend |
| `task app:run` | Runs API and web frontend concurrently |
| `task app:run:api` | Runs the .NET API (auto-configures AI Foundry endpoint) |
| `task app:run:web` | Runs the Vue dev server |
| `task app:test` | Runs all tests |
| `task app:test:api` | Runs .NET API tests |
| `task app:test:web` | Runs Vue type checking |
| `task app:docker:up` | Starts all services via Docker Compose |
| `task app:docker:down` | Stops Docker Compose services |
| `task app:docker:logs` | Tails Docker Compose logs |

### Local Infrastructure (`task local:*`)
| Task | Description |
|------|-------------|
| `task local:up` | Provisions Azure AI Foundry for local dev |
| `task local:apply` | Applies Terraform changes |
| `task local:output` | Shows Terraform outputs (including AI Foundry endpoint) |
| `task local:down` | Destroys local Azure resources |

### Azure Infrastructure (`task azure:*`)
| Task | Description |
|------|-------------|
| `task azure:up` | Creates full Azure environment |
| `task azure:plan` | Plans Terraform changes |
| `task azure:output` | Shows Terraform outputs |
| `task azure:down` | Destroys all Azure resources |

## Project Structure

```
├── src/
│   ├── api/                    # .NET 10 Web API
│   │   └── SipPuff.Api/
│   └── web/                    # Vue 3 Frontend
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
