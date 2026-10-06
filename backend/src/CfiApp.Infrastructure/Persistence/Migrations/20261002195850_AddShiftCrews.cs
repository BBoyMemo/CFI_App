using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftCrews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftTypeMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftTypeId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTypeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftTypeMembers_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftTypeMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_ShiftTypeId",
                table: "ShiftTypeMembers",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypeMembers_UserId",
                table: "ShiftTypeMembers",
                column: "UserId",
                unique: true);

            // Until now people were put straight onto shifts in the pool. Their place is now on
            // the drawn-up shift's crew, so everybody on a running shift today gets that crew,
            // and the planner opens showing what the site already set up. Where somebody has a
            // move queued, the later one wins - that is where they are heading.
            //
            // Plain ANSI SQL apart from the quoted names; a one-off copy, not a rule.
            migrationBuilder.Sql("""
                INSERT INTO "ShiftTypeMembers" ("ShiftTypeId", "UserId", "CreatedAt")
                SELECT a."SourceShiftTypeId", r."UserId", CURRENT_TIMESTAMP
                FROM "ShiftRosterEntries" r
                JOIN "ActiveShifts" a ON a."Id" = r."ActiveShiftId"
                JOIN "ShiftTypes" t ON t."Id" = a."SourceShiftTypeId"
                WHERE a."EndsOn" >= CURRENT_DATE
                  AND r."EffectiveTo" >= CURRENT_DATE
                  AND r."EffectiveFrom" = (
                      SELECT MAX(r2."EffectiveFrom")
                      FROM "ShiftRosterEntries" r2
                      JOIN "ActiveShifts" a2 ON a2."Id" = r2."ActiveShiftId"
                      WHERE r2."UserId" = r."UserId"
                        AND r2."EffectiveTo" >= CURRENT_DATE
                        AND a2."EndsOn" >= CURRENT_DATE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftTypeMembers");
        }
    }
}
