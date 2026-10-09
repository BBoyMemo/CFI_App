using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyTasks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdateId",
                table: "task_photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_updates_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_updates_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_photos_UpdateId",
                table: "task_photos",
                column: "UpdateId");

            migrationBuilder.CreateIndex(
                name: "IX_task_updates_AuthorId",
                table: "task_updates",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_task_updates_TaskId_CreatedAt",
                table: "task_updates",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_task_photos_task_updates_UpdateId",
                table: "task_photos",
                column: "UpdateId",
                principalTable: "task_updates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_photos_task_updates_UpdateId",
                table: "task_photos");

            migrationBuilder.DropTable(
                name: "task_updates");

            migrationBuilder.DropIndex(
                name: "IX_task_photos_UpdateId",
                table: "task_photos");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "UpdateId",
                table: "task_photos");
        }
    }
}
