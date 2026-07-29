using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M8_LineLifecycleState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LineLifecycleState",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.Sql(@"
UPDATE line_protocol_assignments
SET LineLifecycleState = CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Disabled' END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineLifecycleState",
                table: "line_protocol_assignments");
        }
    }
}
