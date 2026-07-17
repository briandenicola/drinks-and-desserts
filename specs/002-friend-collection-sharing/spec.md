# Feature Specification: Share / Add to Collection from Friends' Venues

**Feature Branch**: `002-friend-collection-sharing`
**Created**: 2026-07-17
**Status**: Draft
**Input**: GitHub Issue [#117](https://github.com/briandenicola/drinks-and-desserts/issues/117) — "Share/Add to Collection from Friends' Venues"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add an item from a friend's collection (Priority: P1) 🎯 MVP

While browsing a friend's collection, a user finds a drink or dessert they want to
save and adds it to their own collection in one action, without re-entering any
details by hand.

**Why this priority**: This is the core "discovery → save" loop the issue is
asking for, and it is the highest-value, most self-contained slice. Friend
collection browsing already exists (`/friends/:friendId`, `FriendCollectionView.vue`,
`FriendItemDetailView.vue`); today the only save action is a "Save to My
Wishlist" button that copies just name/type/brand and stuffs the friend's raw
ID into a free-text note — no structured attribution, no photos, no rating.

**Independent Test**: As User A (friends with User B), open one of User B's
items via `/friends/:friendId/items/:id`, click "Add to My Collection", and
confirm the item appears in User A's own `/items` list with User B's name shown
as the source. Works standalone without Share (US3) existing.

**Acceptance Scenarios**:

1. **Given** User A is viewing an item in User B's collection, **When** User A
   clicks "Add to My Collection", **Then** a copy of the item (name, type,
   brand, category, photos, tags, venue reference) is created in User A's own
   collection with status `reviewed` and a visible "Added from {User B}'s
   collection" attribution.
2. **Given** User A is not friends with User B (friendship removed/declined),
   **When** User A calls the add endpoint directly, **Then** the server
   returns 403 and no item is created.
3. **Given** the add request fails (network/server error), **When** the
   failure occurs, **Then** the UI shows a clear error message and the button
   returns to its actionable state (no silent failure, no duplicate spinner).
4. **Given** User A has already added the same friend item once, **When**
   User A adds it again, **Then** a second, independent copy is created (adds
   are copies into the user's own data, not a link — re-adding is allowed and
   does not corrupt either user's collection).

---

### User Story 2 - Add a venue from a friend's venue list (Priority: P1)

While browsing a friend's saved venues (bars, restaurants, cafés), a user adds
one directly into their own venue list, preserving attribution.

**Why this priority**: Named explicitly in the issue title ("Friends' Venues")
and in Proposed Functionality. It is the same pattern as US1 applied to a
second entity type, and is independently valuable/testable even if US1 were
the only other thing shipped.

**Independent Test**: As User A, open one of User B's venues via
`/friends/:friendId/venues/:id`, click "Add to My Venues", and confirm the
venue appears in User A's own `/venues` list with attribution to User B.

**Acceptance Scenarios**:

1. **Given** User A is viewing a venue in User B's venue list, **When** User A
   clicks "Add to My Venues", **Then** a copy of the venue (name, address,
   website, type, photos, labels, location) is created in User A's own venue
   list with a visible "Added from {User B}'s collection" attribution.
2. **Given** User A is not friends with User B, **When** User A calls the add
   endpoint directly, **Then** the server returns 403 and no venue is created.
3. **Given** the add request fails, **When** the failure occurs, **Then** the
   UI shows a clear error message.

---

### User Story 3 - Share an item or venue with a specific friend (Priority: P2)

From their own collection, a user proactively shares a specific item or venue
with one friend, so the friend is notified and can pull it into their own
collection with one tap.

**Why this priority**: Explicitly requested in Proposed Functionality
("Share from collection") as the push-side counterpart to US1/US2's pull
side. It depends on the add flow already existing (US1/US2) so the recipient
has something to do when they open the share, which is why it is sequenced
after them.

**Independent Test**: As User A, open one of your own items, click "Share",
pick User B from the friends list, and confirm User B receives a notification
that deep-links to the item with an "Add to My Collection" action.

**Acceptance Scenarios**:

1. **Given** User A owns an item, **When** User A clicks "Share" and selects
   friend User B, **Then** User B receives a notification ("{User A} shared
   {item name} with you") referencing the item.
2. **Given** User A owns a venue, **When** User A shares it with User B,
   **Then** User B receives an equivalent venue-shared notification.
3. **Given** User A attempts to share with a user who is not an accepted
   friend, **When** the share is submitted, **Then** the server rejects it
   with 403 and no notification is created.
4. **Given** User B opens a share notification, **When** the linked item/venue
   loads, **Then** User B sees the same "Add to My Collection" action used in
   US1/US2 and can add it in one step.

---

### Edge Cases

- Friendship is removed or declined after a share notification was sent but
  before the recipient opens it: the add action must re-check friendship at
  request time and reject with 403 rather than trusting the stale
  notification.
- Source item/venue is deleted by the friend after being added: the copy
  already in the recipient's collection is unaffected (it is a copy, not a
  live reference); attribution still names the friend by display name captured
  at copy time.
- Sharing/adding photos: photo URLs are copied by reference (existing blob
  URLs), not re-uploaded; if the source photo is later deleted by its owner,
  the copy's image link may 404 — acceptable for v1, no cascading delete
  across users' data.
- Self-share/self-add is not applicable — friend endpoints are already scoped
  to `friendId != callerId` implicitly by the friendship-lookup query.
- Bulk add/share (multiple items at once) is explicitly out of scope for this
  spec (see Assumptions) — each action operates on exactly one item or venue.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a user view a friend's collection and venue list
  (already implemented; no change required).
- **FR-002**: System MUST let a user add a single item from a confirmed
  friend's collection into their own collection via one explicit UI action.
- **FR-003**: System MUST let a user add a single venue from a confirmed
  friend's venue list into their own venue list via one explicit UI action.
- **FR-004**: Items and venues added from a friend MUST retain structured
  source attribution: source user ID, source display name (captured at copy
  time), and the source item/venue ID.
- **FR-005**: Source attribution MUST be visibly rendered on the item/venue
  detail view (e.g., "Added from {name}'s collection").
- **FR-006**: System MUST let a user explicitly share one item or venue from
  their own collection with one specific friend.
- **FR-007**: Sharing MUST create a notification for the recipient using the
  existing notification pipeline (in-app + Pushover, per user preference),
  referencing the shared item/venue and the sharer.
- **FR-008**: The add and share actions MUST be rejected with 403 when the two
  users are not in an `accepted` friendship at request time, even if a prior
  notification or page already implied access.
- **FR-009**: All add/share endpoints MUST require authentication and use the
  existing structural friendship check (`AreFriendsAsync` pattern), not a
  client-supplied trust flag.
- **FR-010**: The UI MUST show a clear, non-blocking error message if an
  add/share action fails, and MUST NOT leave the triggering control stuck in a
  loading state.
- **FR-011**: Re-adding the same friend item/venue more than once MUST be
  permitted (each add creates an independent copy) rather than silently
  failing or corrupting existing data.

### Key Entities *(include if feature involves data)*

- **Item** (extended): existing collection entry (`src/api/Models/Item.cs`).
  Adds an optional `SourceAttribution` (source user ID, source display name,
  source item ID, added-at timestamp) populated only when the item originated
  from a friend's collection via the add action.
- **Venue** (extended): existing venue entry (`src/api/Models/Venue.cs`). Adds
  the same optional `SourceAttribution` shape as Item.
- **Notification** (extended): existing notification entity
  (`src/api/Models/Notification.cs`). Adds two new `NotificationType` values,
  `item-shared` and `venue-shared`, reusing the existing `referenceType`/
  `referenceId`/`sourceUserId` fields — no schema change needed beyond the
  type constants.
- **Friendship** (unchanged): existing entity/endpoint pattern
  (`src/api/Controllers/FriendsController.cs`) continues to gate all
  cross-user reads and now also gates the new add/share writes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from viewing a friend's item/venue to seeing it in
  their own collection in a single click/tap, with no manual re-entry of
  fields.
- **SC-002**: 100% of items/venues created via the add action carry correct,
  visible source attribution to the friend they came from.
- **SC-003**: 100% of add/share requests between non-friends are rejected
  server-side (verified by integration tests), regardless of what the client
  UI shows.
- **SC-004**: A share action results in a notification visible to the
  recipient through the existing notification bell within the same latency as
  other existing notification types (friend-accepted, new-thought) — no new
  delivery path introduced.
- **SC-005**: Existing friend-browsing flows (list items/venues, view detail,
  post thoughts) have zero regressions after this change.

## Assumptions

- "Friend's venue" in the issue title refers to entries in a friend's saved
  collection (their items and venues within this app), not a request to model
  a new "physical venue owned by a friend" concept — the app's `Venue` entity
  already represents a physical location and is reused as-is.
- Items added via the add action land directly in the recipient's main
  collection with `status = reviewed` (not `wishlist`), because the source
  item is already a reviewed entry belonging to a friend, not a to-try
  placeholder. This replaces today's ad hoc "Save to My Wishlist" button on
  `FriendItemDetailView.vue`.
- No permission/shareable flag is required on individual items/venues for v1:
  any item/venue visible to a friend (i.e., any item belonging to an accepted
  friend) can be added or shared, consistent with the existing friend-browsing
  model where all of a friend's items/venues are already visible once
  friendship is accepted.
- No notification is sent to the source user when a friend adds (pulls) from
  their collection — only explicit shares (push) notify. This avoids noise
  from ordinary browsing while still satisfying "friend can share and notify."
  (Resolves the issue's open question on notifications.)
- Bulk add/share (multiple items at once) is deferred; each action targets
  exactly one item or venue. (Resolves the issue's open question on bulk
  support.) A follow-up backlog item can be filed if needed.
- Photos are copied by URL reference, not re-uploaded to the recipient's blob
  storage path, for v1 simplicity.
