# Squad Decisions

## Active Decisions

### Issue #72: Server-Side Ordering and Grouping (2026-05-29, implemented 2026-06-17)

**Backend (Keaton):** Implemented server-side ORDER BY support for paginated list endpoints (Items, Venues) to fix client-side sorting bug where descending sorts on partial data missed newest items.

- Added `sortBy`, `sortDirection`, `orderBy` parameters to repository layer
- Updated both `CosmosDbService` and `LiteDbService` for consistency
- New `/grouped` endpoints for analytics (separate from pagination)
- Query params: `sortBy`, `sortDirection` (asc/desc)
- Allowed sortBy fields: Items (name, createdAt, updatedAt, userRating, brand, type); Venues (name, createdAt, updatedAt, rating, type)
- **Design choice:** Grouping implemented via separate endpoints (client-side grouping) because server-side GROUP BY conflicts with continuation-token pagination
- Backward compatible: all params optional, existing clients unaffected

**Frontend (Fenster):** Removed client-side sorting logic from VenuesView/ItemsView; list views now treat server response as source of truth.

- Added UI controls: sort field dropdown, sort direction toggle, group-by dropdown
- Stores track `sortBy`, `sortDirection`, `groupBy` state
- Pagination automatically resets when sort/group criteria change
- Files changed: services/venues.ts, services/items.ts, stores/venues.ts, stores/items.ts, views/VenuesView.vue, views/ItemsView.vue

### Null Item Type Normalization in Recommendations (2026-06-17)

**Author:** Keaton (Backend)  
**Status:** Implemented  

**Problem:** Legacy items with null/blank `Type` values caused `ArgumentNullException` in `RecommendationService.BuildUserProfileAsync` when grouping by Type for Dictionary keys.

**Decision:** Normalize null/whitespace Type to `ItemType.Custom` before grouping or surfacing in API responses.

**Implementation:**
- Added `NormalizeItemType(string? type)` helper: returns `ItemType.Custom` for null/whitespace, else returns type as-is
- Applied in two places: GroupBy for type preferences, RatedItemSummary DTO output
- Added 4 regression tests covering null/blank/mixed type scenarios
- All tests pass: 130/130 suite

**Rationale:**
- `ItemType.Custom` already the standard fallback (WorkflowAgentService, LocalExtraction)
- Fail-safe: no DB migration required, backward compatible
- Explicit normalization makes fix findable and maintainable

**Consequences:**
- Legacy items grouped under "custom" in type preferences
- Users with legacy data now successfully receive recommendations
- No schema changes required

### Recommendations Persistence via Pinia Store (2026-06-17)

**Author:** Fenster (Frontend)  
**Status:** Implemented  

**Problem:** Users lost all generated recommendations when navigating away from recommendations page (state was component-local).

**Decision:** Move all recommendation state to Pinia store (Composition API pattern) so it persists across navigation.

**Implementation:**
- Created `src/web/src/stores/recommendations.ts`
- Moved state: preferences, loadingFlags, recommendations, reasoning, extracted items, user profile, selected types, saved/saving Sets
- Moved actions: loadUserProfile, uploadMenuPhoto, getRecommendations, toggleType, reset, saveToWishlist
- View updated: RecommendationsView.vue now uses storeToRefs + calls store actions
- Sets immutably reassigned (new Set(...)) for Vue reactivity
- On-mount guard: only loads profile if not already present (avoids clobbering)
- Reset behavior: only on explicit "Start Over" button, not on unmount
- Photo handling: stays in view via useCamera; File passed to store.getRecommendations(photo)

**Design rationale:**
- Matches existing patterns (items, auth stores)
- Testable, centralized state management
- User explicitly chose Pinia over localStorage after previous WIP (PR #98) was reverted

**Artifacts:**
- Created: src/web/src/stores/recommendations.ts
- Modified: src/web/src/views/RecommendationsView.vue
- Validation: vue-tsc -b passes with zero errors

**Coordination:** Keaton fixed null Type crash in parallel; combined these resolve full user flow.

### Locked Bottom Navigation Toolbar (2026-06-17)

**Author:** Fenster (Frontend)  
**Status:** Implemented  

**Problem:** Toolbar hide-on-keyboard-focus behavior (PR #90) was disruptive—toolbar sliding away when focusing textarea on recommendations page.

**Decision:** Remove hide-on-focus behavior; keep toolbar locked to screen bottom at all times. Use content padding to prevent input overlap.

**Implementation:**
- Removed `useKeyboardFocus` import and usage from AppLayout.vue
- Removed nav-keyboard-hide, nav-hidden classes and associated CSS
- Main content areas already have `pb-20`/`pb-24` classes (reserved space for fixed toolbar)
- Inputs now scroll above locked toolbar rather than getting hidden

**Design choice:** Preserves original PR #90 intent (no overlap) while respecting user preference for persistent toolbar.

**Artifacts:**
- Modified: src/web/src/components/common/AppLayout.vue
- Note: useKeyboardFocus.ts can be removed in cleanup pass if unused elsewhere

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
