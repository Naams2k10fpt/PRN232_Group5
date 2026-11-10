using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessObjects.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcardReviewReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL already provides the xmin system column.
            migrationBuilder.CreateTable(
                name: "FlashcardReviewReceipts",
                columns: table => new
                {
                    ProgressId = table.Column<int>(type: "integer", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardReviewReceipts", x => new { x.ProgressId, x.ReviewId });
                    table.ForeignKey(
                        name: "FK_FlashcardReviewReceipts_FlashcardProgresses_ProgressId",
                        column: x => x.ProgressId,
                        principalTable: "FlashcardProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashcardReviewReceipts");
        }
    }
}
