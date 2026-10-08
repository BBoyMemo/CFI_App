using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyTasks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translation_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_EntityType_EntityId",
                table: "translation_jobs",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_NextAttemptAt",
                table: "translation_jobs",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_translations_EntityId",
                table: "translations",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_translations_EntityType_EntityId_Field_Language",
                table: "translations",
                columns: new[] { "EntityType", "EntityId", "Field", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translation_jobs");

            migrationBuilder.DropTable(
                name: "translations");
        }
    }
}
