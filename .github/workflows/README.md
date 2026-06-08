# GitHub Workflows

This folder contains the repository automation baseline.

| Workflow | File | Trigger | Purpose | Owner |
|---|---|---|---|---|
| Quality Gate | `quality-gate.yml` | `push` to `main`, all PRs, weekly schedule, manual | Backend/frontend build validation, vulnerability checks, secret scan, config scan | Engineering |
| Container Publish | `docker-publish.yml` | `push` to `main`, semver tags (`v*.*.*`), manual | Build and push API/Web images to Docker Hub | Engineering |
| Container Image Security Scan | `security-scan.yml` | On successful completion of Container Publish, manual | Trivy scan for published API/Web container images, SARIF upload | Engineering |
| CI (legacy) | `ci.yml` | PR to `main` | Existing PR CI checks retained during migration | Engineering |
| Build & Deploy | `build.yml` | Manual | Environment-targeted Azure build/deploy workflow | Engineering |

## Trigger strategy

- `quality-gate.yml` is the baseline push/PR gate.
- `docker-publish.yml` publishes on `main`, semver tags, and manual runs.
- `security-scan.yml` chains automatically from successful image publish runs.

## Branch protection recommendation

Require `Quality Gate` status checks on `main` after this migration so merge policy aligns with the new gate workflow.
