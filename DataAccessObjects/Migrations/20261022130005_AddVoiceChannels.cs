using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessObjects.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatChannels_GroupId",
                table: "ChatChannels");

            migrationBuilder.DropIndex(
                name: "IX_ChatChannels_GroupId_Name",
                table: "ChatChannels");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ChatChannels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ChatChannels_GroupId_Kind",
                table: "ChatChannels",
                columns: new[] { "GroupId", "Kind" },
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_ChatChannels_GroupId_Kind_Name",
                table: "ChatChannels",
                columns: new[] { "GroupId", "Kind", "Name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatChannels_Kind",
                table: "ChatChannels",
                sql: "\"Kind\" IN (0, 1)");

            // Repair old groups without touching their existing message history or read cursors.
            migrationBuilder.Sql("""
                UPDATE "ChatChannels" SET "IsArchived" = false WHERE "IsDefault";
                INSERT INTO "ChatChannels" ("GroupId", "Name", "Kind", "IsDefault", "IsArchived", "CreatedAt")
                SELECT g."Id", 'chung', 0, true, false, g."CreatedAt" FROM "StudyGroups" g
                WHERE NOT EXISTS (SELECT 1 FROM "ChatChannels" c
                    WHERE c."GroupId" = g."Id" AND c."Kind" = 0 AND c."IsDefault")
                ON CONFLICT ("GroupId", "Kind", "Name") DO UPDATE SET "IsDefault" = true, "IsArchived" = false;
                INSERT INTO "ChatChannels" ("GroupId", "Name", "Topic", "Kind", "IsDefault", "IsArchived", "CreatedAt")
                SELECT g."Id", 'phong-hoc', 'Cùng học qua mic và camera', 1, true, false, g."CreatedAt" FROM "StudyGroups" g
                WHERE NOT EXISTS (SELECT 1 FROM "ChatChannels" c
                    WHERE c."GroupId" = g."Id" AND c."Kind" = 1 AND c."IsDefault")
                ON CONFLICT ("GroupId", "Kind", "Name") DO UPDATE SET "IsDefault" = true, "IsArchived" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "ChatChannels" WHERE "Kind" = 1;""");
            migrationBuilder.DropIndex(
                name: "IX_ChatChannels_GroupId_Kind",
                table: "ChatChannels");

            migrationBuilder.DropIndex(
                name: "IX_ChatChannels_GroupId_Kind_Name",
                table: "ChatChannels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatChannels_Kind",
                table: "ChatChannels");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ChatChannels");

            migrationBuilder.CreateIndex(
                name: "IX_ChatChannels_GroupId",
                table: "ChatChannels",
                column: "GroupId",
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_ChatChannels_GroupId_Name",
                table: "ChatChannels",
                columns: new[] { "GroupId", "Name" },
                unique: true);
        }
    }
}
