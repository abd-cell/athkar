using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class WidgetSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WidgetSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefaultKind = table.Column<int>(type: "int", nullable: false),
                    AllowPrayerWidget = table.Column<bool>(type: "bit", nullable: false),
                    AllowDhikrWidget = table.Column<bool>(type: "bit", nullable: false),
                    Theme = table.Column<int>(type: "int", nullable: false),
                    RefreshMinutes = table.Column<int>(type: "int", nullable: false),
                    ShowHijriDate = table.Column<bool>(type: "bit", nullable: false),
                    ShowCountdown = table.Column<bool>(type: "bit", nullable: false),
                    DhikrCategoryId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WidgetSettings_AthkarCategories_DhikrCategoryId",
                        column: x => x.DhikrCategoryId,
                        principalTable: "AthkarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WidgetSettings_DhikrCategoryId",
                table: "WidgetSettings",
                column: "DhikrCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WidgetSettings");
        }
    }
}
