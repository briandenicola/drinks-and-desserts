# MCP Server

Drinks & Desserts exposes a native [Model Context Protocol](https://modelcontextprotocol.io) (MCP)
endpoint so external agentic harnesses — such as a Hermes Agent, GitHub Copilot CLI, VS Code, or
Claude Code — can query a user's collection and invoke the AI recommendation pipeline as tools.

The server is built on the official `ModelContextProtocol.AspNetCore` SDK, embedded directly in the
existing `WhiskeyAndSmokes.Api` process (no separate process or port). It uses **Streamable HTTP**
transport in **stateless** mode: every request is authenticated and handled independently, with no
durable MCP session state kept on the server.

## Status: default-off

The MCP endpoint is disabled by default. An administrator must turn it on from **Admin → MCP Server**
before `/api/mcp` will respond to anything other than `503 Service Unavailable`. This mirrors the
default-off behavior of other integration surfaces in this app (e.g. OIDC providers) and lets an
operator choose if/when to expose this app to agent harnesses.

Toggle it via the API directly, if you prefer scripting it:

```
GET  /api/admin/mcp-settings   (AdminOnly)  -> { "enabled": false }
PUT  /api/admin/mcp-settings   (AdminOnly)  body: { "enabled": true }
```

## Authentication & capabilities

The endpoint is authenticated exclusively via the existing **API key** scheme — the same keys used
by the [External API](../README.md#external-api) for iOS Shortcuts — sent as an `X-API-Key` header.
JWT/session cookies are not accepted on `/api/mcp`.

API keys carry a list of **capabilities**. Profile → API Keys offers three:

| Access | Capabilities | Grants |
|---|---|---|
| Read only | `read` | The 5 read-only MCP tools and the life store feed |
| Read + write | `read`, `write` | Also changes data: capturing from an iOS Shortcut (`POST /api/external/capture`) |
| Read + AI agents | `read`, `agentic` | Also `get_recommendations` over MCP (invokes the AI pipeline) |

Create a key from **Profile → API Keys**:

1. Enter a name for the key and pick its access.
2. Click **Create**. The raw key is shown once — copy it immediately, it cannot be retrieved again.

A key's access can be changed later on the same page, without replacing the key, so the app using
it keeps working (`PATCH /api/users/me/api-keys/{id}` with `{ "capabilities": [...] }`).

Or via the API:

```
POST /api/users/me/api-keys
Body: { "name": "hermes-agent", "capabilities": ["read", "agentic"] }
```

`"read"` is always force-included server-side even if omitted from the request body. Keys created
before write became its own option could always capture, so they keep `write` until their access is
changed. A key without `write` that tries to capture gets a 403.

All MCP tools are always *discoverable* by any authenticated key — the server does not hide the
`get_recommendations` tool from keys lacking `agentic`. Instead, invoking it without the `agentic`
capability returns an MCP tool error ("Capability 'agentic' is required to use this tool."). This is
a deliberate simplification versus per-connection tool filtering: simpler and safer to implement
correctly, at the cost of the client seeing a tool it can't call. Document this for your own agent
prompts if you rely on tool-list filtering for UX.

Every tool call is scoped to the API key owner's data — there is no way for one user's key to read
or affect another user's collection.

## Endpoint

```
POST /api/mcp
Headers:
  X-API-Key: <your-api-key>
  Content-Type: application/json
```

This is a standard MCP Streamable HTTP endpoint — any MCP-compliant client library can speak to it
directly once it has a valid `X-API-Key` header configured as a custom header on the connection.

## Available tools

### Read-only (baseline `read` capability)

| Tool | Description |
|---|---|
| `search_items` | Search the collection (drinks, coffee, desserts, cigars) by free text and/or type. Excludes wishlist items. |
| `get_item` | Fetch one collection item by id. |
| `list_venues` | Search venues (bars, restaurants, cafes, lounges) by free text and/or type. |
| `get_venue` | Fetch one venue by id. |
| `collection_stats` | Aggregate stats: total items, wishlist count, average rating, counts by item type. |

None of these tools create, modify, or delete data.

### Agentic (`agentic` capability required)

| Tool | Description |
|---|---|
| `get_recommendations` | Runs the AI recommendation pipeline (same engine as the in-app Recommendations feature — see [Recommendation Engine](recommendation-engine.md)) against the user's rating history, with optional free-text preferences and item-type filters. Read-only with respect to collection data; makes a live call to Azure AI Foundry. |

`get_recommendations` does not accept a menu photo via MCP (the in-app feature does) — it is
intentionally kept to simple text-in/JSON-out input so it works well from any MCP client.

## Connecting a Hermes Agent (or any MCP client)

Register the server with your agent/MCP client, supplying the API key as a custom header. Example
using the GitHub Copilot CLI:

```bash
copilot mcp add drinks-and-desserts \
  --transport http \
  --url https://<your-deployment-host>/api/mcp \
  --header "X-API-Key: <your-api-key>"
```

For a self-hosted/local deployment, the URL is typically `http://localhost:5000/api/mcp` (or
whatever host/port your API is bound to — see [Local Development](local-development.md)).

Generic MCP client configuration (JSON-style, e.g. VS Code `mcp.json` or Claude Desktop):

```json
{
  "mcpServers": {
    "drinks-and-desserts": {
      "type": "http",
      "url": "https://<your-deployment-host>/api/mcp",
      "headers": {
        "X-API-Key": "<your-api-key>"
      }
    }
  }
}
```

After connecting, list tools to confirm discovery, then try a read-only call such as
`collection_stats` before testing `get_recommendations`.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `503 Service Unavailable` on every call | MCP is disabled. Enable it at Admin → MCP Server. |
| `401 Unauthorized` | Missing/invalid `X-API-Key` header, or the key was revoked. Check **Profile → API Keys**. |
| Tool call for `get_recommendations` returns a capability error | The key lacks the `agentic` capability. Change its access to Read + AI agents on Profile → API Keys. |
| A Shortcut's capture gets 403 | The key lacks `write`. Change its access to Read + write on Profile → API Keys. |
| `404` on `/api/mcp` | You're hitting the wrong host/port, or a reverse proxy isn't forwarding the path. Confirm the API process (not just the static frontend) is reachable at that URL. |
| Empty results from `search_items`/`list_venues` | The key's owner has no matching data, or the query/type filter is too narrow — try with no arguments first. |

## Security notes

- Stateless transport: no MCP session is held across requests, so there's no session-hijacking
  surface beyond the API key itself.
- Capability checks happen per-tool-call server-side; they cannot be bypassed from the client.
- The admin-gating toggle lets operators keep this surface fully closed until they intend to use it.
- API keys are hashed (SHA-256) at rest and compared in constant time, same as the existing
  External API.
