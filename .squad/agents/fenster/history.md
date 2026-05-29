# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** Vue 3.5, TypeScript 5.x, Tailwind CSS 4, Vite 8, Pinia, vue-router, VitePWA, @tanstack/vue-virtual, Apache ECharts (vue-echarts), Leaflet (vue-leaflet), ASP.NET Core / .NET 10 backend, Azure CosmosDB
- **Created:** 2026-07-22

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- Docker build uses `vue-tsc -b` (project mode) which enforces stricter checks than `vue-tsc --noEmit`, including `noUnusedLocals` and `noUnusedParameters`. Always verify with `vue-tsc -b` before pushing.
- Server-side sorting and grouping: List API calls accept `sortBy`, `sortDirection`, and `groupBy` parameters. Stores track current sort/group state and automatically reset pagination when criteria change. Client-side sorting removed from VenuesView and ItemsView (server is source of truth). Views expose sort direction toggle and group-by dropdowns in UI. (Issue #72)
