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
