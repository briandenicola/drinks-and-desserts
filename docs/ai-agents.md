# How Our AI Agents Read Your Photos

You snap a photo of a bottle, a glass, or a cigar band, maybe type a quick note, and a few minutes later a
fully filled-in entry shows up in your collection. This page explains what happens in between.

Think of it as a small **tasting panel**: one person looks at the photo, one reads your note, one is the
resident expert who knows the products, and one is the editor who checks the paperwork before it's filed.

## The Cast

| Agent | Think of it as... | What it does | Model |
|-------|-------------------|--------------|-------|
| **Vision Analyst** | The person with the sharp eyes | Looks at your photos and describes what's in front of you: labels, bottle shapes, liquid color, cigar bands, plating. It only reports what it *sees*. It doesn't guess the product. | `gpt-4o` |
| **Note Analyst** | The person who reads your scribbles | Reads your quick note and pulls out *where* you were, *how you felt* (turned into a 1-5 star suggestion), and the *occasion* (date night, celebration...). | `gpt-5-mini` |
| **Domain Expert** | The sommelier / master blender / tobacconist | Takes the Vision Analyst's description and names the actual product: distillery, age, region, vintage, cigar vitola. Adds tasting notes, price range, pairings, and a confidence score. | `gpt-5-mini` |
| **Data Curator** | The editor | Turns the expert's write-up into a tidy, validated record and checks it's complete and sensible. If something is off, it sends it back with feedback. | `gpt-5-mini` |

A few ground rules baked into the agents:

- **Foreground only.** They focus on the 1-3 things *you* are drinking or smoking. Bottles on the back
  shelf and other tables are ignored.
- **A bottle and the glass poured from it count as one item**, not two.
- **Maximum of 3 items per capture**, keeping the highest-confidence ones.
- **Your note is treated as information, not orders.** Text in a note can't tell an agent to behave differently.

## Where Everything Runs

| Piece | Where it lives |
|-------|----------------|
| **The app's API** (the "conductor" that runs the workflow) | Azure Container Apps (or Docker Compose when self-hosted) |
| **The agents and AI models** | Microsoft Azure AI Foundry (Foundry Agent Service) |
| **Your photos** | Blob storage (local disk when self-hosted) |
| **Your items, captures and the step-by-step log** | Azure Cosmos DB |
| **Agent instructions (prompts)** | Markdown files in `src/AgentInitiator/Prompts/`, loaded into Foundry by the **Agent Initiator** tool |

The **Agent Initiator** is a small command-line tool run at setup time (`task local:agents`). It registers
the agents in Foundry and gives each its instructions. To change how an agent behaves, edit its prompt file
and re-run that tool. Sign-in between the app and Foundry uses Azure identity, so there are no passwords
or keys stored in the app.

## The Workflow, Step by Step

1. **You capture.** The photos and your note are saved and the capture is placed in a queue. You're free to
   leave the app; processing happens in the background.
2. **Location check.** If you didn't share your location, the app tries to read GPS data hidden in the photo.
3. **Vision Analyst and Note Analyst run at the same time.** Neither needs the other's help, so
   they work in parallel. (The Note Analyst only runs if you wrote a note.)
4. **Domain Expert** gets the photo description, your note, and location, and identifies the products.
5. **Data Curator** structures the result and reviews it.
   - **Approved:** on to the next step.
   - **Rejected:** the curator explains why, and the Domain Expert tries again with that feedback.
     This can happen **up to 2 times**; after that the best attempt is accepted so you're never stuck.
6. **Item creation.** Results are combined with the Note Analyst's findings (venue, star suggestion,
   occasion tag) and saved as **AI Draft** items tagged `needs-review`, so you can confirm or fix them.
7. **You get a notification** that your capture is done. Every step is recorded and visible in History.

### Workflow diagram

```mermaid
flowchart TD
    A([📷 You capture photos + optional note]) --> B[Saved and queued for background processing]
    B --> C{Location provided?}
    C -- No --> D[Read GPS from photo]
    C -- Yes --> E
    D --> E[Start analysis]

    E --> V[👁️ Vision Analyst<br/>describes what's in the photos]
    E --> N[📝 Note Analyst<br/>venue, star rating, occasion]

    V --> X[🥃 Domain Expert<br/>identifies the actual products]
    N -. held until the end .-> M
    X --> K[🧾 Data Curator<br/>structures and validates]

    K --> Q{Approved?}
    Q -- "No (up to 2 retries)" --> X
    Q -- Yes --> M[Merge with note findings<br/>keep top 3 items]
    M --> S[(Saved as AI Draft items<br/>tagged needs-review)]
    S --> Z([🔔 You're notified])
```

### Who talks to whom (sequence)

```mermaid
sequenceDiagram
    autonumber
    actor You
    participant API as App API<br/>(Container Apps)
    participant Q as Background queue
    participant V as Vision Analyst<br/>(gpt-4o)
    participant N as Note Analyst<br/>(Foundry agent)
    participant E as Domain Expert<br/>(Foundry agent)
    participant C as Data Curator<br/>(Foundry agent)
    participant DB as Cosmos DB

    You->>API: Upload photos + note
    API->>DB: Save capture (Processing)
    API->>Q: Enqueue capture
    API-->>You: Accepted, you can leave the app
    Q->>API: Next capture, go!

    par Look at the photos
        API->>V: Photos + note + location
        V-->>API: Description of what's visible
    and Read the note
        API->>N: Your note
        N-->>API: Venue, rating, occasion
    end

    API->>E: Vision description (+ note, location)
    E-->>API: Product identification and tasting notes

    loop Up to 3 tries (1 + 2 retries)
        API->>C: Expert analysis
        C-->>API: Approve, or reject with reason
        opt Rejected
            API->>E: Please revise: reason
            E-->>API: Revised analysis
        end
    end

    API->>DB: Save items (AI Draft) and log every step
    API-->>You: Notification: workflow completed
```

### Where things run (architecture)

```mermaid
flowchart LR
    subgraph Device["Your device"]
        PWA[Web app / PWA]
    end

    subgraph Azure["Azure"]
        API["API<br/>Container Apps"]
        subgraph Foundry["AI Foundry"]
            GPT4o["gpt-4o<br/>Vision Analyst"]
            subgraph Agents["Registered agents (gpt-5-mini)"]
                NA[Note Analyst]
                DE[Domain Expert]
                DC[Data Curator]
            end
        end
        Blob[(Photo storage)]
        Cosmos[(Cosmos DB)]
    end

    Init["Agent Initiator CLI<br/>(setup time)"] -. registers agents and prompts .-> Agents
    PWA -->|photos and note| API
    API --> Blob
    API --> Cosmos
    API --> GPT4o
    API --> NA
    API --> DE
    API --> DC
```

## What if Something Goes Wrong?

The app is built to always give you *something* rather than a blank screen.

```mermaid
flowchart TD
    S([Capture received]) --> F{AI Foundry configured?}
    F -- No --> L[Local keyword extraction<br/>uses your note only]
    F -- Yes --> W[Run agent workflow]
    W --> OK{Worked?}
    OK -- Yes --> Done([Completed by AI])
    OK -- "No, or no items found" --> L
    L --> LD{Worked?}
    LD -- Yes --> D2([Completed, marked as fallback<br/>low confidence, needs-review])
    LD -- No --> Fail([Failed, you're notified<br/>details in History])
```

- **Note Analyst fails?** The workflow carries on without it. You just lose the venue/star suggestions.
- **An agent takes longer than 3 minutes?** It's cut off and the fallback kicks in.
- **Fallback items** are guessed from your note's keywords, get a low confidence score, and are tagged
  `needs-review` so you know to double-check them.

## Other Agents: Wishlist and Venue Links

The same Foundry setup powers two "paste a link" features. Each works in the background too:

| Agent | Trigger | What it does |
|-------|---------|--------------|
| **Wishlist URL Extractor** | You paste a product page into your wishlist | The app fetches the page, and the agent pulls out name, brand, category and notes to fill in the wishlist item. |
| **Venue URL Extractor** | You paste an Apple Maps or venue website link | The app fetches the page, and the agent pulls out venue name, address and details. |

```mermaid
flowchart LR
    U([You paste a link]) --> Fetch[App fetches the page]
    Fetch --> Agent[URL Extractor agent<br/>reads the page content]
    Agent --> Save[(Wishlist item or venue<br/>filled in)]
```

## Seeing It Work

- **History** shows each step of a capture (Vision Analyst, Note Analyst, Domain Expert, Data Curator, and
  any retries), what each one concluded, and whether it succeeded.
- **Admin > Foundry Status** checks that the agents exist and Foundry is reachable.
- Item cards show whether an item came from the **AI workflow** or the **local fallback**.

## Related Docs

- [Recommendation Engine](recommendation-engine.md): a separate, faster AI feature that doesn't use these agents
- [Azure Deployment](azure-deployment.md): how Foundry and the API are provisioned
- [Local Development](local-development.md): running the agents locally
