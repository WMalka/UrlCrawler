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
- `Crawler.Worker`: fetch (retry/timeout/2 MB/HTML-only), AngleSharp link extraction, idempotent persistence under a
  per-job lock, child enqueueing within MaxPages, retry 1s/5s/15s, DLQ, fault consumer, health endpoints.
- **End-to-end verified (2026-10-19)** against Docker SQL Server + RabbitMQ:
  - `scrapethissite.com/pages/`, depth 2: Completed in ~4 s, 38 pages / 774 edges, tree and ratios correct.
  - Root message delivered 3x concurrently: identical rows to a single delivery (0 duplicate pages/edges);
    duplicates stopped by the conditional page update under the job lock.
  - Malformed message and unknown-page message both land in `crawl-page-requested_error`; the poison message is not retried.
  - API and Worker `/health/ready` Healthy.
- Docker: one multi-stage `Dockerfile` (build arg `PROJECT`) for API and Worker, non-root, curl health checks,
  optional extra CAs from `certs/`. `docker-compose.yml`: SQL Server, RabbitMQ (management UI), API (migrates,
  Swagger), 2 Worker replicas, web (nginx; built by the UI work). Health-gated startup order; secrets from `.env`.
  **Verified:** stack healthy, crawl Completed (38 pages) with work split across both replicas (21/17).
- `web/`: Vite + React 18 + TypeScript, react-router, TanStack Query. Start Crawl (validation, server field errors),
  Job Details (polls every 1.5 s while Pending/Running, progress, cancel, recursive tree with DLR and lazy links once
  Completed), History (paged). Loading/error/empty states, accessible toggles. nginx image proxies `/api` to the API.
  **Verified:** `npm run build` + lint pass; full compose stack serves the UI on :3000 and a crawl renders end to end.
- **Tests: 77 passing** (Release). Unit 64. Integration 13: Testcontainers SQL Server + MassTransit in-memory harness +
  local HTML fixtures (worker pipeline: crawl, concurrent duplicates, redelivery, limits, failures, cancel, poison;
  API: create via outbox, validation, tree 409, cancel, history). Found and fixed: `MaxRetryAttempts=0` crashed the
  HTTP pipeline (Polly needs >= 1); connection string was read before test configuration applied (now resolved lazily).
- CI: `.github/workflows/ci.yml`: .NET build + tests, web lint + build, `docker compose build`.
- Docs: README (run, API, rules, message schema, idempotency, retries, DLQ, SQL, cuts, next steps), ARCHITECTURE.md.
- Docs: ADR-001..003 ([DECISIONS.md](DECISIONS.md)), [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Next
- Push to GitHub (https://github.com/WMalka/UrlCrawler) and confirm the CI run is green.
- Then the known gaps below, starting with SSRF protection.

## Known gaps (to address later, in priority order)
1. **SSRF**: job URLs are not checked against loopback/private/link-local ranges (e.g. `http://localhost`,
   `http://169.254.169.254`). Fix: resolve the host and reject non-public IPs in the validator **and** in the worker
   (redirects and DNS rebinding), configurable for local fixtures.
2. **No automated API tests yet**: planned as WebApplicationFactory + Testcontainers SQL Server integration tests.
3. **History uses offset paging**: switch to keyset paging (`CreatedAt DESC, Id DESC` cursor) if time allows; the
   covering index already supports it.
4. **Rate limit is hardcoded** (10 job creations/min per IP): move to `RateLimiting` config section.
5. **Page links endpoint is not paged**: the worker caps stored links per page; add paging if the cap is raised.
6. **Root redirect to another host** (e.g. `example.com` → `www.example.com`): every link counts as external, so the
   crawl stops at the root. This follows the spec literally ("host of the initial job URL"). Option: use the root
   page's final host after redirects as the start host.
7. **Malformed messages** cannot be deserialized, so no `Fault<T>` is raised; harmless (they reference no page).
   A message lost without a fault would leave its job Running: add a sweeper for pages Queued longer than N minutes.
