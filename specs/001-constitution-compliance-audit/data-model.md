# Data Model: Constitution Compliance Audit

**Branch**: `001-constitution-compliance-audit` | **Date**: 2026-04-03

## Overview

This audit does not introduce new entities or data model changes. All fixes
are behavioral (validation, error handling, retry logic, UI sizing) applied
to existing code.

## Affected Models

### Input Validation Models (new)

These are not new database entities but request validation constraints to be
enforced on existing API model classes.

| Model | Field | Constraint |
|-------|-------|------------|
| CreateCaptureRequest | Photos | 1-10 items, each URL <= 2048 chars |
| CreateCaptureRequest | UserNote | <= 1000 chars |
| AddPhotoRequest | BlobUrl | Required, <= 2048 chars, must match userId pattern |
| RemovePhotoRequest | BlobUrl | Required, <= 2048 chars |
| LoginRequest | Email | Required, valid email format, <= 254 chars |
| LoginRequest | Password | Required, 8-128 chars |
| RegisterRequest | Email | Required, valid email format, <= 254 chars |
| RegisterRequest | Password | Required, 8-128 chars |
| RegisterRequest | DisplayName | Required, <= 100 chars |
| UpdateProfileRequest | DisplayName | <= 100 chars |
| CreateApiKeyRequest | Name | Required, <= 100 chars |

### Capture Processing (modified behavior)

| Change | Current | Target |
|--------|---------|--------|
| Channel type | `Channel.CreateUnbounded<Capture>()` | `Channel.CreateBounded<Capture>(100)` |
| Retry strategy | None (log and discard) | 3 attempts, exponential backoff (1s, 4s, 16s) |
| Failed capture | Lost | Logged with full context at Error level |

## No Schema Changes

- CosmosDB containers: unchanged
- Blob storage structure: unchanged
- LiteDB schema: unchanged
