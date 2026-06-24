# OIDC sign-in provider setup

Whiskey & Smokes supports local username/password sign-in and admin-configured OpenID Connect providers such as Microsoft Entra ID and Pocket ID. Keep at least one admin account with a local password so the app can be recovered if an external provider is unavailable or misconfigured.

## Redirect URIs

Register both frontend redirect URIs for each provider, replacing the host and provider ID:

- Login: `https://your-app.example/auth/oidc/callback/{providerId}`
- Account linking: `https://your-app.example/profile/oidc/link/callback/{providerId}`

Use the deployed Static Web App host in Azure and `http://localhost:<WEB_PORT>` for local Docker. In Microsoft Entra ID, add these under the **Web** platform because the API redeems the authorization code with a client secret.

Before enabling providers in production, open **Admin Panel > Settings** and set **OIDC public web origin** to the browser-facing app origin, for example `https://your-app.example`. This value is used to build OIDC redirect URIs and takes precedence over deployment fallback configuration.

## Microsoft Entra ID

1. In Entra admin center, create or choose an App registration.
2. Add the two frontend Web redirect URIs above under Authentication.
3. Create a client secret and copy the **Value** immediately; do not use the Secret ID.
4. In Admin Settings, create an OIDC provider:
   - Provider type: `Microsoft Entra ID`
   - Display name: `Microsoft` or another user-facing label
   - Issuer URL: `https://login.microsoftonline.com/{tenant-id}/v2.0`
   - Client ID: Entra app client ID
   - Client secret: Entra client secret value
   - Scopes: `openid profile email`
5. Run the provider test and confirm discovery succeeds.
6. Enable the provider.

The older MSAL-based Entra configuration (`ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE`) is still wired for compatibility, but new deployments should prefer the admin-configured OIDC provider flow.

## Pocket ID

1. In Pocket ID, create an OIDC client/application.
2. Add the two frontend redirect URIs above.
3. Copy the client ID and client secret.
4. In Admin Settings, create an OIDC provider:
   - Provider type: `Pocket ID`
   - Display name: `Pocket ID`
   - Issuer URL: Pocket ID issuer base URL that serves `/.well-known/openid-configuration`
   - Client ID: Pocket ID client ID
   - Client secret: Pocket ID client secret
   - Scopes: `openid profile email`
5. Run the provider test and confirm discovery succeeds.
6. Enable the provider.

Provider client secrets are app data, not deployment variables. Store and rotate them through the admin UI; do not commit them to `.env`, Terraform files, or GitHub Actions.

## Azure Container Apps configuration

Terraform writes these API environment variables:

| Variable | Source | Required |
|----------|--------|----------|
| `Jwt__Secret` | `var.jwt_secret` secret | Yes |
| `Jwt__Issuer` | fixed `whiskey-and-smokes` | Yes |
| `Jwt__Audience` | fixed `whiskey-and-smokes` | Yes |
| `Oidc__PublicOrigin` | Bootstrap fallback from Static Web App host or `OIDC_PUBLIC_ORIGIN` override | Recommended |
| `EntraId__TenantId` | `var.entra_tenant_id` | No |
| `EntraId__ClientId` | `var.entra_client_id` | No |
| `EntraId__Audience` | `var.entra_audience` | No |

For `task azure:app:deploy`, set `JWT_SECRET` in the shell. Terraform defaults `Oidc__PublicOrigin` to the Static Web App host for bootstrap; set `OIDC_PUBLIC_ORIGIN` only when using a custom domain before the admin setting exists. After deployment, manage the canonical origin in **Admin Panel > Settings**. Set `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, and optionally `ENTRA_AUDIENCE` only if you still need the older MSAL-based Microsoft sign-in.

The Azure stack also creates Cosmos DB containers for OIDC providers, auth states, and linked external identities so provider configuration works without portal clicks.
