using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class WidgetCatalogue : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// The five added columns carry the entity's own defaults rather than
        /// SQL Server's zeroes.
        ///
        /// Scaffolded they came out <c>false</c> and <c>0</c>, which is not
        /// merely untidy: the settings row already exists in every deployment,
        /// so the first run of this migration would have withdrawn every
        /// customisation from every reader and set the pinned-text limit to
        /// nothing — silently, on a surface nobody is watching, and looking
        /// exactly like a deliberate administrative decision.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowBackgroundColor",
                table: "WidgetSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowBackgroundImage",
                table: "WidgetSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCustomWidget",
                table: "WidgetSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTransparency",
                table: "WidgetSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomWidgetMaxLength",
                table: "WidgetSettings",
                type: "int",
                nullable: false,
                defaultValue: 280);

            migrationBuilder.CreateTable(
                name: "WidgetCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Surface = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<int>(type: "int", nullable: false),
                    DesignCount = table.Column<int>(type: "int", nullable: false),
                    DefaultDesign = table.Column<int>(type: "int", nullable: false),
                    IsExclusive = table.Column<bool>(type: "bit", nullable: false),
                    IsNew = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WidgetCatalogItemTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WidgetCatalogItemId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetCatalogItemTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WidgetCatalogItemTranslations_WidgetCatalogItems_WidgetCatalogItemId",
                        column: x => x.WidgetCatalogItemId,
                        principalTable: "WidgetCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WidgetCatalogItems_Key",
                table: "WidgetCatalogItems",
                column: "Key",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WidgetCatalogItems_Surface_SortOrder",
                table: "WidgetCatalogItems",
                columns: new[] { "Surface", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WidgetCatalogItemTranslations_WidgetCatalogItemId_LanguageCode",
                table: "WidgetCatalogItemTranslations",
                columns: new[] { "WidgetCatalogItemId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WidgetCatalogItemTranslations");

            migrationBuilder.DropTable(
                name: "WidgetCatalogItems");

            migrationBuilder.DropColumn(
                name: "AllowBackgroundColor",
                table: "WidgetSettings");

            migrationBuilder.DropColumn(
                name: "AllowBackgroundImage",
                table: "WidgetSettings");

            migrationBuilder.DropColumn(
                name: "AllowCustomWidget",
                table: "WidgetSettings");

            migrationBuilder.DropColumn(
                name: "AllowTransparency",
                table: "WidgetSettings");

            migrationBuilder.DropColumn(
                name: "CustomWidgetMaxLength",
                table: "WidgetSettings");
        }
    }
}
