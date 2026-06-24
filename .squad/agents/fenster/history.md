# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** Vue 3.5, TypeScript 5.x, Tailwind CSS 4, Vite 8, Pinia, vue-router, VitePWA, @tanstack/vue-virtual, Apache ECharts (vue-echarts), Leaflet (vue-leaflet), ASP.NET Core / .NET 10 backend, Azure CosmosDB
- **Created:** 2026-07-22

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- Docker build uses `vue-tsc -b` (project mode) which enforces stricter checks than `vue-tsc --noEmit`, including `noUnusedLocals` and `noUnusedParameters`. Always verify with `vue-tsc -b` before pushing.
- Notification bell lives at `src\web\src\components\common\NotificationBell.vue` and uses `src\web\src\services\notifications.ts` for API calls. Clearing notifications now uses `DELETE /api/notifications`, empties local bell state, and resets unread count to zero.
- On Windows, run frontend project validation from `src\web` with `npm.cmd exec -- vue-tsc -b`; plain `npm` can be blocked by PowerShell execution policy and `--` is needed so `-b` reaches vue-tsc.
- Server-side sorting and grouping: List API calls accept `sortBy`, `sortDirection`, and `groupBy` parameters. Stores track current sort/group state and automatically reset pagination when criteria change. Client-side sorting removed from VenuesView and ItemsView (server is source of truth). Views expose sort direction toggle and group-by dropdowns in UI. (Issue #72)
- Detail-page lookup pickers filter client-side, so they must fetch every continuation-token page before searching; the list APIs return only 25 records per page by default.
- Recommendations store (`src/web/src/stores/recommendations.ts`) persists state across navigation. State survives route changes; `reset()` is only called on explicit "Start Over" button. On mount, views check if profile is already loaded to avoid clobbering existing recommendations. Sets (savedItems, savingItems) are immutably reassigned (`new Set(...)`) for reactivity.
- Bottom navigation toolbar is locked to the screen bottom in AppLayout.vue. Removed useKeyboardFocus composable and hide-on-focus behavior; main content areas have pb-20/pb-24 classes to reserve space so inputs scroll above the fixed toolbar rather than getting hidden behind it.
- PWA state persistence: `usePwa()` composable detects PWA mode. In PWA mode, ItemsView and VenuesView skip reloading if data already loaded and restore scroll position. Stores track `hasInitialLoad` and `scrollPosition`. Store load functions accept `skipIfLoaded` param to prevent redundant API calls. Preserves list state, pagination, filters, sort, and scroll position across navigation. (2026-06-19)
- PWA list state correctness: Stores now check criteria (type, sort, sortDirection, groupBy) before skipping loads in PWA mode to prevent mismatch between loaded data and UI controls. Views restore sort/filter/search controls from Pinia store state in PWA mode, falling back to auth preferences only in browser mode or on first load. Ensures UI controls always reflect the loaded data state. (2026-06-19)
- OIDC frontend auth follows Aurearia behavior: LoginView dynamically loads `/api/auth/oidc/providers`, starts provider redirects with `/auth/oidc/{id}/start`, and has `/auth/oidc/callback/:providerId` plus `/profile/oidc/link/callback/:providerId` callback views. Auth store exposes `applyAuthResponse()` so OIDC callbacks can persist tokens without duplicating login/register logic. ProfileView manages linked identities under `/api/users/me/oidc-identities`, and AdminView includes an OIDC provider tab for Entra ID, Pocket ID, and generic providers. (2026-06-24)
- OIDC frontend implementation (2026-06-24): OIDC Login Page dynamically loads providers from `/api/auth/oidc/providers` and renders provider buttons. Callback views `/auth/oidc/callback/:providerId` and `/profile/oidc/link/callback/:providerId` handle token persistence via auth store. ProfileView shows linked identities with unlink confirmations; AdminView provides provider config UI. All OIDC provider/identity IDs use string (GUID as string) contracts matching backend OidcModels. Frontend validation: `npm.cmd exec -- vue-tsc -b` zero errors. Team coordination: Keaton (backend OIDC service), McManus (deployment config), Ralph (security review).


- Admin user management now treats roles as account-type agnostic: users show role/auth provider badges, promote/demote through /api/admin/users/{id}/role, and surface backend safety errors for last-admin/self-lockout. Admin Settings uses /api/admin/auth-settings for OIDC public web origin. (2026-06-24)
