# Project Context

- **Project:** whiskeys-and-smokes
- **Created:** 2026-04-24

## Core Context

Agent Ralph initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-04-24

### OIDC Security Review (2026-06-24)

Conducted security review of OIDC implementation and identified host-header/forwarded-host injection vulnerability.

**Finding:** OIDC redirect URI validation used untrusted X-Forwarded-Host/Proto headers without configured origin guard. HIGH confidence exploitability—attacker could manipulate redirect URIs to external sites.

**Recommendation:** (1) Require configured `Oidc:PublicOrigin` for production; (2) Ignore X-Forwarded-Host/Proto headers for OIDC origins; (3) Validate origin shape (HTTPS, no untrusted ports); (4) Wire `Oidc__PublicOrigin` through config/deployment; (5) Add regression tests.

**Coordinator Actions (Completed):**
- Backend: Added `PublicOrigin` to OidcSettings, updated OidcService to use configured origin
- Deployment: Added `Oidc__PublicOrigin` variable and secret reference in containerapp.tf
- Tests: Added regression tests to OidcControllerTests.cs (invalid origins rejected, configured origin respected, X-Forwarded-Host ignored)
- Result: 135/135 tests passed, including host-header regression tests

**Impact:** Prevents host-header injection attacks in OIDC redirect URIs; OIDC callbacks verified to configured origin only.


## Learnings

Initial setup complete.
