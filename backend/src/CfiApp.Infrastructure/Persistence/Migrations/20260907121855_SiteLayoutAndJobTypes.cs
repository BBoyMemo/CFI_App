using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteLayoutAndJobTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Critical has gone from the Priority scale. Anything already marked Critical
            // becomes High, rather than an integer the application can no longer name.
            migrationBuilder.Sql(@"UPDATE ""WorkOrders"" SET ""Priority"" = 2 WHERE ""Priority"" = 3;");

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

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "WorkOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentEquipmentId",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_ParentEquipmentId_IsActive",
                table: "Equipment",
                columns: new[] { "ParentEquipmentId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_Equipment_ParentEquipmentId",
                table: "Equipment",
                column: "ParentEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_AssignedTechnicianId",
                table: "WorkOrders",
                column: "AssignedTechnicianId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_Equipment_ParentEquipmentId",
                table: "Equipment");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_AssignedTechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_ParentEquipmentId_IsActive",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ParentEquipmentId",
                table: "Equipment");

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
    }
}
