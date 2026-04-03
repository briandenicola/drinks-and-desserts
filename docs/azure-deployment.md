# Azure Deployment

This project deploys to Azure using **Terraform** for infrastructure and **GitHub Actions** for CI/CD. The infrastructure is split into two stacks that are deployed sequentially.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Azure Stack (infrastructure/azure/)                    │
│                                                         │
│  Resource Group ─┬─ AI Foundry Hub + Project            │
│                  ├─ Model Deployments (gpt-4o, gpt-5-mini) │
│                  ├─ Cosmos DB (items, captures, users, prompts) │
│                  ├─ Storage Account (photos blob)        │
│                  ├─ Application Insights + Log Analytics │
│                  └─ Container App Environment + ACR      │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  App Stack (infrastructure/app/)                        │
│                                                         │
│  Resource Group ─┬─ API Container App                   │
│                  ├─ Static Web App (Vue SPA)             │
│                  ├─ User Assigned Managed Identity       │
│                  └─ Role Assignments (Cosmos, Storage,   │
│                     Cognitive Services, ACR)             │
└─────────────────────────────────────────────────────────┘
```

## Terraform Stacks

### Azure Stack (`infrastructure/azure/`)

Provisions the core Azure resources. Run first.

```bash
task azure:up      # init + apply
task azure:plan    # plan only
task azure:output  # show outputs
task azure:down    # destroy
```

**Key outputs** consumed by the app stack and CI/CD:

| Output | Description |
|--------|-------------|
| `OPENAI_ENDPOINT` | AI Foundry cognitive services endpoint |
| `FOUNDRY_PROJECT_ENDPOINT` | Foundry project endpoint for agent API |
| `APPLICATION_INSIGHTS_CONNECTION_STRING` | App Insights connection string |
| `COSMOSDB_ENDPOINT` | Cosmos DB account endpoint |
| `STORAGE_BLOB_ENDPOINT` | Storage account blob endpoint |

### App Stack (`infrastructure/app/`)

Deploys the container apps and role assignments. Requires the azure stack to be provisioned first.

**Variables** (passed from azure stack outputs or GitHub vars):

| Variable | Description |
|----------|-------------|
| `app_name` | Base resource name (e.g. `whiskey-and-smokes`) |
| `tags` | Resource tags |
| `commit_version` | Container image tag (git SHA) |
| `cosmosdb_endpoint` | Cosmos DB endpoint (from azure stack output) |
| `storage_blob_endpoint` | Storage blob endpoint (from azure stack output) |

## GitHub Actions Workflows

### `ci.yml` — PR Checks

Runs on pull requests to `main`. Builds the .NET solution and type-checks/builds the Vue frontend.

### `build.yml` — Build & Deploy (Azure)

Builds the API container image and deploys the Vue frontend to Azure Static Web Apps.

**Triggers:**
- Manual dispatch with environment selection (dev/prod)

**Jobs:**
1. `build-api` — uses `az acr build` (ACR Build Tasks) to build and push the API image directly in ACR
2. `deploy-web` — runs `npm ci && npm run build`, then deploys the `dist/` folder to Azure Static Web Apps using the SWA deployment token

### `docker-publish.yml` — Build & Push (Docker Hub)

Builds and pushes API and Web container images to Docker Hub for self-hosted deployments.

**Triggers:**
- Push to `main`
- Manual dispatch

**Jobs:**
1. `build-and-push-api` — builds `src/api/Dockerfile`, pushes to `{DOCKERHUB_USERNAME}/whiskey-and-smokes-api`
2. `build-and-push-web` — builds `src/web/Dockerfile`, pushes to `{DOCKERHUB_USERNAME}/whiskey-and-smokes-web`

Tags: `latest`, `sha-{short}`, `sha-{long}`.

## GitHub Configuration

### Secrets (per environment)

Configure these in **Settings → Environments → dev** (and `prod` if applicable):

| Secret | Description | Example |
|--------|-------------|---------|
| `AZURE_CLIENT_ID` | Service principal app (client) ID for OIDC | `00000000-0000-0000-0000-000000000000` |
| `AZURE_TENANT_ID` | Azure AD tenant ID | `00000000-0000-0000-0000-000000000000` |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID | `00000000-0000-0000-0000-000000000000` |
| `ACR_NAME` | Azure Container Registry name | `whiskeyandsmokesacr` |
| `SWA_DEPLOYMENT_TOKEN` | Static Web App deployment token (from `terraform output SWA_DEPLOYMENT_TOKEN`) | `(auto-generated)` |

### Secrets (repository-level, for Docker Hub)

Configure these in **Settings → Secrets and variables → Actions**:

| Secret | Description |
|--------|-------------|
| `DOCKERHUB_USERNAME` | Your Docker Hub username |
| `DOCKERHUB_TOKEN` | Docker Hub access token |

### Variables (per environment)

Configure these in **Settings → Environments → dev**:

| Variable | Description | Example |
|----------|-------------|---------|
| `APP_NAME` | Base resource name | `whiskey-and-smokes` |

### OIDC Setup

The workflows use [OIDC federation](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure) for authentication — no client secrets are stored in GitHub.

#### 1. Create a Service Principal

```bash
# Set variables
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
APP_NAME="whiskey-and-smokes-github"

# Create the Azure AD app registration and service principal
az ad app create --display-name "$APP_NAME"
APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
az ad sp create --id "$APP_ID"
SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
```

#### 2. Grant Permissions

The service principal needs **Contributor** (to create resources) and **User Access Administrator** (to assign RBAC roles to managed identities) on the subscription:

```bash
az role assignment create \
  --assignee "$SP_OBJECT_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

az role assignment create \
  --assignee "$SP_OBJECT_ID" \
  --role "User Access Administrator" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"
```

#### 3. Add Federated Credentials

Create federated credentials so GitHub Actions can authenticate without a client secret. You need one credential for each subject pattern used by the workflows:

```bash
# For pushes to main branch (used by build.yml)
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-main-branch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:briandenicola/whiskey-and-smokes:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# For the "dev" GitHub environment (used by build.yml)
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-env-dev",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:briandenicola/whiskey-and-smokes:environment:dev",
  "audiences": ["api://AzureADTokenExchange"]
}'

# For the "prod" GitHub environment (optional, when ready)
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-env-prod",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:briandenicola/whiskey-and-smokes:environment:prod",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

#### 4. Retrieve Values for GitHub Secrets

```bash
echo "AZURE_CLIENT_ID:       $APP_ID"
echo "AZURE_TENANT_ID:       $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
```

#### 5. Configure GitHub Environment

1. Go to **Settings → Environments** in your GitHub repository
2. Create the `dev` environment (and `prod` when ready)
3. Add the secrets listed above
4. Add the **variables** listed above

## Container Apps

### API (`{app_name}-api`)

| Setting | Value |
|---------|-------|
| Image | `{acr_name}.azurecr.io/whiskey-and-smokes-api:{sha}` |
| Port | 8080 |
| CPU / Memory | 1 core / 2Gi |
| Replicas | 1–3 |
| Identity | User-assigned managed identity |

**Environment variables:**

| Var | Source |
|-----|--------|
| `AiFoundry__ProjectEndpoint` | Derived from AI Foundry project |
| `AiFoundry__Models__Vision` | `gpt-4o` |
| `AiFoundry__Models__Reasoning` | `gpt-5-mini` |
| `CosmosDb__Endpoint` | Terraform variable |
| `CosmosDb__DatabaseName` | `whiskey-and-smokes` |
| `BlobStorage__Endpoint` | Terraform variable |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights |
| `AZURE_CLIENT_ID` | Managed identity client ID |

## Static Web App

### Web (`{app_name}-web`)

| Setting | Value |
|---------|-------|
| SKU | Free |
| Framework | Vue 3 (pre-built, deployed as static files) |
| Deployment | GitHub Actions via `Azure/static-web-apps-deploy@v1` |

The Vue SPA is built in CI and deployed as static files to Azure Static Web Apps.
API calls are proxied to the API Container App via a linked backend (configured in Azure Portal: SWA → Settings → APIs → Link to Container App).

## Role Assignments

The managed identity is granted:

| Role | Scope | Purpose |
|------|-------|---------|
| Cognitive Services OpenAI User | AI Services account | Call Foundry models/agents |
| Cosmos DB Built-in Data Contributor | Cosmos DB account | Read/write application data |
| Storage Blob Data Contributor | Storage account | Upload/download photos |
| AcrPull | Container Registry | Pull container images |

## Deployment Sequence

For a fresh environment:

```bash
# 1. Provision Azure infrastructure
task azure:up

# 2. Deploy Container Apps + Static Web App
task azure:app:deploy

# 3. Note the outputs
task azure:output
task azure:app:output

# 4. Add SWA_DEPLOYMENT_TOKEN to GitHub Secrets
#    (from `terraform -chdir=infrastructure/app output SWA_DEPLOYMENT_TOKEN`)

# 5. Link SWA backend to API Container App
#    Azure Portal → Static Web App → Settings → APIs → Link to Container App

# 6. Initialize Foundry agents
task local:agents

# 7. Push to main (or manually trigger build.yml)
git push origin main
```

For subsequent deployments, just push to `main` — the workflow builds the API image and deploys the SWA automatically.
