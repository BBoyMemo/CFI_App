using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSeveralShiftsPerPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftTypeMembers_ShiftTypeId",
                table: "ShiftTypeMembers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypeMembers_UserId",
                table: "ShiftTypeMembers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftRosterEntries_UserId_EffectiveTo",
                table: "ShiftRosterEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_ShiftTypeId_UserId",
                table: "ShiftTypeMembers",
                columns: new[] { "ShiftTypeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_UserId",
                table: "ShiftTypeMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosterEntries_UserId_ActiveShiftId_EffectiveTo",
                table: "ShiftRosterEntries",
                columns: new[] { "UserId", "ActiveShiftId", "EffectiveTo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosterEntries_UserId_EffectiveTo",
                table: "ShiftRosterEntries",
                columns: new[] { "UserId", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftTypeMembers_ShiftTypeId_UserId",
                table: "ShiftTypeMembers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypeMembers_UserId",
                table: "ShiftTypeMembers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftRosterEntries_UserId_ActiveShiftId_EffectiveTo",
                table: "ShiftRosterEntries");

            migrationBuilder.DropIndex(
                name: "IX_ShiftRosterEntries_UserId_EffectiveTo",
                table: "ShiftRosterEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_ShiftTypeId",
                table: "ShiftTypeMembers",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_UserId",
                table: "ShiftTypeMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosterEntries_UserId_EffectiveTo",
                table: "ShiftRosterEntries",
                columns: new[] { "UserId", "EffectiveTo" },
                unique: true);
        }
    }
}
