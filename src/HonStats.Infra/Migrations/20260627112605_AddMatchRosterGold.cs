using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRosterGold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeathGoldLost",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "GoldFromAssists",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "GoldFromBuildings",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "GoldFromCreeps",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "GoldFromKills",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "GoldFromNeutrals",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "StartingGold",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeathGoldLost", table: "match_roster");

            migrationBuilder.DropColumn(name: "GoldFromAssists", table: "match_roster");

            migrationBuilder.DropColumn(name: "GoldFromBuildings", table: "match_roster");

            migrationBuilder.DropColumn(name: "GoldFromCreeps", table: "match_roster");

            migrationBuilder.DropColumn(name: "GoldFromKills", table: "match_roster");

            migrationBuilder.DropColumn(name: "GoldFromNeutrals", table: "match_roster");

            migrationBuilder.DropColumn(name: "StartingGold", table: "match_roster");
        }
    }
}
