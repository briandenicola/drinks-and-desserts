---

description: "Task list for Share / Add to Collection from Friends' Venues (issue #117)"

---

# Tasks: Share / Add to Collection from Friends' Venues

**Input**: Design documents from `/specs/002-friend-collection-sharing/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/

**Tests**: Included — the constitution requires accumulating integration
coverage for security-critical flows (ownership/friendship checks), and this
feature is entirely a set of new authorization-gated write endpoints.

**Organization**: Tasks are grouped by user story so each can ship and be
demoed independently, per spec.md priorities (US1/US2 = P1 MVP, US3 = P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps task to spec.md user story (US1, US2, US3)

## Path Conventions

- **Backend**: `src/api/` (.NET 10 / C#)
- **Frontend**: `src/web/src/` (Vue 3 / TypeScript / Tailwind CSS 4)
- **Tests**: `tests/WhiskeyAndSmokes.Tests/`

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Shared model/type changes every user story depends on.

**⚠️ CRITICAL**: No endpoint or UI work can begin until this phase is complete.

- [ ] T001 Add `SourceAttribution` class (`SourceUserId`, `SourceDisplayName`,
      `SourceItemId`, `AddedAt`) to `src/api/Models/Item.cs` per data-model.md
- [ ] T002 [P] Add nullable `SourceAttribution? SourceAttribution` property to
      `Item` in `src/api/Models/Item.cs`
- [ ] T003 [P] Add nullable `SourceAttribution? SourceAttribution` property to
      `Venue` in `src/api/Models/Venue.cs` (reusing the type from T001)
- [ ] T004 [P] Add `ItemShared` and `VenueShared` constants to
      `NotificationType` in `src/api/Models/Notification.cs`
- [ ] T005 [P] Add `ShareRequest` DTO (`FriendId`, `[Required]`,
      `[StringLength(100)]`) to `src/api/Models/ApiModels.cs`
- [ ] T006 Update `NotificationService.BuildNotificationUrl` in
      `src/api/Services/NotificationService.cs` to branch on
      `notification.Type` first: for `ItemShared`/`VenueShared`, build
      `/friends/{SourceUserId}/items/{ReferenceId}` or
      `/friends/{SourceUserId}/venues/{ReferenceId}`; fall back to the
      existing `ReferenceType` switch for all other types (see
      contracts/share-item.md Notes — without this, share notifications link
      to the wrong page)
- [ ] T007 [P] Add `SourceAttribution` interface and optional
      `sourceAttribution?: SourceAttribution` field to the `Item` interface in
      `src/web/src/services/items.ts`
- [ ] T008 [P] Add optional `sourceAttribution?: SourceAttribution` field to
      the `Venue` interface in `src/web/src/services/venues.ts` (import the
      shared `SourceAttribution` type from `items.ts` or duplicate the small
      interface — match existing project convention of per-service type
      files)

**Checkpoint**: Models compile; `dotnet build -c Release` and
`vue-tsc --noEmit` both pass with no behavior change yet.

---

## Phase 2: User Story 1 — Add an item from a friend's collection (Priority: P1) 🎯 MVP

**Goal**: One-click copy of a friend's item into the caller's own collection
with attribution.

**Independent Test**: See spec.md US1.

### Tests for User Story 1

- [ ] T009 [P] [US1] Add test `AddFriendItem_WhenFriends_CreatesOwnedCopyWithAttribution`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`
- [ ] T010 [P] [US1] Add test `AddFriendItem_WhenNotFriends_Returns403` to
      `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`
- [ ] T011 [P] [US1] Add test `AddFriendItem_WhenSourceItemMissing_Returns404`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`

### Implementation for User Story 1

- [ ] T012 [US1] Implement `POST {friendId}/items/{itemId}/add` in
      `src/api/Controllers/FriendsController.cs` per
      contracts/add-friend-item.md (reuse `AreFriendsAsync`; copy fields as
      specified; set `Status = ItemStatus.Reviewed`; populate
      `SourceAttribution` from the caller's own `Friendship.FriendDisplayName`)
- [ ] T013 [US1] Add `addFriendItem(friendId, itemId)` to
      `friendsApi` in `src/web/src/services/friends.ts`
      (`POST /friends/{friendId}/items/{itemId}/add`)
- [ ] T014 [US1] Replace the ad hoc `saveToWishlist()` handler in
      `src/web/src/views/FriendItemDetailView.vue` with a call to
      `friendsApi.addFriendItem`, rename the button to "Add to My Collection",
      and show a success/error state instead of `alert()` (constitution FR-010
      / UX consistency — no blocking browser alerts)
- [ ] T015 [US1] Add a source-attribution badge ("Added from {name}'s
      collection") to `src/web/src/views/ItemDetailView.vue`, rendered when
      `item.sourceAttribution` is present

**Checkpoint**: US1 fully functional and independently testable/demoable.

---

## Phase 3: User Story 2 — Add a venue from a friend's venue list (Priority: P1)

**Goal**: One-click copy of a friend's venue into the caller's own venue list
with attribution.

**Independent Test**: See spec.md US2.

### Tests for User Story 2

- [ ] T016 [P] [US2] Add test `AddFriendVenue_WhenFriends_CreatesOwnedCopyWithAttribution`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`
- [ ] T017 [P] [US2] Add test `AddFriendVenue_WhenNotFriends_Returns403` to
      `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`
- [ ] T018 [P] [US2] Add test `AddFriendVenue_WhenSourceVenueMissing_Returns404`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/FriendsControllerTests.cs`

### Implementation for User Story 2

- [ ] T019 [US2] Implement `POST {friendId}/venues/{venueId}/add` in
      `src/api/Controllers/FriendsController.cs` per
      contracts/add-friend-venue.md
- [ ] T020 [US2] Add `addFriendVenue(friendId, venueId)` to `friendsApi` in
      `src/web/src/services/friends.ts`
      (`POST /friends/{friendId}/venues/{venueId}/add`)
- [ ] T021 [US2] Add an "Add to My Venues" button to the venue-detail branch
      of `src/web/src/views/FriendVenuesView.vue` (currently has no add
      action at all), calling `friendsApi.addFriendVenue`, with loading/error
      state matching T014's pattern
- [ ] T022 [US2] Add the same source-attribution badge used in T015 to
      `src/web/src/views/VenueDetailView.vue`, rendered when
      `venue.sourceAttribution` is present

**Checkpoint**: US1 + US2 both fully functional. MVP (per spec.md) complete —
suitable to demo/deploy.

---

## Phase 4: User Story 3 — Share an item or venue with a specific friend (Priority: P2)

**Goal**: Push a specific item/venue to one friend via notification; friend
can act on it using the US1/US2 add action.

**Independent Test**: See spec.md US3. Depends on Phase 2/3 being complete so
the recipient has an add action to use from the notification.

### Tests for User Story 3

- [ ] T023 [P] [US3] Add test `ShareItem_WhenFriends_CreatesNotificationForRecipient`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/ItemsControllerTests.cs`
- [ ] T024 [P] [US3] Add test `ShareItem_WhenNotFriends_Returns403` to
      `tests/WhiskeyAndSmokes.Tests/Controllers/ItemsControllerTests.cs`
- [ ] T025 [P] [US3] Add test `ShareItem_WhenNotOwner_Returns404` to
      `tests/WhiskeyAndSmokes.Tests/Controllers/ItemsControllerTests.cs`
- [ ] T026 [P] [US3] Add test `ShareVenue_WhenFriends_CreatesNotificationForRecipient`
      to `tests/WhiskeyAndSmokes.Tests/Controllers/VenuesControllerTests.cs`
- [ ] T027 [P] [US3] Add test `ShareVenue_WhenNotFriends_Returns403` to
      `tests/WhiskeyAndSmokes.Tests/Controllers/VenuesControllerTests.cs`

### Implementation for User Story 3

- [ ] T028 [US3] Implement `POST {id}/share` in
      `src/api/Controllers/ItemsController.cs` per contracts/share-item.md
      (inject `ICosmosDbService` friendship read + `INotificationService`,
      already both available via DI patterns used elsewhere)
- [ ] T029 [US3] Implement `POST {id}/share` in
      `src/api/Controllers/VenuesController.cs` per contracts/share-venue.md
- [ ] T030 [P] [US3] Add `shareItem(id, friendId)` to `itemsApi` in
      `src/web/src/services/items.ts` (`POST /items/{id}/share`)
- [ ] T031 [P] [US3] Add `shareVenue(id, friendId)` to `venuesApi` in
      `src/web/src/services/venues.ts` (`POST /venues/{id}/share`)
- [ ] T032 [US3] Create `ShareModal.vue` in
      `src/web/src/components/common/ShareModal.vue`: fetches the caller's
      accepted friends via `friendsApi.list()`, renders a friend picker list,
      emits a `share` event with the chosen `friendId`, matches existing
      modal/overlay styling conventions in the codebase (44px tap targets, no
      emoji)
- [ ] T033 [US3] Add a "Share" button + `ShareModal` integration to
      `src/web/src/views/ItemDetailView.vue`, calling `itemsApi.shareItem` on
      friend selection, with success/error feedback (no `alert()`)
- [ ] T034 [US3] Add a "Share" button + `ShareModal` integration to
      `src/web/src/views/VenueDetailView.vue`, calling `venuesApi.shareVenue`
      on friend selection

**Checkpoint**: US3 fully functional. All three user stories independently
testable and demoable; full feature scope from spec.md complete.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T035 [P] Verify `NotificationBell.vue` (or equivalent notification list
      UI) renders `item-shared`/`venue-shared` notifications with sensible
      copy (reuses existing generic notification rendering — confirm no
      special-casing is missing)
- [ ] T036 [P] Run backend build+tests: `dotnet build -c Release` and
      `dotnet test` in `src/api`/`tests` and fix any errors
- [ ] T037 [P] Run frontend type-check: `npx vue-tsc --noEmit` in `src/web`
      and fix any errors
- [ ] T038 Manually verify the full loop end-to-end with two test accounts:
      share an item → recipient gets notification → recipient clicks through
      → recipient adds to their own collection → attribution badge shows
      correctly on the copy
- [ ] T039 [P] Update `docs/` (or README, if friend features are documented
      there) to mention the add/share actions, per constitution §10
      (user-facing behavior changes require doc updates in the same change)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately. Blocks
  every other phase (shared model fields).
- **US1 (Phase 2)**: Depends on Phase 1 only.
- **US2 (Phase 3)**: Depends on Phase 1 only — independent of US1, can run in
  parallel with Phase 2 (different files: `FriendsController.cs` add
  endpoints are additive to the same file but on different methods/routes; a
  single developer can still do both sequentially without conflict).
- **US3 (Phase 4)**: Depends on Phase 1 (T006 notification URL fix in
  particular) and benefits from Phase 2/3 being done first so the recipient's
  add action exists when demoing the full loop — not a hard code dependency,
  but required for the *independent test* in spec.md to be meaningful.
- **Polish (Phase 5)**: Depends on all desired user stories being complete.

### Within Each User Story

- Tests before implementation (write T009-T011 before T012, etc.)
- Backend endpoint before frontend service call before frontend UI wiring

### Parallel Opportunities

- **Phase 1**: T002-T005 in parallel after T001; T007-T008 in parallel with
  each other and with backend tasks
- **Phase 2 tests**: T009-T011 in parallel
- **Phase 3 tests**: T016-T018 in parallel
- **Phase 4 tests**: T023-T027 in parallel
- **Phase 4 services**: T030-T031 in parallel
- **US1 (Phase 2) and US2 (Phase 3)** can be built in parallel by two
  developers once Phase 1 is done

---

## Implementation Strategy

### MVP First (US1 + US2 only)

1. Complete Phase 1: Foundational model/type changes
2. Complete Phase 2: US1 — Add item from friend's collection
3. Complete Phase 3: US2 — Add venue from friend's venue list
4. **STOP and VALIDATE**: both add flows work end-to-end with attribution
5. Deploy/demo — satisfies the issue's core acceptance criteria

### Incremental Delivery

1. Foundational → US1 → US2 → MVP demo (pull/add covers the issue's stated
   acceptance criteria in full)
2. Add US3 (Share) → completes the "Share from collection" half of Proposed
   Functionality → Deploy/Demo
3. Polish pass → done

---

## Notes

- [P] tasks = different files, no dependencies
- Each user story is independently completable and testable per spec.md
- Commit after each task or logical group
- No bulk operations — every add/share task operates on exactly one item or
  venue, per spec.md Assumptions
- No emoji anywhere in UI; 44x44px minimum tap targets on new buttons
  (constitution §4)
