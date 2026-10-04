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

Postman: import `docs/postman/QuizManagement.postman_collection.json`, set `baseUrl`, run **Auth / Login** first.

New `/api/*` controllers: add `[ApiController]` and `[ApiAuthorize]` (bearer only, 401/403 as JSON); see
`Controllers/Api/ProfileApiController.cs`.
