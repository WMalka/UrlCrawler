# Current Status

_Last updated: 2026-10-19_

## Done
- Solution skeleton: .NET 8 (`global.json`), central package versions (`Directory.Packages.props`), warnings as errors.
- `Crawler.Domain`: entities, statuses, `UrlNormalizer`, `PageLinkAnalyzer`, `DomainLinkRatio`, job transition rules.
- `Crawler.Contracts`: `CrawlPageRequested { JobId, PageId }`.
- `Crawler.Infrastructure`: `CrawlerDbContext`, entity configurations (persisted SHA-256 hash columns), MassTransit
  outbox entities, `AddCrawlerPersistence`, migration `InitialCreate` (includes RCSI).
  Verified on LocalDB: RCSI on, unique indexes present, duplicate URL rejected (2601), job delete cascades.
- `Crawler.Api`: `POST /api/jobs` (202 + Location), `GET /api/jobs/{id}` (job + page counts, one query), `/tree` (409 until Completed), `/pages/{pageId}/links`,
  `GET /api/jobs?page&pageSize`, `POST /api/jobs/{id}/cancel`, `/health/live`, `/health/ready`. Job + root page +
  first message written in one transaction via the MassTransit bus outbox. ProblemDetails for all errors, enum
  strings in JSON, UTC timestamps, per-IP rate limit on create, CORS from config, migrate-on-startup (dev).
  Smoke-tested against LocalDB (RabbitMQ down): all endpoints and error paths behave; outbox row has CorrelationId = JobId.
- Unit tests: 54 passing.
- Docs: ADR-001..003 ([DECISIONS.md](DECISIONS.md)), [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Next (in order)
1. `Crawler.Worker`: consumer, page processor (fetch → analyze → persist under job lock), HTTP resilience, retry/DLQ, fault consumer.
2. docker-compose + Dockerfiles.
3. React UI (`web/`).
4. Integration tests (incl. API tests via WebApplicationFactory) (Testcontainers SQL Server), CI workflow.
5. README: run instructions, message schema, idempotency, retry/DLQ, what was cut.

## Known gaps (to address later, in priority order)
1. **SSRF**: job URLs are not checked against loopback/private/link-local ranges (e.g. `http://localhost`,
   `http://169.254.169.254`). Fix: resolve the host and reject non-public IPs in the validator **and** in the worker
   (redirects and DNS rebinding), configurable for local fixtures.
2. **No automated API tests yet**: planned as WebApplicationFactory + Testcontainers SQL Server integration tests.
3. **History uses offset paging**: switch to keyset paging (`CreatedAt DESC, Id DESC` cursor) if time allows; the
   covering index already supports it.
4. **Rate limit is hardcoded** (10 job creations/min per IP): move to `RateLimiting` config section.
5. **Page links endpoint is not paged**: the worker caps stored links per page; add paging if the cap is raised.
