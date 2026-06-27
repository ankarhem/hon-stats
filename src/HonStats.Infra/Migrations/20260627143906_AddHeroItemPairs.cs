using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HonStats.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroItemPairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hero_item_pairs",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemA = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemB = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_hero_item_pairs",
                        x => new
                        {
                            x.AccountId,
                            x.HeroId,
                            x.ItemA,
                            x.ItemB,
                        }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_hero_item_pairs_AccountId_HeroId",
                table: "hero_item_pairs",
                columns: new[] { "AccountId", "HeroId" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "hero_item_pairs");
        }
    }
}
