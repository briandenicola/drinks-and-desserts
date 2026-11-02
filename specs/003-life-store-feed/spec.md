# Feature Specification: Read-only feed for the household life store

**Feature Branch**: `claude/lifestore-records`
**Created**: 2026-10-26
**Status**: Complete
**Input**: Owner request, 2026-10-26: "start drinks-and-desserts", the last of the household apps given a full-read feed for the life store.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The life store reads everything with one key (Priority: P1)

The household life store answers questions across the household's apps ("what did I
rate highest at Jack Rose?"). It reads each app in full on every poll, skips what is
unchanged, and treats a record that is no longer listed as deleted. The owner makes an
API key in the Profile tab and pastes it into the life store's settings page.

**Independent Test**: Create a key, call `GET /api/lifestore/records` with it, follow
`next` until it is `null`, and confirm every item and venue the owner has is listed once.

**Acceptance Scenarios**:

1. **Given** a valid API key, **When** the feed is read page by page, **Then** every item
   (any status: AI draft, reviewed, wishlist) and then every venue the key's owner has is
   listed exactly once, and the last page's `next` is `null`.
2. **Given** another user's data, **When** the owner's key reads the feed, **Then** none of
   it is listed.
3. **Given** no key, an invalid key, or a browser session token, **When** the feed is
   called, **Then** it returns `401`.
4. **Given** a `page` value the feed did not give out, **When** it is called, **Then** it
   returns `400` with a message saying to start again without a page.

### Edge Cases

- A user with no items or venues gets pages with empty `records` and the feed still ends.
- `limit` outside 1–200 is clamped, not refused: the reader follows `next` either way.

## Requirements *(mandatory)*

- **FR-001**: `GET /api/lifestore/records?page=&limit=` answers
  `{schema: "drinksanddesserts.lifestore.records/1", records: [{kind, id, updatedAt, data}], next}`.
- **FR-002**: Authenticated by the existing `ApiKey` scheme only (§6: `[Authorize]` by
  default). Scoped to the key's owner (§5). GET only; it changes nothing.
- **FR-003**: Records leave out photo URLs, capture ids, AI processing internals
  (`aiConfidence`, `processedBy`, workflow steps, processing errors) and owner ids.
  `aiSummary` is kept and labelled as such.
- **FR-004**: Friends' thoughts are **not** listed: they are other people's words.
- **FR-005**: No schema, container or partition changes; no change to any existing endpoint.

## Success Criteria

- **SC-001**: The life store's definition for this app maps a captured feed response
  without error, and its end-to-end test reads it through the settings page.
