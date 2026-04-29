# whiskeys-and-smokes Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-24

## Active Technologies
- TypeScript 5.x (frontend), C# / .NET 10 (backend) + Vue 3.5, Pinia, Vue Router, Tailwind CSS 4, Vite 8, @tanstack/vue-virtual, Apache ECharts (vue-echarts), Leaflet (vue-leaflet) (001-desktop-responsive-dashboard)
- Azure CosmosDB (existing, no schema changes) (001-desktop-responsive-dashboard)

- C# / .NET 10, TypeScript / Vue 3.5 + ASP.NET Core, Vite 8, Tailwind CSS 4, VitePWA (001-constitution-compliance-audit)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

npm test; npm run lint

## Code Style

C# / .NET 10, TypeScript / Vue 3.5: Follow standard conventions

## Recent Changes
- 001-desktop-responsive-dashboard: Added TypeScript 5.x (frontend), C# / .NET 10 (backend) + Vue 3.5, Pinia, Vue Router, Tailwind CSS 4, Vite 8, @tanstack/vue-virtual, Apache ECharts (vue-echarts), Leaflet (vue-leaflet)

- 001-constitution-compliance-audit: Added C# / .NET 10, TypeScript / Vue 3.5 + ASP.NET Core, Vite 8, Tailwind CSS 4, VitePWA

<!-- MANUAL ADDITIONS START -->
## Build Verification

The Docker build uses `vue-tsc -b` (project mode) which enforces stricter checks than `vue-tsc --noEmit`, including `noUnusedLocals` and `noUnusedParameters`. After making frontend changes, verify with `vue-tsc -b` (or ensure no unused imports/variables exist) before pushing to avoid Docker build failures.
<!-- MANUAL ADDITIONS END -->
