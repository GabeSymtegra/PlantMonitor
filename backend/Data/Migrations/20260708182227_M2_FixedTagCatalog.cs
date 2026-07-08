using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M2_FixedTagCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "line_tag_catalog_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogicalKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Driver = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PlcAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Scale = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadFrequencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_tag_catalog_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_line_tag_catalog_entries_LineId_LogicalKey",
                table: "line_tag_catalog_entries",
                columns: new[] { "LineId", "LogicalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_line_tag_catalog_entries_LineId_PlcAddress",
                table: "line_tag_catalog_entries",
                columns: new[] { "LineId", "PlcAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "line_tag_catalog_entries");
        }
    }
}
