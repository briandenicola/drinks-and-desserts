# Feature Spec: Constitution Compliance Audit

**Branch**: `001-constitution-compliance-audit` | **Date**: 2026-04-03

## Overview

Audit the entire Whiskey & Smokes codebase against the project constitution
(`.specify/memory/constitution.md` v1.0.0) and fix all violations found.
The constitution defines 4 core principles plus security requirements and
development workflow standards. This is a remediation effort, not a new feature.

## Scope

**In scope**: All code violations of the 4 constitutional principles and
security requirements section. Specifically:
- Principle I: Code Quality & Defensive Coding
- Principle II: Testing Standards
- Principle III: User Experience Consistency
- Principle IV: Performance & Reliability
- Security Requirements section

**Out of scope**:
- New features or UI changes beyond what the constitution requires
- Full automated test suite (constitution uses SHOULD, not MUST for integration tests)
- Rate limiting (deferred security item from prior audit)
- Optimistic concurrency / ETags (deferred)

## Success Criteria

1. All API endpoints validate inputs before processing
2. All path operations use hardened canonical validation
3. All AI prompt user data wrapped in untrusted-input delimiters
4. Blob URL ownership uses structural path parsing (not substring)
5. No error responses leak stack traces or internal paths
6. No empty catch blocks
7. No CancellationToken.None on external service calls
8. Upload size limits aligned to 15MB per constitution
9. AI parse failures reject rather than auto-approve
10. Touch targets meet 44x44px minimum on mobile
11. Form inputs use consistent styling per constitution
12. Capture processing uses bounded channel with retry
13. AuthController actions annotated with [AllowAnonymous] where public
14. Cross-partition queries documented as justified exceptions
15. Integration test project scaffolded with at least auth endpoint coverage
