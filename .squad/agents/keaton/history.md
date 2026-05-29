# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** ASP.NET Core / .NET 10, CosmosDB, LiteDB, JWT auth, OpenTelemetry, Vue 3.5 frontend, Azure Container Apps hosting
- **Created:** 2026-07-22

## Learnings

### Issue #72: Server-Side Ordering and Grouping (2026-05-29)

Implemented server-side ordering for paginated list endpoints to fix issue where client-side sorting on partial data misses newest items.

**Key files:**
- `src/api/Services/CosmosDbService.cs` - Added `orderBy` and `orderDescending` parameters to `QueryAsync<T>`
- `src/api/Services/LiteDbService.cs` - Implemented matching signature for dev environment compatibility
- `src/api/Controllers/ItemsController.cs` - Added `sortBy`, `sortDirection` query params; added `/grouped` endpoint
- `src/api/Controllers/VenuesController.cs` - Added `sortBy`, `sortDirection` query params; added `/grouped` endpoint
- `src/api/Models/ApiModels.cs` - Added `GroupedResponse<T>` DTO

**Design decisions:**
- Server-side ORDER BY: Uses Cosmos LINQ OrderBy/OrderByDescending expressions that translate to SQL ORDER BY clauses. Continuation tokens remain valid because ordering is deterministic.
- Allowed sort fields (Items): name, createdAt, updatedAt, userRating, brand, type
- Allowed sort fields (Venues): name, createdAt, updatedAt, rating, type
- Grouping trade-off: Full server-side GROUP BY aggregation conflicts with continuation-token pagination (aggregation requires full dataset). Implemented separate `/grouped` endpoints that fetch all user records and group client-side. Documented this explicitly in XML comments.
- Backward compatibility: All new query params are optional; existing clients continue working without changes.

**Patterns:**
- Expression-based ordering in repository layer keeps business logic out of data access
- Invalid sortBy fields return 400 BadRequest with allowed values
- OpenTelemetry spans log ordering metadata for observability
