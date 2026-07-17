# Contract: Share a Venue with a Friend

`POST /api/venues/{id}/share`

Controller: `VenuesController`

## Authorization
- `[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]` (existing on controller)
- Caller MUST own the venue (existing ownership pattern — 404 if not
  owned/found).
- Caller and `request.FriendId` MUST have an `accepted` `Friendship`.

## Request

```json
{ "friendId": "string" }
```

Body: `ShareRequest` (see data-model.md).

## Behavior
1. Load the venue by `id` scoped to caller; `404` if not found/not owned.
2. Verify caller/`friendId` are accepted friends; `403` if not.
3. Create a `Notification`:
   - `UserId = friendId`
   - `Type = NotificationType.VenueShared`
   - `Title = "{caller display name} shared {venue.Name} with you"`
   - `SourceUserId = callerId`
   - `SourceDisplayName = <caller display name>`
   - `ReferenceType = "venue"`
   - `ReferenceId = venue.Id`
4. `await _notificationService.CreateAsync(notification)`.
5. Return `200 OK`.

## Responses
- `200 OK` — notification created.
- `403 Forbidden` — caller and `friendId` are not accepted friends.
- `404 Not Found` — venue does not exist or is not owned by caller.
- `400 Bad Request` — `friendId` missing/invalid.

## Notes
- Same `BuildNotificationUrl` caveat as `share-item.md`: the click-through
  target must be `/friends/{sourceUserId}/venues/{referenceId}`, not
  `/venues/{referenceId}`, so the recipient sees the "Add to My Venues"
  action rather than a 404/wrong-owner page.
