using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyTasks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_photos_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_photos_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_photos_CreatedById",
                table: "task_photos",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_task_photos_TaskId",
                table: "task_photos",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_photos");
        }
    }
}
