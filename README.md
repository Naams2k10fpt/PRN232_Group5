https://prn232-group5.onrender.com/

## Mobile API

REST API for the PRM393 mobile app, next to the web app.

| | Local | Render |
| --- | --- | --- |
| Base URL | `http://localhost:5039` (Android emulator: `http://10.0.2.2:5039`) | `https://prn232-group5.onrender.com` |
| Swagger UI | `/swagger` (always on in Development) | `/swagger` (when `Swagger__Enabled=true`) |

1. `POST /api/auth/login` with `{ "email": "user.demo@quiz.local", "password": "Test@123456" }` → `accessToken`.
2. Send it as `Authorization: Bearer <accessToken>` (SignalR: `?access_token=<accessToken>` on `/hubs/*`).
   In Swagger UI, click **Authorize** and paste the token.
3. Errors are always `{ "code", "message", "errors"?, "retryAfterSeconds"? }`. Switch on `code`, show `message`.

| Endpoint | Returns |
| --- | --- |
| `GET /api/subjects?search=&page=1&pageSize=20` | Subjects with deck and question counts (paged, `pageSize` ≤ 100) |
| `GET /api/subjects/{id}` | One subject |
| `GET /api/subjects/{subjectId}/decks` | Decks of a subject with the user's flashcard progress (`dueCount`, `learnedCount`, `nextReviewAtUtc`) |
| `GET /api/decks/{id}` | One deck with the user's flashcard progress |
| `GET /api/decks/{deckId}/flashcards?mode=due` | Flashcards to study: new and due cards (`mode=all` for every card); `content`/`explanation` are Markdown |
| `POST /api/flashcards/progress` | Saves one review `{ deckId, questionId, remembered }` and returns the card's next review time |

Postman: import `docs/postman/QuizManagement.postman_collection.json`, set `baseUrl`, run **Auth / Login** first.

New `/api/*` controllers: add `[ApiController]` and `[ApiAuthorize]` (bearer only, 401/403 as JSON); see
`Controllers/Api/ProfileApiController.cs`.
