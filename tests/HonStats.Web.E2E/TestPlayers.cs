namespace HonStats.Web.E2E;

// Real juvio accounts (exact usernames, so search redirects straight to the
// profile). ManyGames has a deep match history — good for pagination/scroll
// scenarios, far too slow to reindex. FewGames indexes in seconds.
internal static class TestPlayers
{
    public const string ManyGames = "idealpink";
    public const string FewGames = "Testie";
}
