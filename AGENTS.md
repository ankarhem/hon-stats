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
- `dotnet ef` is in `flake.nix` (devShell package) AND a local tool
  (`.config/dotnet-tools.json`, v10.0.9) — run `dotnet tool restore` or enter the
  nix shell. `HonStatsDbContextDesignFactory` lets `dotnet ef` run with
  `--project src/HonStats.Infra --startup-project src/HonStats.Infra`.
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
- Heroes: `GET gamedata /entities/heroes` (PUBLIC, 102 heroes; `translatedName`=display). Hero `inventory0`–`inventory4` are ability NAME strings (e.g. `Ability_Blacksmith1`, each a 1-element array) that JOIN to `/entities/abilities`. Combat stats live here: `attackDamageMin/Max` (BASE — display damage = base + primary-attribute value; `primaryAttribute` 0=str/1=agi/2=int), `attackRange`, `attackCooldown` (ms → atk speed = `1000/cooldown`), `moveSpeed`, base `strength/agility/intelligence`. Role ratings `carry/mid/hardSupport/softSupport/offLane/jungleRating` (0-5, plain ints not arrays) drive the role filter. **Stat growth (`strengthPerLevel` etc.) is NOT usable: the field exists in the schema but only ~17/102 heroes have it, as inconsistently-rounded INTEGERS (e.g. Chronos real agi-gain 2.8 → API `3`); 94 heroes have nothing; the detail endpoint `/entities/heroes/{id}` returns null for everything. Real decimal growth lives only in local `.entity` files (`heroes/<name>/hero.entity` inside `resources0.s2z`, attr `strengthperlevel` etc.) or hon.fandom.com — ship a static table if ever needed. The official client reads growth locally, NOT from the API.** `Ability_AttributeBoost` (the generic stat-boost) is filtered out of hero abilities in `MapHero`.
- Abilities: `GET gamedata /entities/abilities` (PUBLIC, 536 abilities; some have duplicate `name` — use `DistinctBy`). Per-level arrays: `manaCost`, `cooldownTime` (ms), `range`, `targetRadius`.
- Items: `GET gamedata /entities/items` (PUBLIC, 210 items). **`cost` is the RECIPE cost only** — total value = `cost + recursive sum of component costs`. `components` is a 1-element array holding a **space-separated string** of component `Item_X` names (e.g. `["Item_Slayer Item_AlacrityBand Item_Halberd"]`); resolve by name (2-pass: map all items, then link components). `Item.TotalCost` recurses; Savage Mace = 400 recipe + 2200 + 1200 + 1000 = 4800. Item **stat fields are per-level arrays** (e.g. Nullfire Blade `strength:[3,5,7]`) — render slash-joined. An item's own stat fields are ALREADY the final aggregate (do NOT sum components — that double-counts); magic-armor/resistance bonuses come from a HoN "Modifier" not exposed as numeric fields, so they can't render. Percent stats (`castSpeed`, `manaRegenMultiplier`) are fractions (1 = 100%). Active items have `manaCost`/`cooldownTime`(ms)/`range`. **Tiered item NAMES carry markup**: `translatedName` like `"Phoenix's Talon\n^vTier IV^*"` — split on literal `\n` (`HonText.NameOnly`/`TierOnly`) and slug via `HonText.Slugify` (strips `^X`/`^*`, non-alphanumeric→hyphen) or the slug 404s.
- Strings (tooltips/descriptions): `GET gamedata /strings` (PUBLIC, ~9k keys). Flat `Dictionary<string,string>` keyed by `{EntityName}_{suffix}`: `Ability_X_description_simple`/`_description2`/`_description`, `Item_X_description`(+`_description2`), `Hero_X_role` (gameplay) + `Hero_X_description` (lore). **GamedataClient joins all endpoints** — heroes enriched with combat stats + `List<Ability>`, items with `Description` + components.
- **Description markup** uses HoN codes: `^X ... ^*` color spans (X=letter; `^*` resets), `{a,b,c,d}` per-level values, literal `\n` (backslash-n, not a newline) line breaks. `HonStats.Web.Rendering.GameText.Render(raw)` parses this to XSS-safe `IHtmlContent` (text HTML-encoded, only generated `<span class="gt gt-X">`/`<br>` are raw). CSS colors via `.gt-o/-y/-r/-g/-b/-t/-p`.

**Image CDN** (public, predictable): `https://gamestorage.juvio.com/heroes/{id}/icon.webp` and `.../items/{id}/icon.webp`.

**Known dead ends:**
- `auth /v1/userinfo/getusernamebyid` has no documented params → always empty. Use POST getuserinfo instead.
- Stats endpoints use different id param names: profilestats=`userId`, playersummary/rank=`accountId`, recentmatches/recentplayers=`playerId`. Same UUID value.
- gamedata entities are public (no bearer needed). **MANY fields are single-element arrays** (not just `icon`): `attackType`, `inventory0-4`, `moveSpeed`, `maxHealth`, `maxMana`, `magicArmor`, `attackRange`, `attackDamageMin/Max`, `sightRangeDay/Night`, etc. Always check the actual JSON — assume arrays, use `[0]`. `translatedName` (not `name`) is the display name.
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

- **URL scheme**: Profile uses `@page "/players/{username}"` (absolute route
  override — bypasses folder-based routing). OnGet resolves username→accountId
  via `IPlayerSearch` (one `getidbyusername` call per page load). HTMX requests
  use **relative URLs** (`?tab=teammates`, `?handler=MatchesMore&accountId=...`)
  so `username` stays in the path automatically. Heroes/Items use
  `@page "/heroes"`, `@page "/heroes/{slug}"`, `@page "/items"`,
  `@page "/items/{slug}"` — slug is `TranslatedName.ToLowerInvariant().Replace(' ', '-')`.
- **Search**: dropdown positioned absolutely inside `.search` (which is
  `position: relative`). `#search-results` lives INSIDE the form; `:empty` hides
  it. Enter submits the form → non-HTMX branch redirects to
  `/players/{firstResult.Username}`. HTMX typeahead still returns `_SearchResults`
  partial.
- **Infinite scroll**: sentinel `<tr hx-trigger="revealed" hx-swap="outerHTML">`
  replaces itself with new rows + next sentinel. Tutorial uses `afterend` on the
  last item; both are valid.
- **Heroes/Items filter**: `/heroes` is a 3-column grid by primary attribute
  (Agility/Intelligence/Strength); `/items` is a flat grid. The filter `<form>`
  (search input + role checkbox toggles for heroes) carries
  `hx-get hx-target="#hero-grid"/"#item-grid" hx-trigger="input changed delay:80ms, change"`
  → server re-renders `_HeroGrid`/`_ItemGrid` with a `--dimmed` (grayscale) class
  on non-matching entries (name-contains AND, for heroes, active-role rating > 0).
  Debounce is short (80ms) since reference data is cached in-memory. The form lives
  OUTSIDE the grid target, so checkbox state survives swaps; the `:checked` toggle
  visual is pure CSS (no server round-trip). Hover tooltips (hero combat
  stats/abilities; item value+tier+description+activation+passive bonuses) are pure
  CSS `:hover` panels — no JS. Ability/item descriptions render via
  `GameText.Render` (HoN `^X...^*`/`{a,b,c,d}`/`\n` markup → XSS-safe HTML). Display
  text from `translatedName` uses `DisplayName`/`HonText.Strip` (markup removed);
  lowercase source values (attack type "melee"/"ranged", shop categories) get
  `text-transform: capitalize` via `.capitalize`.
- **Match-detail modal**: native `<dialog class="modal" closedby="any">`
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
- **HTMX URLs**: relative (`?tab=...`, `?handler=...&accountId=...`) for all
  profile-page HTMX requests. The Htmx.TagHelpers `hx-page` / `hx-page-handler`
  would be type-safe but aren't used yet.
- **Production caching**: endpoints that branch on `Request.IsHtmx()` should set
  `Vary: HX-Request` to avoid a caching proxy serving a fragment for a full-page
  request (or vice versa). Not set yet (app not deployed).