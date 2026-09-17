using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsWorkAreaToArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWorkArea",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // The three utility/plant rooms nobody is stationed in - see the Areas table
            // in DatabaseSeeder for the full reasoning. Every other existing room already
            // has this column's default (true); only these three need flipping.
            migrationBuilder.Sql(
                """UPDATE "Areas" SET "IsWorkArea" = false WHERE "Code" IN ('PTANKS', 'BOILER', 'OFFICE');""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWorkArea",
                table: "Areas");
        }
    }
}
