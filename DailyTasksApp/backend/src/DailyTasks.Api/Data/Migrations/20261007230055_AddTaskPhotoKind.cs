using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyTasks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPhotoKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "task_photos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Plan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "task_photos");
        }
    }
}
