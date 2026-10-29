# Infrastructure Queues

## Overview
Asynchronous background queuing mechanism providing an in-memory deduplicated channel and hosted worker for cascading tenant recalculations (preparations, technical sheets, combos, and margin alerts).

## Interface
- `RecalculationQueue`: Implements `IRecalculationQueue` using a bounded `Channel<Guid>` with in-memory deduplication of queued tenant IDs (`[RecalculationQueue.cs:RecalculationQueue]`)
- `RecalculationWorker`: Hosted service (`BackgroundService`) consuming recalculation tasks and executing `IRecalculationService` within an isolated DI service scope (`[RecalculationWorker.cs:RecalculationWorker]`)

## Invariants
- Queue deduplication: If a tenant is already queued in `_pending`, subsequent enqueue requests are discarded as redundant (`[RecalculationQueue.cs:EnqueueAsync]`)
- Bounded capacity: Channel is configured with a bounded limit of 1,000 items (`BoundedChannelOptions`) (`[RecalculationQueue.cs:RecalculationQueue]`)
- Scope isolation: Each tenant recalculation runs in a freshly created `IServiceScope` to isolate database context lifecycle (`[RecalculationWorker.cs:ExecuteAsync]`)
- Worker resilience: Exceptions thrown during tenant calculation are caught and logged, preventing the background hosted service from terminating (`[RecalculationWorker.cs:ExecuteAsync]`)

## Side Effects
- Enqueues and dequeues tenant identifiers into an in-memory bounded channel (`[RecalculationQueue.cs]`)
- Triggers cascade updates across entity sheets, preparations, and margin alerts via scoped `IRecalculationService` (`[RecalculationWorker.cs:ExecuteAsync]`)

## Verification
- Run: `dotnet test backend/`
