using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace DataAccessObjects.Migrations;

[DbContext(typeof(QuizManagementDbContext))]
[Migration("20261001090000_AddStudyGroups")]
public sealed class AddStudyGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudyGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SubjectId = table.Column<int>(type: "integer", nullable: true),
                CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudyGroups", x => x.Id);
                table.ForeignKey("FK_StudyGroups_Subjects_SubjectId", x => x.SubjectId,
                    "Subjects", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_StudyGroups_Users_CreatedByUserId", x => x.CreatedByUserId,
                    "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StudyGroupMembers",
            columns: table => new
            {
                GroupId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                LastReadMessageId = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudyGroupMembers", x => new { x.GroupId, x.UserId });
                table.ForeignKey("FK_StudyGroupMembers_StudyGroups_GroupId", x => x.GroupId,
                    "StudyGroups", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_StudyGroupMembers_Users_UserId", x => x.UserId,
                    "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GroupMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SenderUserId = table.Column<string>(type: "text", nullable: false),
                Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GroupMessages", x => x.Id);
                table.ForeignKey("FK_GroupMessages_StudyGroups_GroupId", x => x.GroupId,
                    "StudyGroups", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_GroupMessages_Users_SenderUserId", x => x.SenderUserId,
                    "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_StudyGroups_SubjectId", "StudyGroups", "SubjectId");
        migrationBuilder.CreateIndex("IX_StudyGroups_CreatedByUserId", "StudyGroups", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_StudyGroupMembers_UserId", "StudyGroupMembers", "UserId");
        migrationBuilder.CreateIndex("IX_GroupMessages_GroupId_Id", "GroupMessages", new[] { "GroupId", "Id" });
        migrationBuilder.CreateIndex("IX_GroupMessages_SenderUserId", "GroupMessages", "SenderUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("GroupMessages");
        migrationBuilder.DropTable("StudyGroupMembers");
        migrationBuilder.DropTable("StudyGroups");
    }
}
