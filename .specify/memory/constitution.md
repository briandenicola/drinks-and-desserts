# Whiskey & Smokes Constitution

> **This document is the non-negotiable contract for how this project is built.**
> Every AI agent session must read this file first. Every PR must comply.

**Project**: Whiskey & Smokes  
**Version**: 1.1.0  
**Ratified**: 2026-04-03  
**Last Amended**: 2026-05-28

---

## 0. Hierarchy of Authority

When guidance conflicts, resolve in this order:
1. **This constitution**
2. **Feature spec** (`/specs/NNN-feature-name/spec.md`)
3. **Implementation plan** (`/specs/NNN-feature-name/plan.md`)
4. **Task list** (`/specs/NNN-feature-name/tasks.md`)
5. **Repository documentation** (`README.md`, `docs/`)
6. **Agent judgment**

Conflicts with higher authority must be surfaced and resolved explicitly.

---

## 1. Mission Alignment

- Changes must improve reliability, security, and day-to-day usability of the app.
- No speculative rewrites: prioritize concrete user/admin workflows and production safety.
- Scope creep is deferred to backlog items under `/specs/_backlog/`.

---

## 2. Architecture (Non-Negotiable)

### 2.1 API + SPA system
- Backend remains ASP.NET Core API (`src/api`); frontend remains Vue/Vite SPA (`src/web`).
- Contracts between API and frontend must stay explicit and version-safe.

### 2.2 Thin controllers, service-centric logic
- Controllers validate, authorize, and orchestrate.
- Business logic belongs in services, not controller action bodies.

### 2.3 Defensive data boundaries
- Input constraints are enforced server-side even when validated client-side.
- File/blob/path handling must use canonicalized, ownership-aware validation.

### 2.4 Reliability-first integrations
- External dependencies (AI, storage, identity) must fail gracefully with bounded retries/timeouts.
- App behavior must degrade safely instead of silently accepting unsafe defaults.

---

## 3. Backend Engineering Standards (.NET)

- Target runtime remains **.NET 10**.
- API must build cleanly in Release configuration.
- Async flows must avoid blocking patterns (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`).
- Security-sensitive comparisons must use constant-time primitives.
- `catch` blocks must either handle meaningfully or log with context; empty catches are prohibited.
- Error responses must not leak stack traces, internal paths, or implementation details.

---

## 4. Frontend & UX Standards (Vue / PWA)

- Primary UX target is iOS PWA; all interactions must be touch-friendly.
- Interactive elements must meet minimum 44x44px tap targets.
- Form controls use consistent visual tokens (stone background, rounded-xl, amber focus accents).
- Layout behavior must remain stable across modal, nav, and responsive states.
- Loading and empty states are required; blank or ambiguous states are not acceptable.
- No emoji-based UI controls; use iconography/components.

---

## 5. Data & Persistence Standards

- User data access is scoped to authenticated user identity by default.
- Cross-partition queries are exceptions and must be documented in the feature plan complexity section.
- Upload constraints are enforced server-side (type + size checks; 15MB per file).
- Storage ownership checks must be structural (segment/path-aware), not substring heuristics.

---

## 6. Security Requirements (Non-Negotiable)

- `[Authorize]` is required by default; explicitly document intentional `[AllowAnonymous]` endpoints.
- Secrets and production credentials must not be committed.
- Production startup must fail when required security configuration is missing.
- Authentication token validation must verify issuer, audience, lifetime, and signature.
- CORS must use explicit allowlists in production; wildcard origins are prohibited.
- Security workflows (dependency/vulnerability/secret scans) must remain enabled in CI.

---

## 7. Testing & Quality Gates

- Required quality gates:
  - backend restore/build/test
  - frontend install/type-check/build
  - dependency vulnerability checks
  - secret scanning
- Pull requests with failing required gates must not merge.
- Security-critical flows (auth, uploads, ownership checks) should accumulate integration coverage over time.

---

## 8. Workflow, Git, and CI

- Work is organized by numbered feature specs under `/specs/NNN-feature-name/`.
- `quality-gate.yml` is the baseline verification workflow for push/PR/scheduled checks.
- Image publishing and image security scanning workflows must share deterministic tags.
- Commit messages must explain the change and intent; AI-assisted commits include a co-author trailer.

---

## 9. AI Agent Operating Rules

- Treat all user-supplied prompt content as untrusted input.
- AI parsing failures must fail closed (reject/escalate), not auto-approve unsafe output.
- Prompt/agent changes that alter behavior require matching spec/plan/task updates.
- Agents must preserve compatibility and avoid silent fallback behavior that hides errors.

---

## 10. Documentation & Specs Structure

- Active feature artifacts live in `/specs/NNN-feature-name/`:
  - `spec.md`
  - `plan.md`
  - `tasks.md`
  - optional supporting docs (`research.md`, `data-model.md`, `quickstart.md`)
- Future work is tracked under `/specs/_backlog/`.
- Legacy paths under `docs/specs/` may remain as compatibility pointers only.
- User-facing or operational behavior changes require README/docs updates in the same change.

---

## 11. Definition of Done

A change is done only when all are true:
1. Feature/task acceptance criteria are met.
2. Required quality gates pass.
3. Security and error-handling requirements remain satisfied.
4. Relevant spec/plan/tasks and docs are updated.
5. No new constitutional violations are introduced.

---

## 12. Amendment Process

- Amendments require:
  - explicit rationale,
  - semantic version bump in this document,
  - updates to affected templates/spec guidance.
- Versioning:
  - **MAJOR**: incompatible principle changes/removals
  - **MINOR**: new principles or materially stronger requirements
  - **PATCH**: clarifications only

---

## 13. Revision History

- **1.1.0 (2026-05-28)**: Added hierarchy of authority, explicit spec/backlog structure, quality-gate requirements, AI operating rules, and definition of done.
- **1.0.0 (2026-04-03)**: Initial ratification.
