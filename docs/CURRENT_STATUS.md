# Current Status

_Last updated: 2026-10-19_

## Done
- Solution skeleton (`Crawler.sln`, .NET 8 pinned via `global.json`, warnings as errors).
- `Crawler.Domain`: entities (`CrawlJob`, `CrawlPage`, `PageLink`), statuses, `UrlNormalizer`,
  `DomainLinkRatio`, `PageLinkAnalyzer`.
- `Crawler.Contracts`: `CrawlPageRequested { JobId, PageId }`.
- Unit tests: 54 passing (normalization, ratio, link analysis, job rules).
- Decisions: ADR-001 schema, ADR-002 URL rules, ADR-003 domain rules vs SQL ([DECISIONS.md](DECISIONS.md)).

## Next (in order)
1. `Crawler.Infrastructure`: DbContext, EF configurations (computed hash columns), InitialCreate migration + RCSI, MassTransit outbox entities.
2. `Crawler.Api`: jobs endpoints, validation, ProblemDetails, health.
3. `Crawler.Worker`: consumer, page processor (fetch → analyze → persist under job lock), HTTP resilience, retry/DLQ, fault consumer.
4. docker-compose + Dockerfiles.
5. React UI (`web/`).
6. Integration tests (Testcontainers SQL Server), CI workflow.
7. README: run instructions, message schema, idempotency, retry/DLQ, what was cut.

## Known issues
- None yet.
