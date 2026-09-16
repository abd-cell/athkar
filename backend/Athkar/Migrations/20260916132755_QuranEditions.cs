using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class QuranEditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuranPackages_Version",
                table: "QuranPackages");

            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "QuranPackages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "QuranPackages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "QuranPackages",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            // Every row that already exists belongs to an edition that had no
            // name yet. It is named after the script it is set in — which is the
            // only thing that distinguished two packages before this migration —
            // and the uniqueness of (Edition, Version) survives because Version
            // was globally unique until now.
            migrationBuilder.Sql(@"
                UPDATE QuranPackages
                SET Edition = CASE Script
                        WHEN 2 THEN 'indopak'
                        WHEN 3 THEN 'naskh'
                        ELSE 'hafs-uthmani'
                    END,
                    Name = CASE Script
                        WHEN 2 THEN N'المصحف الهندي الباكستاني'
                        WHEN 3 THEN N'مصحف النسخ'
                        ELSE N'المصحف العثماني · حفص'
                    END
                WHERE Edition = '';");

            // Whatever was published is the default, or a device that asks
            // without naming an edition — which is every install shipped before
            // this migration — would be told there is nothing to download, and
            // would quietly stop receiving updates to a mushaf it already has.
            migrationBuilder.Sql(@"
                UPDATE QuranPackages
                SET IsDefault = 1
                WHERE Id IN (
                    SELECT TOP 1 Id FROM QuranPackages
                    WHERE IsPublished = 1 AND IsDeleted = 0
                    ORDER BY Version DESC);");

            migrationBuilder.CreateIndex(
                name: "IX_QuranPackages_Edition_Version",
                table: "QuranPackages",
                columns: new[] { "Edition", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuranPackages_IsPublished_IsDefault",
                table: "QuranPackages",
                columns: new[] { "IsPublished", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuranPackages_Edition_Version",
                table: "QuranPackages");

            migrationBuilder.DropIndex(
                name: "IX_QuranPackages_IsPublished_IsDefault",
                table: "QuranPackages");

            migrationBuilder.DropColumn(
                name: "Edition",
                table: "QuranPackages");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "QuranPackages");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "QuranPackages");

            migrationBuilder.CreateIndex(
                name: "IX_QuranPackages_Version",
                table: "QuranPackages",
                column: "Version",
                unique: true);
        }
    }
}
