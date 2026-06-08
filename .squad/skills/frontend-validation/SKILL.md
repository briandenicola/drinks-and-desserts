---
name: "frontend-validation"
description: "Validate the Vue frontend in project mode on Windows"
domain: "frontend-testing"
confidence: "high"
source: "observed"
tools:
  - name: "powershell"
    description: "Runs repository validation commands"
    when: "Use from src\\web for Vue typechecking"
---

## Context

Use this skill after changing Vue SFCs, frontend TypeScript services, or other code that participates in the Vite/Vue build.

## Patterns

- Run validation from `src\web`.
- Prefer `npm.cmd exec -- vue-tsc -b` on Windows PowerShell.
- Keep the `--` separator so `-b` is passed to `vue-tsc` instead of npm.

## Examples

```powershell
Set-Location -Path 'C:\Users\brian.denicolafamily\Code\whiskeys-and-smokes\src\web'
npm.cmd exec -- vue-tsc -b
```

## Anti-Patterns

- Do not use plain `npm exec vue-tsc -b` in PowerShell; execution policy can block `npm.ps1`.
- Do not omit `--`; npm may consume `-b` as its own option.
