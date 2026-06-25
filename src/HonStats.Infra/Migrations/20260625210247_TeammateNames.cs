using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class TeammateNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "teammates",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "teammates",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "teammates",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Country", table: "teammates");

            migrationBuilder.DropColumn(name: "DisplayName", table: "teammates");

            migrationBuilder.DropColumn(name: "Username", table: "teammates");
        }
    }
}
