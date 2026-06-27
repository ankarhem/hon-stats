using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Persistence;

public sealed class HonStatsDbContext(DbContextOptions<HonStatsDbContext> options)
    : DbContext(options)
{
    public DbSet<IndexedPlayer> IndexedPlayers => this.Set<IndexedPlayer>();
    public DbSet<PlayerMatch> PlayerMatches => this.Set<PlayerMatch>();
    public DbSet<MatchRoster> MatchRoster => this.Set<MatchRoster>();
    public DbSet<MatchPlayerItem> MatchPlayerItems => this.Set<MatchPlayerItem>();
    public DbSet<HeroBuild> HeroBuilds => this.Set<HeroBuild>();
    public DbSet<Teammate> Teammates => this.Set<Teammate>();
    public DbSet<Player> Players => this.Set<Player>();
    public DbSet<MatchItemTiming> MatchItemTimings => this.Set<MatchItemTiming>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // All IEntityTypeConfiguration<T> implementations in this assembly are
        // applied automatically, keeping model + mapping concerns in one place.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HonStatsDbContext).Assembly);
    }
}
