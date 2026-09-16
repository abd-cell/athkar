using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class RadioStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RadioStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StreamUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioStations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadioStationTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioStationTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadioStationTranslations_RadioStations_StationId",
                        column: x => x.StationId,
                        principalTable: "RadioStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RadioStations_Key",
                table: "RadioStations",
                column: "Key",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RadioStations_SortOrder",
                table: "RadioStations",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_RadioStationTranslations_StationId_LanguageCode",
                table: "RadioStationTranslations",
                columns: new[] { "StationId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RadioStationTranslations");

            migrationBuilder.DropTable(
                name: "RadioStations");
        }
    }
}
