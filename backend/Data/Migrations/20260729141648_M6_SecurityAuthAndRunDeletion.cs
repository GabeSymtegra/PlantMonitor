using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class M6_SecurityAuthAndRunDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "completed_production_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUsername",
                table: "completed_production_runs",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "completed_production_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "completed_run_deletion_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LineName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DeletedByUsername = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DeletedByRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completed_run_deletion_audits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "local_users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_completed_production_runs_IsDeleted",
                table: "completed_production_runs",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_completed_run_deletion_audits_DeletedAtUtc",
                table: "completed_run_deletion_audits",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_completed_run_deletion_audits_RunId",
                table: "completed_run_deletion_audits",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_local_users_Username",
                table: "local_users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completed_run_deletion_audits");

            migrationBuilder.DropTable(
                name: "local_users");

            migrationBuilder.DropIndex(
                name: "IX_completed_production_runs_IsDeleted",
                table: "completed_production_runs");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "completed_production_runs");

            migrationBuilder.DropColumn(
                name: "DeletedByUsername",
                table: "completed_production_runs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "completed_production_runs");
        }
    }
}
