using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class CaptureMatchMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new metric columns to match_roster (nullable, forward-only).
            migrationBuilder.AddColumn<int>(
                name: "Assists",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "BuildingDamage",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Buybacks",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "CreepDenies",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "CreepKills",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Deaths",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "HeroId",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Kills",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "Level",
                table: "match_roster",
                type: "REAL",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "NetWorth",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "NeutralKills",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "RavenPlaced",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "RoleIndex",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "WardOfRevelationPlaced",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "WardOfSightPlaced",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_match_roster_AccountId_HeroId",
                table: "match_roster",
                columns: new[] { "AccountId", "HeroId" }
            );

            // 2. Backfill: lift per-match scalars from match_player_items onto
            // match_roster. The values are duplicated identically across every
            // inventory-slot row for a (game, player), so LIMIT 1 is sufficient.
            // Games with zero item rows (0-items edge case) leave the columns null
            // — which is correct (forward-only: no lying 0).
            migrationBuilder.Sql(
                """
                UPDATE match_roster
                SET HeroId = (
                        SELECT mpi.HeroId FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    Kills = (
                        SELECT mpi.Kills FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    Deaths = (
                        SELECT mpi.Deaths FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    Assists = (
                        SELECT mpi.Assists FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    NetWorth = (
                        SELECT mpi.NetWorth FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    RoleIndex = (
                        SELECT mpi.RoleIndex FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    WardOfSightPlaced = (
                        SELECT mpi.WardsPlaced FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    ),
                    WardOfRevelationPlaced = (
                        SELECT mpi.WardsRevelation FROM match_player_items mpi
                        WHERE mpi.GameId = match_roster.GameId
                          AND mpi.AccountId = match_roster.AccountId
                        LIMIT 1
                    )
                WHERE EXISTS (
                    SELECT 1 FROM match_player_items mpi
                    WHERE mpi.GameId = match_roster.GameId
                      AND mpi.AccountId = match_roster.AccountId
                )
                """
            );

            // 3. Drop the old hero-scoping index and the misplaced scalar columns
            // from match_player_items (now a pure item-slot store).
            migrationBuilder.DropIndex(
                name: "IX_match_player_items_AccountId_HeroId",
                table: "match_player_items"
            );

            migrationBuilder.DropColumn(name: "Assists", table: "match_player_items");

            migrationBuilder.DropColumn(name: "Deaths", table: "match_player_items");

            migrationBuilder.DropColumn(name: "HeroId", table: "match_player_items");

            migrationBuilder.DropColumn(name: "Kills", table: "match_player_items");

            migrationBuilder.DropColumn(name: "NetWorth", table: "match_player_items");

            migrationBuilder.DropColumn(name: "RoleIndex", table: "match_player_items");

            migrationBuilder.DropColumn(name: "WardsPlaced", table: "match_player_items");

            migrationBuilder.DropColumn(name: "WardsRevelation", table: "match_player_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_match_roster_AccountId_HeroId",
                table: "match_roster"
            );

            migrationBuilder.DropColumn(name: "Assists", table: "match_roster");

            migrationBuilder.DropColumn(name: "BuildingDamage", table: "match_roster");

            migrationBuilder.DropColumn(name: "Buybacks", table: "match_roster");

            migrationBuilder.DropColumn(name: "CreepDenies", table: "match_roster");

            migrationBuilder.DropColumn(name: "CreepKills", table: "match_roster");

            migrationBuilder.DropColumn(name: "Deaths", table: "match_roster");

            migrationBuilder.DropColumn(name: "HeroId", table: "match_roster");

            migrationBuilder.DropColumn(name: "Kills", table: "match_roster");

            migrationBuilder.DropColumn(name: "Level", table: "match_roster");

            migrationBuilder.DropColumn(name: "NetWorth", table: "match_roster");

            migrationBuilder.DropColumn(name: "NeutralKills", table: "match_roster");

            migrationBuilder.DropColumn(name: "RavenPlaced", table: "match_roster");

            migrationBuilder.DropColumn(name: "RoleIndex", table: "match_roster");

            migrationBuilder.DropColumn(name: "WardOfRevelationPlaced", table: "match_roster");

            migrationBuilder.DropColumn(name: "WardOfSightPlaced", table: "match_roster");

            migrationBuilder.AddColumn<int>(
                name: "Assists",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "Deaths",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "HeroId",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "Kills",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "NetWorth",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "RoleIndex",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "WardsPlaced",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "WardsRevelation",
                table: "match_player_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "IX_match_player_items_AccountId_HeroId",
                table: "match_player_items",
                columns: new[] { "AccountId", "HeroId" }
            );
        }
    }
}
