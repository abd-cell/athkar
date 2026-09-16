using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class WidgetDefaultKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultWidgetKey",
                table: "WidgetSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultWidgetKey",
                table: "WidgetSettings");
        }
    }
}
