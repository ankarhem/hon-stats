using HonStats.App.Insights;
using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.App.ReferenceData;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class MatchDetailModel(
    IMatchQuery matches,
    IParsedReplayQuery parsedReplays,
    IReferenceDataQuery reference,
    IPlayerNameResolver nameResolver
) : PageModel
{
    public MatchPageView? View { get; set; }

    public async Task<IActionResult> OnGet(int gameId, CancellationToken ct)
    {
        var detail = await matches.GetSummaryAsync(gameId, ct);
        if (detail is null)
            return NotFound();

        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        var items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
        var abilities = (await reference.GetAbilitiesAsync(ct)).ToDictionary(a => a.Id);
        var names = await nameResolver.ResolveAsync(
            detail.Players.Select(p => p.AccountId).ToList(),
            ct
        );

        var replay = await parsedReplays.GetAsync(gameId, ct);
        var hasReplay = replay is not null && !ItemTimingAggregator.IsDegenerate(replay);

        var itemBuys =
            hasReplay && replay is not null
                ? ItemTimingAggregator.Build(replay)
                : Array.Empty<ItemBuyTime>();
        var skillEvents =
            hasReplay && replay is not null
                ? SkillBuildAggregator.Build(replay)
                : Array.Empty<SkillLevelEvent>();
        var timeline =
            hasReplay && replay is not null
                ? BuildTimeline(replay)
                : new Dictionary<Guid, List<TimelinePoint>>();

        var slotByAccount = BuildSlotIndexMap(replay);

        var players = detail
            .Players.GroupBy(p => p.Team)
            .SelectMany(team =>
            {
                var ordered = team.Select((p, idx) => new { Player = p, Index = idx })
                    .OrderBy(x => slotByAccount.GetValueOrDefault(x.Player.AccountId) ?? x.Index)
                    .ToList();
                return ordered.Select(
                    (x, effectiveSlot) =>
                    {
                        var acct = x.Player.AccountId;
                        var name = names.GetValueOrDefault(acct);
                        return new PlayerSeries(
                            acct,
                            x.Player.HeroId,
                            x.Player.RoleIndex,
                            x.Player.Team,
                            name?.DisplayName ?? name?.Username ?? acct.ToString(),
                            effectiveSlot,
                            timeline.GetValueOrDefault(acct) ?? new List<TimelinePoint>(),
                            itemBuys
                                .Where(b => b.AccountId == acct)
                                .OrderBy(b => b.FirstSeenSeconds)
                                .ToList(),
                            skillEvents.Where(s => s.AccountId == acct).ToList()
                        );
                    }
                );
            })
            .OrderBy(p => p.Team == "Legion" ? 0 : 1)
            .ThenBy(p => p.SlotIndex)
            .ToList();

        View = new MatchPageView(
            new MatchDetailView(
                detail,
                heroes,
                items,
                names,
                HeroDamageAggregator.Build(replay),
                slotByAccount.ToDictionary(kv => kv.Key, kv => kv.Value ?? 0)
            ),
            hasReplay,
            abilities,
            players
        );
        return Page();
    }

    private static Dictionary<Guid, List<TimelinePoint>> BuildTimeline(ParsedReplay replay)
    {
        if (replay.Snapshots.Count == 0)
            return new();

        var anchor = replay.Snapshots[0];
        var positionToAccount = new Dictionary<(int Team, int Player), Guid>();
        for (var ti = 0; ti < anchor.Teams.Count; ti++)
        {
            for (var pi = 0; pi < anchor.Teams[ti].Players.Count; pi++)
            {
                if (anchor.Teams[ti].Players[pi].AccountId is { } id && id != Guid.Empty)
                    positionToAccount[(ti, pi)] = id;
            }
        }

        if (positionToAccount.Count == 0)
            return new();

        var byAccount = new Dictionary<Guid, List<TimelinePoint>>();
        var ordered = replay
            .Snapshots.Select((snapshot, index) => new { snapshot, index })
            .OrderBy(x => x.snapshot.Time)
            .ThenBy(x => x.index)
            .Select(x => x.snapshot);

        foreach (var snapshot in ordered)
        {
            if (snapshot.Time < 0)
                continue;
            for (var ti = 0; ti < snapshot.Teams.Count; ti++)
            {
                var players = snapshot.Teams[ti].Players;
                for (var pi = 0; pi < players.Count; pi++)
                {
                    if (!positionToAccount.TryGetValue((ti, pi), out var account))
                        continue;
                    var rp = players[pi];
                    if (rp.NetWorth is null && rp.HeroDamage is null && rp.BuildingDamage is null)
                        continue;
                    if (!byAccount.TryGetValue(account, out var list))
                        byAccount[account] = list = new List<TimelinePoint>();
                    list.Add(
                        new TimelinePoint(
                            snapshot.Time,
                            rp.NetWorth,
                            rp.HeroDamage,
                            rp.BuildingDamage
                        )
                    );
                }
            }
        }

        return byAccount;
    }

    private static Dictionary<Guid, int?> BuildSlotIndexMap(ParsedReplay? replay)
    {
        if (replay is null || replay.Snapshots.Count == 0)
            return new Dictionary<Guid, int?>();

        var anchor = replay.Snapshots[0];
        var map = new Dictionary<Guid, int?>();
        foreach (var team in anchor.Teams)
        {
            foreach (var player in team.Players)
            {
                if (player.AccountId is { } id && id != Guid.Empty)
                    map[id] = player.SlotIndex;
            }
        }

        return map;
    }
}

public sealed record MatchPageView(
    MatchDetailView Summary,
    bool HasReplay,
    IReadOnlyDictionary<int, Ability> Abilities,
    IReadOnlyList<PlayerSeries> Players
);

public sealed record PlayerSeries(
    Guid AccountId,
    int HeroId,
    int RoleIndex,
    string Team,
    string DisplayName,
    int? SlotIndex,
    IReadOnlyList<TimelinePoint> Timeline,
    IReadOnlyList<ItemBuyTime> Items,
    IReadOnlyList<SkillLevelEvent> Skills
);

public sealed record TimelinePoint(
    int TimeSeconds,
    int? NetWorth,
    int? HeroDamage,
    int? BuildingDamage
);

public sealed record BuildTimelineView(PlayerSeries Player, IReadOnlyDictionary<int, Item> Items);

public sealed record SkillBuildView(
    PlayerSeries Player,
    IReadOnlyDictionary<int, Ability> Abilities
);
