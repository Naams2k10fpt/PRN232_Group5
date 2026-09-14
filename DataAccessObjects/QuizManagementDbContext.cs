using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using BusinessObjects;

namespace DataAccessObjects;

public partial class QuizManagementDbContext : DbContext
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

    public virtual DbSet<TestHistory> TestHistories { get; set; }

    public virtual DbSet<TestResultDetail> TestResultDetails { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<QuestionReport> QuestionReports { get; set; }

    public virtual DbSet<LoginAttempt> LoginAttempts { get; set; }

    public virtual DbSet<QuizAttempt> QuizAttempts { get; set; }

    public virtual DbSet<FlashcardProgress> FlashcardProgresses { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Answers__3214EC0718E562F7");

            entity.HasOne(d => d.Question).WithMany(p => p.Answers)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("FK_Answers_Questions");
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Decks__3214EC07F540EDFA");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Decks_TimeLimitMinutes",
                "[TimeLimitMinutes] BETWEEN 0 AND 180"));

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property<string>("NormalizedName")
                .HasMaxLength(255)
                .IsRequired()
                .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Name])))", stored: true);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.Property(e => e.TimeLimitMinutes).HasDefaultValue(0);

            entity.HasIndex("SubjectId", "NormalizedName")
                .IsUnique()
                .HasDatabaseName("UX_Decks_SubjectId_NormalizedName_Active")
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(d => d.Subject).WithMany(p => p.Decks)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("FK_Decks_Subjects");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Question__3214EC079D8AB6B4");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Questions_QuestionType",
                "[QuestionType] IN (1, 2)"));

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.QuestionType).HasDefaultValue(1);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.DeckId, e.CreatedAt }, "IX_Questions_DeckId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(d => d.Deck).WithMany(p => p.Questions)
                .HasForeignKey(d => d.DeckId)
                .HasConstraintName("FK_Questions_Decks");
        });

        modelBuilder.Entity<FlashcardProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_FlashcardProgresses_Repetition", "[Repetition] >= 0");
                table.HasCheckConstraint("CK_FlashcardProgresses_IntervalMinutes", "[IntervalMinutes] >= 1");
                table.HasCheckConstraint("CK_FlashcardProgresses_EaseFactor", "[EaseFactor] BETWEEN 1.3 AND 3.0");
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

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Subjects__3214EC073667D612");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => new { e.Name, e.UserId }, "IX_Subjects_Name_UserId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);

            entity.HasOne(d => d.User).WithMany(p => p.Subjects)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Subjects_Users");
        });

        modelBuilder.Entity<TestHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TestHist__3214EC07D6370CED");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TestHistories_ResultSnapshotJson",
                    "[ResultSnapshotJson] IS NULL OR ISJSON([ResultSnapshotJson]) = 1");
                table.HasCheckConstraint("CK_TestHistories_Score", "[Score] BETWEEN 0 AND 10");
                table.HasCheckConstraint("CK_TestHistories_Percentage", "[Percentage] BETWEEN 0 AND 100");
            });

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ResultSnapshotJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UserId).HasMaxLength(450);

            entity.HasIndex(e => e.QuizAttemptId, "UX_TestHistories_QuizAttemptId")
                .IsUnique()
                .HasFilter("[QuizAttemptId] IS NOT NULL");
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
                table.HasCheckConstraint("CK_QuizAttempts_QuestionIdsJson", "ISJSON([QuestionIdsJson]) = 1");
                table.HasCheckConstraint("CK_QuizAttempts_TimeLimit", "[TimeLimitMinutes] BETWEEN 0 AND 180");
                table.HasCheckConstraint(
                    "CK_QuizAttempts_Expiry",
                    "([TimeLimitMinutes] = 0 AND [ExpiresAtUtc] IS NULL) OR ([TimeLimitMinutes] > 0 AND [ExpiresAtUtc] > [StartedAtUtc])");
                table.HasCheckConstraint(
                    "CK_QuizAttempts_Completed",
                    "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
            });
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.StartedAtUtc).HasColumnType("datetimeoffset(7)");
            entity.Property(e => e.ExpiresAtUtc).HasColumnType("datetimeoffset(7)");
            entity.Property(e => e.CompletedAtUtc).HasColumnType("datetimeoffset(7)");

            entity.HasIndex(e => new { e.UserId, e.StartedAtUtc }, "IX_QuizAttempts_UserId_StartedAtUtc");
            entity.HasIndex(e => e.CompletedAtUtc, "IX_QuizAttempts_Pending")
                .HasFilter("[CompletedAtUtc] IS NULL");

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
            entity.HasKey(e => e.Id).HasName("PK__TestResu__3214EC07BD567949");

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
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC077068E03B");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Users_Role",
                "[Role] IS NULL OR [Role] IN (N'Admin', N'Mentor', N'User')"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property<string>("NormalizedEmail")
                .HasMaxLength(256)
                .IsRequired()
                .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Email])))", stored: true);
            entity.Property<string>("NormalizedUsername")
                .HasMaxLength(256)
                .IsRequired()
                .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Username])))", stored: true);
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
                "[Reason] IN (N'WrongAnswer', N'UnclearQuestion', N'DuplicateQuestion', N'Other')"));
            entity.Property(e => e.Reason).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsResolved).HasDefaultValue(false);

            entity.HasIndex(e => new { e.QuestionId, e.UserId },
                    "UX_QuestionReports_QuestionId_UserId_Pending")
                .IsUnique()
                .HasFilter("[IsResolved] = 0");
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
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
