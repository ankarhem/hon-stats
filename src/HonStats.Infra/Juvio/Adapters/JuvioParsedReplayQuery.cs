using System.Net;
using System.Text.Json;
using HonStats.App.Matches;
using HonStats.Domain.Matches;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioParsedReplayQuery(IHttpClientFactory httpClientFactory)
    : IParsedReplayQuery
{
    public async Task<ParsedReplay?> GetAsync(int gameId, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getparsedreplay?gameId={gameId}",
            ct
        );

        // Old matches lack a parsed replay — treat 404 as absence, not an error.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<ParsedReplayResponseDto>(
            stream,
            JuvioJson.Options,
            ct
        );
        return dto?.ParsedReplay is { } replay ? Map(replay) : null;
    }

    private static ParsedReplay Map(ParsedReplayDto dto) =>
        new()
        {
            GameId = dto.GameId,
            Date = dto.Date,
            WinningTeam = dto.WinningTeam,
            Snapshots = dto.SnapshotsValue.Select(MapSnapshot).ToList(),
        };

    private static ReplaySnapshot MapSnapshot(ParsedReplaySnapshotDto s) =>
        new() { Time = s.Time, Teams = s.TeamsValue.Select(MapTeam).ToList() };

    private static ReplayTeam MapTeam(ParsedReplayTeamDto t) =>
        new() { Players = t.PlayersValue.Select(MapPlayer).ToList() };

    private static ReplayPlayer MapPlayer(ParsedReplayPlayerDto p) =>
        new()
        {
            Items = p
                .ItemsValue.Select(i => new ReplayItem { ItemId = i.ItemId, Slot = i.Slot })
                .ToList(),
            NetWorth = p.NetWorth,
            Level = p.Level,
            Experience = p.Experience,
            CreepDenies = p.CreepDenies,
            RavenPlaced = p.RavenPlaced,
            SlotIndex = p.SlotIndex,
            StartingGold = p.StartingGold,
        };
}
