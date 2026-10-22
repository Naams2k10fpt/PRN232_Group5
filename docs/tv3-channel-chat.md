# TV3: Channel chat and study files

Study groups act as servers. Each group has a default `#chung` text channel, and owners/moderators can create, rename, update the topic of, and archive additional channels. All active channels share the group's membership rules; a channel is not a separate access boundary.

The interaction model follows Discord's [text-channel guide](https://support.discord.com/hc/en-us/articles/4412085582359-Text-Channels-Text-Chat-In-Voice-Channels) and [message resource](https://github.com/discord/discord-api-docs/blob/main/developers/resources/message.mdx): a channel timeline, direct replies, author edits, moderation, reactions, and cursor-based history. This implementation uses the existing ASP.NET Core, PostgreSQL, SignalR and Flutter stack.

## Behaviour

- A member can send text and up to three attachments, reply to a non-deleted chat message in the same channel, edit their own text, and delete their own message. Owners and moderators may also delete other members' messages.
- Supported reactions: 👍 ❤️ 😂 🎉 🤔 👀 ✅ ❌. Setting a reaction is idempotent for each member/emoji pair.
- A deletion leaves a timeline tombstone and removes the original content, file bodies and reactions. Replies display that the source was deleted. Deleted messages cannot be edited, reacted to or downloaded.
- Files: PNG/JPEG/GIF/WebP, PDF, TXT/CSV, ZIP, DOCX/XLSX/PPTX; at most **5 MiB per file**, **three per message**, **100 MiB of stored attachments per group**. Images require a matching file signature. HTML, SVG, executables and legacy Office formats are not accepted. Files are downloaded as attachments; validated image types may be previewed inline with `nosniff` and a sandbox response policy.
- File bytes live in PostgreSQL (`bytea`), outside all public static-file routes. This avoids Render's ephemeral filesystem. Only current group members can download them; leaving, removal, bans and archived channels revoke access. File metadata is projected separately from bytes when fetching history.
- Upload and message creation are one database transaction. A client-generated `clientMessageId` prevents duplicate messages/files when an acknowledgement is lost. Concurrent uploads serialize the storage-quota check for their group.
- Read cursors are per channel and advance atomically with `GREATEST`, so delayed requests cannot restore an old unread position. Archived channels and deleted messages do not contribute to group unread counts.
- After reconnect, clients page forward from their last message until all missed pages are loaded. They also reconcile edits/deletions/reactions in the previously loaded range. Revisions reject older updates. An update to an unloaded message does not insert an isolated row into the timeline.
- Web: channel sidebar, timeline, member panel; compact channel navigation on smaller screens. Enter sends; Shift+Enter starts a new line. Flutter: channel drawer on phones, sidebars on larger screens, draft text/files/reply retained separately per channel while the room is open.

Voice/video channels are documented in [voice rooms](voice-rooms.md). Direct messages, threads, custom emoji, per-channel permission overrides, and OS push notifications remain outside this release.

## Routes

All routes support mobile JWT and the existing web cookie. Cookie writes additionally require `RequestVerificationToken`; no bearer token is placed in a file URL.

| Route | Meaning |
| --- | --- |
| `GET /api/groups/{groupId}/channels` | Active channels and the caller's unread counts |
| `POST /api/groups/{groupId}/channels` | Manager creates `{ name, topic }` |
| `PUT /api/groups/{groupId}/channels/{channelId}` | Manager updates name/topic |
| `DELETE /api/groups/{groupId}/channels/{channelId}` | Manager archives a non-default channel |
| `GET .../channels/{channelId}/messages?beforeId=&afterId=&limit=30` | One cursor at a time; forward pages use ascending IDs; limit 1–100 |
| `POST .../channels/{channelId}/messages` | Multipart `content`, optional `replyToId`, optional UUID `clientMessageId`, repeated `files` |
| `PUT .../messages/{messageId}` | Author edits `{ content }` |
| `DELETE .../messages/{messageId}` | Author/manager deletes the message and files |
| `PUT .../messages/{messageId}/reaction` | Member sets `{ emoji, active }` |
| `POST .../channels/{channelId}/read` | Advance `{ lastMessageId }` |
| `GET /api/chat/attachments/{uuid}?download=true` | Authorized file download; omit `download` for an image preview |

`ReceiveMessage` delivers a new message. `MessageUpdated` delivers edit/delete/reaction updates. `ChannelsChanged(groupId)` tells clients to reload channels. Message payloads include `channelId`, `revision`, `editedAt`, `deletedAt`, `reply` (with its source revision), attachment metadata and flat `{ emoji, userId }` reactions. Existing presence and access-revocation events remain in use.

Legacy group-level message/history routes and `ChatHub.SendMessage` use `#chung`.

## Migration and verification

`20261022050153_AddDiscordStyleChat` creates a default channel for every existing group, assigns all existing messages to it, and copies existing read positions. It changes no quiz data. Apply the migration before serving the new clients:

```powershell
dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
```

Checks cover migration with existing messages, membership and moderation, cross-channel reply rejection, retries, file validation/download revocation, forward pagination and monotonic read positions. Flutter covers channel drafts, reply targets, multi-page reconnect, stale updates, deleted quotes and attachment actions. Live tests use a separate disposable local database with two actual SignalR clients. Browser verification uses `QuizChatPreview_20261022`, not the developer's existing database.

The web/Flutter builds and automated checks do not replace testing Android/iOS file pickers on physical devices. Archived channels retain database history; this release exposes archive, not a restore UI.
