# Project Context

- **Owner:** briandenicola
- **Project:** whiskeys-and-smokes — a web application for tracking whiskeys and smokes with dashboards, maps, and charts
- **Stack:** ASP.NET Core / .NET 10, CosmosDB, LiteDB, JWT auth, OpenTelemetry, Vue 3.5 frontend, Azure Container Apps hosting
- **Created:** 2026-07-22

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- Item/venue collection endpoints default to 25-item pages via the shared repository; lookup UIs should use server-side `search` plus bounded `pageSize` rather than filtering the first page client-side.
- Notification deletes should stay partition-safe by querying only the authenticated user's partition and deleting with that same authenticated user partition key; for bulk clears, drain repeated first pages after deletion instead of continuing through a mutating result set.
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

### Null Item Type Crash Fix (2026-06-17)

Fixed ArgumentNullException in recommendation service when legacy items have null/blank `Type` values.

**Root cause:**
- In `RecommendationService.BuildUserProfileAsync`, the code grouped items by Type and used `ToDictionary(g => g.Key, ...)`. Legacy items with null Type caused Dictionary key constraints to throw ArgumentNullException, resulting in 500 errors and "Failed to generate recommendations" message.

**Fix approach:**
- Added `NormalizeItemType(string? type)` helper that converts null/whitespace Type to `ItemType.Custom`
- Applied normalization in two places:
  1. Before GroupBy: `.GroupBy(i => NormalizeItemType(i.Type))`
  2. In RatedItemSummary: `Type = NormalizeItemType(i.Type)`
- This ensures consistency: null Types are grouped under Custom and surfaced as Custom in recommendations

**Key files:**
- `src/api/Services/RecommendationService.cs` - Added NormalizeItemType helper, applied in BuildUserProfileAsync
- `tests/WhiskeyAndSmokes.Tests/Services/RecommendationServiceTests.cs` - Added 4 targeted regression tests covering null/blank Type scenarios

**Test coverage:**
- `BuildUserProfile_WithNullItemType_NormalizesToCustom` - Mixed null/blank/normal types
- `BuildUserProfile_WithOnlyNullTypes_DoesNotThrow` - All null types edge case
- `BuildUserProfile_WithNoRatedItems_ReturnsEmptyProfile` - Baseline unrated scenario
- `BuildUserProfile_WithMixedTypes_GroupsCorrectly` - Normal grouping + null type

**Test command:**
```bash
dotnet test src/WhiskeyAndSmokes.sln --filter "FullyQualifiedName~RecommendationServiceTests"
```

### OIDC Provider Login Support (2026-06-24)

Ported Aurearia-style OIDC backend behavior for Microsoft Entra ID, Pocket ID, and generic providers.

**Key files:**
- `src/api/Models/OidcModels.cs` - Provider, auth state, external identity, and OIDC DTO contracts
- `src/api/Services/OidcService.cs` - Authorization-code + PKCE login/linking, discovery, ID token validation, provider admin operations
- `src/api/Controllers/OidcController.cs` - Public login, protected linking, linked identity, and admin provider endpoints
- `src/api/Program.cs` - OIDC service DI and Cosmos containers (`oidc-providers`, `oidc-auth-states`, `external-identities`)
- `tests/WhiskeyAndSmokes.Tests/Services/OidcServiceTests.cs` - Regression coverage for provider defaults, PKCE state, redirect validation, public provider filtering, and unlink guard
- `tests/WhiskeyAndSmokes.Tests/Controllers/OidcControllerTests.cs` - Security regression tests for host-header/forwarded-host origin validation

**Design decisions:**
- Preserved existing `/api/auth/entra` token-exchange behavior for compatibility; new OIDC providers use explicit account linking and do not silently merge or auto-provision users.
- OIDC login requires a previously linked external identity. Verified-email matches with existing local users return conflict and require explicit linking from account settings.
- Auth state stores only hashed state/nonce plus plaintext PKCE verifier, expires after 10 minutes, and is marked consumed before token exchange to prevent replay.
- Unlinking is blocked when a user has no local password and no other linked OIDC identity.
- **Security:** Configured `Oidc:PublicOrigin` is required for production; OIDC redirect URIs ignore X-Forwarded-Host/Proto headers and validate HTTPS.

**Validation:**
- `dotnet test src\WhiskeyAndSmokes.sln --no-restore` passed: 135/135 tests.

**Team coordination:**
- Fenster (Frontend) implemented OIDC login UI, callback routes, profile linking/unlinking, admin provider management
- McManus (DevOps) wired OIDC deployment config, Cosmos containers, Docker env vars
- Ralph (Security Review) identified host-header injection vulnerability; Coordinator applied fixes and added regression tests

### Admin Role Management and Auth Settings (2026-06-24)

- Admin role changes are admin-only and work across local, Entra, and OIDC-linked accounts because role is stored on the shared user document.
- Last-admin protections now block demoting or deleting the only remaining admin account.
- OIDC public origin is stored in the shared `settings` container as `auth-settings`; this admin-managed value takes precedence over `Oidc:PublicOrigin`, which remains a bootstrap/local fallback.
- Public origin validation is centralized and requires an absolute origin, HTTPS except localhost HTTP, and no path/query/fragment.
- Validation: `dotnet test src\WhiskeyAndSmokes.sln --no-restore` passed: 142/142 tests.
