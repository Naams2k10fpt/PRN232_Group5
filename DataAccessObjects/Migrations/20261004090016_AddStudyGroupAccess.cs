using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessObjects.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyGroupAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessMode",
                table: "StudyGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "StudyGroupMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "StudyGroupMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StudyGroupInvitations",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyGroupInvitations", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_StudyGroupInvitations_StudyGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyGroupInvitations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyGroupJoinRequests",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyGroupJoinRequests", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_StudyGroupJoinRequests_StudyGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyGroupJoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve every existing room and make its creator the owner.
            migrationBuilder.Sql("""
                INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "JoinedAt", "Role")
                SELECT "Id", "CreatedByUserId", "CreatedAt", 2 FROM "StudyGroups"
                ON CONFLICT ("GroupId", "UserId") DO UPDATE SET "Role" = 2;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudyGroupMembers_GroupId",
                table: "StudyGroupMembers",
                column: "GroupId",
                unique: true,
                filter: "\"Role\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_StudyGroupInvitations_UserId_Status",
                table: "StudyGroupInvitations",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyGroupJoinRequests_GroupId_Status",
                table: "StudyGroupJoinRequests",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyGroupJoinRequests_UserId",
                table: "StudyGroupJoinRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyGroupInvitations");

            migrationBuilder.DropTable(
                name: "StudyGroupJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_StudyGroupMembers_GroupId",
                table: "StudyGroupMembers");

            migrationBuilder.DropColumn(
                name: "AccessMode",
                table: "StudyGroups");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "StudyGroupMembers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StudyGroupMembers");
        }
    }
}
