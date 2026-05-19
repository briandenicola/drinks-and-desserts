# Recommendation Engine Architecture

## Overview

The Drinks & Desserts recommendation engine is a sophisticated AI-powered personalization system that analyzes user rating history to suggest items they're likely to enjoy. Unlike the capture workflow which uses multi-agent orchestration, the recommendation engine operates with **direct AI inference** using Azure OpenAI models through the Azure AI Foundry platform.

## Why Not Foundry Agents?

The recommendation engine intentionally does **not** use the Azure AI Foundry agent framework (Microsoft Agent Framework) for several important architectural reasons:

### 1. Single-Shot Inference Pattern

Recommendations require a **single, direct inference call** rather than multi-step agent orchestration:

- **Capture workflow**: Needs 3-4 agents (Vision Analyst → Domain Expert → Data Curator) with feedback loops and refinement cycles
- **Recommendations**: Takes user profile + optional menu photo → generates recommendations in one pass

The agent framework overhead (state management, routing, multi-turn conversations) adds unnecessary complexity when a simple LLM call suffices.

### 2. Real-Time Response Requirements

Users expect recommendations **immediately** when they:
- Browse a menu and snap a photo
- Enter preferences like "something smoky" or "fruity dessert"
- Filter by item type (whiskey, wine, dessert, etc.)

Multi-agent workflows introduce latency through:
- Agent state initialization
- Inter-agent message passing
- Coordinator overhead
- Multiple network round-trips

Direct inference provides sub-2-second response times.

### 3. No Refinement or Validation Needed

The capture workflow uses agents because:
- Vision analysis needs validation by domain expert
- Domain expert output needs quality checking by curator
- Parse failures require retry with feedback

Recommendations have **no such requirements**:
- Output is a simple ranked list with reasoning
- No structured extraction (like JSON parsing of item details)
- Failure mode is graceful: empty list + explanation message
- No iterative refinement needed

### 4. Different AI Capabilities Required

**Capture agents** use:
- **Vision model (gpt-4o)**: Analyze photos, read labels, identify products
- **Reasoning model (gpt-5-mini)**: Domain expertise, tasting notes, quality assessment
- **Structured extraction**: Parse JSON, validate fields, ensure completeness

**Recommendation engine** uses:
- **Vision model (gpt-4o)**: Menu photo OCR/analysis (optional, single-shot)
- **Reasoning model (gpt-5-mini)**: Pattern matching on user history, similarity scoring, explanation generation
- **Natural language output**: Recommendations + reasoning (no strict schema)

### 5. Stateless vs Stateful Operations

- **Agents**: Maintain conversation state across multiple turns (Vision → Expert → Curator feedback loop)
- **Recommendations**: Stateless function call (user profile in → recommendations out)

The agent framework shines for stateful, multi-turn workflows. Recommendations are pure functions that don't benefit from state management.

## Architecture

### High-Level Flow

```
┌─────────────────────────────────────────────────┐
│ User Input                                       │
│  • Rating history (required)                    │
│  • Text preferences (optional)                  │
│  • Menu photo (optional)                        │
│  • Item type filters (optional)                 │
└──────────────────┬──────────────────────────────┘
                   │
                   v
┌─────────────────────────────────────────────────┐
│ Build User Profile (src/api/Services/           │
│   RecommendationService.cs:86-167)              │
│                                                 │
│  • Query CosmosDB for all rated items          │
│  • Filter to rated items (rating > 0)          │
│  • Calculate type preferences                   │
│  • Extract top-rated items (≥4.0 stars)        │
└──────────────────┬──────────────────────────────┘
                   │
                   v
         ┌─────────┴─────────┐
         │ Menu photo?        │
         └─────────┬─────────┘
                   │
        ┌──────────┴───────────┐
        │ Yes                  │ No
        v                      v
┌────────────────────┐  ┌──────────────────────┐
│ Extract Menu Items │  │ Skip to Generation   │
│ (Vision Model)     │  └──────────────────────┘
│ gpt-4o             │
│ Single inference   │
└────────┬───────────┘
         │
         v
┌─────────────────────────────────────────────────┐
│ Generate Recommendations                         │
│ (Reasoning Model - gpt-5-mini)                  │
│ Single inference with:                          │
│  • User profile (ratings, preferences)          │
│  • Menu items (if provided)                     │
│  • Text preferences                             │
│  • Type filters                                 │
│                                                 │
│ Prompt includes:                                │
│  • Top-rated items with details                │
│  • Type preferences & averages                  │
│  • Menu items (if available)                    │
│  • User's additional preferences                │
│                                                 │
│ Returns JSON:                                   │
│  {                                              │
│    "recommendations": [                         │
│      {                                          │
│        "name": "Item Name",                     │
│        "type": "whiskey",                       │
│        "brand": "Brand",                        │
│        "confidence": 0.95,                      │
│        "reason": "Why recommended",             │
│        "matchedFromMenu": true                  │
│      }                                          │
│    ],                                           │
│    "reasoning": "Overall strategy"              │
│  }                                              │
└──────────────────┬──────────────────────────────┘
                   │
                   v
┌─────────────────────────────────────────────────┐
│ Response to User                                │
│  • Ranked recommendations                       │
│  • Confidence scores                            │
│  • Personalized reasoning                       │
│  • Items used for matching                      │
└─────────────────────────────────────────────────┘
```

## How AI Powers Recommendations

### 1. User Profile Construction (No AI)

The system analyzes the user's rating history **without AI**:

```csharp
// src/api/Services/RecommendationService.cs:86-167
public async Task<UserRatingProfile> BuildUserProfileAsync(string userId, ...)
{
    // 1. Fetch all user items from CosmosDB
    var allItems = await FetchUserItems(userId);

    // 2. Filter to rated items only
    var ratedItems = allItems.Where(i => i.UserRating > 0);

    // 3. Calculate statistics
    var avgRating = ratedItems.Average(i => i.UserRating);

    // 4. Build type preferences (count + avg rating per type)
    var typePreferences = ratedItems.GroupBy(i => i.Type)
        .ToDictionary(g => g.Key, g => new TypePreference {
            Count = g.Count(),
            AverageRating = g.Average(i => i.UserRating),
            TopRated = g.OrderByDescending(i => i.UserRating).Take(3)
        });

    // 5. Extract top-rated items (4.0+ stars)
    var topRated = ratedItems.Where(i => i.UserRating >= 4.0).Take(10);

    return new UserRatingProfile {
        TopRatedItems = topRated,
        ItemTypePreferences = typePreferences,
        AverageRating = avgRating,
        TotalRatedItems = ratedItems.Count
    };
}
```

**Key Insight**: Profile building is deterministic SQL-style aggregation. No AI needed.

### 2. Menu Photo Analysis (Optional, Vision AI)

If user provides a menu photo, extract items using **gpt-4o vision model**:

```csharp
// src/api/Services/RecommendationService.cs:169-233
public async Task<List<string>> ExtractMenuItemsAsync(string photoUrl, ...)
{
    var credential = CredentialFactory.Create();
    var projectClient = new AIProjectClient(new Uri(_foundryOptions.ProjectEndpoint), credential);
    var chatClient = projectClient.OpenAI.GetChatClient(_foundryOptions.Models.Vision).AsIChatClient();

    var prompt = @"You are a menu analyst. Analyze this photo of a menu and extract all drink and dessert items listed.

    For each item, provide:
    1. The exact name as it appears on the menu
    2. The type (whiskey, wine, cocktail, vodka, gin, cigar, dessert, coffee, espresso, latte, etc.)
    3. Any visible brand, vintage, or category information

    Return ONLY a JSON array of strings, where each string is the full item listing as it appears on the menu.
    Example: [\"Macallan 18 Year Whiskey\", \"Cabernet Sauvignon 2019\", \"Espresso Martini\"]

    If no items are visible or the photo is not of a menu, return an empty array: []";

    var messages = new List<ChatMessage> {
        new(ChatRole.User, [
            new TextContent(prompt),
            new DataContent(photoBytes, "image/jpeg")
        ])
    };

    var response = await chatClient.GetResponseAsync(messages, cancellationToken);
    var items = JsonSerializer.Deserialize<List<string>>(StripMarkdownCodeFences(response.Text)) ?? [];

    return items;
}
```

**AI Usage**: Single-shot OCR + entity extraction. No multi-turn dialogue, no refinement loop.

### 3. Recommendation Generation (Reasoning AI)

Core intelligence powered by **gpt-5-mini reasoning model**:

```csharp
// src/api/Services/RecommendationService.cs:248-365
private async Task<RecommendationResponse> GenerateRecommendationsAsync(
    UserRatingProfile profile,
    RecommendationRequest request,
    List<string>? menuItems,
    CancellationToken cancellationToken)
{
    var credential = CredentialFactory.Create();
    var projectClient = new AIProjectClient(new Uri(_foundryOptions.ProjectEndpoint), credential);
    var chatClient = projectClient.OpenAI.GetChatClient(_foundryOptions.Models.Reasoning).AsIChatClient();

    // Build prompt with user profile
    var promptBuilder = new StringBuilder();
    promptBuilder.AppendLine("You are a personalized recommendation engine for drinks and desserts.");
    promptBuilder.AppendLine();
    promptBuilder.AppendLine("## User's Rating History");
    promptBuilder.AppendLine($"Total rated items: {profile.TotalRatedItems}");
    promptBuilder.AppendLine($"Average rating: {profile.AverageRating:F2}/5.0");
    promptBuilder.AppendLine();
    promptBuilder.AppendLine("### Top Rated Items (4.0+):");
    foreach (var item in profile.TopRatedItems)
    {
        promptBuilder.AppendLine($"- {item.Name} ({item.Type}) - {item.Rating:F1} stars");
        if (!string.IsNullOrEmpty(item.Brand))
            promptBuilder.AppendLine($"  Brand: {item.Brand}");
        if (!string.IsNullOrEmpty(item.AiSummary))
            promptBuilder.AppendLine($"  Notes: {item.AiSummary}");
    }

    promptBuilder.AppendLine();
    promptBuilder.AppendLine("### Type Preferences:");
    foreach (var (type, pref) in profile.ItemTypePreferences.OrderByDescending(kvp => kvp.Value.AverageRating))
    {
        promptBuilder.AppendLine($"- {type}: {pref.Count} rated, avg {pref.AverageRating:F2} stars");
        if (pref.TopRated.Any())
            promptBuilder.AppendLine($"  Favorites: {string.Join(\", \", pref.TopRated)}");
    }

    if (menuItems != null && menuItems.Any())
    {
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("## Available Menu Items:");
        foreach (var item in menuItems)
        {
            promptBuilder.AppendLine($"- {item}");
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Task: Recommend items from the menu above that match the user's preferences.");
    }
    else
    {
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Task: Recommend new items the user might enjoy based on their rating history.");
    }

    // Single inference call
    var messages = new List<ChatMessage> {
        new(ChatRole.User, promptBuilder.ToString())
    };

    var response = await chatClient.GetResponseAsync(messages, cancellationToken);
    var result = JsonSerializer.Deserialize<RecommendationResponse>(
        StripMarkdownCodeFences(response.Text),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    ) ?? new RecommendationResponse();

    return result;
}
```

**AI Capabilities Leveraged**:

1. **Pattern Recognition**: Identifies preferences from rating history
   - "User rates high-proof whiskeys highly → recommend cask strength bourbon"
   - "User loves smoky scotches → suggest Islay malts"

2. **Similarity Matching**: Finds items similar to top-rated favorites
   - "You loved Lagavulin 16 → try Ardbeg 10 (similar smoky profile)"
   - "You rated Oban 14 highly → consider Highland Park 12 (similar coastal character)"

3. **Menu Contextualization**: When menu items provided, filters recommendations
   - Only suggests items actually available on the menu
   - Ranks menu items by fit with user preferences

4. **Confidence Scoring**: Estimates likelihood user will enjoy recommendation
   - Based on similarity to top-rated items
   - Considers type preferences and average ratings

5. **Explanation Generation**: Provides natural language reasoning
   - "Recommended because you rated similar smoky scotches highly"
   - "Matches your preference for full-bodied red wines"

## Key Differences: Recommendations vs Capture Workflow

| Aspect | Capture Workflow | Recommendation Engine |
|--------|-----------------|----------------------|
| **AI Framework** | Azure AI Foundry Agents (Microsoft Agent Framework) | Direct Azure OpenAI inference via AI Foundry SDK |
| **Orchestration** | Multi-agent graph (Vision → Expert → Curator) | Single-shot inference (no orchestration) |
| **AI Calls** | 3-6 calls per capture (with refinement loops) | 1-2 calls per recommendation (menu + recs) |
| **State Management** | Stateful multi-turn conversations | Stateless function calls |
| **Validation** | Data Curator validates and refines | No validation needed |
| **Retry Logic** | Refinement feedback loop (max 2 retries) | None (graceful failure with empty list) |
| **Latency** | 10-30 seconds (multi-agent coordination) | 2-5 seconds (direct inference) |
| **Output Format** | Strictly validated JSON (Item schema) | Flexible JSON (recommendation list) |
| **AI Models Used** | Vision (gpt-4o) + Reasoning (gpt-5-mini) | Vision (gpt-4o) + Reasoning (gpt-5-mini) |
| **Use Case** | Complex multi-step analysis requiring expert validation | Simple pattern matching and ranking |
| **Failure Mode** | Reject capture if parsing fails | Return empty list with explanation |

## When to Use Agents vs Direct Inference

### Use Foundry Agents When:
- Multiple specialized steps required (vision → domain expertise → validation)
- Output needs structural validation and refinement
- Iterative feedback loops improve quality
- Stateful conversation context is beneficial
- Complex routing between agent types needed

### Use Direct Inference When:
- Single-purpose task (pattern matching, classification, generation)
- Real-time response required
- Output is flexible (natural language, simple JSON)
- No validation or refinement needed
- Stateless operation is sufficient

The recommendation engine is a textbook case for **direct inference**: simple input → single AI call → simple output, with no need for multi-agent orchestration complexity.

## AI Foundry Platform

Both systems use **Azure AI Foundry** but at different abstraction levels:

### Capture Workflow: High-Level Agent Framework
```csharp
// Uses WorkflowAgentService with agent definitions
var visionAgent = await projectClient.Agents.CreateAgentAsync(
    "gpt-4o",
    visionPrompt,
    "Vision Analyst"
);

var expertAgent = await projectClient.Agents.CreateAgentAsync(
    "gpt-5-mini",
    expertPrompt,
    "Domain Expert"
);

// Route between agents, maintain conversation threads
await agentClient.CreateThreadAsync(...);
await agentClient.CreateRunAsync(thread.Id, visionAgent.Id);
```

### Recommendation Engine: Low-Level OpenAI SDK
```csharp
// Direct OpenAI client with AI Foundry credentials
var credential = CredentialFactory.Create();
var projectClient = new AIProjectClient(new Uri(foundryProjectEndpoint), credential);
var chatClient = projectClient.OpenAI.GetChatClient(modelName).AsIChatClient();

// Single inference call
var response = await chatClient.GetResponseAsync(messages, cancellationToken);
```

**Both approaches leverage Azure AI Foundry's**:
- Managed model deployments (gpt-4o, gpt-5-mini)
- Authentication and authorization (Azure credentials)
- Cost tracking and monitoring
- Scaling and reliability infrastructure

The **only difference** is the abstraction level:
- Agents = high-level orchestration framework for multi-step workflows
- Direct inference = low-level OpenAI SDK for single-shot calls

## Configuration

AI Foundry configuration in `appsettings.json`:

```json
{
  "AiFoundry": {
    "ProjectEndpoint": "https://<project>.projects.api.azureml.ms",
    "Models": {
      "Vision": "gpt-5.1",       // Used for menu photo analysis
      "Reasoning": "gpt-5.4-mini" // Used for recommendation generation
    }
  }
}
```

**Fallback Behavior**: If AI Foundry is not configured (`ProjectEndpoint` is empty), the recommendation engine returns:
- Empty recommendations list
- Reasoning: "AI Foundry is not configured. Please configure Azure AI Foundry to get personalized recommendations."

No local fallback exists for recommendations (unlike capture workflow which has keyword-based extraction).

## API Endpoints

### POST /api/recommendations
Get personalized recommendations

**Request Body**:
```json
{
  "preferences": "Something smoky with a hint of peat",
  "menuPhoto": "https://.../menu.jpg",
  "itemTypes": ["whiskey", "cigar"],
  "limit": 5
}
```

**Response**:
```json
{
  "recommendations": [
    {
      "name": "Lagavulin 16",
      "type": "whiskey",
      "brand": "Lagavulin",
      "category": "Scotch",
      "confidence": 0.95,
      "reason": "Matches your preference for smoky scotches. You rated Ardbeg 10 and Laphroaig Quarter Cask highly.",
      "matchedFromMenu": true
    }
  ],
  "reasoning": "Based on your love of peated Islay malts (avg 4.5 stars), I've prioritized smoky scotches from the menu. You tend to rate full-bodied, high-proof whiskeys highly.",
  "basedOnItems": [
    "Ardbeg 10",
    "Laphroaig Quarter Cask",
    "Bruichladdich Port Charlotte"
  ],
  "extractedMenuItems": [
    "Lagavulin 16",
    "Oban 14",
    "Talisker 10"
  ]
}
```

### GET /api/recommendations/profile
Get user's rating profile (for debugging)

**Response**:
```json
{
  "userId": "user123",
  "topRatedItems": [
    {
      "itemId": "item-abc",
      "name": "Ardbeg 10",
      "type": "whiskey",
      "brand": "Ardbeg",
      "rating": 5.0,
      "aiSummary": "Intensely smoky Islay single malt..."
    }
  ],
  "itemTypePreferences": {
    "whiskey": {
      "count": 15,
      "averageRating": 4.2,
      "topRated": ["Ardbeg 10", "Lagavulin 16", "Laphroaig 10"]
    }
  },
  "averageRating": 3.8,
  "totalRatedItems": 42
}
```

### POST /api/recommendations/extract-menu
Extract items from menu photo (standalone)

**Request Body**:
```json
{
  "photoUrl": "https://.../menu.jpg"
}
```

**Response**:
```json
[
  "Lagavulin 16",
  "Oban 14",
  "Talisker 10",
  "Highland Park 12"
]
```

## Frontend Integration

The recommendations feature is exposed through `/recommendations` route:

- **Location**: `src/web/src/views/RecommendationsView.vue`
- **Service**: `src/web/src/services/recommendations.ts`

**User Flow**:
1. User navigates to Recommendations tab
2. (Optional) Takes photo of menu or enters preferences
3. (Optional) Filters by item type
4. Clicks "Get Recommendations"
5. System builds profile, extracts menu items (if photo), generates recommendations
6. Displays ranked list with reasoning and confidence scores

## Performance Characteristics

- **User Profile Build**: 100-500ms (CosmosDB query + aggregation)
- **Menu Extraction**: 2-4 seconds (vision model inference)
- **Recommendation Generation**: 1-3 seconds (reasoning model inference)
- **Total Latency**: 3-7 seconds (worst case with menu photo)

**Optimization**: Profile is built once per request, reused for menu extraction and recommendation generation.

## Testing

Recommendation engine tests: `tests/WhiskeyAndSmokes.Tests/Controllers/RecommendationsControllerTests.cs`

**Test Coverage**:
- User with no rated items → empty recommendations with helpful message
- User with rated items → valid recommendation response
- Menu photo extraction → extracted items list
- Error handling → graceful degradation

## Future Enhancements

Potential improvements that could leverage AI:

1. **Collaborative Filtering**: Use embeddings to find similar users, recommend what they liked
2. **Temporal Patterns**: Suggest items based on time of day, season, occasion
3. **Venue Context**: If at known venue, prioritize items user hasn't tried there
4. **Price Awareness**: Filter recommendations by price range (if menu has pricing)
5. **Dietary Restrictions**: Respect allergies, preferences (e.g., "no peat", "vegetarian desserts")

All of these could be implemented with **direct inference** — no need for agent orchestration.

## Conclusion

The recommendation engine demonstrates that **not every AI feature requires agent orchestration**. By using direct Azure OpenAI inference through AI Foundry, the system achieves:

- **Simplicity**: Single-shot inference is easier to reason about, test, and debug
- **Performance**: Sub-3-second response times (vs 10-30s for multi-agent workflows)
- **Cost Efficiency**: 1-2 AI calls vs 3-6+ calls with refinement loops
- **Maintainability**: No complex state management or routing logic

**Foundry Agents** remain essential for the capture workflow where multi-step analysis, validation, and refinement are critical. But for straightforward pattern matching and ranking tasks, **direct inference is the right architectural choice**.

This dual-mode approach (agents for complex workflows, direct inference for simple tasks) represents a pragmatic AI architecture that balances sophistication with simplicity.
