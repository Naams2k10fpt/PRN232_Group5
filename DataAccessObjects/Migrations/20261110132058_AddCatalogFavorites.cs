using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessObjects.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckFavorites",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckFavorites", x => new { x.UserId, x.DeckId });
                    table.ForeignKey(
                        name: "FK_DeckFavorites_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeckFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectFavorites",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectFavorites", x => new { x.UserId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_SubjectFavorites_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckFavorites_DeckId",
                table: "DeckFavorites",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectFavorites_SubjectId",
                table: "SubjectFavorites",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckFavorites");

            migrationBuilder.DropTable(
                name: "SubjectFavorites");
        }
    }
}
