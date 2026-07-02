using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMmrSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mmr_snapshots",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CurrentMmr = table.Column<double>(type: "REAL", nullable: false),
                    RankedCaldavarRating = table.Column<double>(type: "REAL", nullable: false),
                    RankedMidwarsRating = table.Column<double>(type: "REAL", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mmr_snapshots", x => new { x.AccountId, x.CapturedDate });
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_mmr_snapshots_AccountId",
                table: "mmr_snapshots",
                column: "AccountId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mmr_snapshots");
        }
    }
}
