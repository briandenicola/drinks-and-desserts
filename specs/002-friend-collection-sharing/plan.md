# Implementation Plan: Share / Add to Collection from Friends' Venues

**Branch**: `002-friend-collection-sharing` | **Date**: 2026-07-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-friend-collection-sharing/spec.md`

## Summary

Add two complementary actions on top of the existing friend-browsing feature
(`FriendsController`, `FriendCollectionView.vue`, `FriendVenuesView.vue`,
`FriendItemDetailView.vue`): a **pull** action ("Add to My Collection" /
"Add to My Venues") that copies a friend's item or venue into the caller's own
collection with structured source attribution, and a **push** action
("Share") that lets a user notify one specific friend about one of their own
items or venues, reusing the existing notification pipeline. Both actions are
gated by the existing accepted-friendship check. `Item` and `Venue` gain an
optional `SourceAttribution` field; `Notification` gains two new type
constants. No new containers, no new top-level UI sections — the changes are
additive to controllers/models already in place.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript 5.x (frontend)
**Primary Dependencies**: ASP.NET Core, Azure CosmosDB SDK (existing
`ICosmosDbService`), Vue 3.5, Pinia, Vue Router, Tailwind CSS 4
**Storage**: Azure CosmosDB — existing `items`, `venues`, `notifications`,
`friendships` containers; no new containers, additive fields only
**Testing**: `dotnet test` (xUnit, `tests/WhiskeyAndSmokes.Tests`) for new
controller endpoints; `vue-tsc --noEmit` for frontend type-check
**Target Platform**: Existing iOS PWA + desktop web (no platform-specific work)
**Project Type**: Web application (Vue 3 SPA + ASP.NET Core API), extending
existing friend-collection feature
**Performance Goals**: Add/share actions complete within existing single-item
CRUD latency (no batch/bulk operations introduced)
**Constraints**: Must reuse `AreFriendsAsync`-style structural friendship
checks; no client-trusted authorization; no schema migration (additive fields
only, backward compatible with existing documents where the field is simply
absent/null)
**Scale/Scope**: 1 extended model field (`SourceAttribution`, used by both
`Item` and `Venue`), 2 new notification type constants, 4 new API endpoints
(add-item, add-venue, share-item, share-venue), ~5 modified/new frontend
files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Check

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Code Quality & Defensive Coding | PASS | New endpoints validate friendship server-side before any read/write; controllers stay thin, copy/attribution logic can live inline (small, single-purpose) consistent with existing controller style |
| II. Testing Standards | PASS | New xUnit tests added to `FriendsControllerTests.cs`, `ItemsControllerTests.cs`, `VenuesControllerTests.cs` covering happy path + non-friend 403 path; `dotnet build -c Release` and `dotnet test` gate the change |
| III. User Experience Consistency | PASS | Reuses existing button/badge styling tokens (stone/`#96BEE6`/`#1e407c` palette already used in `FriendItemDetailView.vue`); no emoji; 44px min tap targets on new buttons; loading/error states required per FR-010 |
| IV. Performance & Reliability | PASS | Single-document copy operations only (no bulk/batch); notification creation reuses existing fire-and-forget-safe `NotificationService.CreateAsync` which already isolates push failures from persistence |
| Security Requirements | PASS | All new endpoints require `[Authorize]`; friendship is re-verified server-side per request (FR-008/FR-009), not inferred from the referring page or notification payload |
| Data & Persistence Standards | PASS | No cross-partition queries introduced beyond the existing single-friend-partition reads already used by `FriendsController`; new field is additive, no migration needed |

**Gate Result: PASS** — No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/002-friend-collection-sharing/
├── plan.md              # This file
├── data-model.md         # Phase 1: SourceAttribution shape, Notification types
├── contracts/            # Phase 1: new endpoint contracts
│   ├── add-friend-item.md
│   ├── add-friend-venue.md
│   ├── share-item.md
│   └── share-venue.md
└── tasks.md              # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── api/
│   ├── Models/
│   │   ├── Item.cs                       # Extended: SourceAttribution field
│   │   ├── Venue.cs                      # Extended: SourceAttribution field
│   │   ├── Notification.cs               # Extended: ItemShared, VenueShared types
│   │   └── ApiModels.cs                  # New: ShareRequest DTO
│   └── Controllers/
│       ├── FriendsController.cs          # New: POST {friendId}/items/{itemId}/add
│       │                                 #      POST {friendId}/venues/{venueId}/add
│       ├── ItemsController.cs            # New: POST {id}/share
│       └── VenuesController.cs           # New: POST {id}/share
├── web/
│   └── src/
│       ├── services/
│       │   ├── friends.ts                # New: addFriendItem, addFriendVenue
│       │   ├── items.ts                  # New: shareItem
│       │   └── venues.ts                 # New: shareVenue
│       ├── components/
│       │   └── common/
│       │       └── ShareModal.vue        # NEW: friend picker for Share action
│       └── views/
│           ├── FriendItemDetailView.vue  # Modified: replace ad hoc wishlist
│           │                             #   POST with "Add to My Collection"
│           ├── FriendVenuesView.vue      # Modified: add "Add to My Venues"
│           │                             #   button in venue detail mode
│           ├── ItemDetailView.vue        # Modified: add "Share" button +
│           │                             #   ShareModal, attribution badge
│           └── VenueDetailView.vue       # Modified: add "Share" button +
│                                         #   ShareModal, attribution badge

tests/
└── WhiskeyAndSmokes.Tests/
    └── Controllers/
        ├── FriendsControllerTests.cs     # Extended: add-item, add-venue tests
        ├── ItemsControllerTests.cs       # Extended: share-item tests
        └── VenuesControllerTests.cs      # Extended: share-venue tests
```

**Structure Decision**: Follows the existing web application pattern — no new
top-level directories. All backend changes are additive to controllers/models
already used by the friend-browsing feature (`FriendsController`,
`ItemsController`, `VenuesController`). The one new frontend component
(`ShareModal.vue`) is a small, reusable friend-picker used from both
`ItemDetailView.vue` and `VenueDetailView.vue`, mirroring how `StarRating.vue`
and other small shared components already live under `components/common/`.

## Complexity Tracking

> No Constitution violations identified. Table intentionally left empty.
