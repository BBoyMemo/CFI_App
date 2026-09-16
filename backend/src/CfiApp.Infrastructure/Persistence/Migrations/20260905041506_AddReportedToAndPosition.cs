using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportedToAndPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportedByPosition",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedToName",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedToPosition",
                table: "WorkOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportedByPosition",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ReportedToName",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ReportedToPosition",
                table: "WorkOrders");
        }
    }
}
