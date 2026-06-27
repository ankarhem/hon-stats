using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HonStats.Infra.Persistence;

public sealed class IndexedPlayerConfiguration : IEntityTypeConfiguration<IndexedPlayer>
{
    public void Configure(EntityTypeBuilder<IndexedPlayer> b)
    {
        b.ToTable("indexed_players");
        b.HasKey(x => x.AccountId);
        b.Property(x => x.Status).HasConversion<string>();
        b.Property(x => x.LastIndexedMatchId).HasColumnType("INTEGER");
    }
}

public sealed class PlayerMatchConfiguration : IEntityTypeConfiguration<PlayerMatch>
{
    public void Configure(EntityTypeBuilder<PlayerMatch> b)
    {
        b.ToTable("player_matches");
        b.HasKey(x => new { x.AccountId, x.GameId });
        b.HasIndex(x => x.AccountId);
    }
}

public sealed class MatchRosterConfiguration : IEntityTypeConfiguration<MatchRoster>
{
    public void Configure(EntityTypeBuilder<MatchRoster> b)
    {
        b.ToTable("match_roster");
        b.HasKey(x => new { x.GameId, x.AccountId });
        b.HasIndex(x => x.AccountId);
    }
}

public sealed class MatchPlayerItemConfiguration : IEntityTypeConfiguration<MatchPlayerItem>
{
    public void Configure(EntityTypeBuilder<MatchPlayerItem> b)
    {
        b.ToTable("match_player_items");
        b.HasKey(x => new
        {
            x.GameId,
            x.AccountId,
            x.Slot,
        });
        b.HasIndex(x => new { x.AccountId, x.HeroId });
    }
}

public sealed class HeroBuildConfiguration : IEntityTypeConfiguration<HeroBuild>
{
    public void Configure(EntityTypeBuilder<HeroBuild> b)
    {
        b.ToTable("hero_builds");
        b.HasKey(x => new
        {
            x.AccountId,
            x.HeroId,
            x.ItemId,
        });
        b.HasIndex(x => new { x.AccountId, x.HeroId });
    }
}

public sealed class TeammateConfiguration : IEntityTypeConfiguration<Teammate>
{
    public void Configure(EntityTypeBuilder<Teammate> b)
    {
        b.ToTable("teammates");
        b.HasKey(x => new { x.AccountId, x.TeammateAccountId });
        b.HasIndex(x => x.AccountId);
    }
}

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("players");
        b.HasKey(x => x.AccountId);
        // Non-unique index for fast username→accountId lookup (profile local-first
        // resolution). App-layer steal-on-conflict keeps Username effectively unique;
        // a DB unique constraint would complicate rename-collision handling.
        b.HasIndex(x => x.Username);
    }
}

public sealed class MatchItemTimingConfiguration : IEntityTypeConfiguration<MatchItemTiming>
{
    public void Configure(EntityTypeBuilder<MatchItemTiming> b)
    {
        b.ToTable("match_item_timing");
        b.HasKey(x => new
        {
            x.GameId,
            x.AccountId,
            x.ItemId,
        });
        b.HasIndex(x => new { x.AccountId, x.ItemId });
    }
}
