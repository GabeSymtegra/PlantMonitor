using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M4_LineConfigPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LineName",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "line_protocol_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlcIp",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "LineName",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "PlcIp",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "line_protocol_assignments");
        }
    }
}
