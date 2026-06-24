---
name: "oidc-auth-frontend"
description: "Port backend-driven OIDC login/linking flows into the Vue frontend"
domain: "frontend-auth"
confidence: "medium"
source: "observed"
tools:
  - name: "powershell"
    description: "Runs Vue project-mode typechecking"
    when: "Validate changed auth views and TypeScript services"
---

## Context

Use this pattern when adding dynamic OIDC providers such as Entra ID or Pocket ID to this Vue frontend.

## Patterns

- Keep existing local/MSAL login flows intact; add backend-driven OIDC providers as an alternate sign-in section.
- Load public providers from `/api/auth/oidc/providers`.
- Start login with `/api/auth/oidc/{providerId}/start` and callback path `/auth/oidc/callback/{providerId}`.
- Complete login callbacks in a dedicated route and call `auth.applyAuthResponse()` to store returned tokens.
- Put account linking in `ProfileView.vue`; start link with `/api/auth/oidc/{providerId}/link/start` and callback path `/profile/oidc/link/callback/{providerId}`.
- Use `/api/users/me/oidc-identities` for linked identity list/delete because this app uses `/users/me` for account-scoped APIs.
- Put admin provider management in `AdminView.vue` unless admin architecture is later split into components.

## Validation

```powershell
Set-Location -Path 'C:\Users\brian.denicolafamily\Code\whiskeys-and-smokes\src\web'
npm.cmd exec -- vue-tsc -b
```
