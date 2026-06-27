using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchItemTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_item_timing",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_match_item_timing",
                        x => new
                        {
                            x.GameId,
                            x.AccountId,
                            x.ItemId,
                        }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_match_item_timing_AccountId_ItemId",
                table: "match_item_timing",
                columns: new[] { "AccountId", "ItemId" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "match_item_timing");
        }
    }
}
