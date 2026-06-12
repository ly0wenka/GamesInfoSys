using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesInfoSys.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedGames",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawgGameId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SteamAppId = table.Column<string>(type: "TEXT", nullable: true),
                    PsnProductId = table.Column<string>(type: "TEXT", nullable: true),
                    XboxProductId = table.Column<string>(type: "TEXT", nullable: true),
                    NintendoProductId = table.Column<string>(type: "TEXT", nullable: true),
                    EpicOfferId = table.Column<string>(type: "TEXT", nullable: true),
                    GogProductId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreOffers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackedGameId = table.Column<long>(type: "INTEGER", nullable: false),
                    Store = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    PriceMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    OriginalPriceMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreOffers_TrackedGames_TrackedGameId",
                        column: x => x.TrackedGameId,
                        principalTable: "TrackedGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfferPricePoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoreOfferId = table.Column<long>(type: "INTEGER", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    PriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalPriceMinor = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferPricePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferPricePoints_StoreOffers_StoreOfferId",
                        column: x => x.StoreOfferId,
                        principalTable: "StoreOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfferPricePoints_StoreOfferId_AtUtc",
                table: "OfferPricePoints",
                columns: new[] { "StoreOfferId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreOffers_Store_ExternalId_Region",
                table: "StoreOffers",
                columns: new[] { "Store", "ExternalId", "Region" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreOffers_TrackedGameId_Platform_Region",
                table: "StoreOffers",
                columns: new[] { "TrackedGameId", "Platform", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedGames_RawgGameId",
                table: "TrackedGames",
                column: "RawgGameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfferPricePoints");

            migrationBuilder.DropTable(
                name: "StoreOffers");

            migrationBuilder.DropTable(
                name: "TrackedGames");
        }
    }
}
