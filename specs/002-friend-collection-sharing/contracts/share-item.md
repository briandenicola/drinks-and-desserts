# Contract: Share an Item with a Friend

`POST /api/items/{id}/share`

Controller: `ItemsController`

## Authorization
- `[Authorize]` (existing on controller)
- Caller MUST own the item (existing `GetAsync<Item>(ContainerName, id,
  userId)` ownership pattern — 404 if not owned/found).
- Caller and `request.FriendId` MUST have an `accepted` `Friendship`
  (reuse the same structural check pattern as `FriendsController
  .AreFriendsAsync`; `ItemsController` will need read access to the
  `friendships` container — inject via `ICosmosDbService`, same as
  `FriendsController` does, no new service needed).

## Request

```json
{ "friendId": "string" }
```

Body: `ShareRequest` (see data-model.md).

## Behavior
1. Load the item by `id` scoped to caller; `404` if not found/not owned.
2. Verify caller/`friendId` are accepted friends; `403` if not.
3. Create a `Notification`:
   - `UserId = friendId` (recipient)
   - `Type = NotificationType.ItemShared`
   - `Title = "{caller display name} shared {item.Name} with you"`
   - `SourceUserId = callerId`
   - `SourceDisplayName = <caller display name>`
   - `ReferenceType = "item"`
   - `ReferenceId = item.Id`
4. `await _notificationService.CreateAsync(notification)` (existing service —
   persists then best-effort Pushover push per recipient preference).
5. Return `200 OK` (no response body needed beyond success ack, consistent
   with `FriendsController.AcceptRequest`).

## Responses
- `200 OK` — notification created.
- `403 Forbidden` — caller and `friendId` are not accepted friends.
- `404 Not Found` — item does not exist or is not owned by caller.
- `400 Bad Request` — `friendId` missing/invalid (model validation).

## Notes
- The recipient's item detail route (`/friends/{callerId}/items/{itemId}`) is
  reached via the notification's built URL
  (`NotificationService.BuildNotificationUrl`, `referenceType: "item"` →
  `/items/{id}`) — **caveat**: that URL maps to the *recipient's own* items
  route, which is wrong for a shared item they don't own yet. The frontend
  notification click-handler (or `BuildNotificationUrl`) needs a
  `item-shared`/`venue-shared`-aware branch that instead links to
  `/friends/{sourceUserId}/items/{referenceId}` (or the equivalent venue
  route) so the recipient lands on the friend-view page that has the "Add to
  My Collection" action from US1. This is called out explicitly because it is
  an easy correctness trap: reusing the plain `"item"`/`"venue"` reference
  type for the URL breaks the whole point of the share. Recommended fix:
  branch `BuildNotificationUrl` on `notification.Type` first
  (`item-shared`/`venue-shared` → friend-view URL using `SourceUserId`), fall
  back to the existing `ReferenceType` switch otherwise.
