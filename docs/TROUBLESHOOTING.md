# Troubleshooting

## `INSERT failed because the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'` (Msg 1934)
- **Cause:** `CrawlPages.UrlHash` / `PageLinks.TargetUrlHash` are indexed computed columns, which require
  `QUOTED_IDENTIFIER ON`. `sqlcmd` defaults to OFF; SqlClient/EF and SSMS default to ON.
- **Fix:** run `sqlcmd -I …`, or `SET QUOTED_IDENTIFIER ON;` in ad-hoc scripts.

## `Method 'get_LockReleaseBehavior' … does not have an implementation` when running `dotnet ef`
- **Cause:** MassTransit.EntityFrameworkCore **8.5+** depends on EF Core 9 on net8.0, which mixes with EF Core 8 SqlServer.
- **Fix:** MassTransit is pinned to **8.4.1** in `Directory.Packages.props` (EF Core 8 dependency). Do not upgrade it
  without upgrading all EF Core packages together.

## Hash lookups find nothing
- Hash the parameter as **nvarchar** (`CAST(HASHBYTES('SHA2_256', @url) AS binary(32))` with `@url nvarchar(2048)`).
  Hashing a `varchar` value produces different bytes.

## Pages fail with `HTTP 418 Blocked by NetFree`
- **Cause:** the local network uses the NetFree content filter, which blocks many sites (e.g. `quotes.toscrape.com`,
  `books.toscrape.com`, `crawler-test.com`).
- **Workaround for manual tests:** `https://www.scrapethissite.com/pages/`, `https://webscraper.io/test-sites`,
  `https://docs.python.org/3/`. Automated tests use local HTML fixtures and are unaffected.

## Page skipped with "Redirect … not followed: insecure downgrade or redirect limit"
- HttpClient deliberately refuses HTTPS → HTTP redirects (and stops after `Crawler:MaxRedirects`). Such pages are
  recorded as **Skipped** with the redirect target, not as failures.

## Running API/Worker locally against Docker
- Credentials come from the shared user-secrets store `alteva-crawler-dev` (`ConnectionStrings:Crawler`,
  `RabbitMq:Username`, `RabbitMq:Password`), set once with `dotnet user-secrets set … --project src/Crawler.Api`.
- A running app locks its `bin` folder: stop it before `dotnet build`/`dotnet test`.
