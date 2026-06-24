# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** Terraform, Azure Container Apps, Azure Static Web Apps, GitHub Actions, Docker, Azure Blob Storage, .NET 10 backend, Vue 3.5 frontend
- **Created:** 2026-07-22

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- 2026-06-24: OIDC deployment config: Azure Container Apps receives `Jwt__Secret` as secret plus optional legacy `EntraId__*` values; provider client IDs/secrets configured through app admin UI. Cosmos containers for OIDC provisioned in `infrastructure/azure/cosmosdb.tf` (`oidc-providers`, `oidc-auth-states`, `external-identities`) with `/partitionKey`. Local Docker uses LiteDB with env wiring in `tasks/docker-compose*.yml`. OIDC setup docs in `docs/oidc-setup.md`. Docker helper env names: `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE`. Azure app deployment requires `JWT_SECRET` shell variable before `task azure:app:deploy`. **Security:** `Oidc__PublicOrigin` required for production, ignoring X-Forwarded-Host/Proto for OIDC URIs. Terraform validation: fmt-clean and validate passed. Team coordination: Keaton (backend), Fenster (frontend), Ralph (security review).
