using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M9_AbConnectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConnectionTimeoutMs",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3000);

            migrationBuilder.AddColumn<string>(
                name: "ProcessorType",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "ControlLogix");

            migrationBuilder.AddColumn<int>(
                name: "ReadTimeoutMs",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3000);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelayMs",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 250);

            migrationBuilder.AddColumn<string>(
                name: "RoutePath",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "1,0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionTimeoutMs",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "ProcessorType",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "ReadTimeoutMs",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "RetryDelayMs",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "RoutePath",
                table: "line_protocol_assignments");
        }
    }
}
