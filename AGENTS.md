# User Instructions

This file is the agent's persisted memory. Keep it concise — small, important instructions that prevent repeated mistakes. Improve it proactively when you learn something worth remembering. Does not require user approval — edit it right away and mention that you did it.

Always run `just validate` before committing. Warnings are treated as errors (`-warnaserror`) — fix all warnings before committing.

The app is not deployed and is under active development.

## Project: HoN Reborn community stats site

Stack: .NET 10 ASP.NET Core **Razor Pages + HTMX**, hexagonal seams (Web lean /
App unit-tested / Infra = HttpClients). **NEVER write JavaScript.** Use native
semantic HTML only (`<dialog>`, popover attr, `<details>`). Razor is templating
only — interactivity via HTMX fragments. No JS/CSS libraries without approval.
C# formatter is **csharpier** (runs via `nix fmt` / treefmt).

## Architecture (4 layers)

- **HonStats.Domain** — all models (mutable classes) + domain events. No deps.
  Persisted models (`IndexedPlayer`, `PlayerMatch`, `MatchRoster`,
  `MatchPlayerItem`, `HeroBuild`, `Teammate`) are configured as EF entities
  directly via `IEntityTypeConfiguration<T>` in Infra — **no separate `*Entity`
  classes**, no mapping layer. Transient juvio read-DTOs (`PlayerProfile`,
  `Hero`, `MatchDetail`, …) are also classes here.
- **HonStats.App** — ports (interfaces), use cases, pure aggregators
  (`HeroBuildAggregator`, `TeammateAggregator`), domain-event dispatcher/handler
  abstractions. References Domain only.
- **HonStats.Infra** — EF Core DbContext + `IEntityTypeConfiguration<T>` on
  Domain classes, juvio HttpClient adapters, SQLite repos, BackgroundServices.
  References App + Domain.
- **HonStats.Web** — Razor Pages composition root. References App + Infra.

## Testing

xUnit + **AwesomeAssertions** (fluent) + **Snapper** (snapshots). Tests in
`tests/HonStats.App.Tests` cover App + Infra. Hand-written fakes (no mocking lib).
Persisted model behavior validated via `Database.MigrateAsync()` + table asserts.

## Notes

- EF Core 10.0.9 + SQLite. **`SQLitePCLRaw.bundle_e_sqlite3` pinned to 3.0.3**
  in Infra (transitive 2.1.11 from EF has GHSA-2m69-gcr7-jv3q → NU1903 fails the
  `-warnaserror` gate; 3.0.3 is patched and EF-compatible).
- `dotnet ef` is a **local tool** (`.config/dotnet-tools.json`, v10.0.9) — run
  `dotnet tool restore` before migrations. `HonStatsDbContextDesignFactory` lets
  `dotnet ef` run with `--project src/HonStats.Infra --startup-project src/HonStats.Infra`.
- Solution is `HonStats.slnx` (.NET 10 XML format).

## Juvio API integration map (validated)

Hosts: `auth|gamedata|stats|player|economy|chat|gamestorage .juvio.com`.
Swagger: `https://<svc>.juvio.com/swagger/v1/swagger.json` (gamestorage has none — CDN).

**Auth** (one server-side service account token for ALL calls):
`POST auth.juvio.com/v1/auth/plainauth?username=&password=&deviceInfo.installId=<uuid>&clientName=`
→ `{account, authToken (JWT), refreshToken}`. **GOTCHA: POST with query params,
empty body — `curl -G` gives 405.** Credentials in `dotnet user-secrets`
(`HonStats:Juvio:Username` / `:Password`). Inject token via a DelegatingHandler
setting `Authorization: Bearer`.

**Endpoints by feature** (param names vary per endpoint — see notes):
- Search: `GET auth /v1/userinfo/getidbyusername?Usernames=<name>` → exact match → accountId.
- Bulk name resolve: `POST auth /v1/userinfo/getuserinfo` body `{accountIds:[...]}` → displayName, username, **country**.
- Profile overview: `GET stats /v1/stats/getprofilestats?userId=<uuid>` → avgKDA/DPM/GPM/XPM, winRate, `allTimeRecord.wardsPlaced`, topRoles, topHeroes (heroImageUrl pre-populated).
- Profile totals: `GET stats /v1/stats/getplayersummary?accountId=<uuid>` → totalK/D/A, KDR, winRate.
- Rank: `GET stats /v1/stats/getplayerrank?accountId=<uuid>` → rankName, starLevel, icon, color.
- Matches: `GET stats /v1/stats/getrecentmatchesforplayer?playerId=<uuid>&limit=&offset=`.
- Match detail: `GET stats /v1/stats/getmatchsummary?gameId=<int>` → players[10] with `inventory48Id`–`inventory64Id` (item ids), `wardOfSight/RevelationPlaced`, netWorth, RoleIndex.
- Teammates: `GET stats /v1/stats/getrecentplayers?playerId=<uuid>` → flat `{playerId,matchId}[]`, aggregate by count, exclude self, resolve names via getuserinfo.
- Hero builds: filter recent matches by heroId → getmatchsummary each → aggregate inventory ids → map via gamedata items.
- Heroes: `GET gamedata /entities/heroes` (PUBLIC, 102 heroes; `translatedName`=display, `icon` is an **array** take [0]).
- Items: `GET gamedata /entities/items` (PUBLIC, 210 items; same icon-array quirk).

**Image CDN** (public, predictable): `https://gamestorage.juvio.com/heroes/{id}/icon.webp` and `.../items/{id}/icon.webp`.

**Known dead ends:**
- `auth /v1/userinfo/getusernamebyid` has no documented params → always empty. Use POST getuserinfo instead.
- Stats endpoints use different id param names: profilestats=`userId`, playersummary/rank=`accountId`, recentmatches/recentplayers=`playerId`. Same UUID value.
- gamedata `icon` fields are arrays; `translatedName` (not `name`) is the display name; entities are public (no bearer needed).
- `getrecentmatchesforplayer` rejects a large `limit` with **400** (e.g. 200 fails; 25 works, max unknown). The indexer pages via `offset` at `Indexing:RecentMatchesLimit` (25) to ingest full history.
- Razor Pages PageModels don't auto-associate by convention when `_ViewImports` sets `@namespace` — each page `.cshtml` needs an explicit `@model <PageModel>` or its `OnGet*` handlers silently don't run (page renders as an empty shell).