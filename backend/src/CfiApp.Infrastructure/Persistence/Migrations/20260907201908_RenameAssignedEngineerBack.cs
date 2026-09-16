using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameAssignedEngineerBack : Migration
    {
        /// <summary>
        /// The site kept "Engineer" after all, so the rename in SiteLayoutAndJobTypes is
        /// undone here rather than edited out of it - that migration has already run, and
        /// rewriting applied history to save one column rename is not worth the risk.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Accounts already let in as Technicians. On a database seeded after this
            // change the role is called Engineer from the start and no row matches.
            migrationBuilder.Sql(
                @"UPDATE ""Roles"" SET ""Name"" = 'Engineer' WHERE ""Name"" = 'Technician';");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_AssignedTechnicianId",
                table: "WorkOrders");

            migrationBuilder.RenameColumn(
                name: "AssignedTechnicianId",
                table: "WorkOrders",
                newName: "AssignedEngineerId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_AssignedTechnicianId_Status",
                table: "WorkOrders",
                newName: "IX_WorkOrders_AssignedEngineerId_Status");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_AssignedEngineerId",
                table: "WorkOrders",
                column: "AssignedEngineerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_AssignedEngineerId",
                table: "WorkOrders");

            migrationBuilder.RenameColumn(
                name: "AssignedEngineerId",
                table: "WorkOrders",
                newName: "AssignedTechnicianId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_AssignedEngineerId_Status",
                table: "WorkOrders",
                newName: "IX_WorkOrders_AssignedTechnicianId_Status");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_AssignedTechnicianId",
                table: "WorkOrders",
                column: "AssignedTechnicianId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
