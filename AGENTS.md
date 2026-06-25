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

**CSS (single global `wwwroot/css/site.css`, no Razor CSS isolation).** Isolation
was evaluated and rejected: this app composes UI across a shared `_Layout` + HTMX
partials, so file-scoped `b-<hash>` cuts across component boundaries — `:root`
tokens stop matching, shared classes (`.btn`/`.card`) only style one page,
HTMX-injected partials don't inherit the page scope, and tag-helper elements
(`<a asp-page>`, `<form>`) don't get the scope attribute. Keep ONE global file.
**Color tokens are OKLCH** (`--accent: oklch(71.53% 0.1518 253.31)` etc., computed
via `culori`, not by hand). Derive translucent variants with relative-color syntax:
`oklch(from var(--accent) l c h / 0.18)` — NOT `var(--accent / 0.5)` (slash is
illegal inside `var()`). Target is Chrome/Firefox (Safari `from` support skipped
for now), so no `@supports` guards / `rgba()` fallbacks are kept.

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

**E2E** (`tests/HonStats.Web.E2E`, Playwright + xUnit): `E2EFixture` spawns the
real app + headless Chromium; run via `just e2e` (separate from `just validate` —
needs browsers + live juvio). **Locators: prefer user-facing over CSS classes** —
`GetByRole(AriaRole.Dialog)` for the modal, `GetByRole(AriaRole.Link/Button, Name)`,
`GetByPlaceholder` for the search box (NOTE: `<input type="search">` is role
`searchbox`, not `textbox`), `GetByTestId(...)` (`data-testid` on `.match-row` /
teammate rows) where role/text aren't unique. NEVER `WaitForTimeoutAsync` — use
auto-retrying web-first assertions (`Expect(locator).ToBeVisibleAsync`).

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
- `POST auth /v1/userinfo/getuserinfo` rejects a large `accountIds` array with **400** (somewhere in (50, 200]; ≤50 works, exact max unknown). The name resolver chunks at 50 (`JuvioPlayerNameResolver`).
- Razor Pages PageModels don't auto-associate by convention when `_ViewImports` sets `@namespace` — each page `.cshtml` needs an explicit `@model <PageModel>` or its `OnGet*` handlers silently don't run (page renders as an empty shell).

## HTMX patterns (cross-checked against the JetBrains htmx+ASP.NET tutorial)

References:
- Official docs: https://htmx.org/docs/
- Examples gallery: https://htmx.org/examples/
- JetBrains htmx+ASP.NET tutorial: https://www.jetbrains.com/dotnet/guide/tutorials/htmx-aspnetcore/

Canonical pattern for every HTMX endpoint: `Request.IsHtmx() ? Partial("_Fragment") : Page()`.
The same URL serves the full page (non-htmx / form submit / shared link) AND the
partial fragment (htmx swap). Always set `@model` on every `.cshtml`.

- **Search**: input `hx-trigger="keyup changed delay:300ms"` (typeahead) + form
  `action="/search" method="get"` (Enter → full results page). Both go to the same
  endpoint; `IsHtmx()` distinguishes fragment vs page.
- **Infinite scroll**: sentinel `<tr hx-trigger="revealed" hx-swap="outerHTML">`
  replaces itself with new rows + next sentinel. Tutorial uses `afterend` on the
  last item; both are valid.
- **Match-detail modal**: native `<dialog class="match-modal" closedby="any">`
  (no `open` attr) swapped via HTMX into `#match-detail-host`. The host carries
  `hx-on::after-swap="this.querySelector('dialog:not([open])')?.showModal()"` — the
  ONE sanctioned JS exception, because Esc-to-close, `::backdrop` click-outside,
  focus trap and `aria-modal` only work on a modal opened via `showModal()`, and
  HTML has no "open as modal" attribute. `closedby="any"` enables click-outside
  dismissal (Chrome 134+/Firefox 141+; NO Safari yet — polyfill later if needed).
  Close paths: Esc, backdrop click, and the `<form method="dialog">` button — all
  fire the native `close` event. The closed `<dialog>` stays in the DOM (just
  `display:none`); reopening re-fires `showModal()` via the `:not([open])` guard.
  Covered by `tests/HonStats.Web.E2E/MatchModalCloseTests.cs`.
- **Tabs**: each handler returns a region partial (tab bar + content) swapped
  into `#profile-region`. Active state is server-rendered (HATEOAS). Mark the
  selected tab/pill with `aria-current="true"` (`null` when inactive), NOT an
  `--active` modifier class — `site.css` styles active state via `[aria-current]`
  selectors, so `--active` classes silently render nothing.
- **Improvement**: HTMX URLs are hand-coded strings (e.g.
  `hx-get="/Players/Profile/@id?handler=matches&offset=0"`). The Htmx.TagHelpers
  `hx-page` / `hx-page-handler` would be type-safe but aren't used yet.
- **Production caching**: endpoints that branch on `Request.IsHtmx()` should set
  `Vary: HX-Request` to avoid a caching proxy serving a fragment for a full-page
  request (or vice versa). Not set yet (app not deployed).