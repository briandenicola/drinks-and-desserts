---
name: using-drinks-and-desserts-mcp
description: "Use Drinks & Desserts' read-only collection/venue tools and the AI recommendations tool safely."
---

# Using Drinks & Desserts MCP

Use this skill when a user asks for facts, comparisons, or suggestions grounded
in their Drinks & Desserts collection (whiskey, wine, cocktails, coffee,
cigars, desserts) or venues (bars, restaurants, cafes, lounges).

## Connection

Connect by Streamable HTTP to:

```text
{DRINKS_AND_DESSERTS_URL}/api/mcp
```

Send the API key only in this header:

```text
X-API-Key: {DRINKS_AND_DESSERTS_API_KEY}
```

Never place the key in a prompt, URL, repository file, tool argument, log, or
answer. A `read` key exposes the five collection/venue tools. The
`get_recommendations` tool additionally requires an `agentic` key.

The server is admin-gated and default-off — if every call returns
`503 Service Unavailable`, tell the user an administrator must enable it from
Admin → MCP Server before this skill can be used.

For GitHub Copilot CLI, create the key in Drinks & Desserts under
Profile → API Keys (check "Grant 'agentic' capability" if you need
recommendations), put it in the current shell's secret environment, then
register the server:

```powershell
$env:DRINKS_AND_DESSERTS_URL = "https://your-deployment-host"
$env:DRINKS_AND_DESSERTS_API_KEY = "<replace_with_generated_key>"
copilot mcp add --transport http --header "X-API-Key: $env:DRINKS_AND_DESSERTS_API_KEY" drinks-and-desserts "$env:DRINKS_AND_DESSERTS_URL/api/mcp"
```

Use `/mcp show drinks-and-desserts` to verify discovery. Do not commit the
resulting credential or copy a populated user configuration into a
repository.

For GitHub Copilot CLI, VS Code, Claude Code, generic Streamable HTTP clients,
and status-code troubleshooting, see
[`docs/mcp-server.md`](../../../docs/mcp-server.md).

## Read Tools

- `search_items`: search owned collection items (drinks, coffee, desserts,
  cigars) by free text and/or type. Excludes wishlist items. Use a focused
  query and bounded limit.
- `get_item`: fetch one known owned item by id.
- `list_venues`: search owned venues (bars, restaurants, cafes, lounges) by
  free text and/or type.
- `get_venue`: fetch one known owned venue by id.
- `collection_stats`: read aggregate counts (total items, wishlist count,
  average rating, counts by type).

Treat empty results as evidence that the user has no matching data. Do not
infer items, venues, ratings, or stats that tools did not return.

## Recommendations (Agentic)

`get_recommendations` requires the `agentic` capability. It runs a live AI
inference call (Azure AI Foundry) against the user's rating history and
returns personalized suggestions with confidence scores and reasoning. It
accepts optional free-text `preferences` and `itemTypes` filters, and a
`limit` (max 10). It does not accept a menu photo over MCP and does not
modify any data — calling it has no side effects on the user's collection.

If a call to `get_recommendations` fails with a capability error, tell the
user their API key needs to be recreated with the `agentic` capability
enabled; capabilities cannot be edited on an existing key.

## Boundaries

The MCP surface is entirely read-only with respect to collection and venue
data. These tools do not add, edit, delete, import, or rate items or venues.
If asked for a mutation, explain that it must be completed in the Drinks &
Desserts application.
