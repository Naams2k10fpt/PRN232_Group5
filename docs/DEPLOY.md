# Triển khai Quiz Management

Hệ thống gồm 3 phần, đều dùng gói miễn phí:

| Phần | Dịch vụ | Vai trò |
| --- | --- | --- |
| Web app | **Render** (Docker web service, Singapore) | Chạy ASP.NET Core 9 MVC từ `Dockerfile` |
| Database | **Neon** PostgreSQL (Singapore) | Dữ liệu + khóa Data Protection (cookie đăng nhập, link email) |
| Email | **Brevo** Transactional API | Gửi email xác minh/đặt lại mật khẩu qua HTTPS (port 443) |

```
Trình duyệt ──HTTPS──> Render (web app) ──SSL──> Neon (PostgreSQL)
                            └──HTTPS 443──> Brevo API ──> hộp thư người dùng
```

## 1. Tài khoản dùng chung (copy-paste)

Link bản deploy: **`https://<ten-service>.onrender.com`** — cập nhật dòng này sau lần deploy đầu tiên.

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Admin | `admin.demo@quiz.local` | `Test@123456` |
| Mentor | `mentor.demo@quiz.local` | `Test@123456` |
| User | `user.demo@quiz.local` | `Test@123456` |

- Các tài khoản này có sẵn cả ở database local (sau khi chạy seed) và trên Neon `production`.
- Repository là **private**, nên chỉ thành viên nhóm xem được mật khẩu. Nếu repo chuyển sang public, hãy đổi mật khẩu các tài khoản trên bản deploy trước.
- Lần mở đầu tiên sau 15 phút không dùng, Render cần khoảng 30–60 giây để khởi động lại container. Đó là bình thường với gói Free.

## 2. Phát triển local

Cách chạy local không đổi so với trước: `dotnet run` / Visual Studio trên cổng `launchSettings.json`, email ghi ra console.

1. PostgreSQL local (chọn một):
   - `docker compose up db` — PostgreSQL 17 trên `localhost:5432` (user/pass `postgres`/`postgres`, database `quizmanagement`).
   - Hoặc PostgreSQL 16+ cài sẵn trên máy.
2. Cấu hình:
   ```powershell
   Copy-Item .\QuizManagement\appsettings.Local.example.json .\QuizManagement\appsettings.Local.json
   ```
   File mẫu đã trỏ tới `localhost` và đặt `Email:Provider = Log`.
3. Tạo schema và dữ liệu demo:
   ```powershell
   dotnet tool restore
   dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
   psql "host=localhost dbname=quizmanagement user=postgres password=postgres" -f db/seed-demo.sql
   ```
   Seed tạo ba tài khoản `admin_demo`, `mentor_demo`, `user_demo`. Các phòng mẫu do `admin_demo` hoặc `mentor_demo` làm chủ; số thành viên trên thẻ phòng gồm cả chủ phòng. Tài khoản tự đăng ký trên máy local không tự được thêm vào phòng mẫu mà cần bấm **Tham gia** hoặc gửi yêu cầu tham gia.
4. Chạy: `dotnet run --project QuizManagement --launch-profile http` → http://localhost:5039

Muốn thử đúng image chạy trên Render: `docker compose up` (db + web) → http://localhost:8080. Container tự áp dụng migration khi khởi động.

**Thứ tự cấu hình:** `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` → biến môi trường. Biến môi trường luôn thắng, nên có thể trỏ tạm sang database khác mà không sửa file.

## 3. Triển khai lên cloud

### 3.1. Neon (database) — đã tạo

- Project `withered-sun-79522954`, region `aws-ap-southeast-1`, PostgreSQL 18, branch `production`.
- Đã áp dụng migration và nạp `db/seed-demo.sql`.

Lấy connection string: Neon Console → **Connect** → tắt **Connection pooling** (dùng endpoint direct, host không có `-pooler`). Neon hiển thị dạng URI `postgresql://user:pass@host/db`; Npgsql **không** đọc được dạng này, phải đổi sang key/value:

```
Host=ep-xxx.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require
```

Nếu dùng Neon CLI: `neon link` (đã chạy) tạo file `.env.local` chứa `DATABASE_URL_UNPOOLED` — cũng là dạng URI, cần đổi như trên. `.env.local` và `.neon` đã được git ignore.

### 3.2. Brevo (email)

Render gói Free chặn các cổng SMTP 25/465/587, nên ứng dụng gửi email qua **Brevo API** (HTTPS 443).

1. Đăng ký miễn phí tại brevo.com (khoảng 300 email/ngày).
2. **Senders, Domains & Dedicated IPs → Senders → Add a sender**: thêm và xác minh email gửi đi (không cần sở hữu domain).
3. **SMTP & API → API Keys → Generate a new API key** → lưu lại (dạng `xkeysib-...`).

### 3.3. Render (web app)

1. Render Dashboard → **New → Blueprint** → chọn repo này. Render đọc `render.yaml` và tạo service `quiz-management` (Docker, Free, Singapore, health check `/healthz`).
2. Render hỏi các giá trị bí mật (`sync: false`) — điền:

   | Biến | Giá trị |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | Connection string Neon dạng key/value (mục 3.1) |
   | `App__PublicBaseUrl` | `https://quiz-management.onrender.com` (hoặc URL Render cấp, xem bước 4) |
   | `Email__ApiKey` | API key Brevo |
   | `Email__FromAddress` | Email sender đã xác minh trên Brevo |
   | `Jwt__SigningKey` | Khóa ký JWT cho mobile API: chuỗi ngẫu nhiên ≥ 32 byte. `render.yaml` để Render tự sinh (`generateValue`); với service đã tạo trước đó, thêm thủ công trong tab **Environment** (nút **Generate**). Đổi khóa sẽ đăng xuất mọi người dùng mobile |

   Các biến còn lại đã có sẵn trong `render.yaml`:

   | Biến | Giá trị | Ý nghĩa |
   | --- | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` | Bật kiểm tra cấu hình, HSTS, trang lỗi an toàn |
   | `Hosting__BehindProxy` | `true` | Tin header `X-Forwarded-*` của Render để lấy IP thật và HTTPS |
   | `Database__MigrateOnStartup` | `true` | Tự áp dụng migration mới khi deploy |
   | `Email__Provider` | `BrevoApi` | Gửi email qua Brevo API |
   | `Email__FromName` | `Quiz Management` | Tên người gửi |
   | `Swagger__Enabled` | `true` | Mở Swagger UI tại `/swagger` cho nhóm mobile; đặt `false` để ẩn |

3. **Apply** → Render build `Dockerfile` và deploy (lần đầu khoảng 5–10 phút).
4. Nếu tên `quiz-management` đã bị người khác dùng, Render thêm hậu tố vào URL. Khi đó sửa `App__PublicBaseUrl` theo URL thật trong tab **Environment** → **Save, rebuild, and deploy**. Cập nhật link ở mục 1.
5. Kiểm tra: mở `https://<url>/healthz` → `Healthy`, rồi đăng nhập bằng tài khoản ở mục 1.

Nếu thiếu hoặc sai cấu hình, container **không khởi động** và log trên Render ghi rõ biến nào thiếu (ví dụ `App:PublicBaseUrl là bắt buộc ngoài Development`).

## 4. Quy trình cập nhật

- Push lên `main` **sẽ deploy lên production**. Luôn làm trên nhánh riêng → Pull Request → CI xanh → merge.
- Render chỉ deploy commit mới trên `main` sau khi GitHub checks (CI) đạt (`autoDeployTrigger: checksPass`).
- CI build Docker image và khởi động thử container với cấu hình chỉ qua biến môi trường, nên lỗi `Dockerfile` hoặc cấu hình bị phát hiện ở PR.
- Thay đổi schema: thêm migration (`dotnet ef migrations add <Tên> --project DataAccessObjects --startup-project QuizManagement`) trong **cùng** PR; Render tự áp dụng khi khởi động.

**Rollback:** Render Dashboard → service → **Events** → chọn bản deploy trước → **Rollback**. Lưu ý: rollback code không tự hoàn tác migration đã chạy trên Neon; nếu cần, dùng Neon **Restore** (khôi phục branch về thời điểm trước) trong giới hạn lịch sử của gói Free.

**Reset dữ liệu demo trên Neon:** chạy lại `db/seed-demo.sql` trong Neon SQL Editor. Dữ liệu không thuộc tài khoản demo được giữ nguyên.

## 5. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp |
| --- | --- |
| Deploy lỗi, log có `Cấu hình không hợp lệ` | Thiếu biến ở mục 3.3 — log liệt kê từng biến |
| Đăng ký báo "chưa gửi được email" | Log có `Brevo rejected email ... 401` (sai API key) hoặc `400` (sender chưa xác minh) |
| Link trong email trỏ sai domain | `App__PublicBaseUrl` chưa khớp URL Render |
| Không kết nối được database | Connection string dạng URI thay vì key/value, thiếu `SSL Mode=Require`, hoặc dùng endpoint `-pooler` |
| Trang tải rất lâu lần đầu | Container Free đang khởi động lại sau khi ngủ (30–60 giây) |
