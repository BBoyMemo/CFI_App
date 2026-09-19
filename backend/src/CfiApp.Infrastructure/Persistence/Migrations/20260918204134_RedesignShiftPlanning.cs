using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignShiftPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "ShiftTypes",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "Weekdays",
                table: "ShiftTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The shifts that already exist predate these two columns. Left at the column
            // defaults they would come back running on no day of the week at all, which is a
            // shift nobody can be put on. 31 is Monday to Friday.
            migrationBuilder.Sql(
                """UPDATE "ShiftTypes" SET "Weekdays" = 31, "StartsOn" = '2026-01-01' WHERE "Weekdays" = 0;""");

            migrationBuilder.CreateTable(
                name: "ActiveShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Weekdays = table.Column<int>(type: "integer", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    SourceShiftTypeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveShifts", x => x.Id);
                    table.CheckConstraint("CK_ActiveShift_DateOrder", "\"EndsOn\" >= \"StartsOn\"");
                });

            migrationBuilder.CreateTable(
                name: "ShiftOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ActiveShiftId = table.Column<int>(type: "integer", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftOverrides", x => x.Id);
                    table.CheckConstraint("CK_ShiftOverride_DateOrder", "\"ToDate\" >= \"FromDate\"");
                    table.ForeignKey(
                        name: "FK_ShiftOverrides_ActiveShifts_ActiveShiftId",
                        column: x => x.ActiveShiftId,
                        principalTable: "ActiveShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRosterEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ActiveShiftId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftRosterEntries", x => x.Id);
                    table.CheckConstraint("CK_ShiftRosterEntry_DateOrder", "\"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_ShiftRosterEntries_ActiveShifts_ActiveShiftId",
                        column: x => x.ActiveShiftId,
                        principalTable: "ActiveShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftRosterEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveShifts_StartsOn_EndsOn",
                table: "ActiveShifts",
                columns: new[] { "StartsOn", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftOverrides_ActiveShiftId",
                table: "ShiftOverrides",
                column: "ActiveShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftOverrides_UserId_FromDate_ToDate",
                table: "ShiftOverrides",
                columns: new[] { "UserId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosterEntries_ActiveShiftId_EffectiveFrom_EffectiveTo",
                table: "ShiftRosterEntries",
                columns: new[] { "ActiveShiftId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosterEntries_UserId_EffectiveTo",
                table: "ShiftRosterEntries",
                columns: new[] { "UserId", "EffectiveTo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftOverrides");

            migrationBuilder.DropTable(
                name: "ShiftRosterEntries");

            migrationBuilder.DropTable(
                name: "ActiveShifts");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "Weekdays",
                table: "ShiftTypes");

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftTypeId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_Date_ShiftTypeId",
                table: "ShiftAssignments",
                columns: new[] { "Date", "ShiftTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftTypeId",
                table: "ShiftAssignments",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_UserId_Date_ShiftTypeId",
                table: "ShiftAssignments",
                columns: new[] { "UserId", "Date", "ShiftTypeId" },
                unique: true);
        }
    }
}
