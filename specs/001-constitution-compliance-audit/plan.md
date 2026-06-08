# Implementation Plan: Constitution Compliance Audit

**Branch**: `001-constitution-compliance-audit` | **Date**: 2026-04-03 | **Spec**: [spec.md](spec.md)
**Input**: Codebase audit against `.specify/memory/constitution.md` v1.0.0

## Summary

Comprehensive compliance audit found **27 violations** across all 4 constitutional
principles and the security requirements section. This plan remediates all
violations through 9 implementation tasks organized by principle and severity.
No new features are added — this is purely hardening and consistency work.

## Technical Context

**Language/Version**: C# / .NET 10, TypeScript / Vue 3.5
**Primary Dependencies**: ASP.NET Core, Vite 8, Tailwind CSS 4, VitePWA
**Storage**: Azure CosmosDB, Azure Blob Storage, LiteDB (local)
**Testing**: dotnet test (xUnit — to be scaffolded), vue-tsc type checking
**Target Platform**: Web + iOS PWA (primary), Docker containers
**Project Type**: Web application (API + SPA)
**Performance Goals**: 3-minute max AI timeout, 3 items max per capture
**Constraints**: 15MB per-file upload limit, offline PWA shell loading
**Scale/Scope**: Single-user personal app, ~6 views, ~8 controllers

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: Code Quality & Defensive Coding

| Requirement | Pre-Audit | Post-Design |
|-------------|-----------|-------------|
| Input validation on all endpoints | FAIL — 6 controllers lack validation | Task 1 addresses |
| Canonical path validation | FAIL — prefix check bypassable | Task 2 addresses |
| AI prompt untrusted-input delimiters | PARTIAL — 4 executors inject raw | Task 3 addresses |
| Blob URL ownership validation | FAIL — substring check spoofable | Task 2 addresses |
| No internal leaks in error responses | FAIL — 3 locations expose internals | Task 4 addresses |
| No secrets in source control | FAIL — dev JWT + Cosmos key hardcoded | Task 4 addresses |
| No empty catch blocks | FAIL — 1 empty catch in ExternalController | Task 4 addresses |
| Fire-and-forget logging | PASS | — |
| Constant-time hash comparison | PASS | — |

### Principle II: Testing Standards

| Requirement | Pre-Audit | Post-Design |
|-------------|-----------|-------------|
| Zero warnings under dotnet build | PASS | — |
| CI runs build + type-check on PRs | PASS | — |
| AI parse rejects unparseable responses | FAIL — auto-approves on failure | Task 5 addresses |
| Integration tests for security paths | FAIL — no test project | Task 9 addresses |

### Principle III: User Experience Consistency

| Requirement | Pre-Audit | Post-Design |
|-------------|-----------|-------------|
| No emojis in UI text | PASS | — |
| Touch targets 44x44px minimum | FAIL — multiple undersized buttons | Task 7 addresses |
| Form input styling consistent | FAIL — AdminView uses rounded-lg | Task 8 addresses |
| Loading/empty states indicated | PASS | — |
| Pinch-to-zoom disabled in PWA | PASS | — |

### Principle IV: Performance & Reliability

| Requirement | Pre-Audit | Post-Design |
|-------------|-----------|-------------|
| Timeouts, no CancellationToken.None | FAIL — 2 locations | Task 6 addresses |
| Max 3 items per capture | PASS | — |
| AI fallback to local extraction | PASS | — |
| Upload type+size 15MB validation | FAIL — 20MB limit, missing checks | Task 6 addresses |
| Bounded retry for capture queue | FAIL — unbounded, no retry | Task 6 addresses |
| PWA precache app shell | PASS | — |
| Queries scoped to user partition | FAIL — 5 cross-partition queries | Task 4 documents |

### Security Requirements

| Requirement | Pre-Audit | Post-Design |
|-------------|-----------|-------------|
| All controllers [Authorize] | FAIL — AuthController missing | Task 4 addresses |
| Data ops scoped to GetUserId() | FAIL — admin ops on arbitrary userId | Task 4 documents |
| JWT secret via env, fail if missing | PASS | — |
| CORS explicit origins | PASS | — |

**Gate result**: 15 FAIL, 2 PARTIAL → all addressed by tasks below.

## Project Structure

### Documentation (this feature)

```text
specs/001-constitution-compliance-audit/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Audit findings and decisions
├── data-model.md        # Validation constraints and model changes
├── quickstart.md        # Verification steps
└── tasks.md             # Task list (to be generated)
```

### Source Code (affected files)

```text
src/api/
├── Controllers/
│   ├── AuthController.cs        # Input validation, [AllowAnonymous], timeout
│   ├── CapturesController.cs    # Input validation, upload URL validation
│   ├── ItemsController.cs       # Input validation, blob ownership, upload URL
│   ├── UsersController.cs       # Input validation
│   ├── AdminController.cs       # Input validation, error sanitization
│   ├── ExternalController.cs    # Empty catch fix, note length validation
│   └── UploadsController.cs     # Path validation hardening, size limit
├── Agents/
│   ├── WorkflowAgentService.cs  # Parse rejection, prompt delimiters
│   └── Executors/
│       ├── VisionExecutor.cs    # Prompt delimiters
│       ├── ExpertExecutor.cs    # Prompt delimiters
│       └── CuratorExecutor.cs   # Prompt delimiters, parse rejection
├── Services/
│   ├── LocalBlobStorageService.cs  # Path validation hardening
│   ├── BlobStorageService.cs       # Blob ownership validation
│   ├── AgentValidationService.cs   # Error sanitization
│   ├── LiteDbService.cs            # Path validation
│   └── CaptureProcessingService.cs # Bounded retry
├── Models/
│   └── ApiModels.cs             # Validation attributes
└── Program.cs                   # Bounded channel, dev secret cleanup

src/web/src/views/
├── CaptureDetailView.vue     # Touch targets
├── ProfileView.vue           # Touch targets
├── ItemDetailView.vue        # Touch targets
├── ItemsView.vue             # Touch targets
└── AdminView.vue             # Touch targets, form styling

tests/
└── WhiskeyAndSmokes.Tests/   # New test project (scaffolded)
    └── Controllers/
        └── AuthControllerTests.cs  # Initial integration tests
```

**Structure Decision**: Existing web-app structure (src/api + src/web). New
test project added under tests/ following .NET convention.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Cross-partition queries in AuthService (email lookup, Entra ID lookup) | Authentication inherently requires looking up users by non-partition-key fields | Denormalizing email→userId would add write complexity for single-user app |
| Cross-partition query in ApiKeyAuthHandler | API key hash lookup cannot use userId partition (key is the lookup) | Storing userId in API key header would leak user info |
| Cross-partition query in AuthController (first-user check) | Bootstrap check runs once, before any user exists | Pre-seeding a sentinel record adds deployment complexity |
| Cross-partition query in AdminController (user listing) | Admin operations are explicitly exempted by constitution | No alternative needed |
| Hardcoded dev JWT secret in Program.cs | Development convenience; production throws if not overridden | Requiring env var in dev would slow local development |
