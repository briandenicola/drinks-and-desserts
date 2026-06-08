# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** ASP.NET Core / .NET 10, CosmosDB, LiteDB, JWT auth, OpenTelemetry, Vue 3.5 frontend, Azure Container Apps hosting
- **Created:** 2026-07-22

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- Notification deletes should stay partition-safe by querying only the authenticated user's partition and deleting with that same authenticated user partition key; for bulk clears, drain repeated first pages after deletion instead of continuing through a mutating result set.
