-- One-file bootstrap for a fresh/demo database.
-- Run this file after dropping QuizManagementDB:
--   * Missing database: create the current schema and demo data.
--   * Existing current schema: keep the schema and refresh only fixed demo data.
--   * Existing legacy/partial schema: stop. This script never drops data or guesses an upgrade path.
USE [master];
GO

IF DB_ID(N'QuizManagementDB') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [QuizManagementDB];');
    PRINT N'Created database QuizManagementDB.';
END
ELSE
    PRINT N'Database QuizManagementDB already exists; validating schema.';
GO

USE [QuizManagementDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    DECLARE @MissingRequiredObjects TABLE (ObjectName NVARCHAR(256) NOT NULL);

    INSERT INTO @MissingRequiredObjects
    SELECT N'Table dbo.' + expected.Name
    FROM (VALUES
        (N'Users'), (N'Subjects'), (N'Decks'), (N'Questions'), (N'Answers'),
        (N'QuizAttempts'), (N'TestHistories'), (N'TestResultDetails'),
        (N'QuestionReports'), (N'LoginAttempts'), (N'FlashcardProgresses')) expected(Name)
    WHERE OBJECT_ID(N'dbo.' + expected.Name, N'U') IS NULL;

    IF COL_LENGTH(N'dbo.TestHistories', N'QuizAttemptId') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.TestHistories.QuizAttemptId');
    IF COL_LENGTH(N'dbo.TestHistories', N'ResultSnapshotJson') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.TestHistories.ResultSnapshotJson');
    IF COL_LENGTH(N'dbo.Users', N'NormalizedEmail') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.Users.NormalizedEmail');
    IF COL_LENGTH(N'dbo.Users', N'NormalizedUsername') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.Users.NormalizedUsername');
    IF COL_LENGTH(N'dbo.Users', N'EmailConfirmed') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.Users.EmailConfirmed');
    IF COL_LENGTH(N'dbo.Decks', N'NormalizedName') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.Decks.NormalizedName');
    IF COL_LENGTH(N'dbo.Questions', N'RowVersion') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.Questions.RowVersion');
    IF COL_LENGTH(N'dbo.LoginAttempts', N'CountsTowardLockout') IS NULL INSERT INTO @MissingRequiredObjects VALUES (N'Column dbo.LoginAttempts.CountsTowardLockout');

    INSERT INTO @MissingRequiredObjects
    SELECT N'Index dbo.' + expected.TableName + N'.' + expected.IndexName
    FROM (VALUES
        (N'TestHistories', N'UX_TestHistories_QuizAttemptId'),
        (N'Users', N'UX_Users_NormalizedEmail'),
        (N'Users', N'UX_Users_NormalizedUsername'),
        (N'Decks', N'UX_Decks_SubjectId_NormalizedName_Active'),
        (N'QuestionReports', N'UX_QuestionReports_QuestionId_UserId_Pending'),
        (N'Questions', N'IX_Questions_DeckId_CreatedAt'),
        (N'TestHistories', N'IX_TestHistories_UserId_CreatedAt'),
        (N'TestHistories', N'IX_TestHistories_DeckId_Percentage_CreatedAt'),
        (N'QuestionReports', N'IX_QuestionReports_IsResolved_CreatedAt'),
        (N'LoginAttempts', N'IX_LoginAttempts_IsSuccess_CreatedAt'),
        (N'LoginAttempts', N'IX_LoginAttempts_Email_IpAddress_CreatedAt'),
        (N'FlashcardProgresses', N'UX_FlashcardProgresses_UserId_QuestionId'),
        (N'FlashcardProgresses', N'IX_FlashcardProgresses_UserId_NextReviewAtUtc')) expected(TableName, IndexName)
    WHERE NOT EXISTS (
        SELECT 1
        FROM sys.indexes actual
        WHERE actual.object_id = OBJECT_ID(N'dbo.' + expected.TableName)
          AND actual.name = expected.IndexName);

    INSERT INTO @MissingRequiredObjects
    SELECT N'Constraint ' + expected.Name
    FROM (VALUES
        (N'CK_QuizAttempts_QuestionIdsJson'),
        (N'CK_QuizAttempts_TimeLimit'),
        (N'CK_QuizAttempts_Expiry'),
        (N'CK_QuizAttempts_Completed'),
        (N'CK_TestHistories_ResultSnapshotJson'),
        (N'CK_Users_Role'),
        (N'CK_Decks_TimeLimitMinutes'),
        (N'CK_Questions_QuestionType'),
        (N'CK_TestHistories_Score'),
        (N'CK_TestHistories_Percentage'),
        (N'CK_QuestionReports_Reason'),
        (N'CK_FlashcardProgresses_Repetition'),
        (N'CK_FlashcardProgresses_IntervalMinutes'),
        (N'CK_FlashcardProgresses_EaseFactor')) expected(Name)
    WHERE NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints actual
        WHERE actual.name = expected.Name
          AND actual.is_disabled = 0
          AND actual.is_not_trusted = 0);

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id IN (OBJECT_ID(N'dbo.QuestionReports'), OBJECT_ID(N'dbo.LoginAttempts'))
          AND name = N'CreatedAt'
          AND TYPE_NAME(system_type_id) <> N'datetime2')
        INSERT INTO @MissingRequiredObjects VALUES (N'QuestionReports/LoginAttempts CreatedAt DATETIME2');

    IF EXISTS (SELECT 1 FROM @MissingRequiredObjects)
    BEGIN
        SELECT ObjectName AS MissingRequiredObject
        FROM @MissingRequiredObjects
        ORDER BY ObjectName;

        THROW 51020, 'QuizManagementDB does not match the current schema. For a disposable demo database, drop it manually and rerun CreateDB.sql. No changes were made.', 1;
    END;

    PRINT N'QuizManagementDB already matches the current schema; refreshing demo data.';
    RETURN;
END;

IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 51021, 'QuizManagementDB contains a partial or unrecognized schema. No changes were made; use a clean database.', 1;

BEGIN TRY
BEGIN TRANSACTION;

-- 1. Bảng Users
CREATE TABLE Users (
    Id NVARCHAR(450) PRIMARY KEY, -- Khớp với kiểu Id mặc định của ASP.NET Core Identity
    Username NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    NormalizedUsername AS UPPER(LTRIM(RTRIM(Username))) PERSISTED,
    NormalizedEmail AS UPPER(LTRIM(RTRIM(Email))) PERSISTED,
    PasswordHash NVARCHAR(MAX),
    Role NVARCHAR(50),
    -- Role/Profile/Security fields (merged from Phase 2)
    AvatarUrl NVARCHAR(500) NULL,
    IsDisabled BIT NOT NULL DEFAULT 0,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    SecurityStamp NVARCHAR(450) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_Users_Role CHECK (Role IS NULL OR Role IN (N'Admin', N'Mentor', N'User'))
);
CREATE UNIQUE INDEX UX_Users_NormalizedUsername ON Users(NormalizedUsername);
CREATE UNIQUE INDEX UX_Users_NormalizedEmail ON Users(NormalizedEmail);

-- 2. Bảng Subjects
CREATE TABLE Subjects (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(256) NULL,
    UpdatedBy NVARCHAR(256) NULL,
    CONSTRAINT FK_Subjects_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
-- Tạo Filtered Index để tránh trùng tên môn học nhưng bỏ qua các môn đã bị xóa (Soft Delete)
CREATE UNIQUE INDEX IX_Subjects_Name_UserId ON Subjects(Name, UserId) WHERE IsDeleted = 0;

-- 3. Bảng Decks
CREATE TABLE Decks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SubjectId INT NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    NormalizedName AS UPPER(LTRIM(RTRIM(Name))) PERSISTED,
    TimeLimitMinutes INT NOT NULL DEFAULT 0, -- 0 = không giới hạn; > 0 = số phút; do Mentor đặt
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(256) NULL,
    UpdatedBy NVARCHAR(256) NULL,
    CONSTRAINT FK_Decks_Subjects FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Decks_TimeLimitMinutes CHECK (TimeLimitMinutes BETWEEN 0 AND 180)
);
CREATE UNIQUE INDEX UX_Decks_SubjectId_NormalizedName_Active
    ON Decks(SubjectId, NormalizedName) WHERE IsDeleted = 0;

-- 4. Bảng Questions
CREATE TABLE Questions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DeckId INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    Explanation NVARCHAR(MAX) NULL,
    QuestionType INT NOT NULL DEFAULT 1, -- 1: SingleChoice, 2: MultipleChoice
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(256) NULL,
    UpdatedBy NVARCHAR(256) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT FK_Questions_Decks FOREIGN KEY (DeckId) REFERENCES Decks(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Questions_QuestionType CHECK (QuestionType IN (1, 2))
);
CREATE INDEX IX_Questions_DeckId_CreatedAt
    ON Questions(DeckId, CreatedAt DESC) WHERE IsDeleted = 0;

-- 5. Bảng Answers
CREATE TABLE Answers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QuestionId INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsCorrect BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Answers_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE
);

-- 6. Bảng QuizAttempts — server-side state cho timeout/idempotency
CREATE TABLE QuizAttempts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    DeckId INT NOT NULL,
    QuestionIdsJson NVARCHAR(MAX) NOT NULL,
    TimeLimitMinutes INT NOT NULL,
    StartedAtUtc DATETIMEOFFSET(7) NOT NULL,
    ExpiresAtUtc DATETIMEOFFSET(7) NULL,
    CompletedAtUtc DATETIMEOFFSET(7) NULL,
    CONSTRAINT FK_QuizAttempts_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_QuizAttempts_Decks FOREIGN KEY (DeckId) REFERENCES Decks(Id),
    CONSTRAINT CK_QuizAttempts_QuestionIdsJson CHECK (ISJSON(QuestionIdsJson) = 1),
    CONSTRAINT CK_QuizAttempts_TimeLimit CHECK (TimeLimitMinutes BETWEEN 0 AND 180),
    CONSTRAINT CK_QuizAttempts_Expiry CHECK (
        (TimeLimitMinutes = 0 AND ExpiresAtUtc IS NULL) OR
        (TimeLimitMinutes > 0 AND ExpiresAtUtc > StartedAtUtc)
    ),
    CONSTRAINT CK_QuizAttempts_Completed CHECK (CompletedAtUtc IS NULL OR CompletedAtUtc >= StartedAtUtc)
);
CREATE INDEX IX_QuizAttempts_UserId_StartedAtUtc ON QuizAttempts(UserId, StartedAtUtc DESC);
CREATE INDEX IX_QuizAttempts_Pending ON QuizAttempts(CompletedAtUtc) WHERE CompletedAtUtc IS NULL;

-- 7. Bảng TestHistories
CREATE TABLE TestHistories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    DeckId INT NOT NULL,
    QuizAttemptId UNIQUEIDENTIFIER NULL,
    ResultSnapshotJson NVARCHAR(MAX) NULL,
    Score FLOAT NOT NULL,
    Percentage FLOAT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_TestHistories_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_TestHistories_Decks FOREIGN KEY (DeckId) REFERENCES Decks(Id),
    CONSTRAINT FK_TestHistories_QuizAttempts FOREIGN KEY (QuizAttemptId) REFERENCES QuizAttempts(Id),
    CONSTRAINT CK_TestHistories_ResultSnapshotJson CHECK (ResultSnapshotJson IS NULL OR ISJSON(ResultSnapshotJson) = 1),
    CONSTRAINT CK_TestHistories_Score CHECK (Score BETWEEN 0 AND 10),
    CONSTRAINT CK_TestHistories_Percentage CHECK (Percentage BETWEEN 0 AND 100)
);
CREATE UNIQUE INDEX UX_TestHistories_QuizAttemptId ON TestHistories(QuizAttemptId) WHERE QuizAttemptId IS NOT NULL;
CREATE INDEX IX_TestHistories_UserId_CreatedAt ON TestHistories(UserId, CreatedAt DESC);
CREATE INDEX IX_TestHistories_DeckId_Percentage_CreatedAt
    ON TestHistories(DeckId, Percentage DESC, CreatedAt DESC);

-- 8. Bảng TestResultDetails
CREATE TABLE TestResultDetails (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TestHistoryId INT NOT NULL,
    QuestionId INT NOT NULL,
    SelectedAnswerId INT NULL,
    IsCorrect BIT NOT NULL,
    CONSTRAINT FK_Details_History FOREIGN KEY (TestHistoryId) REFERENCES TestHistories(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Details_Question FOREIGN KEY (QuestionId) REFERENCES Questions(Id),
    CONSTRAINT FK_Details_Answer FOREIGN KEY (SelectedAnswerId) REFERENCES Answers(Id)
);

-- 9. Bảng QuestionReports
-- Người dùng báo cáo câu hỏi sai/không rõ; Mentor/Admin xem và xử lý
CREATE TABLE QuestionReports (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QuestionId INT NOT NULL,
    UserId NVARCHAR(450) NOT NULL,
    Reason NVARCHAR(100) NOT NULL,           -- WrongAnswer | UnclearQuestion | DuplicateQuestion | Other
    Note NVARCHAR(500) NULL,                 -- Ghi chú thêm của người báo cáo
    IsResolved BIT NOT NULL DEFAULT 0,       -- Mentor/Admin đánh dấu đã xử lý
    CreatedAt DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_QuestionReports_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id),
    CONSTRAINT FK_QuestionReports_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_QuestionReports_Reason CHECK (Reason IN (N'WrongAnswer', N'UnclearQuestion', N'DuplicateQuestion', N'Other'))
);
CREATE UNIQUE INDEX UX_QuestionReports_QuestionId_UserId_Pending
    ON QuestionReports(QuestionId, UserId) WHERE IsResolved = 0;
CREATE INDEX IX_QuestionReports_IsResolved_CreatedAt
    ON QuestionReports(IsResolved, CreatedAt DESC);

-- 10. Bảng LoginAttempts
-- Ghi lại mọi lần đăng nhập (thành công và thất bại) để Admin theo dõi
CREATE TABLE LoginAttempts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    IpAddress NVARCHAR(50) NOT NULL,
    IsSuccess BIT NOT NULL,
    CountsTowardLockout BIT NOT NULL DEFAULT 0,
    UserId NVARCHAR(450) NULL,               -- NULL nếu email không tồn tại
    CreatedAt DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LoginAttempts_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
CREATE INDEX IX_LoginAttempts_IsSuccess_CreatedAt
    ON LoginAttempts(IsSuccess, CreatedAt DESC);
CREATE INDEX IX_LoginAttempts_Email_IpAddress_CreatedAt
    ON LoginAttempts(Email, IpAddress, CreatedAt DESC);

-- 11. Tiến độ spaced repetition của từng người dùng/câu hỏi
CREATE TABLE FlashcardProgresses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    QuestionId INT NOT NULL,
    Repetition INT NOT NULL DEFAULT 0,
    IntervalMinutes INT NOT NULL DEFAULT 10,
    EaseFactor FLOAT NOT NULL DEFAULT 2.5,
    LastReviewedAtUtc DATETIME2(7) NOT NULL,
    NextReviewAtUtc DATETIME2(7) NOT NULL,
    CONSTRAINT FK_FlashcardProgresses_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FlashcardProgresses_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT CK_FlashcardProgresses_Repetition CHECK (Repetition >= 0),
    CONSTRAINT CK_FlashcardProgresses_IntervalMinutes CHECK (IntervalMinutes >= 1),
    CONSTRAINT CK_FlashcardProgresses_EaseFactor CHECK (EaseFactor BETWEEN 1.3 AND 3.0)
);
CREATE UNIQUE INDEX UX_FlashcardProgresses_UserId_QuestionId
    ON FlashcardProgresses(UserId, QuestionId);
CREATE INDEX IX_FlashcardProgresses_UserId_NextReviewAtUtc
    ON FlashcardProgresses(UserId, NextReviewAtUtc);

COMMIT TRANSACTION;
PRINT N'QuizManagementDB schema was created successfully.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO


-- ============================================================
-- Demo data
-- ============================================================
-- Demo data: ISOLATED DEMO DATABASES ONLY.
-- This section creates accounts with public credentials and replaces all data owned by fixed seed identities.
-- Never run CreateDB.sql against production or a database containing real user data.

USE QuizManagementDB;
GO

SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;

DECLARE @SeedBy NVARCHAR(256) = N'Naams2k10fpt';
DECLARE @AdminId NVARCHAR(450) = N'seed-admin-001';
DECLARE @MentorId NVARCHAR(450) = N'seed-mentor-001';
DECLARE @UserId NVARCHAR(450) = N'seed-user-001';

DECLARE @SeedUserIds TABLE (Id NVARCHAR(450) PRIMARY KEY);
INSERT INTO @SeedUserIds (Id)
VALUES (@AdminId), (@MentorId), (@UserId);

DECLARE @SeedEmails TABLE (Email NVARCHAR(256) PRIMARY KEY);
INSERT INTO @SeedEmails (Email)
VALUES
    (N'admin.demo@quiz.local'),
    (N'mentor.demo@quiz.local'),
    (N'user.demo@quiz.local'),
    (N'unknown.demo@quiz.local');

DECLARE @SeedDeckIds TABLE (Id INT PRIMARY KEY);
INSERT INTO @SeedDeckIds (Id)
SELECT d.Id
FROM Decks d
JOIN Subjects s ON s.Id = d.SubjectId
WHERE s.UserId IN (SELECT Id FROM @SeedUserIds);

DECLARE @SeedQuestionIds TABLE (Id INT PRIMARY KEY);
INSERT INTO @SeedQuestionIds (Id)
SELECT q.Id
FROM Questions q
WHERE q.DeckId IN (SELECT Id FROM @SeedDeckIds);

DECLARE @SeedAnswerIds TABLE (Id INT PRIMARY KEY);
INSERT INTO @SeedAnswerIds (Id)
SELECT a.Id
FROM Answers a
WHERE a.QuestionId IN (SELECT Id FROM @SeedQuestionIds);

DECLARE @SeedQuizAttemptIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @SeedQuizAttemptIds (Id)
SELECT qa.Id
FROM QuizAttempts qa
WHERE qa.UserId IN (SELECT Id FROM @SeedUserIds)
   OR qa.DeckId IN (SELECT Id FROM @SeedDeckIds);

DELETE FROM QuestionReports
WHERE UserId IN (SELECT Id FROM @SeedUserIds)
   OR QuestionId IN (SELECT Id FROM @SeedQuestionIds);

DELETE FROM LoginAttempts
WHERE UserId IN (SELECT Id FROM @SeedUserIds)
   OR Email IN (SELECT Email FROM @SeedEmails);

DELETE FROM FlashcardProgresses
WHERE UserId IN (SELECT Id FROM @SeedUserIds)
   OR QuestionId IN (SELECT Id FROM @SeedQuestionIds);

DELETE trd
FROM TestResultDetails trd
JOIN TestHistories th ON th.Id = trd.TestHistoryId
WHERE th.UserId IN (SELECT Id FROM @SeedUserIds)
   OR th.DeckId IN (SELECT Id FROM @SeedDeckIds)
   OR th.QuizAttemptId IN (SELECT Id FROM @SeedQuizAttemptIds)
   OR trd.QuestionId IN (SELECT Id FROM @SeedQuestionIds)
   OR trd.SelectedAnswerId IN (SELECT Id FROM @SeedAnswerIds);

DELETE th
FROM TestHistories th
WHERE th.UserId IN (SELECT Id FROM @SeedUserIds)
   OR th.DeckId IN (SELECT Id FROM @SeedDeckIds)
   OR th.QuizAttemptId IN (SELECT Id FROM @SeedQuizAttemptIds);

DELETE FROM QuizAttempts
WHERE Id IN (SELECT Id FROM @SeedQuizAttemptIds);

DELETE a
FROM Answers a
JOIN Questions q ON q.Id = a.QuestionId
JOIN Decks d ON d.Id = q.DeckId
JOIN Subjects s ON s.Id = d.SubjectId
WHERE s.UserId IN (SELECT Id FROM @SeedUserIds);

DELETE q
FROM Questions q
JOIN Decks d ON d.Id = q.DeckId
JOIN Subjects s ON s.Id = d.SubjectId
WHERE s.UserId IN (SELECT Id FROM @SeedUserIds);

DELETE d
FROM Decks d
JOIN Subjects s ON s.Id = d.SubjectId
WHERE s.UserId IN (SELECT Id FROM @SeedUserIds);

DELETE FROM Subjects WHERE UserId IN (SELECT Id FROM @SeedUserIds);
DELETE FROM Users WHERE Id IN (SELECT Id FROM @SeedUserIds);

IF EXISTS (
    SELECT 1
    FROM Users
    WHERE Email IN (N'admin.demo@quiz.local', N'mentor.demo@quiz.local', N'user.demo@quiz.local')
       OR Username IN (N'admin_demo', N'mentor_demo', N'user_demo')
)
BEGIN
    THROW 51000, 'Demo username/email already exists outside seed data. Please rename or remove the duplicate account first.', 1;
END;

INSERT INTO Users (Id, Username, Email, PasswordHash, Role, AvatarUrl, IsDisabled, EmailConfirmed, SecurityStamp, CreatedAt)
VALUES
(
    @AdminId,
    N'admin_demo',
    N'admin.demo@quiz.local',
    N'AQAAAAIAAYagAAAAEDzzWWoiT7nnykl+8erbwXYh6DYpC9EO7Rnh/DFhzx1R2pdkUBltTQaL3/k60Uq/BQ==',
    N'Admin',
    NULL,
    0,
    1,
    CONVERT(NVARCHAR(450), NEWID()),
    SYSUTCDATETIME()
),
(
    @MentorId,
    N'mentor_demo',
    N'mentor.demo@quiz.local',
    N'AQAAAAIAAYagAAAAEB7bK/ID0O9kFUY2PhmAzbPayqyF4PtR+IY1PCeDb8L8dCfu03y+1yDBBY4E4i0ITg==',
    N'Mentor',
    NULL,
    0,
    1,
    CONVERT(NVARCHAR(450), NEWID()),
    SYSUTCDATETIME()
),
(
    @UserId,
    N'user_demo',
    N'user.demo@quiz.local',
    N'AQAAAAIAAYagAAAAEIaKi+BX47VrcqNs2tXvpIg6Hv0UNPpeRcrGUh4IxPMwydoTC4eyQo86LyU/gnzGtA==',
    N'User',
    NULL,
    0,
    1,
    CONVERT(NVARCHAR(450), NEWID()),
    SYSUTCDATETIME()
);

DECLARE @SubjectCSharpId INT;
DECLARE @SubjectAspNetId INT;
DECLARE @DeckCSharpId INT;
DECLARE @DeckMvcId INT;
DECLARE @DeckEfId INT;

INSERT INTO Subjects (UserId, Name, CreatedBy, CreatedAt)
VALUES (@MentorId, N'C# Fundamentals', @SeedBy, SYSUTCDATETIME());
SET @SubjectCSharpId = SCOPE_IDENTITY();

INSERT INTO Subjects (UserId, Name, CreatedBy, CreatedAt)
VALUES (@MentorId, N'PRN222 - ASP.NET Core MVC', @SeedBy, SYSUTCDATETIME());
SET @SubjectAspNetId = SCOPE_IDENTITY();

DECLARE @DeckMap TABLE (DeckKey NVARCHAR(50) PRIMARY KEY, Id INT NOT NULL);

INSERT INTO Decks (SubjectId, Name, TimeLimitMinutes, CreatedBy, CreatedAt)
VALUES (@SubjectCSharpId, N'C# and OOP Basics', 20, @SeedBy, SYSUTCDATETIME());
SET @DeckCSharpId = SCOPE_IDENTITY();
INSERT INTO @DeckMap (DeckKey, Id) VALUES (N'csharp', @DeckCSharpId);

INSERT INTO Decks (SubjectId, Name, TimeLimitMinutes, CreatedBy, CreatedAt)
VALUES (@SubjectAspNetId, N'ASP.NET Core MVC', 30, @SeedBy, SYSUTCDATETIME());
SET @DeckMvcId = SCOPE_IDENTITY();
INSERT INTO @DeckMap (DeckKey, Id) VALUES (N'mvc', @DeckMvcId);

INSERT INTO Decks (SubjectId, Name, TimeLimitMinutes, CreatedBy, CreatedAt)
VALUES (@SubjectAspNetId, N'Entity Framework Core', 0, @SeedBy, SYSUTCDATETIME());
SET @DeckEfId = SCOPE_IDENTITY();
INSERT INTO @DeckMap (DeckKey, Id) VALUES (N'efcore', @DeckEfId);

DECLARE @QuestionSeeds TABLE
(
    QuestionCode NVARCHAR(50) PRIMARY KEY,
    DeckKey NVARCHAR(50) NOT NULL,
    QuestionType INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    Explanation NVARCHAR(MAX) NULL
);

INSERT INTO @QuestionSeeds (QuestionCode, DeckKey, QuestionType, Content, Explanation)
VALUES
(N'CS01', N'csharp', 1, N'CLR trong .NET có vai trò chính là gì?', N'CLR quản lý việc thực thi mã .NET, bộ nhớ, ngoại lệ và bảo mật runtime.'),
(N'CS02', N'csharp', 2, N'Những đặc tính cốt lõi của lập trình hướng đối tượng gồm những gì?', N'OOP thường được mô tả bằng đóng gói, kế thừa, đa hình và trừu tượng hóa.'),
(N'CS03', N'csharp', 1, N'Trong C#, kiểu string có đặc điểm nào?', N'String là immutable, mỗi thao tác thay đổi thường tạo chuỗi mới.'),
(N'CS04', N'csharp', 1, N'Interface thường được dùng để làm gì?', N'Interface mô tả hợp đồng hành vi mà class/struct triển khai.'),
(N'CS05', N'csharp', 1, N'LINQ deferred execution nghĩa là gì?', N'Truy vấn chỉ được thực thi khi dữ liệu được duyệt hoặc materialize.'),
(N'CS06', N'csharp', 2, N'Đâu là ví dụ của value type trong C#?', N'Value type thường lưu trực tiếp giá trị, ví dụ int, bool, struct.'),
(N'CS07', N'csharp', 1, N'Khối finally trong try-catch-finally được chạy khi nào?', N'finally thường chạy sau try/catch, dù có exception hay không, trừ một số trường hợp đặc biệt như process bị dừng.'),
(N'MVC01', N'mvc', 1, N'Controller trong mô hình MVC chịu trách nhiệm chính gì?', N'Controller tiếp nhận request, điều phối xử lý và chọn response/view phù hợp.'),
(N'MVC02', N'mvc', 1, N'Razor View dùng để làm gì trong ASP.NET Core MVC?', N'Razor View render HTML động dựa trên model và cú pháp Razor.'),
(N'MVC03', N'mvc', 1, N'ModelState.IsValid thường dùng để kiểm tra điều gì?', N'Nó kiểm tra dữ liệu binding/validation có hợp lệ theo ViewModel hay không.'),
(N'MVC04', N'mvc', 2, N'Những Tag Helper nào thường dùng để tạo route/form binding?', N'Các asp-controller, asp-action, asp-for giúp Razor sinh HTML gắn với route/model.'),
(N'MVC05', N'mvc', 1, N'Trong pipeline ASP.NET Core, UseAuthentication nên đặt ở đâu?', N'UseAuthentication nên đặt trước UseAuthorization để authorization đọc được user principal.'),
(N'MVC06', N'mvc', 1, N'ViewModel giúp ích gì trong MVC?', N'ViewModel giới hạn dữ liệu cần hiển thị/nhận từ form và tránh bind trực tiếp entity quá mức.'),
(N'MVC07', N'mvc', 1, N'TempData thường phù hợp cho tình huống nào?', N'TempData phù hợp truyền thông báo ngắn sau redirect, ví dụ success/error message.'),
(N'EF01', N'efcore', 1, N'DbContext trong Entity Framework Core đại diện cho điều gì?', N'DbContext đại diện cho phiên làm việc với database, quản lý DbSet, tracking và SaveChanges.'),
(N'EF02', N'efcore', 1, N'Include trong EF Core dùng để làm gì?', N'Include eager-load navigation property để lấy dữ liệu liên quan trong cùng truy vấn.'),
(N'EF03', N'efcore', 1, N'Migration trong EF Core dùng cho mục đích gì?', N'Migration mô tả thay đổi schema theo thời gian và giúp cập nhật database có kiểm soát.'),
(N'EF04', N'efcore', 1, N'AsNoTracking phù hợp khi nào?', N'AsNoTracking phù hợp cho truy vấn chỉ đọc, giảm chi phí change tracking.'),
(N'EF05', N'efcore', 2, N'Quan hệ nào đúng với schema Quiz Management hiện tại?', N'Schema hiện tại có User - Subject, Subject - Deck và Deck - Question theo dạng một-nhiều.'),
(N'EF06', N'efcore', 1, N'SaveChanges trong EF Core có tác dụng gì?', N'SaveChanges gửi các thay đổi đang được DbContext tracking xuống database.'),
(N'EF07', N'efcore', 1, N'Soft delete thường được hiểu là gì?', N'Soft delete đánh dấu dữ liệu đã xóa bằng cờ như IsDeleted thay vì xóa vật lý khỏi database.');

DECLARE @QuestionMap TABLE (QuestionCode NVARCHAR(50) PRIMARY KEY, Id INT NOT NULL);

MERGE Questions AS target
USING
(
    SELECT qs.QuestionCode, dm.Id AS DeckId, qs.Content, qs.Explanation, qs.QuestionType
    FROM @QuestionSeeds qs
    JOIN @DeckMap dm ON dm.DeckKey = qs.DeckKey
) AS source
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (DeckId, Content, Explanation, QuestionType, CreatedBy, CreatedAt)
    VALUES (source.DeckId, source.Content, source.Explanation, source.QuestionType, @SeedBy, SYSUTCDATETIME())
OUTPUT source.QuestionCode, inserted.Id INTO @QuestionMap (QuestionCode, Id);

DECLARE @AnswerSeeds TABLE
(
    QuestionCode NVARCHAR(50) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsCorrect BIT NOT NULL
);

INSERT INTO @AnswerSeeds (QuestionCode, Content, IsCorrect)
VALUES
(N'CS01', N'Quản lý thực thi mã .NET, bộ nhớ và exception', 1),
(N'CS01', N'Thiết kế giao diện người dùng', 0),
(N'CS01', N'Lưu trữ dữ liệu quan hệ', 0),
(N'CS01', N'Tạo file CSS cho ứng dụng', 0),
(N'CS02', N'Đóng gói', 1),
(N'CS02', N'Kế thừa', 1),
(N'CS02', N'Đa hình', 1),
(N'CS02', N'Trừu tượng hóa', 1),
(N'CS03', N'string là immutable', 1),
(N'CS03', N'string luôn là value type', 0),
(N'CS03', N'string không thể so sánh bằng Equals', 0),
(N'CS03', N'string chỉ lưu được ASCII', 0),
(N'CS04', N'Mô tả hợp đồng hành vi cần triển khai', 1),
(N'CS04', N'Bắt buộc chứa constructor có tham số', 0),
(N'CS04', N'Lưu dữ liệu vào database', 0),
(N'CS04', N'Thay thế hoàn toàn mọi class', 0),
(N'CS05', N'Truy vấn chỉ chạy khi được duyệt hoặc materialize', 1),
(N'CS05', N'Truy vấn luôn chạy ngay khi khai báo', 0),
(N'CS05', N'Truy vấn không bao giờ chạy trên database', 0),
(N'CS05', N'Truy vấn chỉ dùng được với mảng', 0),
(N'CS06', N'int', 1),
(N'CS06', N'bool', 1),
(N'CS06', N'struct tự định nghĩa', 1),
(N'CS06', N'class tự định nghĩa', 0),
(N'CS07', N'Sau try/catch, dù có exception hay không trong luồng bình thường', 1),
(N'CS07', N'Chỉ chạy khi không có exception', 0),
(N'CS07', N'Chỉ chạy trước try', 0),
(N'CS07', N'Không bao giờ chạy nếu có catch', 0),
(N'MVC01', N'Tiếp nhận request và điều phối response/view', 1),
(N'MVC01', N'Chỉ chứa CSS của trang', 0),
(N'MVC01', N'Là bảng dữ liệu trong SQL Server', 0),
(N'MVC01', N'Chỉ dùng để lưu ảnh', 0),
(N'MVC02', N'Render HTML động từ model và Razor syntax', 1),
(N'MVC02', N'Thay thế hoàn toàn controller', 0),
(N'MVC02', N'Chỉ chạy câu lệnh SQL', 0),
(N'MVC02', N'Mã hóa password người dùng', 0),
(N'MVC03', N'Dữ liệu gửi lên có hợp lệ theo validation hay không', 1),
(N'MVC03', N'Người dùng có phải admin hay không', 0),
(N'MVC03', N'Ứng dụng có kết nối internet hay không', 0),
(N'MVC03', N'File CSS có tồn tại hay không', 0),
(N'MVC04', N'asp-controller', 1),
(N'MVC04', N'asp-action', 1),
(N'MVC04', N'asp-for', 1),
(N'MVC04', N'href cố định không qua Tag Helper', 0),
(N'MVC05', N'Trước UseAuthorization', 1),
(N'MVC05', N'Sau MapControllerRoute', 0),
(N'MVC05', N'Chỉ đặt trong appsettings.json', 0),
(N'MVC05', N'Không cần dùng khi có cookie auth', 0),
(N'MVC06', N'Giới hạn dữ liệu hiển thị/nhận từ form', 1),
(N'MVC06', N'Thay thế database', 0),
(N'MVC06', N'Bắt buộc trùng 100% với entity', 0),
(N'MVC06', N'Chỉ dùng cho file JavaScript', 0),
(N'MVC07', N'Truyền thông báo ngắn sau redirect', 1),
(N'MVC07', N'Lưu dữ liệu vĩnh viễn nhiều năm', 0),
(N'MVC07', N'Thay thế session authentication', 0),
(N'MVC07', N'Biên dịch Razor View', 0),
(N'EF01', N'Phiên làm việc với database và change tracker', 1),
(N'EF01', N'Một file CSS', 0),
(N'EF01', N'Một loại cookie', 0),
(N'EF01', N'Một trình duyệt web', 0),
(N'EF02', N'Eager-load dữ liệu liên quan qua navigation property', 1),
(N'EF02', N'Mã hóa password', 0),
(N'EF02', N'Tạo branch Git', 0),
(N'EF02', N'Xóa mọi dữ liệu trong database', 0),
(N'EF03', N'Quản lý thay đổi schema database theo thời gian', 1),
(N'EF03', N'Tự động viết README', 0),
(N'EF03', N'Chỉ dùng để tạo CSS', 0),
(N'EF03', N'Thay thế controller', 0),
(N'EF04', N'Khi truy vấn chỉ đọc và không cần cập nhật entity', 1),
(N'EF04', N'Khi bắt buộc sửa entity ngay sau query', 0),
(N'EF04', N'Khi muốn bật tracking mạnh hơn', 0),
(N'EF04', N'Khi muốn bỏ qua validation form', 0),
(N'EF05', N'User có nhiều Subject', 1),
(N'EF05', N'Subject có nhiều Deck', 1),
(N'EF05', N'Deck có nhiều Question', 1),
(N'EF05', N'Answer có nhiều Question làm parent trực tiếp', 0),
(N'EF06', N'Ghi các thay đổi đang tracking xuống database', 1),
(N'EF06', N'Chỉ render HTML', 0),
(N'EF06', N'Tự động đăng nhập người dùng', 0),
(N'EF06', N'Tạo file PDF', 0),
(N'EF07', N'Đánh dấu IsDeleted thay vì xóa vật lý', 1),
(N'EF07', N'Xóa thẳng record khỏi database trong mọi trường hợp', 0),
(N'EF07', N'Tự động tạo tài khoản admin', 0),
(N'EF07', N'Chỉ áp dụng cho file ảnh', 0);

INSERT INTO Answers (QuestionId, Content, IsCorrect)
SELECT qm.Id, a.Content, a.IsCorrect
FROM @AnswerSeeds a
JOIN @QuestionMap qm ON qm.QuestionCode = a.QuestionCode;

DECLARE @Mvc01Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'MVC01');
DECLARE @Mvc02Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'MVC02');
DECLARE @Mvc03Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'MVC03');
DECLARE @Mvc04Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'MVC04');
DECLARE @Mvc05Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'MVC05');
DECLARE @Ef03Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'EF03');
DECLARE @Cs03Id INT = (SELECT Id FROM @QuestionMap WHERE QuestionCode = N'CS03');

DECLARE @SelectedAnswers TABLE
(
    QuestionId INT NOT NULL,
    AnswerId INT NOT NULL,
    PRIMARY KEY (QuestionId, AnswerId)
);

INSERT INTO @SelectedAnswers (QuestionId, AnswerId)
SELECT qm.Id, a.Id
FROM @QuestionMap qm
JOIN Answers a ON a.QuestionId = qm.Id AND a.IsCorrect = 1
WHERE qm.QuestionCode IN (N'MVC01', N'MVC02', N'MVC03');

INSERT INTO @SelectedAnswers (QuestionId, AnswerId)
SELECT @Mvc04Id, a.Id
FROM Answers a
WHERE a.QuestionId = @Mvc04Id AND a.IsCorrect = 1;

INSERT INTO @SelectedAnswers (QuestionId, AnswerId)
SELECT TOP (1) @Mvc05Id, a.Id
FROM Answers a
WHERE a.QuestionId = @Mvc05Id AND a.IsCorrect = 0
ORDER BY a.Id;

DECLARE @QuestionIdsJson NVARCHAR(MAX) =
(
    SELECT N'[' + STRING_AGG(CONVERT(NVARCHAR(MAX), Id), N',')
        WITHIN GROUP (ORDER BY QuestionCode) + N']'
    FROM @QuestionMap
    WHERE QuestionCode IN (N'MVC01', N'MVC02', N'MVC03', N'MVC04', N'MVC05')
);

DECLARE @AttemptId UNIQUEIDENTIFIER = NEWID();
DECLARE @AttemptStartedAt DATETIMEOFFSET(7) = DATEADD(DAY, -1, SYSDATETIMEOFFSET());
DECLARE @AttemptCompletedAt DATETIMEOFFSET(7) = DATEADD(MINUTE, 4, @AttemptStartedAt);

INSERT INTO QuizAttempts
    (Id, UserId, DeckId, QuestionIdsJson, TimeLimitMinutes, StartedAtUtc, ExpiresAtUtc, CompletedAtUtc)
VALUES
    (@AttemptId, @UserId, @DeckMvcId, @QuestionIdsJson, 30,
     @AttemptStartedAt, DATEADD(MINUTE, 30, @AttemptStartedAt), @AttemptCompletedAt);

DECLARE @ResultSnapshotJson NVARCHAR(MAX) =
(
    SELECT
        d.Name AS DeckName,
        s.Name AS SubjectName,
        JSON_QUERY((
            SELECT
                q.Id AS QuestionId,
                q.Content,
                q.Explanation,
                q.QuestionType,
                CAST(CASE WHEN qm.QuestionCode = N'MVC05' THEN 0 ELSE 1 END AS BIT) AS IsCorrect,
                JSON_QUERY((
                    SELECT
                        a.Id AS AnswerId,
                        a.Content,
                        CAST(a.IsCorrect AS BIT) AS IsCorrectAnswer,
                        CAST(CASE WHEN EXISTS (
                            SELECT 1
                            FROM @SelectedAnswers selected
                            WHERE selected.QuestionId = a.QuestionId
                              AND selected.AnswerId = a.Id
                        ) THEN 1 ELSE 0 END AS BIT) AS WasSelected
                    FROM Answers a
                    WHERE a.QuestionId = q.Id
                    ORDER BY a.Id
                    FOR JSON PATH
                )) AS Answers
            FROM @QuestionMap qm
            JOIN Questions q ON q.Id = qm.Id
            WHERE qm.QuestionCode IN (N'MVC01', N'MVC02', N'MVC03', N'MVC04', N'MVC05')
            ORDER BY qm.QuestionCode
            FOR JSON PATH
        )) AS Questions
    FROM Decks d
    JOIN Subjects s ON s.Id = d.SubjectId
    WHERE d.Id = @DeckMvcId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
);

DECLARE @HistoryId INT;

INSERT INTO TestHistories
    (UserId, DeckId, QuizAttemptId, ResultSnapshotJson, Score, Percentage, CreatedAt)
VALUES
    (@UserId, @DeckMvcId, @AttemptId, @ResultSnapshotJson, 8.00, 80.00,
     CONVERT(DATETIME2(7), @AttemptCompletedAt));
SET @HistoryId = SCOPE_IDENTITY();

INSERT INTO TestResultDetails (TestHistoryId, QuestionId, SelectedAnswerId, IsCorrect)
SELECT
    @HistoryId,
    selected.QuestionId,
    selected.AnswerId,
    CAST(CASE WHEN selected.QuestionId = @Mvc05Id THEN 0 ELSE 1 END AS BIT)
FROM @SelectedAnswers selected;

INSERT INTO QuestionReports (QuestionId, UserId, Reason, Note, IsResolved, CreatedAt)
VALUES
    (@Ef03Id, @UserId, N'UnclearQuestion', N'Demo báo cáo đang chờ Mentor/Admin xử lý.', 0,
     DATEADD(HOUR, -12, SYSUTCDATETIME())),
    (@Cs03Id, @UserId, N'Other', N'Demo báo cáo đã được xử lý.', 1,
     DATEADD(DAY, -2, SYSUTCDATETIME()));

INSERT INTO LoginAttempts (Email, IpAddress, IsSuccess, UserId, CreatedAt)
VALUES
    (N'user.demo@quiz.local', N'127.0.0.1', 1, @UserId, DATEADD(HOUR, -2, SYSUTCDATETIME())),
    (N'user.demo@quiz.local', N'127.0.0.1', 0, @UserId, DATEADD(HOUR, -3, SYSUTCDATETIME())),
    (N'unknown.demo@quiz.local', N'127.0.0.2', 0, NULL, DATEADD(HOUR, -4, SYSUTCDATETIME()));

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT Username, Email, Role, N'Test@123456' AS DemoPassword
FROM Users
WHERE Id IN (@AdminId, @MentorId, @UserId)
ORDER BY Role;

SELECT
    (SELECT COUNT(*) FROM Subjects WHERE UserId = @MentorId) AS SubjectCount,
    (SELECT COUNT(*)
     FROM Decks d
     JOIN Subjects s ON s.Id = d.SubjectId
     WHERE s.UserId = @MentorId) AS DeckCount,
    (SELECT COUNT(*)
     FROM Questions q
     JOIN Decks d ON d.Id = q.DeckId
     JOIN Subjects s ON s.Id = d.SubjectId
     WHERE s.UserId = @MentorId) AS QuestionCount;

SELECT
    (SELECT COUNT(*) FROM QuizAttempts WHERE UserId = @UserId) AS QuizAttemptCount,
    (SELECT COUNT(*) FROM TestHistories WHERE UserId = @UserId) AS TestHistoryCount,
    (SELECT COUNT(*) FROM QuestionReports WHERE UserId = @UserId) AS QuestionReportCount,
    (SELECT COUNT(*) FROM LoginAttempts WHERE Email IN (SELECT Email FROM @SeedEmails)) AS LoginAttemptCount;
GO
