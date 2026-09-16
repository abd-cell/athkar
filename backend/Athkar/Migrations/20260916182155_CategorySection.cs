using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class CategorySection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Section",
                table: "AthkarCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // A first filing of the chapters that already exist, so the three
            // sections are not empty on the day the column ships. It reads the
            // Arabic name because that is the only thing that distinguishes
            // «دعاء السفر» from «أذكار السفر» — the key does not, which is why
            // this field exists at all.
            //
            // It is a starting point and not a verdict: an editor re-files any
            // of it in the CMS, and nothing here overwrites a later choice —
            // the migration runs once.
            migrationBuilder.Sql(@"
                UPDATE AthkarCategories SET Section = 1;

                UPDATE c SET c.Section = 2
                FROM AthkarCategories c
                JOIN CategoryTranslations t
                  ON t.CategoryId = c.Id AND t.LanguageCode = 'ar' AND t.IsDeleted = 0
                WHERE t.Name LIKE N'دعاء%' OR t.Name LIKE N'الدعاء%' OR t.Name LIKE N'أدعية%';

                UPDATE c SET c.Section = 3
                FROM AthkarCategories c
                JOIN CategoryTranslations t
                  ON t.CategoryId = c.Id AND t.LanguageCode = 'ar' AND t.IsDeleted = 0
                WHERE t.Name LIKE N'فضل%' OR t.Name LIKE N'فضائل%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Section",
                table: "AthkarCategories");
        }
    }
}
