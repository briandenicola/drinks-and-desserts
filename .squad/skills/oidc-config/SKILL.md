# OIDC Configuration Skill

## When to use

Use this when adding or reviewing deployment support for OIDC providers in Whiskey & Smokes.

## Pattern

- Treat provider client IDs and client secrets as app-managed data configured in the admin UI, not Terraform/GitHub/Docker deployment inputs.
- Terraform should provision durable prerequisites such as Cosmos containers and runtime secrets required to start the API.
- Keep local Docker variable names user-friendly (`ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE`) and map them to ASP.NET configuration keys in compose files.
- Document redirect URIs and provider setup in `docs/oidc-setup.md`; do not invent unused `POCKET_ID_*` environment variables.

## Validation

Run `terraform fmt -check` on changed Terraform files and `task --list` after Taskfile edits.
