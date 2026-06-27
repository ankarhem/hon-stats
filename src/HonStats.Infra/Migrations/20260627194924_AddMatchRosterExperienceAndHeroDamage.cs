using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRosterExperienceAndHeroDamage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Experience",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "HeroDamage",
                table: "match_roster",
                type: "INTEGER",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Experience", table: "match_roster");

            migrationBuilder.DropColumn(name: "HeroDamage", table: "match_roster");
        }
    }
}
