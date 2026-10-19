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
