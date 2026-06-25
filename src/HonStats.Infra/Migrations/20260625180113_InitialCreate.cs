using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hero_builds",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_hero_builds",
                        x => new
                        {
                            x.AccountId,
                            x.HeroId,
                            x.ItemId,
                        }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "indexed_players",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    LastIndexedMatchId = table.Column<long>(type: "INTEGER", nullable: true),
                    LastReindexAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IndexedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indexed_players", x => x.AccountId);
                }
            );

            migrationBuilder.CreateTable(
                name: "match_player_items",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoleIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    WardsPlaced = table.Column<int>(type: "INTEGER", nullable: false),
                    WardsRevelation = table.Column<int>(type: "INTEGER", nullable: false),
                    NetWorth = table.Column<int>(type: "INTEGER", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_match_player_items",
                        x => new
                        {
                            x.GameId,
                            x.AccountId,
                            x.Slot,
                        }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "match_roster",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Team = table.Column<string>(type: "TEXT", nullable: false),
                    Won = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_roster", x => new { x.GameId, x.AccountId });
                }
            );

            migrationBuilder.CreateTable(
                name: "player_matches",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Team = table.Column<string>(type: "TEXT", nullable: false),
                    WinningTeam = table.Column<string>(type: "TEXT", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Map = table.Column<string>(type: "TEXT", nullable: false),
                    IsArranged = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_matches", x => new { x.AccountId, x.GameId });
                }
            );

            migrationBuilder.CreateTable(
                name: "teammates",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeammateAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GamesTogether = table.Column<int>(type: "INTEGER", nullable: false),
                    WinsTogether = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teammates", x => new { x.AccountId, x.TeammateAccountId });
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_hero_builds_AccountId_HeroId",
                table: "hero_builds",
                columns: new[] { "AccountId", "HeroId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_match_player_items_AccountId_HeroId",
                table: "match_player_items",
                columns: new[] { "AccountId", "HeroId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_match_roster_AccountId",
                table: "match_roster",
                column: "AccountId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_matches_AccountId",
                table: "player_matches",
                column: "AccountId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_teammates_AccountId",
                table: "teammates",
                column: "AccountId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "hero_builds");

            migrationBuilder.DropTable(name: "indexed_players");

            migrationBuilder.DropTable(name: "match_player_items");

            migrationBuilder.DropTable(name: "match_roster");

            migrationBuilder.DropTable(name: "player_matches");

            migrationBuilder.DropTable(name: "teammates");
        }
    }
}
