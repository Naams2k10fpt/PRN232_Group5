-- Add sample subjects, decks, questions and answers to one existing account.
-- Run db/seed-demo.sql first, then set quiz.seed_target_user_id for this session.
-- Example: psql "<connection>" -c "SET quiz.seed_target_user_id = '<user-id>'" -f db/seed-user-sample.sql
-- Existing user data is never deleted. Re-running this script is a no-op once seeded.

DO $seed_user$
DECLARE
    target_user_id text := current_setting('quiz.seed_target_user_id', true);
    source_subject record;
    source_deck record;
    source_question record;
    new_id int;
    sample_marker CONSTANT text := 'QuizManagementSample';
BEGIN
    IF target_user_id IS NULL OR target_user_id = ''
       OR NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = target_user_id AND NOT "IsDisabled") THEN
        RAISE EXCEPTION 'Set quiz.seed_target_user_id to an existing active user ID before seeding.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "Subjects" WHERE "UserId" = 'seed-mentor-001') THEN
        RAISE EXCEPTION 'Run db/seed-demo.sql before db/seed-user-sample.sql.';
    END IF;
    IF EXISTS (SELECT 1 FROM "Subjects" WHERE "UserId" = target_user_id AND "CreatedBy" = sample_marker) THEN
        RAISE NOTICE 'Sample subjects already exist for this user; no rows added.';
        RETURN;
    END IF;

    CREATE TEMP TABLE sample_subject_map (source_id int PRIMARY KEY, new_id int NOT NULL) ON COMMIT DROP;
    CREATE TEMP TABLE sample_deck_map (source_id int PRIMARY KEY, new_id int NOT NULL) ON COMMIT DROP;
    CREATE TEMP TABLE sample_question_map (source_id int PRIMARY KEY, new_id int NOT NULL) ON COMMIT DROP;

    FOR source_subject IN
        SELECT "Id", "Name" FROM "Subjects"
        WHERE "UserId" = 'seed-mentor-001' AND NOT "IsDeleted"
        ORDER BY "Id"
    LOOP
        INSERT INTO "Subjects" ("UserId", "Name", "IsDeleted", "CreatedBy", "CreatedAt")
        VALUES (target_user_id, source_subject."Name", false, sample_marker, now())
        RETURNING "Id" INTO new_id;
        INSERT INTO sample_subject_map VALUES (source_subject."Id", new_id);
    END LOOP;

    FOR source_deck IN
        SELECT d."Id", d."Name", d."TimeLimitMinutes", m.new_id AS subject_id
        FROM "Decks" d JOIN sample_subject_map m ON m.source_id = d."SubjectId"
        WHERE NOT d."IsDeleted" ORDER BY d."Id"
    LOOP
        INSERT INTO "Decks" ("SubjectId", "Name", "TimeLimitMinutes", "IsDeleted", "CreatedBy", "CreatedAt")
        VALUES (source_deck.subject_id, source_deck."Name", source_deck."TimeLimitMinutes", false, sample_marker, now())
        RETURNING "Id" INTO new_id;
        INSERT INTO sample_deck_map VALUES (source_deck."Id", new_id);
    END LOOP;

    FOR source_question IN
        SELECT q."Id", q."Content", q."Explanation", q."QuestionType", m.new_id AS deck_id
        FROM "Questions" q JOIN sample_deck_map m ON m.source_id = q."DeckId"
        WHERE NOT q."IsDeleted" ORDER BY q."Id"
    LOOP
        INSERT INTO "Questions"
            ("DeckId", "Content", "Explanation", "QuestionType", "IsDeleted", "CreatedBy", "CreatedAt")
        VALUES (source_question.deck_id, source_question."Content", source_question."Explanation",
            source_question."QuestionType", false, sample_marker, now())
        RETURNING "Id" INTO new_id;
        INSERT INTO sample_question_map VALUES (source_question."Id", new_id);
    END LOOP;

    INSERT INTO "Answers" ("QuestionId", "Content", "IsCorrect")
    SELECT m.new_id, a."Content", a."IsCorrect"
    FROM "Answers" a JOIN sample_question_map m ON m.source_id = a."QuestionId";
END
$seed_user$;
