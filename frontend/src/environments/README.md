# Environment Configuration

## Overview
Runtime environment configurations differentiating local development API targets from production reverse-proxy routes.

## Interface
- `environment.ts:environment`: Development configuration targeting local backend at `http://localhost:5210/api/v1` with `production: false`.
- `environment.prod.ts:environment`: Production configuration targeting reverse-proxied relative path `/api/v1` with `production: true`.

## Invariants
- `[environment.prod.ts:environment]`: Production environment delegates routing to `/api/v1` via reverse proxy (e.g., NGINX).

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`
