using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class PushDispatchInboxWritten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InboxWritten",
                table: "PushDispatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Every row that already settled had its inbox row written by the
            // sender before the flag existed. Left false, the first retry of an
            // old failure would put a second copy of the same message in a
            // reader's inbox — and the reader never saw the failure.
            migrationBuilder.Sql(
                "UPDATE PushDispatches SET InboxWritten = 1 WHERE Status <> 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InboxWritten",
                table: "PushDispatches");
        }
    }
}
