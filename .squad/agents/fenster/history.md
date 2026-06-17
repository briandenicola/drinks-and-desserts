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
