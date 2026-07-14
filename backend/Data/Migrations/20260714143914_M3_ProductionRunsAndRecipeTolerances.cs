using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M3_ProductionRunsAndRecipeTolerances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "completed_production_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LineName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RecipeId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PlcIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FinalStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RuntimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ProductionLength = table.Column<double>(type: "REAL", nullable: false),
                    AutoTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ManualTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    AutoPercentage = table.Column<double>(type: "REAL", nullable: false),
                    ManualPercentage = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completed_production_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recipe_tolerances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MeasurementType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    ToleranceMinus = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    TolerancePlus = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_tolerances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "completed_production_run_zone_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Zone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Segment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MeasurementCount = table.Column<long>(type: "INTEGER", nullable: false),
                    SkippedCount = table.Column<long>(type: "INTEGER", nullable: false),
                    RunningAbsoluteDeviationSum = table.Column<double>(type: "REAL", nullable: false),
                    AverageAbsoluteDeviation = table.Column<double>(type: "REAL", nullable: false),
                    MaxPositiveDeviation = table.Column<double>(type: "REAL", nullable: false),
                    MaxNegativeDeviation = table.Column<double>(type: "REAL", nullable: false),
                    CurrentDeviation = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completed_production_run_zone_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_completed_production_run_zone_stats_completed_production_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "completed_production_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_completed_production_run_zone_stats_RunId_Zone_Segment",
                table: "completed_production_run_zone_stats",
                columns: new[] { "RunId", "Zone", "Segment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_completed_production_runs_EndTimeUtc",
                table: "completed_production_runs",
                column: "EndTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_completed_production_runs_LineId",
                table: "completed_production_runs",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_tolerances_RecipeId_MeasurementType_Version",
                table: "recipe_tolerances",
                columns: new[] { "RecipeId", "MeasurementType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completed_production_run_zone_stats");

            migrationBuilder.DropTable(
                name: "recipe_tolerances");

            migrationBuilder.DropTable(
                name: "completed_production_runs");
        }
    }
}
