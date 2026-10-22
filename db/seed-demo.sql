-- Demo data for Quiz Management (PostgreSQL).
-- Subjects and decks are shared for study, so do not clone them per user.
--
-- ISOLATED DEMO DATABASES ONLY. This script creates accounts with public credentials
-- (password Test@123456) and replaces all data owned by the fixed seed identities.
-- Never run it against production or a database containing real user data.
--
-- Prerequisite: the schema must exist. Apply EF Core migrations first:
--   dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
--
-- Run:  psql "<connection>" -f db/seed-demo.sql      (or paste into the Neon SQL editor)
--
-- Safe to re-run: seed-owned rows are deleted and recreated; unrelated data is untouched.
-- The whole seed runs in one DO block, so any error leaves the database unchanged.

DO $seed$
DECLARE
    seed_by         CONSTANT text := 'Naams2k10fpt';
    admin_id        CONSTANT text := 'seed-admin-001';
    mentor_id       CONSTANT text := 'seed-mentor-001';
    user_id         CONSTANT text := 'seed-user-001';
    seed_user_ids   CONSTANT text[] := ARRAY['seed-admin-001', 'seed-mentor-001', 'seed-user-001'];
    seed_emails     CONSTANT text[] := ARRAY[
        'admin.demo@quiz.local', 'mentor.demo@quiz.local', 'user.demo@quiz.local', 'unknown.demo@quiz.local'];

    seed_deck_ids     int[];
    seed_question_ids int[];
    seed_answer_ids   int[];
    seed_attempt_ids  uuid[];

    subject_csharp_id int;
    subject_aspnet_id int;
    deck_csharp_id    int;
    deck_mvc_id       int;
    deck_ef_id        int;
    mvc04_id          int;
    mvc05_id          int;
    ef03_id           int;
    cs03_id           int;

    attempt_id           uuid := gen_random_uuid();
    attempt_started_at   timestamptz := now() - interval '1 day';
    attempt_completed_at timestamptz := now() - interval '1 day' + interval '4 minutes';
    question_ids_json    text;
    result_snapshot_json text;
    history_id           int;
    group_seed            record;
    group_id              int;
BEGIN
    IF to_regclass('public."Users"') IS NULL THEN
        RAISE EXCEPTION 'Quiz Management schema not found. Apply EF Core migrations before seeding.';
    END IF;

    -- 1. Remove everything owned by or pointing at the previous seed.
    seed_deck_ids := ARRAY(
        SELECT d."Id" FROM "Decks" d
        JOIN "Subjects" s ON s."Id" = d."SubjectId"
        WHERE s."UserId" = ANY (seed_user_ids));
    seed_question_ids := ARRAY(
        SELECT q."Id" FROM "Questions" q WHERE q."DeckId" = ANY (seed_deck_ids));
    seed_answer_ids := ARRAY(
        SELECT a."Id" FROM "Answers" a WHERE a."QuestionId" = ANY (seed_question_ids));
    seed_attempt_ids := ARRAY(
        SELECT qa."Id" FROM "QuizAttempts" qa
        WHERE qa."UserId" = ANY (seed_user_ids) OR qa."DeckId" = ANY (seed_deck_ids));

    -- Demo accounts can also participate in rooms created outside this seed.
    DELETE FROM "GroupMessages" WHERE "SenderUserId" = ANY (seed_user_ids);
    DELETE FROM "StudyGroups" WHERE "CreatedByUserId" = ANY (seed_user_ids);

    DELETE FROM "QuestionReports"
    WHERE "UserId" = ANY (seed_user_ids) OR "QuestionId" = ANY (seed_question_ids);

    -- Chat: demo accounts may have joined or posted in groups other people created; those rows go,
    -- the groups stay. Groups the demo accounts created go with all their members and messages.
    DELETE FROM "GroupMessages" WHERE "SenderUserId" = ANY (seed_user_ids);
    DELETE FROM "StudyGroupMembers" WHERE "UserId" = ANY (seed_user_ids);
    DELETE FROM "StudyGroups" WHERE "CreatedByUserId" = ANY (seed_user_ids);

    DELETE FROM "LoginAttempts"
    WHERE "UserId" = ANY (seed_user_ids) OR "Email" = ANY (seed_emails);

    DELETE FROM "FlashcardProgresses"
    WHERE "UserId" = ANY (seed_user_ids) OR "QuestionId" = ANY (seed_question_ids);

    DELETE FROM "TestResultDetails" trd
    USING "TestHistories" th
    WHERE th."Id" = trd."TestHistoryId"
      AND (th."UserId" = ANY (seed_user_ids)
        OR th."DeckId" = ANY (seed_deck_ids)
        OR th."QuizAttemptId" = ANY (seed_attempt_ids)
        OR trd."QuestionId" = ANY (seed_question_ids)
        OR trd."SelectedAnswerId" = ANY (seed_answer_ids));

    DELETE FROM "TestHistories"
    WHERE "UserId" = ANY (seed_user_ids)
       OR "DeckId" = ANY (seed_deck_ids)
       OR "QuizAttemptId" = ANY (seed_attempt_ids);

    DELETE FROM "QuizAttempts" WHERE "Id" = ANY (seed_attempt_ids);
    DELETE FROM "Answers" WHERE "Id" = ANY (seed_answer_ids);
    DELETE FROM "Questions" WHERE "Id" = ANY (seed_question_ids);
    DELETE FROM "Decks" WHERE "Id" = ANY (seed_deck_ids);
    DELETE FROM "Subjects" WHERE "UserId" = ANY (seed_user_ids);
    DELETE FROM "Users" WHERE "Id" = ANY (seed_user_ids);

    IF EXISTS (
        SELECT 1 FROM "Users"
        WHERE "NormalizedEmail" IN ('ADMIN.DEMO@QUIZ.LOCAL', 'MENTOR.DEMO@QUIZ.LOCAL', 'USER.DEMO@QUIZ.LOCAL')
           OR "NormalizedUsername" IN ('ADMIN_DEMO', 'MENTOR_DEMO', 'USER_DEMO'))
    THEN
        RAISE EXCEPTION 'Demo username/email already exists outside seed data. Rename or remove the duplicate account first.';
    END IF;

    -- 2. Accounts (all passwords: Test@123456).
    INSERT INTO "Users"
        ("Id", "Username", "Email", "PasswordHash", "Role", "AvatarUrl", "IsDisabled", "EmailConfirmed", "SecurityStamp", "CreatedAt")
    VALUES
        (admin_id, 'admin_demo', 'admin.demo@quiz.local',
         'AQAAAAIAAYagAAAAEDzzWWoiT7nnykl+8erbwXYh6DYpC9EO7Rnh/DFhzx1R2pdkUBltTQaL3/k60Uq/BQ==',
         'Admin', NULL, false, true, gen_random_uuid()::text, now()),
        (mentor_id, 'mentor_demo', 'mentor.demo@quiz.local',
         'AQAAAAIAAYagAAAAEB7bK/ID0O9kFUY2PhmAzbPayqyF4PtR+IY1PCeDb8L8dCfu03y+1yDBBY4E4i0ITg==',
         'Mentor', NULL, false, true, gen_random_uuid()::text, now()),
        (user_id, 'user_demo', 'user.demo@quiz.local',
         'AQAAAAIAAYagAAAAEIaKi+BX47VrcqNs2tXvpIg6Hv0UNPpeRcrGUh4IxPMwydoTC4eyQo86LyU/gnzGtA==',
         'User', NULL, false, true, gen_random_uuid()::text, now());

    -- 3. Subjects and decks.
    INSERT INTO "Subjects" ("UserId", "Name", "IsDeleted", "CreatedBy", "CreatedAt")
    VALUES (mentor_id, 'C# Fundamentals', false, seed_by, now())
    RETURNING "Id" INTO subject_csharp_id;

    INSERT INTO "Subjects" ("UserId", "Name", "IsDeleted", "CreatedBy", "CreatedAt")
    VALUES (mentor_id, 'PRN222 - ASP.NET Core MVC', false, seed_by, now())
    RETURNING "Id" INTO subject_aspnet_id;

    INSERT INTO "Decks" ("SubjectId", "Name", "TimeLimitMinutes", "IsDeleted", "CreatedBy", "CreatedAt")
    VALUES (subject_csharp_id, 'C# and OOP Basics', 20, false, seed_by, now())
    RETURNING "Id" INTO deck_csharp_id;

    INSERT INTO "Decks" ("SubjectId", "Name", "TimeLimitMinutes", "IsDeleted", "CreatedBy", "CreatedAt")
    VALUES (subject_aspnet_id, 'ASP.NET Core MVC', 30, false, seed_by, now())
    RETURNING "Id" INTO deck_mvc_id;

    INSERT INTO "Decks" ("SubjectId", "Name", "TimeLimitMinutes", "IsDeleted", "CreatedBy", "CreatedAt")
    VALUES (subject_aspnet_id, 'Entity Framework Core', 0, false, seed_by, now())
    RETURNING "Id" INTO deck_ef_id;

    -- 4. Questions, keyed by a stable code so answers and activity can refer to them.
    CREATE TEMP TABLE seed_question_map (code text PRIMARY KEY, id int NOT NULL) ON COMMIT DROP;

    WITH seeds (code, deck_id, question_type, content, explanation) AS (
        VALUES
        ('CS01', deck_csharp_id, 1, 'CLR trong .NET có vai trò chính là gì?', 'CLR quản lý việc thực thi mã .NET, bộ nhớ, ngoại lệ và bảo mật runtime.'),
        ('CS02', deck_csharp_id, 2, 'Những đặc tính cốt lõi của lập trình hướng đối tượng gồm những gì?', 'OOP thường được mô tả bằng đóng gói, kế thừa, đa hình và trừu tượng hóa.'),
        ('CS03', deck_csharp_id, 1, 'Trong C#, kiểu string có đặc điểm nào?', 'String là immutable, mỗi thao tác thay đổi thường tạo chuỗi mới.'),
        ('CS04', deck_csharp_id, 1, 'Interface thường được dùng để làm gì?', 'Interface mô tả hợp đồng hành vi mà class/struct triển khai.'),
        ('CS05', deck_csharp_id, 1, 'LINQ deferred execution nghĩa là gì?', 'Truy vấn chỉ được thực thi khi dữ liệu được duyệt hoặc materialize.'),
        ('CS06', deck_csharp_id, 2, 'Đâu là ví dụ của value type trong C#?', 'Value type thường lưu trực tiếp giá trị, ví dụ int, bool, struct.'),
        ('CS07', deck_csharp_id, 1, 'Khối finally trong try-catch-finally được chạy khi nào?', 'finally thường chạy sau try/catch, dù có exception hay không, trừ một số trường hợp đặc biệt như process bị dừng.'),
        ('MVC01', deck_mvc_id, 1, 'Controller trong mô hình MVC chịu trách nhiệm chính gì?', 'Controller tiếp nhận request, điều phối xử lý và chọn response/view phù hợp.'),
        ('MVC02', deck_mvc_id, 1, 'Razor View dùng để làm gì trong ASP.NET Core MVC?', 'Razor View render HTML động dựa trên model và cú pháp Razor.'),
        ('MVC03', deck_mvc_id, 1, 'ModelState.IsValid thường dùng để kiểm tra điều gì?', 'Nó kiểm tra dữ liệu binding/validation có hợp lệ theo ViewModel hay không.'),
        ('MVC04', deck_mvc_id, 2, 'Những Tag Helper nào thường dùng để tạo route/form binding?', 'Các asp-controller, asp-action, asp-for giúp Razor sinh HTML gắn với route/model.'),
        ('MVC05', deck_mvc_id, 1, 'Trong pipeline ASP.NET Core, UseAuthentication nên đặt ở đâu?', 'UseAuthentication nên đặt trước UseAuthorization để authorization đọc được user principal.'),
        ('MVC06', deck_mvc_id, 1, 'ViewModel giúp ích gì trong MVC?', 'ViewModel giới hạn dữ liệu cần hiển thị/nhận từ form và tránh bind trực tiếp entity quá mức.'),
        ('MVC07', deck_mvc_id, 1, 'TempData thường phù hợp cho tình huống nào?', 'TempData phù hợp truyền thông báo ngắn sau redirect, ví dụ success/error message.'),
        ('EF01', deck_ef_id, 1, 'DbContext trong Entity Framework Core đại diện cho điều gì?', 'DbContext đại diện cho phiên làm việc với database, quản lý DbSet, tracking và SaveChanges.'),
        ('EF02', deck_ef_id, 1, 'Include trong EF Core dùng để làm gì?', 'Include eager-load navigation property để lấy dữ liệu liên quan trong cùng truy vấn.'),
        ('EF03', deck_ef_id, 1, 'Migration trong EF Core dùng cho mục đích gì?', 'Migration mô tả thay đổi schema theo thời gian và giúp cập nhật database có kiểm soát.'),
        ('EF04', deck_ef_id, 1, 'AsNoTracking phù hợp khi nào?', 'AsNoTracking phù hợp cho truy vấn chỉ đọc, giảm chi phí change tracking.'),
        ('EF05', deck_ef_id, 2, 'Quan hệ nào đúng với schema Quiz Management hiện tại?', 'Schema hiện tại có User - Subject, Subject - Deck và Deck - Question theo dạng một-nhiều.'),
        ('EF06', deck_ef_id, 1, 'SaveChanges trong EF Core có tác dụng gì?', 'SaveChanges gửi các thay đổi đang được DbContext tracking xuống database.'),
        ('EF07', deck_ef_id, 1, 'Soft delete thường được hiểu là gì?', 'Soft delete đánh dấu dữ liệu đã xóa bằng cờ như IsDeleted thay vì xóa vật lý khỏi database.')
    ),
    inserted AS (
        INSERT INTO "Questions" ("DeckId", "Content", "Explanation", "QuestionType", "IsDeleted", "CreatedBy", "CreatedAt")
        SELECT deck_id, content, explanation, question_type, false, seed_by, now()
        FROM seeds
        RETURNING "Id", "DeckId", "Content"
    )
    INSERT INTO seed_question_map (code, id)
    SELECT s.code, i."Id"
    FROM seeds s
    JOIN inserted i ON i."DeckId" = s.deck_id AND i."Content" = s.content;

    -- 5. Answers.
    INSERT INTO "Answers" ("QuestionId", "Content", "IsCorrect")
    SELECT m.id, a.content, a.is_correct
    FROM (VALUES
        ('CS01', 'Quản lý thực thi mã .NET, bộ nhớ và exception', true),
        ('CS01', 'Thiết kế giao diện người dùng', false),
        ('CS01', 'Lưu trữ dữ liệu quan hệ', false),
        ('CS01', 'Tạo file CSS cho ứng dụng', false),
        ('CS02', 'Đóng gói', true),
        ('CS02', 'Kế thừa', true),
        ('CS02', 'Đa hình', true),
        ('CS02', 'Trừu tượng hóa', true),
        ('CS03', 'string là immutable', true),
        ('CS03', 'string luôn là value type', false),
        ('CS03', 'string không thể so sánh bằng Equals', false),
        ('CS03', 'string chỉ lưu được ASCII', false),
        ('CS04', 'Mô tả hợp đồng hành vi cần triển khai', true),
        ('CS04', 'Bắt buộc chứa constructor có tham số', false),
        ('CS04', 'Lưu dữ liệu vào database', false),
        ('CS04', 'Thay thế hoàn toàn mọi class', false),
        ('CS05', 'Truy vấn chỉ chạy khi được duyệt hoặc materialize', true),
        ('CS05', 'Truy vấn luôn chạy ngay khi khai báo', false),
        ('CS05', 'Truy vấn không bao giờ chạy trên database', false),
        ('CS05', 'Truy vấn chỉ dùng được với mảng', false),
        ('CS06', 'int', true),
        ('CS06', 'bool', true),
        ('CS06', 'struct tự định nghĩa', true),
        ('CS06', 'class tự định nghĩa', false),
        ('CS07', 'Sau try/catch, dù có exception hay không trong luồng bình thường', true),
        ('CS07', 'Chỉ chạy khi không có exception', false),
        ('CS07', 'Chỉ chạy trước try', false),
        ('CS07', 'Không bao giờ chạy nếu có catch', false),
        ('MVC01', 'Tiếp nhận request và điều phối response/view', true),
        ('MVC01', 'Chỉ chứa CSS của trang', false),
        ('MVC01', 'Là bảng dữ liệu trong SQL Server', false),
        ('MVC01', 'Chỉ dùng để lưu ảnh', false),
        ('MVC02', 'Render HTML động từ model và Razor syntax', true),
        ('MVC02', 'Thay thế hoàn toàn controller', false),
        ('MVC02', 'Chỉ chạy câu lệnh SQL', false),
        ('MVC02', 'Mã hóa password người dùng', false),
        ('MVC03', 'Dữ liệu gửi lên có hợp lệ theo validation hay không', true),
        ('MVC03', 'Người dùng có phải admin hay không', false),
        ('MVC03', 'Ứng dụng có kết nối internet hay không', false),
        ('MVC03', 'File CSS có tồn tại hay không', false),
        ('MVC04', 'asp-controller', true),
        ('MVC04', 'asp-action', true),
        ('MVC04', 'asp-for', true),
        ('MVC04', 'href cố định không qua Tag Helper', false),
        ('MVC05', 'Trước UseAuthorization', true),
        ('MVC05', 'Sau MapControllerRoute', false),
        ('MVC05', 'Chỉ đặt trong appsettings.json', false),
        ('MVC05', 'Không cần dùng khi có cookie auth', false),
        ('MVC06', 'Giới hạn dữ liệu hiển thị/nhận từ form', true),
        ('MVC06', 'Thay thế database', false),
        ('MVC06', 'Bắt buộc trùng 100% với entity', false),
        ('MVC06', 'Chỉ dùng cho file JavaScript', false),
        ('MVC07', 'Truyền thông báo ngắn sau redirect', true),
        ('MVC07', 'Lưu dữ liệu vĩnh viễn nhiều năm', false),
        ('MVC07', 'Thay thế session authentication', false),
        ('MVC07', 'Biên dịch Razor View', false),
        ('EF01', 'Phiên làm việc với database và change tracker', true),
        ('EF01', 'Một file CSS', false),
        ('EF01', 'Một loại cookie', false),
        ('EF01', 'Một trình duyệt web', false),
        ('EF02', 'Eager-load dữ liệu liên quan qua navigation property', true),
        ('EF02', 'Mã hóa password', false),
        ('EF02', 'Tạo branch Git', false),
        ('EF02', 'Xóa mọi dữ liệu trong database', false),
        ('EF03', 'Quản lý thay đổi schema database theo thời gian', true),
        ('EF03', 'Tự động viết README', false),
        ('EF03', 'Chỉ dùng để tạo CSS', false),
        ('EF03', 'Thay thế controller', false),
        ('EF04', 'Khi truy vấn chỉ đọc và không cần cập nhật entity', true),
        ('EF04', 'Khi bắt buộc sửa entity ngay sau query', false),
        ('EF04', 'Khi muốn bật tracking mạnh hơn', false),
        ('EF04', 'Khi muốn bỏ qua validation form', false),
        ('EF05', 'User có nhiều Subject', true),
        ('EF05', 'Subject có nhiều Deck', true),
        ('EF05', 'Deck có nhiều Question', true),
        ('EF05', 'Answer có nhiều Question làm parent trực tiếp', false),
        ('EF06', 'Ghi các thay đổi đang tracking xuống database', true),
        ('EF06', 'Chỉ render HTML', false),
        ('EF06', 'Tự động đăng nhập người dùng', false),
        ('EF06', 'Tạo file PDF', false),
        ('EF07', 'Đánh dấu IsDeleted thay vì xóa vật lý', true),
        ('EF07', 'Xóa thẳng record khỏi database trong mọi trường hợp', false),
        ('EF07', 'Tự động tạo tài khoản admin', false),
        ('EF07', 'Chỉ áp dụng cho file ảnh', false)
    ) AS a (code, content, is_correct)
    JOIN seed_question_map m ON m.code = a.code;

    SELECT id INTO mvc04_id FROM seed_question_map WHERE code = 'MVC04';
    SELECT id INTO mvc05_id FROM seed_question_map WHERE code = 'MVC05';
    SELECT id INTO ef03_id FROM seed_question_map WHERE code = 'EF03';
    SELECT id INTO cs03_id FROM seed_question_map WHERE code = 'CS03';

    -- 6. One completed quiz attempt on the MVC deck: MVC01-04 correct, MVC05 wrong (8/10).
    CREATE TEMP TABLE seed_selected_answers (
        question_id int NOT NULL,
        answer_id int NOT NULL,
        PRIMARY KEY (question_id, answer_id)
    ) ON COMMIT DROP;

    INSERT INTO seed_selected_answers (question_id, answer_id)
    SELECT m.id, a."Id"
    FROM seed_question_map m
    JOIN "Answers" a ON a."QuestionId" = m.id AND a."IsCorrect"
    WHERE m.code IN ('MVC01', 'MVC02', 'MVC03', 'MVC04');

    INSERT INTO seed_selected_answers (question_id, answer_id)
    SELECT mvc05_id, a."Id"
    FROM "Answers" a
    WHERE a."QuestionId" = mvc05_id AND NOT a."IsCorrect"
    ORDER BY a."Id"
    LIMIT 1;

    SELECT '[' || string_agg(id::text, ',' ORDER BY code) || ']'
    INTO question_ids_json
    FROM seed_question_map
    WHERE code IN ('MVC01', 'MVC02', 'MVC03', 'MVC04', 'MVC05');

    INSERT INTO "QuizAttempts"
        ("Id", "UserId", "DeckId", "QuestionIdsJson", "TimeLimitMinutes", "StartedAtUtc", "ExpiresAtUtc", "CompletedAtUtc")
    VALUES
        (attempt_id, user_id, deck_mvc_id, question_ids_json, 30,
         attempt_started_at, attempt_started_at + interval '30 minutes', attempt_completed_at);

    -- Same shape as BusinessObjects.QuizResultSnapshot.
    SELECT json_build_object(
        'DeckName', d."Name",
        'SubjectName', s."Name",
        'Questions', (
            SELECT json_agg(json_build_object(
                'QuestionId', q."Id",
                'Content', q."Content",
                'Explanation', q."Explanation",
                'QuestionType', q."QuestionType",
                'IsCorrect', m.code <> 'MVC05',
                'Answers', (
                    SELECT json_agg(json_build_object(
                        'AnswerId', a."Id",
                        'Content', a."Content",
                        'IsCorrectAnswer', a."IsCorrect",
                        'WasSelected', EXISTS (
                            SELECT 1 FROM seed_selected_answers sel
                            WHERE sel.question_id = a."QuestionId" AND sel.answer_id = a."Id"))
                        ORDER BY a."Id")
                    FROM "Answers" a
                    WHERE a."QuestionId" = q."Id"))
                ORDER BY m.code)
            FROM seed_question_map m
            JOIN "Questions" q ON q."Id" = m.id
            WHERE m.code IN ('MVC01', 'MVC02', 'MVC03', 'MVC04', 'MVC05')))::text
    INTO result_snapshot_json
    FROM "Decks" d
    JOIN "Subjects" s ON s."Id" = d."SubjectId"
    WHERE d."Id" = deck_mvc_id;

    INSERT INTO "TestHistories"
        ("UserId", "DeckId", "QuizAttemptId", "ResultSnapshotJson", "Score", "Percentage", "CreatedAt")
    VALUES
        (user_id, deck_mvc_id, attempt_id, result_snapshot_json, 8.00, 80.00, attempt_completed_at)
    RETURNING "Id" INTO history_id;

    INSERT INTO "TestResultDetails" ("TestHistoryId", "QuestionId", "SelectedAnswerId", "IsCorrect")
    SELECT history_id, sel.question_id, sel.answer_id, sel.question_id <> mvc05_id
    FROM seed_selected_answers sel;

    -- 7. Question reports and login history.
    INSERT INTO "QuestionReports" ("QuestionId", "UserId", "Reason", "Note", "IsResolved", "CreatedAt")
    VALUES
        (ef03_id, user_id, 'UnclearQuestion', 'Demo báo cáo đang chờ Mentor/Admin xử lý.', false, now() - interval '12 hours'),
        (cs03_id, user_id, 'Other', 'Demo báo cáo đã được xử lý.', true, now() - interval '2 days');

    INSERT INTO "LoginAttempts" ("Email", "IpAddress", "IsSuccess", "CountsTowardLockout", "UserId", "CreatedAt")
    VALUES
        ('user.demo@quiz.local', '127.0.0.1', true, false, user_id, now() - interval '2 hours'),
        ('user.demo@quiz.local', '127.0.0.1', false, false, user_id, now() - interval '3 hours'),
        ('unknown.demo@quiz.local', '127.0.0.2', false, false, NULL, now() - interval '4 hours');

    -- 8. Study rooms: enough public rooms to exercise search, filters and pagination.
    CREATE TEMP TABLE seed_group_map (code text PRIMARY KEY, id int NOT NULL) ON COMMIT DROP;
    FOR group_seed IN
        SELECT * FROM (VALUES
            ('cs-basics', 'C# căn bản', 'Hỏi đáp về cú pháp, kiểu dữ liệu và .NET.', subject_csharp_id, mentor_id, 0, 16),
            ('cs-oop', 'Lập trình hướng đối tượng', 'Luyện class, interface, kế thừa và đa hình.', subject_csharp_id, mentor_id, 0, 15),
            ('cs-linq', 'LINQ cùng nhau', 'Thảo luận truy vấn LINQ và bài tập thực hành.', subject_csharp_id, mentor_id, 1, 14),
            ('cs-debug', 'Gỡ lỗi C#', 'Chia sẻ cách đọc lỗi và tìm nguyên nhân.', subject_csharp_id, mentor_id, 0, 13),
            ('asp-mvc', 'ASP.NET Core MVC', 'Cùng làm controller, Razor View và routing.', subject_aspnet_id, mentor_id, 0, 12),
            ('asp-auth', 'Authentication & Authorization', 'Trao đổi về cookie, role và phân quyền.', subject_aspnet_id, mentor_id, 1, 11),
            ('asp-api', 'Thiết kế Web API', 'REST API, validation và xử lý lỗi.', subject_aspnet_id, mentor_id, 0, 10),
            ('asp-razor', 'Razor View thực hành', 'Tag Helper, form và giao diện MVC.', subject_aspnet_id, mentor_id, 0, 9),
            ('ef-query', 'Entity Framework Core', 'DbContext, quan hệ và truy vấn dữ liệu.', subject_aspnet_id, mentor_id, 0, 8),
            ('ef-migrations', 'EF Core Migrations', 'Thực hành migration và xử lý schema.', subject_aspnet_id, mentor_id, 1, 7),
            ('ef-performance', 'Tối ưu truy vấn EF', 'AsNoTracking, phân trang và hiệu năng.', subject_aspnet_id, mentor_id, 0, 6),
            ('prn-project', 'Đồ án PRN232', 'Trao đổi tiến độ và kinh nghiệm làm đồ án.', subject_aspnet_id, mentor_id, 1, 5),
            ('career', 'Hỏi đáp nghề .NET', 'Chia sẻ lộ trình học và kinh nghiệm phỏng vấn.', NULL::int, admin_id, 0, 4),
            ('general', 'Góc học tập chung', 'Cùng đặt câu hỏi và tìm bạn học.', NULL::int, admin_id, 0, 3),
            ('mentor-private', 'Nhóm ôn tập riêng', 'Phòng chỉ dành cho thành viên được mời.', subject_aspnet_id, mentor_id, 2, 2),
            ('team-private', 'Nhóm dự án riêng', 'Phòng thảo luận nội bộ theo lời mời.', NULL::int, admin_id, 2, 1)
        ) AS rooms (code, name, description, subject_id, owner_id, access_mode, age_days)
    LOOP
        INSERT INTO "StudyGroups"
            ("Name", "Description", "SubjectId", "CreatedByUserId", "AccessMode", "CreatedAt")
        VALUES (group_seed.name, group_seed.description, group_seed.subject_id,
            group_seed.owner_id, group_seed.access_mode, now() - make_interval(days => group_seed.age_days))
        RETURNING "Id" INTO group_id;
        INSERT INTO seed_group_map (code, id) VALUES (group_seed.code, group_id);
        INSERT INTO "ChatChannels" ("GroupId", "Name", "IsDefault", "IsArchived", "CreatedAt")
        VALUES (group_id, 'chung', true, false, now());
    END LOOP;

    INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "Role", "JoinedAt")
    SELECT m.id, g."CreatedByUserId", 2, g."CreatedAt"
    FROM seed_group_map m JOIN "StudyGroups" g ON g."Id" = m.id;

    INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "Role", "JoinedAt")
    SELECT m.id, user_id, 0, now() - interval '1 day'
    FROM seed_group_map m
    WHERE m.code IN ('cs-basics', 'asp-mvc', 'ef-query', 'mentor-private');

    INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "Role", "JoinedAt")
    SELECT m.id, admin_id, 0, now() - interval '1 day'
    FROM seed_group_map m
    WHERE m.code IN ('cs-basics', 'asp-mvc', 'prn-project');

    INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "Role", "JoinedAt")
    SELECT m.id, mentor_id, 0, now() - interval '1 day'
    FROM seed_group_map m
    WHERE m.code IN ('career', 'general');

    INSERT INTO "GroupMessages" ("GroupId", "ChannelId", "SenderUserId", "Content", "SentAt", "Revision")
    SELECT m.id, c."Id", g."CreatedByUserId", 'Chào mừng bạn đến với ' || g."Name" || '! Hãy giới thiệu bản thân và đặt câu hỏi.',
           g."CreatedAt" + interval '1 hour', 0
    FROM seed_group_map m JOIN "StudyGroups" g ON g."Id" = m.id
    JOIN "ChatChannels" c ON c."GroupId" = m.id AND c."IsDefault";

    INSERT INTO "GroupMessages" ("GroupId", "ChannelId", "SenderUserId", "Content", "SentAt", "Revision")
    SELECT m.id, c."Id", user_id, 'Mình đang ôn phần này. Mọi người có tài liệu hoặc ví dụ nào hay không?', now() - interval '12 hours', 0
    FROM seed_group_map m JOIN "ChatChannels" c ON c."GroupId" = m.id AND c."IsDefault"
    WHERE m.code IN ('asp-mvc', 'ef-query', 'mentor-private');

    INSERT INTO "StudyGroupJoinRequests" ("GroupId", "UserId", "Status", "RequestedAt")
    SELECT m.id, user_id, 0, now() - interval '3 hours'
    FROM seed_group_map m WHERE m.code = 'asp-auth';

    INSERT INTO "StudyGroupInvitations"
        ("GroupId", "UserId", "InvitedByUserId", "Status", "InvitedAt", "ExpiresAt")
    SELECT m.id, user_id, admin_id, 0, now() - interval '1 hour', now() + interval '7 days'
    FROM seed_group_map m WHERE m.code = 'team-private';
END
$seed$;

SELECT "Username", "Email", "Role", 'Test@123456' AS "DemoPassword"
FROM "Users"
WHERE "Id" IN ('seed-admin-001', 'seed-mentor-001', 'seed-user-001')
ORDER BY "Role";

SELECT
    (SELECT COUNT(*) FROM "Subjects" WHERE "UserId" = 'seed-mentor-001') AS "SubjectCount",
    (SELECT COUNT(*) FROM "Decks" d JOIN "Subjects" s ON s."Id" = d."SubjectId"
     WHERE s."UserId" = 'seed-mentor-001') AS "DeckCount",
    (SELECT COUNT(*) FROM "Questions" q JOIN "Decks" d ON d."Id" = q."DeckId"
     JOIN "Subjects" s ON s."Id" = d."SubjectId" WHERE s."UserId" = 'seed-mentor-001') AS "QuestionCount",
    (SELECT COUNT(*) FROM "StudyGroups" WHERE "CreatedByUserId" IN
        ('seed-admin-001', 'seed-mentor-001')) AS "GroupCount";
