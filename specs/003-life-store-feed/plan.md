# Implementation Plan: Read-only feed for the household life store

**Branch**: `claude/lifestore-records` | **Date**: 2026-10-26 | **Spec**: [spec.md](./spec.md)

## Summary

One controller (`LifestoreController`, thin per §2.2) and one service
(`LifestoreFeedService`). The service pages the owner's partition of `items` and then
`venues` through the existing `ICosmosDbService.QueryAsync`, so Cosmos and LiteDB each
use their own continuation. The `next` token is base64url JSON of the container being read
and that container's continuation; anything else is refused.

## Technical Context

**Language/Version**: C# / .NET 10
**Storage**: existing `items` and `venues` containers, single-partition queries on the
owner's id. No cross-partition query (§5).
**Testing**: `LifestoreFeedServiceTests` against a real LiteDB file (paging is the
storage's own); `LifestoreControllerTests` through `CustomWebApplicationFactory`.

## Constitution Check

- §2.2 thin controller, logic in the service. §2.3 the token is validated server-side.
- §3 Release build clean; no blocking async; no empty catch (the token decode catch
  documents why it falls through).
- §5 owner-scoped, single-partition. §6 `[Authorize(AuthenticationSchemes = "ApiKey")]`.
- §10 README updated with the endpoint.

## Complexity

None. A tampered Cosmos continuation surfaces as a Cosmos `400`, which the service turns
into the same "start again" refusal.
