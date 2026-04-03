# Whiskey & Smokes

Track your whiskey, wine, cocktails, cigars, and venues. Snap a photo at the bar, let AI do the rest, refine later.

## Features

- **Photo Capture** with AI-powered item identification (1-3 items per capture)
- **Six item types**: Whiskey, Wine, Cocktail, Cigar, Venue, and Custom
- **Star ratings** with half/quarter star precision
- **Journal entries** for tasting notes and thoughts
- **Wishlist** to track items you want to try
- **Collection stats** with breakdowns by type
- **Sorting** by rating, date added, or date updated (configurable default in settings)
- **Filtering** by item type via dropdown menu
- **Autocomplete** for name, brand, and tags based on your existing collection
- **Photo management** on items (add, remove photos after capture)
- **External API** for iOS Shortcuts and other integrations (API key auth)
- **PWA support** — installable on iOS/Android with offline-capable service worker

## Architecture

- **Frontend**: Vue 3 + TypeScript + TailwindCSS (mobile-first PWA)
- **Backend**: .NET 10 Web API
- **AI Pipeline**: Multi-agent workflow via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (1.0.0-rc4)
- **Orchestration**: .NET Aspire AppHost (OpenTelemetry dashboard)
- **Database**: Azure CosmosDB (prod) / LiteDB (local dev)
- **Storage**: Azure Blob Storage (prod) / local filesystem (local dev)
- **AI Models**: Azure AI Foundry — gpt-4o (vision) + gpt-5-mini (reasoning)
- **Observability**: OpenTelemetry → Azure Application Insights + Aspire Dashboard
- **Auth**: JWT (local/self-hosted) / Azure Entra ID (prod) / API keys (external integrations)
- **Infra**: Terraform → Azure Container Apps (API) + Static Web App (frontend)
- **CI/CD**: GitHub Actions — PR checks, Azure deployment, Docker Hub publishing

## AI Pipeline

The app uses a multi-agent graph workflow to analyze captured photos:

```
  CaptureInput
      |
      v
+--------------+
|   Vision     |  gpt-4o -- examines photos, describes visible items
|   Analyst    |  (labels, bottles, glasses, cigar bands)
+------+-------+
       |
       v
+--------------+
|   Domain     |  gpt-5-mini -- identifies specific products,
|   Expert     |  adds tasting notes, origins, flavor profiles
+------+-------+
       |
       v
+--------------+     +--------------+
|    Data      |---->|   Output     |  Approved -> structured Item JSON
|   Curator    |     +--------------+
+------+-------+
       | reject (max 2x)
       +-----------> back to Domain Expert with feedback
```

The pipeline focuses on 1-3 primary items per capture. Related observations (e.g., a bottle and the glass poured from it) are combined into a single item. The system will not catalog background items like menu boards or shelf displays.

Agent prompts are stored as markdown files in `src/AgentInitiator/Prompts/` and viewable (read-only) in the admin panel. To update prompts, edit the files and re-run `task local:agents`.

When AI Foundry is not configured, the system falls back to keyword-based local extraction.

## Documentation

| Guide | Description |
|-------|-------------|
| [Local Development](docs/local-development.md) | Prerequisites, setup, running, building, testing, troubleshooting |
| [Local Docker Deployment](docs/local-docker-deployment.md) | Self-hosted deployment with Docker Compose and Portainer |
| [Azure Deployment](docs/azure-deployment.md) | Terraform stacks, GitHub Actions, OIDC setup, secrets & variables |

## Quick Start

```bash
az login
task local:up          # Provision AI Foundry
task local:agents      # Create Foundry agents
task test:run          # Start API + Web
```

See [Local Development](docs/local-development.md) for full setup instructions.

## External API

The app exposes an API for external integrations like iOS Shortcuts:

```
POST /api/external/capture
Headers: X-API-Key: <your-key>
Body: multipart/form-data (images + optional "note" field)
```

API keys are managed in the Profile tab. Keys are hashed with SHA256 and stored securely. The raw key is shown only once at creation time.

Accepted image formats: JPEG, PNG, GIF, WebP, HEIC (max 15MB per file, 50MB total).

## Admin Features

Access the admin panel at `/admin` (requires admin role — the first registered user is automatically promoted to admin).

| Feature | Description |
|---------|-------------|
| **User Management** | List users, toggle roles, reset passwords, delete accounts |
| **AI Prompts** | View prompts for each agent (Vision Analyst, Domain Expert, Data Curator) |
| **Foundry Status** | Agent validation status, connectivity test, configuration display |
| **Logging** | Configure per-category log levels at runtime |

## Security

- JWT tokens for authentication; API key auth for external integrations
- Path traversal protection on all file upload/download endpoints
- Server-side file type validation (allowlisted image extensions only)
- Blob URL ownership validation (users can only modify their own photos)
- AI call timeouts (3 minutes) to prevent thread starvation
- Prompt injection mitigation with explicit input delimiters
- Constant-time API key hash comparison
- Production startup fails if JWT secret is not configured

## Project Structure

```
src/
  api/                          .NET 10 Web API
    Agents/                     Multi-agent workflow (WorkflowAgentService, executors)
    Controllers/                REST API endpoints
      AuthController            Registration, login, Entra ID sign-in
      CapturesController        Photo capture + AI processing
      ItemsController           Collection CRUD, photo management, suggestions
      ExternalController        External API for iOS Shortcuts (API key auth)
      UsersController           Profile, preferences, API key management
      AdminController           User management, prompts, logging, diagnostics
      UploadsController         Local file upload/download (dev/self-hosted)
    Models/                     Domain models (Capture, Item, User, Prompt)
    Services/                   Business logic (Auth, CosmosDB, Blob, Prompts)
  AgentInitiator/               CLI tool -- creates/recreates agents in Foundry
    Prompts/                    Agent prompt markdown files
  AppHost/                      .NET Aspire orchestrator
  ServiceDefaults/              Shared OpenTelemetry, health checks
  web/                          Vue 3 Frontend (PWA)
  WhiskeyAndSmokes.sln
infrastructure/
  local/                        Terraform -- local dev (AI Foundry only)
  azure/                        Terraform -- full Azure environment
  app/                          Terraform -- Container App (API) + Static Web App
tasks/                          Taskfile configs + Docker Compose files
  docker-compose.local.test.yml   Local dev services (Aspire dashboard)
  docker-compose.local.prod.yml   Self-hosted build-from-source deployment
  docker-compose.portainer.yml    Self-hosted pre-built image deployment
docs/
  local-development.md
  local-docker-deployment.md
  azure-deployment.md
.github/workflows/
  ci.yml                        PR checks (build + type-check)
  build.yml                     Build API image + deploy SWA (Azure)
  docker-publish.yml            Build and push images to Docker Hub
Taskfile.yml
README.md
```
