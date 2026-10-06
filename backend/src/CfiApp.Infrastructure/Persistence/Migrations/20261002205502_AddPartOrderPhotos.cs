using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CfiApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartOrderPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartOrderPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartOrderRequestId = table.Column<int>(type: "integer", nullable: false),
                    MediaAssetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartOrderPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartOrderPhotos_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartOrderPhotos_PartOrderRequests_PartOrderRequestId",
                        column: x => x.PartOrderRequestId,
                        principalTable: "PartOrderRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartOrderPhotos_MediaAssetId",
                table: "PartOrderPhotos",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PartOrderPhotos_PartOrderRequestId",
                table: "PartOrderPhotos",
                column: "PartOrderRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartOrderPhotos");
        }
    }
}
