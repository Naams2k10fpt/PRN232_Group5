# TV3: Quiz cá nhân và Group Chat

Tài liệu này là hợp đồng backend để phát triển ứng dụng mobile sau giai đoạn web PRN.
Các route `/api/*` dưới đây dùng `Authorization: Bearer <accessToken>` lấy từ
`POST /api/auth/login`. Trang chat web dùng cookie đăng nhập và các action MVC
`/StudyGroups/*`. Hub `/hubs/chat` nhận cả cookie web lẫn JWT; SignalR có thể
gửi JWT qua `access_token` trên đường dẫn hub.

## Quiz

| Route | Yêu cầu | Kết quả |
| --- | --- | --- |
| `POST /api/quiz/start` | `{ "deckId": 7, "questionCount": 10 }` | `attemptId`, `startedAtUtc`, `expiresAtUtc`, `questions` (không có cờ đáp án đúng) |
| `GET /api/quiz/attempts/{attemptId}?deckId=7` | Lấy lại lượt thi đang còn hiệu lực | Cùng thứ tự câu hỏi đã chọn khi bắt đầu |
| `POST /api/quiz/submit` | Xem ví dụ bên dưới | Kết quả chi tiết gồm `score`, `percentage`, `questions` |
| `GET /api/history/user?page=1` | Lịch sử của người hiện tại, 20 mục/trang | `page`, `pageSize`, `totalCount`, `items` |
| `GET /api/history/{id}` | Chi tiết một bài đã làm | Cùng dạng kết quả chi tiết khi nộp bài |
| `POST /api/quiz/reports` | `{ "testHistoryId": 1, "questionId": 21, "reason": "WrongAnswer", "note": "..." }` | `204`; `409 report_pending` nếu đã có báo cáo đang chờ |

```json
{
  "attemptId": "11111111-1111-1111-1111-111111111111",
  "deckId": 7,
  "answers": [
    { "questionId": 21, "selectedAnswerIds": [84] },
    { "questionId": 22, "selectedAnswerIds": [89, 90] }
  ]
}
```

`questionCount` mặc định là tối đa 10 câu khi bỏ trống và không được vượt quá
500 hoặc số câu của bộ đề. `expiresAtUtc = null` nghĩa là không giới hạn thời
gian. Server chấp nhận nộp bài trong 10 giây sau hạn để bù độ trễ của thao tác
tự nộp trên web; không cho mở lại lượt thi sau hạn. Nộp lại cùng `attemptId`
trả về kết quả đã lưu, không tạo lịch sử thứ hai.

Kết quả có `testHistoryId`, `deckId`, `createdAt` và `questionId` của mỗi câu. Báo cáo chỉ chấp nhận câu có trong lịch sử của chính người gọi và còn tồn tại. `reason`: `WrongAnswer`, `UnclearQuestion`, `DuplicateQuestion`, `Other`; `note` tối đa 500 ký tự.

## Nhóm và tin nhắn

| Route | Công dụng |
| --- | --- |
| `GET /api/groups` | Các nhóm người dùng đang tham gia, gồm `unreadCount` |
| `GET /api/groups/discover` | Các nhóm có thể khám phá |
| `GET /api/groups/discovery?search=&accessMode=0&subjectId=1&sort=popular&page=1` | Khám phá có lọc, sắp xếp và phân trang: `groups`, `totalCount`, `page`, `pageSize`, `pageCount` |
| `GET /api/groups/options` | `publicSubjects` dùng lọc và `mySubjects` dùng tạo nhóm |
| `GET /api/groups/invitations` | Lời mời đang chờ của chính người gọi |
| `POST /api/groups` | Tạo nhóm: `{ "name": "Nhóm học", "description": null, "subjectId": null, "accessMode": 0 }` |
| `GET /api/groups/{id}` | Chi tiết nhóm của thành viên |
| `POST /api/groups/{id}/join` | Tham gia nhóm công khai; trả `202` nếu phải chờ duyệt |
| `DELETE /api/groups/{id}/join-request` | Hủy yêu cầu tham gia đang chờ |
| `GET /api/groups/{id}/messages?beforeId=123&limit=30` | Tin cũ; tối đa 50, trả theo thứ tự cũ đến mới |
| `POST /api/groups/{id}/read` | Đánh dấu đã đọc: `{ "lastMessageId": 123 }` |
| `GET /api/groups/{id}/management` | Người quản lý: `requests`, `banned`, `inviteLinks` (link chỉ trả cho chủ phòng) |
| `POST /api/groups/{id}/invitations` | Người quản lý mời bằng `{ "email": "..." }` |
| `POST /api/groups/{id}/invitation-response` | Người nhận trả lời `{ "accept": true }` |
| `POST /api/groups/{id}/requests/{userId}/review` | Người quản lý duyệt/từ chối `{ "accept": true }` |
| `PUT /api/groups/{id}/access` | Chủ phòng đổi `{ "accessMode": 2 }`; hủy yêu cầu/lời mời/link cũ |
| `PUT /api/groups/{id}/members/{userId}/moderator` | Chủ phòng đặt `{ "moderator": true }` |
| `POST /api/groups/{id}/ownership` | Chủ phòng chuyển quyền bằng `{ "userId": "..." }` |
| `DELETE /api/groups/{id}/members/{userId}` | Người quản lý loại thành viên có vai trò thấp hơn; phát sự kiện thu hồi quyền |
| `DELETE /api/groups/{id}/bans/{userId}` | Người quản lý bỏ chặn |
| `POST /api/groups/{id}/leave` | Thành viên rời nhóm; chủ phòng phải chuyển quyền trước |
| `POST /api/groups/{id}/invite-links` | Chủ phòng tạo link: `{ "durationDays": 7 }`, trả `{ "token": "..." }` |
| `DELETE /api/groups/{id}/invite-links/{linkId}` | Chủ phòng thu hồi link |
| `GET /api/groups/invite/{token}` | Xem trước: `groupId`, `groupName`, `expiresAt` |
| `POST /api/groups/invite/{token}` | Xác nhận tham gia, trả `{ "id": groupId }` |

`accessMode` là số: `0` công khai, `1` cần duyệt, `2` chỉ có lời mời.
Trong tin nhắn, `kind` là `0` tin chat, `1` thành viên vào, `2` thành viên rời.
Chi tiết nhóm trả thêm `myRole` và `members`; vai trò `0` thành viên, `1` người quản lý, `2` chủ phòng. API quản lý trả `403` JSON khi thiếu quyền. Link hết hạn/thu hồi trả `410 invite_expired/invite_revoked`; link không tồn tại trả `404 invite_not_found`. Response chứa token có `Cache-Control: no-store` và `Referrer-Policy: no-referrer`.
`beforeId` là ID tin nhắn đầu tiên đang hiển thị; bỏ trống để lấy trang mới nhất.

Trên web, chủ phòng có thể mở **Phòng chat → Quản lý → Link mời vào nhóm** để
tạo nhiều link, chọn thời hạn 1 ngày, 7 ngày, 30 ngày hoặc vĩnh viễn. Chủ phòng
có thể sao chép và thu hồi từng link trong danh sách. Người nhận phải đăng nhập
bằng tài khoản đã xác minh, mở link và xác nhận tham gia. Link bị thu hồi, hết hạn
hoặc thuộc nhóm đã đổi chế độ sẽ không dùng được; tài khoản bị chặn không thể
tham gia bằng link. Token của link được mã hóa trong database để chủ phòng có
thể sao chép lại khi cần. Luồng mobile dùng các API JWT ở bảng trên; luồng web MVC vẫn hoạt động.
Khi mở link hết hạn hoặc đã bị thu hồi, web hiển thị trang thông báo riêng với
mã HTTP `410`; link không tồn tại vẫn trả `404`.

Kết nối `/hubs/chat`, gọi `JoinGroup(groupId)` khi mở phòng và `LeaveGroup(groupId)`
khi rời phòng. Gửi bằng `SendMessage(groupId, content)`; nội dung từ 1 đến 2000
ký tự. Hub phát `ReceiveMessage` cho thành viên, `RoomRosterChanged` khi danh
sách thành viên đổi, `RoomPresenceChanged` cho trạng thái online,
`WebsitePresenceDelta` cho trạng thái trang web, và `GroupAccessRevoked` khi
người dùng bị thu hồi quyền vào nhóm. Có thể gọi `GetRoomRoster(groupId)` để
đồng bộ lại sau khi kết nối lại.

Các lỗi API trả JSON `{ "code": "...", "message": "..." }`; lỗi validation có
thêm `errors`. Mobile nên dựa vào `code`, không so khớp câu thông báo tiếng Việt.
