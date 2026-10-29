# Core HTTP Interceptors

## Overview
HTTP client interceptors handling outgoing request authentication (Bearer tokens), automatic token refresh on HTTP 401, contextual error formatting, and global notification handling.

## Interface
- `jwt.interceptor.ts:jwtInterceptor`: HttpInterceptorFn attaching the `Authorization: Bearer <token>` header and orchestrating single-flight token refresh on 401 responses.
- `error.interceptor.ts:errorInterceptor`: HttpInterceptorFn translating unhandled HTTP errors into Portuguese user-facing snackbar notifications.
- `error.interceptor.ts:extractApiErrorMessage`: Extracts user-facing error details from structured `ApiError` payloads or standard HTTP status codes.
- `http-context.ts:SILENT_ERROR`: HttpContextToken allowing specific HTTP requests to bypass global error snackbar notifications for local page handling.

## Invariants
- `[jwt.interceptor.ts:jwtInterceptor]`: Requests to `/auth/` endpoints do not trigger token refresh on 401 to prevent infinite recursion.
- `[jwt.interceptor.ts:jwtInterceptor]`: Re-authenticates failed requests at most once per failure sequence; if refresh fails, session is cleared and routed to `/login`.
- `[error.interceptor.ts:errorInterceptor]`: Status 401 is ignored (handled by `jwtInterceptor`), and 409 conflicts are delegated to calling pages unless unhandled.

## Side Effects
- `[jwt.interceptor.ts:jwtInterceptor]`: Mutates outgoing HTTP headers and triggers HTTP POST `/auth/refresh` on 401 responses.
- `[error.interceptor.ts:errorInterceptor]`: Spawns Material snackbar popups upon HTTP failure.

## Verification
- Run: `npm test --prefix frontend`
