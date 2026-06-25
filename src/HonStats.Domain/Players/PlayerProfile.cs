namespace HonStats.Domain.Players;

public sealed class PlayerIdentity
{
    public Guid AccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Country { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class StatsRecord
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

public sealed class RoleStat
{
    public string Role { get; set; } = string.Empty;
    public double WinRate { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int GamesPlayed { get; set; }
}

public sealed class HeroStat
{
    public string HeroName { get; set; } = string.Empty;
    public string HeroImageUrl { get; set; } = string.Empty;
    public string? HeroImageTgaUrl { get; set; }
    public double WinRate { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
}

public sealed class ProfileOverview
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
    public StatsRecord AllTimeRecord { get; set; } = new();
    public List<RoleStat> TopRoles { get; set; } = [];
    public List<HeroStat> TopHeroes { get; set; } = [];
    public bool NewPlayer { get; set; }
}

public sealed class ProfileSummary
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

public sealed class PlayerRank
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

public sealed class PlayerProfile
{
    public PlayerIdentity Identity { get; set; } = new();
    public ProfileOverview Overview { get; set; } = new();
    public ProfileSummary Summary { get; set; } = new();
    public PlayerRank? Rank { get; set; }
}
