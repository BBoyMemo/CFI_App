using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskProgressNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskProgressNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaintenanceTaskId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LoggedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProgressNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskProgressNotes_MaintenanceTasks_MaintenanceTaskId",
                        column: x => x.MaintenanceTaskId,
                        principalTable: "MaintenanceTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskProgressNotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskProgressNotePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskProgressNoteId = table.Column<int>(type: "integer", nullable: false),
                    MediaAssetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProgressNotePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskProgressNotePhotos_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskProgressNotePhotos_TaskProgressNotes_TaskProgressNoteId",
                        column: x => x.TaskProgressNoteId,
                        principalTable: "TaskProgressNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressNotePhotos_MediaAssetId",
                table: "TaskProgressNotePhotos",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressNotePhotos_TaskProgressNoteId",
                table: "TaskProgressNotePhotos",
                column: "TaskProgressNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressNotes_MaintenanceTaskId_LoggedAt",
                table: "TaskProgressNotes",
                columns: new[] { "MaintenanceTaskId", "LoggedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressNotes_UserId",
                table: "TaskProgressNotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskProgressNotePhotos");

            migrationBuilder.DropTable(
                name: "TaskProgressNotes");
        }
    }
}
