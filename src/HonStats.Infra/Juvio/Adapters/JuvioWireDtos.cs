using System.Text.Json;

namespace HonStats.Infra.Juvio.Adapters;

// Wire shapes mirroring juvio's camelCase JSON. Internal so the Domain models
// stay serialization-agnostic — only Infra knows the on-the-wire spelling.
internal static class JuvioJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

internal sealed class AccountsCollectionDto
{
    public List<AccountListInfoDto>? Accounts { get; set; }
    public List<AccountListInfoDto> AccountsValue => this.Accounts ?? [];
}

internal sealed class AccountListInfoDto
{
    public string AccountId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

internal sealed class UserInfoCollectionDto
{
    public List<UserInfoDto>? Users { get; set; }
    public List<UserInfoDto> UsersValue => this.Users ?? [];
}

internal sealed class UserInfoDto
{
    public string AccountId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Country { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

internal sealed class ProfileOverviewDto
{
    public double AverageKDA { get; set; }
    public double AverageDPM { get; set; }
    public double AverageGPM { get; set; }
    public double AverageXPM { get; set; }
    public double CurrentMMR { get; set; }
    public double PublicSkillRating { get; set; }
    public double RankedCaldavarRating { get; set; }
    public double RankedMidwarsRating { get; set; }
    public int MatchesPlayed { get; set; }
    public double WinRate { get; set; }
    public DateTimeOffset LastPlayed { get; set; }
    public StatsRecordDto AllTimeRecord { get; set; } = new();
    public List<RoleStatDto>? TopRoles { get; set; }
    public List<HeroStatDto>? TopHeroes { get; set; }
    public bool NewPlayer { get; set; }

    public List<RoleStatDto> TopRolesValue => this.TopRoles ?? [];
    public List<HeroStatDto> TopHeroesValue => this.TopHeroes ?? [];
}

internal sealed class StatsRecordDto
{
    public int Mvps { get; set; }
    public int Annihilations { get; set; }
    public int Smackdowns { get; set; }
    public int Killstreak { get; set; }
    public int WardsPlaced { get; set; }
    public int WardsDestroyed { get; set; }
    public long GoldEarned { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
}

internal sealed class RoleStatDto
{
    public string Role { get; set; } = string.Empty;
    public double WinRate { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int GamesPlayed { get; set; }
}

internal sealed class HeroStatDto
{
    public string HeroName { get; set; } = string.Empty;
    public string HeroImageUrl { get; set; } = string.Empty;
    public string? HeroImageTgaUrl { get; set; }
    public double WinRate { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
}

internal sealed class ProfileSummaryDto
{
    public int TotalKills { get; set; }
    public int TotalDeaths { get; set; }
    public int TotalAssists { get; set; }
    public int TotalCreepKills { get; set; }
    public int TotalNeutralKills { get; set; }
    public int TotalBuybacks { get; set; }
    public int TotalGames { get; set; }
    public int TotalHellbourne { get; set; }
    public int TotalLegion { get; set; }
    public int TotalWins { get; set; }
    public int TotalLoss { get; set; }
    public double WinRate { get; set; }
    public double KillDeathRatio { get; set; }
    public double KillDeathAssistRatio { get; set; }
    public double AssistDeathRatio { get; set; }
    public double SkillRating { get; set; }
    public double RankedCaldavarRating { get; set; }
    public double RankedMidwarsRating { get; set; }
}

internal sealed class PlayerRankDto
{
    public int CurrentMmr { get; set; }
    public string RankName { get; set; } = string.Empty;
    public int StarLevel { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int MinMmr { get; set; }
    public int MaxMmr { get; set; }
    public bool IsTopRank { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

internal sealed class RecentMatchesDto
{
    public List<RecentMatchDto>? Matches { get; set; }
    public List<RecentMatchDto> MatchesValue => this.Matches ?? [];
}

internal sealed class RecentMatchDto
{
    public int GameId { get; set; }
    public int HeroId { get; set; }
    public string WinningTeam { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public DateTimeOffset Date { get; set; }
    public int Duration { get; set; }
    public string Map { get; set; } = string.Empty;
    public bool IsArranged { get; set; }
}

internal sealed class MatchSummaryDto
{
    public int GameId { get; set; }
    public DateTimeOffset Date { get; set; }
    public int Duration { get; set; }
    public string WinningTeam { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public bool IsArranged { get; set; }
    public List<MatchPlayerSummaryDto>? Players { get; set; }
    public List<MatchPlayerSummaryDto> PlayersValue => this.Players ?? [];
}

internal sealed class MatchPlayerSummaryDto
{
    public string AccountId { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public int HeroId { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int CreepKills { get; set; }
    public int NeutralKills { get; set; }
    public int CreepDenies { get; set; }
    public int NetWorth { get; set; }
    public int GoldFromCreeps { get; set; }
    public int GoldFromNeutrals { get; set; }
    public int GoldFromKills { get; set; }
    public int GoldFromAssists { get; set; }
    public int GoldFromBuildings { get; set; }
    public int StartingGold { get; set; }
    public int DeathGoldLost { get; set; }
    public int WardOfSightPlaced { get; set; }
    public int WardOfRevelationPlaced { get; set; }
    public int RavenPlaced { get; set; }
    public double Experience { get; set; }
    public double Level { get; set; }
    public int HeroDamage { get; set; }
    public int BuildingDamage { get; set; }
    public int Buybacks { get; set; }
    public int Inventory48Id { get; set; }
    public int Inventory49Id { get; set; }
    public int Inventory50Id { get; set; }
    public int Inventory51Id { get; set; }
    public int Inventory52Id { get; set; }
    public int Inventory53Id { get; set; }
    public int Inventory63Id { get; set; }
    public int Inventory64Id { get; set; }
    public int RoleIndex { get; set; }
}

// getparsedreplay wire shape. Only snapshot[0]'s players carry AccountId + HeroId
// (the self-contained position->player anchor) plus SlotIndex + StartingGold; later
// snapshots are positional-only. Fields are nullable since juvio omits most of them
// on non-anchor snapshots.
internal sealed class ParsedReplayResponseDto
{
    public ParsedReplayDto? ParsedReplay { get; set; }
}

internal sealed class ParsedReplayDto
{
    public int GameId { get; set; }
    public string? Date { get; set; }
    public string? WinningTeam { get; set; }
    public List<ParsedReplaySnapshotDto>? Snapshots { get; set; }
    public List<ParsedReplaySnapshotDto> SnapshotsValue => this.Snapshots ?? [];
}

internal sealed class ParsedReplaySnapshotDto
{
    public int Time { get; set; }
    public List<ParsedReplayTeamDto>? Teams { get; set; }
    public List<ParsedReplayTeamDto> TeamsValue => this.Teams ?? [];
}

internal sealed class ParsedReplayTeamDto
{
    public List<ParsedReplayPlayerDto>? Players { get; set; }
    public List<ParsedReplayPlayerDto> PlayersValue => this.Players ?? [];
}

internal sealed class ParsedReplayPlayerDto
{
    public string? AccountId { get; set; }
    public int? HeroId { get; set; }
    public List<ParsedReplayItemDto>? Items { get; set; }
    public int? NetWorth { get; set; }
    public double? Level { get; set; }
    public double? Experience { get; set; }
    public int? CreepDenies { get; set; }
    public int? RavenPlaced { get; set; }
    public int? SlotIndex { get; set; }
    public int? StartingGold { get; set; }
    public List<object>? Skills { get; set; }
    public List<ParsedReplayItemDto> ItemsValue => this.Items ?? [];
}

internal sealed class ParsedReplayItemDto
{
    public int ItemId { get; set; }
    public int Slot { get; set; }
}
