namespace HonStats.Infra.Juvio;

public sealed class JuvioOptions
{
    public const string SectionName = "HonStats:Juvio";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ClientName { get; set; } = "hon-stats";

    public string AuthBaseUrl { get; set; } = "https://auth.juvio.com";
    public string GameDataBaseUrl { get; set; } = "https://gamedata.juvio.com";
    public string StatsBaseUrl { get; set; } = "https://stats.juvio.com";
    public string EconomyBaseUrl { get; set; } = "https://economy.juvio.com";
}
