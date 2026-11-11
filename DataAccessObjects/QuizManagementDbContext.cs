using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BusinessObjects;

namespace DataAccessObjects;

public partial class QuizManagementDbContext : DbContext, IDataProtectionKeyContext
{
    public QuizManagementDbContext()
    {
    }

    public QuizManagementDbContext(DbContextOptions<QuizManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Answer> Answers { get; set; }

    public virtual DbSet<Deck> Decks { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }
    public DbSet<SubjectFavorite> SubjectFavorites { get; set; }
    public DbSet<DeckFavorite> DeckFavorites { get; set; }

    public virtual DbSet<TestHistory> TestHistories { get; set; }

    public virtual DbSet<TestResultDetail> TestResultDetails { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<QuestionReport> QuestionReports { get; set; }

    public virtual DbSet<LoginAttempt> LoginAttempts { get; set; }

    public virtual DbSet<QuizAttempt> QuizAttempts { get; set; }

    public virtual DbSet<FlashcardProgress> FlashcardProgresses { get; set; }

    public DbSet<FlashcardReviewReceipt> FlashcardReviewReceipts { get; set; }

    public virtual DbSet<KahootRoom> KahootRooms { get; set; }

    public virtual DbSet<KahootParticipant> KahootParticipants { get; set; }

    public virtual DbSet<KahootAnswer> KahootAnswers { get; set; }
    public virtual DbSet<StudyGroup> StudyGroups { get; set; }
    public virtual DbSet<StudyGroupMember> StudyGroupMembers { get; set; }
    public virtual DbSet<GroupMessage> GroupMessages { get; set; }
    public virtual DbSet<ChatChannel> ChatChannels { get; set; }
    public virtual DbSet<ChatChannelRead> ChatChannelReads { get; set; }
    public virtual DbSet<ChatAttachment> ChatAttachments { get; set; }
    public virtual DbSet<ChatReaction> ChatReactions { get; set; }
    public virtual DbSet<StudyGroupJoinRequest> StudyGroupJoinRequests { get; set; }
    public virtual DbSet<StudyGroupInvitation> StudyGroupInvitations { get; set; }
    public virtual DbSet<StudyGroupInviteLink> StudyGroupInviteLinks { get; set; }

    // ASP.NET Core Data Protection keys (auth cookies, email/reset tokens) must outlive the
    // container: Render's free-tier disk is wiped on every deploy and idle sleep.
    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    // The ICU collation makes upper() fold non-ASCII letters (e.g. Vietnamese "đ" → "Đ")
    // regardless of the database's default locale; under the "C" locale upper() is ASCII-only.
    private static string NormalizeSql(string column)
        => $"upper(btrim(\"{column}\") COLLATE \"und-x-icu\")";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Question).WithMany(p => p.Answers)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("FK_Answers_Questions");
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Decks_TimeLimitMinutes",
                "\"TimeLimitMinutes\" BETWEEN 0 AND 180"));

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property<string>("NormalizedName")
                .HasMaxLength(255)
                .IsRequired()
                .HasComputedColumnSql(NormalizeSql("Name"), stored: true);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.Property(e => e.TimeLimitMinutes).HasDefaultValue(0);

            entity.HasIndex("SubjectId", "NormalizedName")
                .IsUnique()
                .HasDatabaseName("UX_Decks_SubjectId_NormalizedName_Active")
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(d => d.Subject).WithMany(p => p.Decks)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("FK_Decks_Subjects");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Questions_QuestionType",
                "\"QuestionType\" IN (1, 2)"));

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.QuestionType).HasDefaultValue(1);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            // Maps to the PostgreSQL xmin system column, which changes on every row update.
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.DeckId, e.CreatedAt }, "IX_Questions_DeckId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(d => d.Deck).WithMany(p => p.Questions)
                .HasForeignKey(d => d.DeckId)
                .HasConstraintName("FK_Questions_Decks");
        });

        modelBuilder.Entity<FlashcardProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property<uint>("Version").IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_FlashcardProgresses_Repetition", "\"Repetition\" >= 0");
                table.HasCheckConstraint("CK_FlashcardProgresses_IntervalMinutes", "\"IntervalMinutes\" >= 1");
                table.HasCheckConstraint("CK_FlashcardProgresses_EaseFactor", "\"EaseFactor\" BETWEEN 1.3 AND 3.0");
            });
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IntervalMinutes).HasDefaultValue(10);
            entity.Property(e => e.EaseFactor).HasDefaultValue(2.5);

            entity.HasIndex(e => new { e.UserId, e.QuestionId }, "UX_FlashcardProgresses_UserId_QuestionId")
                .IsUnique();
            entity.HasIndex(e => new { e.UserId, e.NextReviewAtUtc }, "IX_FlashcardProgresses_UserId_NextReviewAtUtc");

            entity.HasOne(e => e.User).WithMany(e => e.FlashcardProgresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_FlashcardProgresses_Users");
            entity.HasOne(e => e.Question).WithMany(e => e.FlashcardProgresses)
                .HasForeignKey(e => e.QuestionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_FlashcardProgresses_Questions");
        });

        modelBuilder.Entity<FlashcardReviewReceipt>(entity =>
        {
            entity.HasKey(e => new { e.ProgressId, e.ReviewId });
            entity.HasOne(e => e.Progress).WithMany()
                .HasForeignKey(e => e.ProgressId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubjectFavorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.SubjectId });
            entity.HasQueryFilter(e => !e.Subject.IsDeleted);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Subject).WithMany().HasForeignKey(e => e.SubjectId);
        });

        modelBuilder.Entity<DeckFavorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.DeckId });
            entity.HasQueryFilter(e => !e.Deck.IsDeleted && !e.Deck.Subject.IsDeleted);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Deck).WithMany().HasForeignKey(e => e.DeckId);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property<string>("NormalizedName")
                .HasMaxLength(255)
                .IsRequired()
                .HasComputedColumnSql(NormalizeSql("Name"), stored: true);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);

            entity.HasIndex("UserId", "NormalizedName")
                .IsUnique()
                .HasDatabaseName("UX_Subjects_UserId_NormalizedName_Active")
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(d => d.User).WithMany(p => p.Subjects)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Subjects_Users");
        });

        modelBuilder.Entity<TestHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TestHistories_ResultSnapshotJson",
                    "\"ResultSnapshotJson\" IS NULL OR \"ResultSnapshotJson\" IS JSON");
                table.HasCheckConstraint("CK_TestHistories_Score", "\"Score\" BETWEEN 0 AND 10");
                table.HasCheckConstraint("CK_TestHistories_Percentage", "\"Percentage\" BETWEEN 0 AND 100");
            });

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UserId).HasMaxLength(450);

            entity.HasIndex(e => e.QuizAttemptId, "UX_TestHistories_QuizAttemptId")
                .IsUnique()
                .HasFilter("\"QuizAttemptId\" IS NOT NULL");
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_TestHistories_UserId_CreatedAt")
                .IsDescending(false, true);
            entity.HasIndex(
                    e => new { e.DeckId, e.Percentage, e.CreatedAt },
                    "IX_TestHistories_DeckId_Percentage_CreatedAt")
                .IsDescending(false, true, true);

            entity.HasOne(d => d.Deck).WithMany(p => p.TestHistories)
                .HasForeignKey(d => d.DeckId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TestHistories_Decks");

            entity.HasOne(d => d.User).WithMany(p => p.TestHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TestHistories_Users");

            entity.HasOne(d => d.QuizAttempt).WithOne(p => p.TestHistory)
                .HasForeignKey<TestHistory>(d => d.QuizAttemptId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_TestHistories_QuizAttempts");
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QuizAttempts_QuestionIdsJson", "\"QuestionIdsJson\" IS JSON");
                table.HasCheckConstraint("CK_QuizAttempts_TimeLimit", "\"TimeLimitMinutes\" BETWEEN 0 AND 180");
                table.HasCheckConstraint(
                    "CK_QuizAttempts_Expiry",
                    "(\"TimeLimitMinutes\" = 0 AND \"ExpiresAtUtc\" IS NULL) OR (\"TimeLimitMinutes\" > 0 AND \"ExpiresAtUtc\" > \"StartedAtUtc\")");
                table.HasCheckConstraint(
                    "CK_QuizAttempts_Completed",
                    "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"StartedAtUtc\"");
            });
            entity.Property(e => e.UserId).HasMaxLength(450);

            entity.HasIndex(e => new { e.UserId, e.StartedAtUtc }, "IX_QuizAttempts_UserId_StartedAtUtc");
            entity.HasIndex(e => e.CompletedAtUtc, "IX_QuizAttempts_Pending")
                .HasFilter("\"CompletedAtUtc\" IS NULL");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_QuizAttempts_Users");

            entity.HasOne(e => e.Deck).WithMany()
                .HasForeignKey(e => e.DeckId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_QuizAttempts_Decks");
        });

        modelBuilder.Entity<TestResultDetail>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Question).WithMany(p => p.TestResultDetails)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Details_Question");

            entity.HasOne(d => d.SelectedAnswer).WithMany(p => p.TestResultDetails)
                .HasForeignKey(d => d.SelectedAnswerId)
                .HasConstraintName("FK_Details_Answer");

            entity.HasOne(d => d.TestHistory).WithMany(p => p.TestResultDetails)
                .HasForeignKey(d => d.TestHistoryId)
                .HasConstraintName("FK_Details_History");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Users_Role",
                "\"Role\" IS NULL OR \"Role\" IN ('Admin', 'Mentor', 'User')"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property<string>("NormalizedEmail")
                .HasMaxLength(256)
                .IsRequired()
                .HasComputedColumnSql(NormalizeSql("Email"), stored: true);
            entity.Property<string>("NormalizedUsername")
                .HasMaxLength(256)
                .IsRequired()
                .HasComputedColumnSql(NormalizeSql("Username"), stored: true);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(256);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.SecurityStamp).HasMaxLength(450);
            entity.Property(e => e.IsDisabled).HasDefaultValue(false);
            entity.Property(e => e.EmailConfirmed).HasDefaultValue(false);

            entity.HasIndex("NormalizedEmail")
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedEmail");
            entity.HasIndex("NormalizedUsername")
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedUsername");
        });

        modelBuilder.Entity<QuestionReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_QuestionReports_Reason",
                "\"Reason\" IN ('WrongAnswer', 'UnclearQuestion', 'DuplicateQuestion', 'Other')"));
            entity.Property(e => e.Reason).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsResolved).HasDefaultValue(false);

            entity.HasIndex(e => new { e.QuestionId, e.UserId },
                    "UX_QuestionReports_QuestionId_UserId_Pending")
                .IsUnique()
                .HasFilter("\"IsResolved\" = false");
            entity.HasIndex(
                    e => new { e.IsResolved, e.CreatedAt },
                    "IX_QuestionReports_IsResolved_CreatedAt")
                .IsDescending(false, true);

            entity.HasOne(e => e.Question).WithMany()
                .HasForeignKey(e => e.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QuestionReports_Questions");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QuestionReports_Users");
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CountsTowardLockout).HasDefaultValue(false);
            entity.HasIndex(
                    e => new { e.IsSuccess, e.CreatedAt },
                    "IX_LoginAttempts_IsSuccess_CreatedAt")
                .IsDescending(false, true);
            entity.HasIndex(
                    e => new { e.Email, e.IpAddress, e.CreatedAt },
                    "IX_LoginAttempts_Email_IpAddress_CreatedAt")
                .IsDescending(false, false, true);

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LoginAttempts_Users");
        });

        modelBuilder.Entity<KahootRoom>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomCode).HasMaxLength(10).IsRequired();
            entity.HasIndex(e => e.RoomCode).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Waiting");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.HostUser)
                .WithMany()
                .HasForeignKey(e => e.HostUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_KahootRooms_Users");

            entity.HasOne(e => e.Deck)
                .WithMany()
                .HasForeignKey(e => e.DeckId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_KahootRooms_Decks");
        });

        modelBuilder.Entity<KahootParticipant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nickname).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ConnectionId).HasMaxLength(100);
            entity.Property(e => e.TotalScore).HasDefaultValue(0);
            entity.Property(e => e.CurrentRank).HasDefaultValue(0);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Participants)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_KahootParticipants_Rooms");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KahootParticipants_Users");
        });

        modelBuilder.Entity<KahootAnswer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsCorrect).HasDefaultValue(false);
            entity.Property(e => e.AnswerTimeSeconds).HasPrecision(5, 2);
            entity.Property(e => e.ScoreEarned).HasDefaultValue(0);

            entity.HasOne(e => e.Participant)
                .WithMany(p => p.Answers)
                .HasForeignKey(e => e.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_KahootAnswers_Participants");

            entity.HasOne(e => e.Question)
                .WithMany()
                .HasForeignKey(e => e.QuestionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_KahootAnswers_Questions");

            entity.HasOne(e => e.SelectedAnswer)
                .WithMany()
                .HasForeignKey(e => e.SelectedAnswerId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KahootAnswers_Answers");
        });

        modelBuilder.Entity<StudyGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.AccessMode).HasConversion<int>().HasDefaultValue(StudyGroupAccessMode.PublicOpen);
            entity.HasOne(e => e.Subject).WithMany()
                .HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_StudyGroups_Subjects");
            entity.HasOne(e => e.CreatedByUser).WithMany()
                .HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_StudyGroups_Users");
        });

        modelBuilder.Entity<StudyGroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Role).HasConversion<int>().HasDefaultValue(StudyGroupMemberRole.Member);
            entity.Property(e => e.IsBanned).HasDefaultValue(false);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.GroupId).IsUnique().HasFilter("\"Role\" = 2");
            entity.HasOne(e => e.Group).WithMany(e => e.Members)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StudyGroupMembers_Groups");
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_StudyGroupMembers_Users");
        });

        modelBuilder.Entity<GroupMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Kind).HasConversion<int>().HasDefaultValue(GroupMessageKind.Chat);
            entity.Property(e => e.SentAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.GroupId, e.SentAt });
            entity.HasIndex(e => e.SenderUserId);
            entity.HasIndex(e => new { e.ChannelId, e.Id });
            entity.HasIndex(e => new { e.SenderUserId, e.ClientMessageId }).IsUnique()
                .HasFilter("\"ClientMessageId\" IS NOT NULL");
            entity.HasOne(e => e.Channel).WithMany(e => e.Messages)
                .HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ReplyToMessage).WithMany()
                .HasForeignKey(e => e.ReplyToMessageId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Group).WithMany(e => e.Messages)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_GroupMessages_Groups");
            entity.HasOne(e => e.SenderUser).WithMany()
                .HasForeignKey(e => e.SenderUserId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_GroupMessages_Users");
        });

        modelBuilder.Entity<ChatChannel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(40);
            entity.Property(e => e.Topic).HasMaxLength(200);
            entity.ToTable(table => table.HasCheckConstraint("CK_ChatChannels_Kind", "\"Kind\" IN (0, 1)"));
            entity.HasIndex(e => new { e.GroupId, e.Kind, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.GroupId, e.Kind }).IsUnique().HasFilter("\"IsDefault\"");
            entity.HasOne(e => e.Group).WithMany(e => e.Channels)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ChatChannelRead>(entity =>
        {
            entity.HasKey(e => new { e.ChannelId, e.UserId });
            entity.HasOne(e => e.Channel).WithMany(e => e.Reads)
                .HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(180);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.HasOne(e => e.Message).WithMany(e => e.Attachments)
                .HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ChatReaction>(entity =>
        {
            entity.HasKey(e => new { e.MessageId, e.UserId, e.Emoji });
            entity.Property(e => e.Emoji).HasMaxLength(16);
            entity.HasOne(e => e.Message).WithMany(e => e.Reactions)
                .HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudyGroupJoinRequest>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.UserId });
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.GroupId, e.Status });
            entity.HasOne(e => e.Group).WithMany(e => e.JoinRequests)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudyGroupInvitation>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.UserId });
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.InvitedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasOne(e => e.Group).WithMany(e => e.Invitations)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudyGroupInviteLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ProtectedToken).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.GroupId);
            entity.HasOne(e => e.Group).WithMany(e => e.InviteLinks)
                .HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser).WithMany()
                .HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
