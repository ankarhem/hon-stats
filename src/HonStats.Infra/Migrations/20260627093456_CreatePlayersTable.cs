using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class CreatePlayersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.AccountId);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_players_Username",
                table: "players",
                column: "Username"
            );

            // Backfill players from existing denormalized name columns so the new
            // single source of truth is seeded before those columns are dropped.
            migrationBuilder.Sql(
                """
                INSERT INTO players (AccountId, Username, DisplayName, Country)
                SELECT AccountId, Username, DisplayName, Country FROM indexed_players
                WHERE Username IS NOT NULL OR DisplayName IS NOT NULL;

                INSERT INTO players (AccountId, Username, DisplayName, Country)
                SELECT TeammateAccountId, MAX(Username), MAX(DisplayName), MAX(Country)
                FROM teammates
                WHERE TeammateAccountId NOT IN (SELECT AccountId FROM players)
                  AND (Username IS NOT NULL OR DisplayName IS NOT NULL)
                GROUP BY TeammateAccountId;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "players");
        }
    }
}
