using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M1_PlcProtocolConfigPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "line_protocol_assignments",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PresetName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PresetVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PollIntervalMs = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_protocol_assignments", x => x.LineId);
                });

            migrationBuilder.CreateTable(
                name: "plc_protocol_presets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PresetName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PresetVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plc_protocol_presets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "line_tag_overrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PlcAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Scale = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_tag_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_line_tag_overrides_line_protocol_assignments_LineId",
                        column: x => x.LineId,
                        principalTable: "line_protocol_assignments",
                        principalColumn: "LineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plc_protocol_preset_tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PlcAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Scale = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plc_protocol_preset_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plc_protocol_preset_tags_plc_protocol_presets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "plc_protocol_presets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_line_tag_overrides_LineId_TagKey",
                table: "line_tag_overrides",
                columns: new[] { "LineId", "TagKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plc_protocol_preset_tags_PresetId_TagKey",
                table: "plc_protocol_preset_tags",
                columns: new[] { "PresetId", "TagKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plc_protocol_presets_Manufacturer_PresetName_PresetVersion",
                table: "plc_protocol_presets",
                columns: new[] { "Manufacturer", "PresetName", "PresetVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "line_tag_overrides");

            migrationBuilder.DropTable(
                name: "plc_protocol_preset_tags");

            migrationBuilder.DropTable(
                name: "line_protocol_assignments");

            migrationBuilder.DropTable(
                name: "plc_protocol_presets");
        }
    }
}
