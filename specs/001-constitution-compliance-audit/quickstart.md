# Quickstart: Constitution Compliance Audit

**Branch**: `001-constitution-compliance-audit` | **Date**: 2026-04-03

## What This Changes

This is a compliance remediation pass — no new features, only hardening of
existing code to match the project constitution v1.0.0.

## Verification Steps

After implementation, verify each category:

### 1. API Input Validation
```bash
# Test missing/invalid inputs return 400, not 500
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d '{}'
# Expected: 400 Bad Request with validation errors

curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d '{"email":"not-an-email","password":"x"}'
# Expected: 400 Bad Request with specific field errors
```

### 2. Build Verification
```bash
cd src/api && dotnet build -c Release --nologo -v q
cd src/web && npx vue-tsc --noEmit && npx vite build
```

### 3. Integration Tests
```bash
cd tests && dotnet test -c Release
```

### 4. Touch Target Verification
- Open PWA on iOS device
- Navigate through all views
- Confirm all buttons/links are comfortably tappable without precision

### 5. Error Response Verification
```bash
# Admin endpoint should not leak stack traces
curl http://localhost:5000/api/admin/status -H "Authorization: Bearer $TOKEN"
# Verify error messages are generic, not exception chains
```
