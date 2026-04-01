# Whiskey & Smokes 🥃💨

Track your whiskey, wine, cocktails & cigars. Snap a photo at the bar, let AI do the rest, refine later.

## Architecture

- **Frontend**: Vue 3 + TypeScript + TailwindCSS (mobile-first PWA)
- **Backend**: .NET 10 Web API
- **AI Pipeline**: Multi-agent workflow via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (1.0.0-rc4)
- **Orchestration**: .NET Aspire AppHost (OpenTelemetry dashboard)
- **Database**: Azure CosmosDB (prod) / LiteDB (local dev)
- **Storage**: Azure Blob Storage (prod) / local filesystem (local dev)
- **AI Models**: Azure AI Foundry — gpt-4o (vision) + gpt-5-mini (reasoning)
- **Observability**: OpenTelemetry → Azure Application Insights + Aspire Dashboard
- **Auth**: JWT (local dev) / Azure Entra ID (prod)
- **Infra**: Terraform → Azure Container Apps
- **CI/CD**: GitHub Actions

## AI Pipeline

The app uses a multi-agent graph workflow to analyze captured photos:

```
📷 CaptureInput
      │
      ▼
┌─────────────┐
│   Vision     │  gpt-4o — examines photos, describes visible items
│   Analyst    │  (labels, bottles, glasses, cigar bands, menus)
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Domain     │  gpt-5-mini — identifies specific products,
│   Expert     │  adds tasting notes, origins, flavor profiles
└──────┬──────┘
       │
       ▼
┌─────────────┐     ┌─────────────┐
│    Data      │────▶│   Output    │  Approved → structured Item JSON
│   Curator    │     └─────────────┘
└──────┬──────┘
       │ reject (max 2×)
       └──────────▶ back to Domain Expert with feedback
```

Agent prompts are stored as markdown files in `src/AgentInitiator/Prompts/` and viewable (read-only) in the admin panel. To update prompts, edit the files and re-run `task test:agent:init`.

When AI Foundry is not configured, the system falls back to keyword-based local extraction.

## Documentation

| Guide | Description |
|-------|-------------|
| [Local Development](docs/local-development.md) | Prerequisites, setup, running, building, testing, troubleshooting |
| [Local Docker Deployment](docs/local-docker-deployment.md) | Self-hosted deployment with Docker Compose (LiteDB + local storage) |
| [Azure Deployment](docs/azure-deployment.md) | Terraform stacks, GitHub Actions, OIDC setup, secrets & variables |

## Quick Start

```bash
az login
task local:up          # Provision AI Foundry
task test:agent:init   # Create Foundry agents
task test:run          # Start API + Web
```

See [Local Development](docs/local-development.md) for full setup instructions.

## Admin Features

Access the admin panel at `/admin` (requires admin role — the first registered user is automatically promoted to admin).

| Feature | Description |
|---------|-------------|
| **User Management** | List users, toggle roles, reset passwords, delete accounts |
| **AI Prompts** | View prompts for each agent (Vision Analyst, Domain Expert, Data Curator) |
| **Foundry Status** | Agent validation status, connectivity test, configuration display |
| **Logging** | Configure per-category log levels at runtime |

## Project Structure

```
├── src/
│   ├── api/                    # .NET 10 Web API
│   │   ├── Agents/             # Multi-agent workflow (WorkflowAgentService, executors)
│   │   ├── Controllers/        # REST API endpoints
│   │   ├── Models/             # Domain models (Capture, Item, User, Prompt)
│   │   └── Services/           # Business logic (Auth, CosmosDB, Blob, Prompts)
│   ├── AgentInitiator/         # CLI tool — creates/recreates agents in Foundry
│   │   └── Prompts/            # Agent prompt markdown files
│   ├── AppHost/                # .NET Aspire orchestrator
│   ├── ServiceDefaults/        # Shared OpenTelemetry, health checks
│   ├── web/                    # Vue 3 Frontend
│   └── WhiskeyAndSmokes.sln
├── infrastructure/
│   ├── local/                  # Terraform — local dev (AI Foundry only)
│   ├── azure/                  # Terraform — full Azure environment
│   ├── app/                    # Terraform — Container Apps deployment
│   └── modules/                # Shared Terraform modules
├── tasks/                      # Taskfile configs + Docker Compose
├── docs/                       # Documentation
│   ├── local-development.md
│   ├── local-docker-deployment.md
│   └── azure-deployment.md
├── .github/workflows/
│   └── build.yml              # Build images + push to ACR
├── docker-compose.yml             # Local Docker deployment
├── Taskfile.yml
└── README.md
```
