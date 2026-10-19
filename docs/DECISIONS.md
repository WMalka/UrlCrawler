# Architecture Decision Records

## ADR-001: Lean crawl schema with derived progress

- **Status:** Accepted
- **Date:** 2026-10-19

### Context
Jobs fan out into one message per page and are processed at least once by N worker replicas.
We need per-job limits, live progress, de-duplication of pages and edges, and reliable completion
detection, within a 4-hour timebox and a ~200-page-per-job limit.

### Options
1. Progress counters on `CrawlJobs`, limits in DB, app-computed URL hash.
2. Limits snapshotted on the job; progress derived from `CrawlPages.Status`; DB-computed URL hash.
3. Limits in config only; separate progress table; URL indexed directly (nvarchar(850)).

### Decision
Option 2.
- `MaxDepth`/`MaxPages` are stored per job as a snapshot of the effective values. Config holds defaults and bounds only.
- Progress and completion are computed with `COUNT(*) … GROUP BY Status` under a per-job `UPDLOCK`.
- `UrlHash`/`TargetUrlHash` are persisted `HASHBYTES('SHA2_256', …)` computed columns with unique
  indexes `(JobId, UrlHash)` and `(SourcePageId, TargetUrlHash)`.
- Page link counts are stored once (immutable after processing); the ratio is computed in the domain.
- Idempotency: conditional `Queued → terminal` page update + unique indexes + MassTransit EF outbox.
- `READ_COMMITTED_SNAPSHOT` is on, so polling reads never block on worker locks.

### Consequences
+ A single source of truth: no counters that drift on the success, skip, fault or cancel paths.
+ Duplicate pages and edges are impossible at the DB level, independent of app logic.
+ Limits stay consistent across replicas and rolling deploys, and history shows what applied.
− A small `COUNT` per page commit and per poll (≤200 indexed rows, negligible).
− Writes within one job are serialized (writes across jobs still run in parallel).
Revisit if MaxPages > ~10k: add counter columns (additive migration).

## ADR-002: URL normalization and link classification

- **Status:** Accepted
- **Date:** 2026-10-19

### Decision
- Canonical URL: absolute http(s); fragment and user-info removed; scheme/host lowercased; default
  port removed; dot-segments resolved; empty path → `/`. Path and query are kept unchanged
  (case- and order-sensitive on many servers). Implemented in `UrlNormalizer`.
- Non-http(s) hrefs (`mailto:`, `tel:`, `javascript:`, `data:`, `ftp:` …) are ignored entirely.
- Outgoing links of a page = **distinct** normalized links, excluding links back to the page itself
  (`#top`, `./`, its own absolute URL). This keeps the ratio and the stored edges consistent: one row per target.
- Internal = link host equals the start URL host, **exact and case-insensitive**: `www.example.com` ≠ `example.com`.
- Only internal links are crawled; external links are stored as edges but never fetched.

### Consequences
+ De-duplication is predictable and fully unit-tested.
− Sites that mix `www.` and apex hosts get lower ratios and fewer crawled pages. A host-alias rule would be a contained change in `PageLinkAnalyzer.IsInternal`.

## ADR-003: Domain owns transition rules, SQL applies them atomically

- **Status:** Accepted
- **Date:** 2026-10-19

### Options
1. Anemic entities; every rule written inside Infrastructure SQL.
2. Rich entities changed through EF change tracking with optimistic concurrency (rowversion).
3. Hybrid: pure rule methods on `CrawlJob` (`CanEnqueueChildrenOf`, `RemainingPageSlots`,
   `ResolveStatusAfterPageFinished`); Infrastructure runs them under the per-job lock and
   persists the result with set-based, conditional SQL.

### Decision
Option 3. Option 1 hides business rules in SQL strings, where they are hard to test. Option 2
needs load-modify-save round trips and retry loops on concurrency conflicts, and it can't express
`INSERT … WHERE NOT EXISTS … OUTPUT`.

### Consequences
+ Rules are unit-tested without a database; atomicity and idempotency stay in SQL.
+ A root page that fails or is not HTML fails the job with a reason.
− A rule touches two layers: Domain decides, Infrastructure enforces.
