using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRejection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedByUserId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Users");
        }
    }
}
