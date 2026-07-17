# Contract: Add Friend Item to My Collection

`POST /api/friends/{friendId}/items/{itemId}/add`

Controller: `FriendsController` (alongside existing `GetFriendItem`)

## Authorization
- `[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]` (existing on controller)
- Caller and `friendId` MUST have an `accepted` `Friendship` (reuse
  `AreFriendsAsync(userId, friendId)`); otherwise `403 Forbid`.

## Request
- Path params: `friendId` (string), `itemId` (string)
- No body.

## Behavior
1. Verify friendship (403 if not friends).
2. Load the source item: `GetAsync<Item>("items", itemId, friendId)`. `404` if
   not found.
3. Create a new `Item` owned by the caller:
   - `UserId = callerId`
   - Copy `Name`, `Type`, `Brand`, `Category`, `Details`, `Venue`, `PhotoUrls`,
     `Tags`, `UserRating` (copy as-is; it is the friend's rating, shown as
     provenance, caller can edit after adding), `UserNotes` (copy as-is).
   - Do NOT copy `Journal` (personal journal entries stay with the original
     owner) or `CaptureId` (no shared capture linkage).
   - `Status = ItemStatus.Reviewed` (lands directly in the caller's
     collection, not the wishlist — see spec Assumptions).
   - `ProcessedBy = ProcessingSource.Manual` (or an equivalent "friend-add"
     constant if the team wants finer analytics — not required for v1).
   - `SourceAttribution = new SourceAttribution { SourceUserId = friendId,
     SourceDisplayName = <friend's display name>, SourceItemId = itemId,
     AddedAt = DateTime.UtcNow }`.
4. `_cosmosDb.CreateAsync("items", newItem, newItem.PartitionKey)`.
5. Return `201 Created` with the new item (`CreatedAtAction` pattern
   consistent with `ItemsController.CreateWishlistItem`).

## Responses
- `201 Created` — new `Item` in caller's collection.
- `403 Forbidden` — caller and `friendId` are not accepted friends.
- `404 Not Found` — source item does not exist in friend's collection.

## Notes
- Friend's display name for attribution: read from the caller's own
  `Friendship` document (`FriendDisplayName` field, already stored) rather
  than an extra lookup against `users` — consistent with existing patterns
  elsewhere in `FriendsController`.
