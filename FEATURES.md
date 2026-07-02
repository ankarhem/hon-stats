# Features

Status: `[exists]` shipped · `[planned]` validated & data-ready · `[blocked]` no data path

Competitor parity benchmarked against honbuff.com (ward-up.com redirects to it —
same site). Data availability verified live against the juvio API (Jul 2026).

---

## Match experience

### `[exists]` Match detail modal
Native `<dialog>` opened from any match row. Shows two team sections (winner
flagged), per-player hero / K-D-A / wards / net worth / final items (with hover
tooltips). Close via Esc, backdrop click, or Close button.

### `[planned]` Enriched match modal (per-minute rates + role + link)
The quick-peek view gets more signal without becoming a graph surface.

**How it should work (user view):**
- **Per-minute rate columns** alongside the raw totals: **GPM** (net worth ÷
  minutes), **XPM** (experience ÷ minutes), **DPM** (hero damage ÷ minutes).
  Raw totals stay the source of truth (stored as-is); the per-minute figure is a
  display transform — `total × 60 ÷ durationSeconds` — computed in the view.
- A **role chip** on each player's hero cell from `roleIndex` (carry / mid /
  offlane / soft-support / hard-support / jungle) — the meaningful stand-in for
  lane, which isn't exposed by juvio.
- Rows **default-sorted by impact** (net worth, or GPM) within each team so the
  carry rises to the top; optional **team-totals** footer row (team K/D/A, team
  net worth, avg GPM).
- A prominent **"View full match →"** link in the modal bar → `/match/{gameId}`,
  the deep-dive page below. Deep-linkable and shareable.

**Implementation proposal:** uses the `getmatchsummary` data the modal already
fetches live — `netWorth, experience, heroDamage, RoleIndex` are all present. No
new endpoint, no persistence change; pure view work (Razor + the existing
`<dialog>`). Effort: S.

### `[planned]` Full match detail page (graphs + timelines)
The deep-dive a graph surface needs room for — the flagship parity feature.
Reached from the modal link, a match row, or deep-linkable at `/match/{gameId}`.

**How it should work (user view):**
- A **net-worth-over-time line chart**: one line per player, team-colored
  (Legion vs Hellbourne), x-axis = game time (0 → duration), y-axis = gold. A
  clickable legend toggles players on/off. The signature visual.
- Optional secondary series / toggle: **hero damage over time** and **building
  damage over time** — same per-player line treatment.
- A **build timeline** per player: chronological item purchases with their
  timestamp, grouped into Early / Core / Late phases (reuse `TierTimingOptions`).
  Item click → item detail; hover → the existing item tooltip.
- A **skill build order** per player: the ability level-up sequence
  (e.g. `Q → W → E → Q → Q → R …`), ability icons in order with the level/minute
  they were taken.
- On-demand per-player progression: CS, K-D-A, killstreaks, ward timings.

**Implementation proposal:**
- `getparsedreplay` returns ~30s-resolution snapshots, each player carrying
  `netWorth, level, experience, creepKills, neutralKills, creepDenies, kills,
  deaths, assists, killstreak, heroDamage, buildingDamage, goldFrom*, passivegpm,
  wardOfSight/RevelationPlaced, ravenPlaced, timeDead, items[], skills[]`.
  Verified populated from t≈30s onward (pre-game t=-90..0 is a null gap except
  the anchor — chart starts at t=0).
- **Already wired** via `IParsedReplayQuery` / `JuvioParsedReplayQuery`; only
  `ItemTimingAggregator` consumes it today (items only). Net worth / level / CS /
  damage / skills are fetched and then discarded.
- Gold/damage charts: pure inline SVG, same technique as our MMR chart
  (`_MmrChart.cshtml`) — server-rendered, no JS. Per-player polylines + legend.
- Skill order: currently **dropped** — `ParsedReplayPlayerDto.Skills` is typed
  `List<object>?` and never mapped. Fix: type it, map into the domain, walk
  snapshots to derive first-seen level per skill, join `skillId` → gamedata
  abilities (verify the join key against `/entities/abilities`).
- Old matches return 404 from `getparsedreplay` → empty state ("Replay data
  unavailable for this match") + fall back to the summary view.
- **No persistence needed** for v1 — serve live like `MatchDetail` does today.
  Effort: M. Data risk: low (verified); one unknown = the `skillId`→ability join.

---

## Community / global

### `[planned]` Leaderboard
A community ranking page — the most-requested stats-site feature and currently
our most conspicuous gap.

**How it should work:** top-100 Caldavar MMR table (#, player, MMR, rank tier +
stars, country flag). Top-3 as a podium; the rest a table. Player rows link to
profiles. A map toggle (Forests of Caldavar / Mid Wars) switches boards. No
pagination — the source is hard-capped at 100.

**Implementation proposal:** `GET stats /v1/stats/getleaderboard?map=…` returns
the full top 100 per entry already carrying `rank, accountId, displayName, mmr,
country, rankName, rankIcon, starLevel` (no enrichment needed). Add a `/leaderboard`
page + nav link; cache ~5 min server-side. NOTE the known limitation: no ordinal
global rank exists for players outside the top 100, and omitting `seasonId` yields
a different/larger board than `seasonId=1` — pin the season. Effort: S–M.

### `[planned]` Total-matches counter (home)
A live "X total matches (Y Caldavar / Z Mid Wars)" stat on the home page, the way
honbuff shows match volume. Cheap signal of scale.

**Implementation proposal:** `GET stats /v1/stats/getnumberofmatches` →
`totalMatches, focMatches, midwarsMatches, byRegion[]`. Cache 10 min. Drop into
the home hero. Effort: S.

### `[planned]` Records
A "top single-match performances" board — high "wow" factor, pure aggregation.

**How it should work:** leaderboards of the best individual performances, e.g.
Most Kills, Highest KDA, Fastest Win, Longest Match, Highest Net Worth, Most
Assists, Most CS, Most Hero Damage, Most Building Damage. Each row: player, hero,
match (link), date, the metric value. Filterable by map (All / FoC / MW) and
maybe a time window (last 30 days / all time).

**Implementation proposal:** all metrics are already in match summaries we
ingest — **except** `creepKills/neutralKills/creepDenies` and `buildingDamage`,
which flow through the indexer but are dropped at persist (only `HeroDamage` is
kept on `MatchRoster`). Add those columns to `MatchRoster` + one assignment line
each (mirror the existing `GoldFrom*`/`HeroDamage` pattern), migrate, backfill.
Then a ranking query over `match_roster`/`match_player_items`. Effort: M (mostly
the migration + query; the page itself is simple).

### `[planned]` Global hero win / pick rates + meta tier list
Hero performance aggregated across the community — honbuff's most-trafficked
content. Our angle: aggregate across **all indexed players** (broader than
honbuff's top-100-only model).

**How it should work:**
- `/heroes` gains a "Stats" mode (toggle vs the current reference view): a ranked
  list of heroes by win rate, with pick rate, ban rate (if available), avg K/D/A,
  games sampled. Role filter + sort (WR / picks / KDA / name) carry over.
- A **meta tier list** view: heroes ranked by a published "Meta Score" (e.g.
  weighted blend of win rate, pick rate, KDA), grouped S/A/B/C/D tiers, per-role
  top-3. Show sample size + time window ("based on N games, last 15 days").

**Implementation proposal:** self-aggregate from `match_roster` + `player_matches`
(heroId, won, K/D/A already persisted per match per player). A periodic
`HeroStatsSnapshot` job (like `ScheduledReindexService`) rolls up hero stats
daily/weekly into a table; pages read the latest snapshot. Tier classification =
a small classifier on the snapshot (we already have `GuideTierClassifier` to
model after). Effort: M. (Alternative: `gettotalherostats?heroId=&from=&to=` —
per-hero + date-range — but self-aggregation is cleaner and broader.)

### `[planned]` Weekly meta archive
Historical meta trends — week-over-week risers and fallers.

**How it should work:** an archive of weekly hero-meta snapshots; each week page
shows biggest risers / fallers (WR% + delta vs prior week) and that week's tier
list. Lets players see "is X hero trending up."

**Implementation proposal:** falls out of the hero-stats snapshot above — keep
weekly rollups instead of overwriting, expose `/meta/week/{YYYY-Www}`. Effort: S
once hero-stats snapshots exist.

### `[planned]` Recent matches feed
A "latest matches" activity stream (no player context) — shows the community is
alive.

**Implementation proposal:** `GET stats /v1/stats/getrecentmatches?limit=` (no
playerId). Could be a home widget or a `/matches` page. Effort: S.

---

## Profile enrichment

### `[planned]` Behavior title + penalty points
The "Chad (0 PP)" / "Toxic (12 PP)" badge honbuff shows on profiles.

**How it should work:** a small title + PP count near the player name on the
profile header, reflecting their standing.

**Implementation proposal:** `player.juvio.com` exposes
`/v1/player/bulkgetplayerpenaltypoints` (PP count) and `/v1/player/honorconfiguration`
(title thresholds). Fetch on profile load, cache. Effort: S. (Penalty-points
leaderboard also exists if we ever want a "most-reported" page — probably skip.)

### `[planned]` Per-hero usage on profile
"My most-played heroes" — absent on honbuff profiles, a natural fit for ours
since we already index full histories.

**How it should work:** a section/tab on the profile showing the player's most
played heroes with games, win rate, avg K/D/A per hero. Click a hero → filtered
match list or that hero's build page.

**Implementation proposal:** aggregate from `player_matches` (heroId) joined with
win/KDA. We already build per-hero `HeroGames` for the Hero Builds tab — surface
the same aggregation as a usage view. Effort: S–M.

### `[exists]` MMR history chart
Daily-snapshot dual-series (Caldavar + Mid Wars) SVG chart with hover pills.
**Our differentiator — honbuff has no MMR graph at all.** Keep, and consider
extending to show rank-tier bands behind the line.

### `[exists]` Teammates, per-player Hero Builds, map-aware filtering, infinite scroll
All strengths to preserve — covered in the existing-features list below.

---

## Platform

### `[planned]` Stream overlay (OBS)
honbuff's flagship differentiator. A transparent browser-source overlay a streamer
drops into OBS showing their live WR / W-L / games / KDA / rank.

**How it should work:** a `/streamers` page with a generator (enter nickname →
resolve → pick a layout → copy overlay URL) and a standalone `/overlay/{username}`
route rendering a transparent widget that auto-refreshes. 2–4 layouts (full /
compact / mini). Optional session vs. overall toggle.

**Implementation proposal:** built entirely from our existing profile data
(`getprofilestats` / `getplayersummary`) — no new data source. Transparent page
(no `_Layout`), HTMXpoll (`hx-trigger="every 30s"`), OKLCH-styled. Effort: M.

### `[planned]` Internationalization (RU / TH)
honbuff ships EN/RU/TH. Low priority for us; flag for later.

**Implementation proposal:** ASP.NET Core built-in i18n (resource files +
`CultureInfo` routing prefix). Large, mechanical effort. Defer.

---

## Existing features (reference)

- **Home** `/` — static marketing tiles. (See planned total-matches counter +
  recent-matches widget to make it live.)
- **Search** — global typeahead (HTMX, 300ms debounce) + exact-match redirect to
  profile; `/search` results page for fuzzy matches. Merges juvio exact + local
  fuzzy index.
- **Player profile** `/players/{username}`:
  - Header: flag, display name, @username, games, last played, rank badge, reindex
    button (with cooldown + progress polling), "last indexed" stamp.
  - MMR panel: Caldavar + Mid Wars MMR, history chart (see above).
  - Map selector: All / Forests of Caldavar / Mid Wars (threaded through all
    tabs + pagination).
  - Profile stats card (per selected map): K/D/A, win rate, KDR, avg wards, GPM,
    XPM, DPM.
  - **Matches tab**: table (hero, result, K/D/A, duration, map, date), infinite
    scroll, row click → match modal.
  - **Teammates tab**: most-played-with roster (games, wins, win%), infinite
    scroll. Requires indexing.
  - **Hero Builds tab**: per-hero item tiers (Early/Core/Late by buy timing) +
    common loadouts, with build-stat tooltips. Requires indexing.
- **Heroes** `/heroes` (reference grid: name + 7-role filter, hover combat-stat
  tooltip) and `/heroes/{slug}` (combat stats, attribute boxes, role sliders,
  ability cards with GameText descriptions, lore).
- **Items** `/items` (reference grid: name + category tabs, hover tooltip) and
  `/items/{slug}` (recipe chain with recursive TotalCost, description, activation,
  passive bonuses, on-attack impact).
- **Background**: reference-data refresh (12h), full-history indexing with replay
  item-timing, scheduled reindex every 6h (feeds MMR snapshots), fuzzy name index.

---

## Out of scope — data-blocked

Verified absent from every juvio endpoint; do not pursue without a new data source:

- **Lane assignment / lane indicators** — no x/y position data in `getparsedreplay`
  snapshots (they carry stats + items + skills only) or anywhere else. honbuff
  must parse raw replays for coordinates; we have no raw-replay access (see below).
  Use per-match `roleIndex` (the assigned role) instead — more meaningful and free.
- **Raw replay download** — does not exist. `uploadreplay`/`uploadparsedreplay` are
  client→server only; juvio parses server-side and serves only via
  `getparsedreplay`. No route to the raw `.honreplay`.
- **Players online / in queue** — real-time game-server state, not in the stats
  API. honbuff's "players online" counter has no equivalent endpoint we can find.
- **Per-match MMR delta** — known dead end (documented in AGENTS.md); only daily
  MMR snapshots exist.
- **Clan / guild / achievements / accounts / dark-mode toggle** — not in scope;
  none data-blocked, just not prioritized.

---

## Suggested build order

Ranked by value × (1/effort), data-readiness already confirmed:

1. **Enriched match modal** (per-minute GPM/XPM/DPM + role chip + link) — quick
   visible win (S), uses data the modal already fetches. Ships first.
2. **Full match detail page** (gold graph + build timeline + skill order) — the
   flagship parity feature; data fully verified & already half-wired (M).
3. **Leaderboard** — highest community value, endpoint ready.
4. **Records** + **total-matches counter** — high "wow", low effort (small
   migration to persist CS/building-damage, then aggregation).
5. **Global hero stats + meta tier list** — medium effort, our broader population
   is a genuine edge over honbuff's top-100-only model.
6. **Per-hero usage on profile** + **behavior title/PP** — small profile wins.
7. **Stream overlay** — distinctive but niche; build once the core parity is closed.
