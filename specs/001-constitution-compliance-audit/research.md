# Research: Constitution Compliance Audit

**Branch**: `001-constitution-compliance-audit` | **Date**: 2026-04-03

## Audit Methodology

Three parallel audit agents scanned the full codebase against every MUST/SHOULD
requirement in the constitution. Each agent covered specific principles:
- Agent 1: Principle I (Code Quality & Defensive Coding) — 9 sub-requirements
- Agent 2: Principles II + III (Testing Standards + UX Consistency) — 9 sub-requirements
- Agent 3: Principle IV + Security Requirements (Performance + Security) — 11 sub-requirements

## Consolidated Findings

### Principle I: Code Quality & Defensive Coding

| # | Requirement | Status | Violations |
|---|-------------|--------|------------|
| 1 | Input validation on all endpoints | VIOLATED | 6 controllers lack null/length/type checks |
| 2 | Canonical path validation | VIOLATED | StartsWith without trailing separator is bypassable |
| 3 | AI prompt untrusted-input delimiters | PARTIAL | WorkflowAgentService compliant; VisionExecutor, ExpertExecutor, CuratorExecutor inject raw |
| 4 | Blob URL ownership validation | VIOLATED | Contains substring check is spoofable |
| 5 | No internal leaks in error responses | VIOLATED | AdminController exposes exception chains, internal paths |
| 6 | No secrets in source control | VIOLATED | Hardcoded dev JWT secret and Cosmos emulator key in Program.cs |
| 7 | No empty catch blocks | VIOLATED | ExternalController EXIF extraction swallows exceptions silently |
| 8 | Fire-and-forget logging | COMPLIANT | ApiKeyAuthHandler logs at Warning |
| 9 | Constant-time hash comparison | COMPLIANT | Uses CryptographicOperations.FixedTimeEquals |

**Details — Requirement 1 (Input Validation)**:
- CapturesController: fileName, id, continuationToken unvalidated; CreateCapture doesn't validate Photos bounds or UserNote length
- ItemsController: type, status, id, fileName, blobUrl accepted without strict validation
- AuthController: Email/Password/AccessToken not fully validated (format/length)
- UsersController: display name, API key name, keyId, current password not validated
- AdminController: userId, id, settings objects unvalidated
- ExternalController: note has no length checks

**Details — Requirement 2 (Path Validation)**:
- UploadsController:44-46, 81-83 — StartsWith(root) without trailing separator allows sibling directory escape (e.g., `C:\base\uploads_evil`)
- LocalBlobStorageService:58-64, 81-87 — Same prefix-check flaw
- LiteDbService:15-20 — DB path used directly with no canonical check

**Details — Requirement 3 (Prompt Delimiting)**:
- VisionExecutor:55-59 — User note injected as plain text
- ExpertExecutor:71-80, 105-113 — Vision description, user note, curator feedback interpolated raw
- CuratorExecutor:48-52 — Expert analysis inserted raw
- WorkflowAgentService:483-493 — Refinement prompt injects rejection reason and vision context without delimiters

**Details — Requirement 4 (Blob Ownership)**:
- BlobStorageService:89-103 — DeleteBlobAsync accepts any blob URL, no ownership check
- LocalBlobStorageService:54-74 — Same for local deletion
- ItemsController:342-345 — ValidateBlobOwnership uses Contains("/{userId}/") which is spoofable

**Details — Requirement 5 (Error Leaks)**:
- AdminController:244-262 — Returns full exception chain in ConnectivityTestResult.Message
- AgentValidationService:113-133 — Stores full exception chain in FoundryStatus, exposed by admin endpoint
- AdminController:159-164 — 405 response exposes internal paths (AgentInitiator/Prompts/)

**Details — Requirement 6 (Secrets)**:
- Program.cs:31-42 — Hardcoded dev JWT secret: `dev-secret-key-change-in-production-min-32-chars!!`
- Program.cs:171-173 — Hardcoded Cosmos emulator connection string with account key

**Details — Requirement 7 (Empty Catch)**:
- ExternalController:81-85 — `catch { /* EXIF extraction is best-effort */ }` with no logging

### Principle II: Testing Standards

| # | Requirement | Status | Violations |
|---|-------------|--------|------------|
| 1 | Zero warnings under dotnet build -c Release | COMPLIANT | No suppressions found |
| 2 | CI runs build + type-check on PRs | COMPLIANT | ci.yml confirmed |
| 3 | AI parse rejects unparseable responses | VIOLATED | Auto-approves on parse failure and after max refinements |
| 4 | Integration tests for security paths | VIOLATED | No test project exists |

**Details — Requirement 3 (AI Parse)**:
- WorkflowAgentService:122-127 — Parse failure falls back to Decision="approve" with ExtractBestEffortItems
- CuratorExecutor:65-77 — After max refinements, force-sets Decision="approve"

**Details — Requirement 4 (Tests)**:
- tests/ directory contains only sample files, no .cs test files or test project

### Principle III: User Experience Consistency

| # | Requirement | Status | Violations |
|---|-------------|--------|------------|
| 1 | No emojis in UI text | COMPLIANT | None found |
| 2 | Touch targets 44x44px minimum | VIOLATED | Multiple views have undersized targets |
| 3 | Form input styling consistent | VIOLATED | AdminView uses rounded-lg not rounded-xl |
| 4 | Loading/empty states indicated | COMPLIANT | All views checked |
| 5 | Pinch-to-zoom disabled in PWA | COMPLIANT | viewport meta confirmed |

**Details — Requirement 2 (Touch Targets)**:
- CaptureDetailView:145-151 — "Back to History" is plain text, tiny target
- ProfileView:210-220 — Sort pills use px-3 py-1.5, too small
- ItemDetailView:291-319 — Back/edit/delete/save icon buttons use p-1
- ItemDetailView:352-379 — Photo remove buttons are w-6 h-6
- ItemsView:161-174, 181-224, 375-386 — Tab/filter/sort/remove controls are small pills
- AdminView:200-235, 279-290, 396-402 — Tab and action buttons compact

**Details — Requirement 3 (Form Styling)**:
- AdminView:446-449, 466-469 — Two select elements use rounded-lg instead of rounded-xl

### Principle IV: Performance & Reliability

| # | Requirement | Status | Violations |
|---|-------------|--------|------------|
| 1 | Timeouts on external calls, no CancellationToken.None | VIOLATED | 2 locations |
| 2 | Max 3 items per capture | COMPLIANT | .Take(3) hard cap confirmed |
| 3 | AI fallback to local extraction | COMPLIANT | Multiple fallback paths exist |
| 4 | Upload type+size validation (15MB) | VIOLATED | Size is 20MB not 15MB; upload URL endpoints skip validation |
| 5 | Bounded retry for capture queue | VIOLATED | Unbounded channel, no retries |
| 6 | PWA precache app shell | COMPLIANT | Workbox confirmed |
| 7 | Queries scoped to user partition | VIOLATED | 5 cross-partition queries |

**Details — Requirement 1 (Timeouts)**:
- AuthController:146-152 — `configManager.GetConfigurationAsync(CancellationToken.None)`
- AdminController:231-233 — `CreateResponseAsync(healthCheckPrompt)` has no timeout

**Details — Requirement 4 (Upload Limits)**:
- UploadsController — Size limit is 20MB, should be 15MB
- CapturesController:32-43 — GetUploadUrl issues URLs without type validation
- ItemsController:264-277 — GetPhotoUploadUrl issues URLs without type validation

**Details — Requirement 5 (Bounded Retry)**:
- Program.cs:211-216 — Channel.CreateUnbounded<Capture>()
- CaptureProcessingService:35-45 — Failed captures logged only, no retry or dead-letter

**Details — Requirement 7 (Cross-Partition)**:
- AdminController:49-52 — User listing (admin operation)
- AuthController:71-72 — First-user bootstrap check
- AuthService:107-111 — Email lookup for login
- AuthService:131-135 — Entra object ID lookup
- ApiKeyAuthHandler:38-42 — API key lookup

### Security Requirements

| # | Requirement | Status | Violations |
|---|-------------|--------|------------|
| 1 | All controllers use [Authorize] | VIOLATED | AuthController missing |
| 2 | Data ops scoped to GetUserId() | VIOLATED | Admin ops on arbitrary userId |
| 3 | JWT secret via env, fail if missing | COMPLIANT | Program.cs throws in non-dev |
| 4 | CORS explicit origins | COMPLIANT | No wildcards |

**Details — Requirement 1**:
- AuthController has no [Authorize] attribute; needs explicit [AllowAnonymous] on public actions

**Details — Requirement 2**:
- AdminController:68-78, 94-103, 122-130 — Operates on arbitrary userId (admin operations)
- These are intentional admin functions but need documentation as constitutional exceptions

## Decisions

| Decision | Rationale | Alternatives Considered |
|----------|-----------|------------------------|
| Fix path validation with trailing separator | Prevents sibling directory escape | Path.GetRelativePath (more complex, same result) |
| Structural URL parsing for blob ownership | Parse URL segments instead of substring | Regex match (fragile with URL encoding) |
| Bounded channel capacity: 100 | Reasonable backpressure for single-user app | Unbounded (current, no backpressure) |
| Retry strategy: 3 attempts, exponential backoff | Standard resilience pattern | Linear delay (less gentle on transient failures) |
| Document cross-partition queries as exceptions | Auth/admin queries inherently cross-partition | Denormalize email→userId mapping (over-engineered) |
| Scaffold xUnit test project | .NET standard, matches existing csproj ecosystem | NUnit (less common in .NET ecosystem) |
| Touch target: min-h-[44px] min-w-[44px] utility | Tailwind approach consistent with project | Explicit px values (less responsive) |
