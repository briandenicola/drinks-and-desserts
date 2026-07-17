# Data Model: Share / Add to Collection from Friends' Venues

## `SourceAttribution` (new shared type, `src/api/Models/Item.cs` or a shared
location — recommend defining once in `Item.cs` and reusing on `Venue`)

```csharp
public class SourceAttribution
{
    [JsonPropertyName("sourceUserId")]
    public string SourceUserId { get; set; } = string.Empty;

    [JsonPropertyName("sourceDisplayName")]
    public string SourceDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("sourceItemId")]
    public string SourceItemId { get; set; } = string.Empty; // source venue ID when attached to a Venue

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
```

- Nullable/absent on documents that were not copied from a friend (default
  behavior for all existing and normally-created items/venues).
- `SourceDisplayName` is captured at copy time (not live-looked-up), matching
  the existing pattern used by `Friendship.FriendDisplayName`. If the friend
  later renames their account, historical attribution keeps the old name —
  acceptable and consistent with how `Friendship` already behaves.
- `SourceItemId` intentionally does not enforce referential integrity against
  the friend's item — the source may later be edited or deleted by its owner
  without affecting the copy (see spec Edge Cases).

## `Item` (extended)

Add one new nullable property:

```csharp
[JsonPropertyName("sourceAttribution")]
public SourceAttribution? SourceAttribution { get; set; }
```

## `Venue` (extended)

Add the same nullable property:

```csharp
[JsonPropertyName("sourceAttribution")]
public SourceAttribution? SourceAttribution { get; set; }
```

## `Notification` (extended — constants only, no schema change)

```csharp
public static class NotificationType
{
    // ...existing values...
    public const string ItemShared = "item-shared";
    public const string VenueShared = "venue-shared";
}
```

`NotificationService.BuildNotificationUrl` already maps `referenceType` values
`"item"` and `"venue"` to `/items/{id}` and `/venues/{id}`. Shares set
`ReferenceType = "item"` / `"venue"` (not the new type constant — `Type` is
for the notification's own category, `ReferenceType` is for URL building), so
no change to `BuildNotificationUrl` is required.

## New request DTOs (`src/api/Models/ApiModels.cs`)

```csharp
public class ShareRequest
{
    [JsonPropertyName("friendId")]
    [Required]
    [StringLength(100)]
    public string FriendId { get; set; } = string.Empty;
}
```

Used identically by both `POST /api/items/{id}/share` and
`POST /api/venues/{id}/share`.

## Frontend type additions

`src/web/src/services/items.ts` / `venues.ts`:

```typescript
export interface SourceAttribution {
  sourceUserId: string
  sourceDisplayName: string
  sourceItemId: string
  addedAt: string
}
```

Add `sourceAttribution?: SourceAttribution` to the existing `Item` and `Venue`
interfaces.
