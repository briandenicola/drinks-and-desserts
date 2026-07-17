# Contract: Add Friend Venue to My Venues

`POST /api/friends/{friendId}/venues/{venueId}/add`

Controller: `FriendsController` (alongside existing `GetFriendVenue`)

## Authorization
- Same as add-friend-item: caller and `friendId` MUST have an `accepted`
  `Friendship`; otherwise `403 Forbid`.

## Request
- Path params: `friendId` (string), `venueId` (string)
- No body.

## Behavior
1. Verify friendship (403 if not friends).
2. Load the source venue: `GetAsync<Venue>("venues", venueId, friendId)`.
   `404` if not found.
3. Create a new `Venue` owned by the caller:
   - `UserId = callerId`
   - Copy `Name`, `Address`, `Website`, `Type`, `Rating`, `PhotoUrls`,
     `Location`, `Labels`.
   - Do NOT copy `PlaceId`-linked workflow state (`WorkflowSteps`,
     `ProcessingError`) — the copy is a completed venue, not one mid-extraction.
   - `Status = VenueStatus.Completed`.
   - `SourceAttribution = new SourceAttribution { SourceUserId = friendId,
     SourceDisplayName = <friend's display name>, SourceItemId = venueId,
     AddedAt = DateTime.UtcNow }`.
4. `_cosmosDb.CreateAsync("venues", newVenue, newVenue.PartitionKey)`.
5. Return `201 Created` with the new venue.

## Responses
- `201 Created` — new `Venue` in caller's venue list.
- `403 Forbidden` — caller and `friendId` are not accepted friends.
- `404 Not Found` — source venue does not exist in friend's venue list.
