# Current Status

_Last updated: 2026-10-19_

## Done
- Solution skeleton: .NET 8 (`global.json`), central package versions (`Directory.Packages.props`), warnings as errors.
- `Crawler.Domain`: entities, statuses, `UrlNormalizer`, `PageLinkAnalyzer`, `DomainLinkRatio`, job transition rules.
- `Crawler.Contracts`: `CrawlPageRequested { JobId, PageId }`.
- `Crawler.Infrastructure`: `CrawlerDbContext`, entity configurations (persisted SHA-256 hash columns), MassTransit
  outbox entities, `AddCrawlerPersistence`, migration `InitialCreate` (includes RCSI).
  Verified on LocalDB: RCSI on, unique indexes present, duplicate URL rejected (2601), job delete cascades.
- Unit tests: 54 passing.
- Docs: ADR-001..003 ([DECISIONS.md](DECISIONS.md)), [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Next (in order)
1. `Crawler.Api`: jobs endpoints, validation, ProblemDetails, health, MassTransit bus outbox, migrate on startup.
2. `Crawler.Worker`: consumer, page processor (fetch → analyze → persist under job lock), HTTP resilience, retry/DLQ, fault consumer.
3. docker-compose + Dockerfiles.
4. React UI (`web/`).
5. Integration tests (Testcontainers SQL Server), CI workflow.
6. README: run instructions, message schema, idempotency, retry/DLQ, what was cut.

## Known issues
- None.
