using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M7_RuntimeStateMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MachineId",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OperatorName",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipeId",
                table: "line_protocol_assignments",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "active_line_runtime_states",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ControlMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CurrentProductId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RunStartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastTickUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProductionLength = table.Column<double>(type: "REAL", nullable: false),
                    HasSeenRunningState = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasPersistedCurrentStop = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_active_line_runtime_states", x => x.LineId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_active_line_runtime_states_UpdatedAtUtc",
                table: "active_line_runtime_states",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "active_line_runtime_states");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "OperatorName",
                table: "line_protocol_assignments");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "line_protocol_assignments");
        }
    }
}
