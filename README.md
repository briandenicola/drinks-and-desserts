# Drinks & Desserts

Track your drinks, desserts, and venues. Snap a photo, let AI do the rest, refine later. Share your collection with friends.

## Features

- **Photo Capture** with AI-powered item identification (1-3 items per capture)
- **Seven item types**: Whiskey, Wine, Cocktail, Cigar, Dessert, Venue, and Custom
- **Venues** — capture bars, restaurants, and lounges with name, address, website, type, rating, and linked items. Create from photos or extract from URLs (including Google Maps links)
- **Friends** — invite friends via shareable links, browse each other's collections and venues, leave thoughts (comments + ratings) on items
- **Notifications** — in-app notification bell for friend requests, new thoughts, and social activity
- **Star ratings** with half/quarter star precision
- **Journal entries** for tasting notes and thoughts
- **Wishlist** to track items you want to try
- **Collection stats** with breakdowns by type
- **Search** across your entire collection
- **Sorting** by rating, date added, or date updated (configurable default in settings)
- **Filtering** by item type via dropdown menu
- **Autocomplete** for name, brand, tags, and venue labels based on your existing data
- **Photo management** on items (add, remove photos after capture)
- **External API** for iOS Shortcuts and other integrations (API key auth)
- **PWA support** — installable on iOS/Android with offline-capable service worker and refresh tokens

## Architecture

- **Frontend**: Vue 3.5 + TypeScript + Tailwind CSS 4 (mobile-first PWA)
- **Backend**: .NET 10 Web API
- **AI Pipeline**: Multi-agent workflow via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (1.0.0-rc4)
- **Orchestration**: .NET Aspire AppHost (OpenTelemetry dashboard)
- **Database**: Azure CosmosDB (prod) / LiteDB (local dev) — 8 containers
- **Storage**: Azure Blob Storage (prod) / local filesystem (local dev)
- **AI Models**: Azure AI Foundry — gpt-4o (vision) + gpt-5-mini (reasoning)
- **Observability**: OpenTelemetry → Azure Application Insights + Aspire Dashboard
- **Auth**: JWT with refresh tokens (local/self-hosted) / Azure Entra ID (prod) / API keys (external integrations)
- **Infra**: Terraform → Azure Container Apps (API) + Static Web App (frontend)
- **CI/CD**: GitHub Actions — PR checks, Azure deployment, Docker Hub publishing

### CosmosDB Containers

| Container | Partition Key | Description |
|-----------|--------------|-------------|
| `users` | `/partitionKey` (userId) | User accounts and preferences |
| `captures` | `/partitionKey` (userId) | Photo captures and AI workflow results |
| `items` | `/partitionKey` (userId) | Collection items (drinks, desserts, cigars) |
| `venues` | `/partitionKey` (userId) | Venues (bars, restaurants, lounges) |
| `friendships` | `/partitionKey` (userId) | Dual-document friendship records |
| `friend-invites` | `/partitionKey` (invite code) | Invite codes for friend discovery |
| `thoughts` | `/partitionKey` (targetUserId) | Friend thoughts/comments on items and venues |
| `notifications` | `/partitionKey` (userId) | In-app notification records |

### Friendship Dual-Document Pattern

Each friendship creates two CosmosDB documents — one in each user's partition. This enables efficient per-user queries (list my friends) without cross-partition reads. When User A and User B become friends:
- Document in A's partition: `{ userId: A, friendId: B, status: "accepted" }`
- Document in B's partition: `{ userId: B, friendId: A, status: "accepted" }`

Invite flow: sharing a link auto-accepts (the inviter pre-approved by sharing). The joiner clicking the link creates the friendship immediately.

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

## Friends API

Friends feature enables social sharing between users. All endpoints require authentication (`MultiAuth` or `ApiKey`).

### Friends

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/friends` | List accepted friends |
| `GET` | `/api/friends/requests` | List sent and received friend requests |
| `POST` | `/api/friends/invite` | Generate an 8-char invite code (7-day expiry) |
| `POST` | `/api/friends/join/{code}` | Join via invite code (auto-accepts) |
| `PUT` | `/api/friends/{id}/accept` | Accept a pending friend request |
| `PUT` | `/api/friends/{id}/decline` | Decline a pending friend request |
| `DELETE` | `/api/friends/{id}` | Remove a friend (deletes both sides) |
| `GET` | `/api/friends/{friendId}/items` | Browse a friend's collection items |
| `GET` | `/api/friends/{friendId}/items/{itemId}` | Get a specific friend's item |
| `GET` | `/api/friends/{friendId}/venues` | Browse a friend's venues |
| `GET` | `/api/friends/{friendId}/venues/{venueId}` | Get a specific friend's venue |

### Thoughts

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/thoughts/{targetType}/{targetId}?targetUserId=` | Get thoughts on an item or venue (`targetType`: `item` or `venue`) |
| `POST` | `/api/thoughts` | Leave a thought on a friend's item/venue (body: `content`, `targetUserId`, `targetType`, `targetId`, optional `rating` 1-5) |
| `PUT` | `/api/thoughts/{id}` | Edit own thought (body: `content`, optional `rating`) |
| `DELETE` | `/api/thoughts/{id}` | Delete own thought |
| `GET` | `/api/thoughts/mine` | Get all thoughts you've written |
| `GET` | `/api/thoughts/on-my-items` | Get all thoughts others left on your items/venues |

### Notifications

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/notifications?limit=50` | List notifications (max 200, returns `{ notifications, unreadCount }`) |
| `PUT` | `/api/notifications/{id}/read` | Mark a single notification as read |
| `PUT` | `/api/notifications/read-all` | Mark all notifications as read |

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

- JWT tokens with refresh token rotation for authentication; API key auth for external integrations
- Friendship validation on all cross-user data access endpoints (IDOR protection)
- Path traversal protection on all file upload/download endpoints
- Server-side file type validation (allowlisted image extensions only)
- Blob URL ownership validation (users can only modify their own photos)
- AI call timeouts (3 minutes) to prevent thread starvation
- Prompt injection mitigation with explicit input delimiters
- Constant-time API key hash comparison
- Invite codes: 8-char alphanumeric (no ambiguous chars), 7-day expiry, single-use
- Thought content validated (500 char limit, rating 1-5 range)
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
      VenuesController          Venue CRUD, URL extraction, logo extraction
      FriendsController         Friend invites, accept/decline, browse friend data
      ThoughtsController        Comments + ratings on friends' items and venues
      NotificationsController   In-app notification list and read status
      ExternalController        External API for iOS Shortcuts (API key auth)
      UsersController           Profile, preferences, API key management
      AdminController           User management, prompts, logging, diagnostics
      UploadsController         Local file upload/download (dev/self-hosted)
    Models/                     Domain models (Capture, Item, User, Venue, Friendship, Thought, Notification)
    Services/                   Business logic (Auth, CosmosDB, Blob, Prompts, Notifications)
  AgentInitiator/               CLI tool -- creates/recreates agents in Foundry
    Prompts/                    Agent prompt markdown files
  AppHost/                      .NET Aspire orchestrator
  ServiceDefaults/              Shared OpenTelemetry, health checks
  web/                          Vue 3.5 Frontend (PWA)
    src/views/                  Page views (Collection, ItemDetail, Venues, Friends, etc.)
    src/components/common/      Reusable components (StarRating, NotificationBell, ThoughtsList, etc.)
    src/services/               API client modules (items, venues, friends, thoughts, notifications)
    src/stores/                 Pinia state stores (auth, items, venues)
  WhiskeyAndSmokes.sln
infrastructure/
  local/                        Terraform -- local dev (AI Foundry only)
  azure/                        Terraform -- full Azure environment
  app/                          Terraform -- Container App (API) + Static Web App
tasks/                          Taskfile configs + Docker Compose files
  docker-compose.local.test.yml   Local dev services (Aspire dashboard)
  docker-compose.local.prod.yml   Self-hosted build-from-source deployment
  docker-compose.portainer.yml    Self-hosted pre-built image deployment
tests/
  WhiskeyAndSmokes.Tests/       xUnit v3 integration tests (55 tests)
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
