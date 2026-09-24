# Hướng dẫn demo Quiz Management từ đầu tới cuối

Tài liệu này dùng để chuẩn bị và trình diễn project PRN222 theo một luồng hoàn chỉnh. Kịch bản chính mất khoảng **20 phút**; phần xác minh email và đặt lại mật khẩu là tùy chọn, thêm khoảng **4 phút**.

## 1. Mục tiêu demo

Sau buổi demo, người xem cần thấy được:

- Kiến trúc ASP.NET Core MVC theo luồng `View → Controller → Service → Repository → DAO → PostgreSQL`.
- Ba vai trò `User`, `Mentor`, `Admin` và phạm vi quyền khác nhau.
- Học flashcard có lịch ôn lưu trong database.
- Làm quiz, chấm điểm, xem lời giải, lịch sử và thống kê.
- Mentor quản lý/import/export nội dung và xử lý báo cáo.
- Admin quản lý người dùng, vai trò, trạng thái và lịch sử đăng nhập.
- Xác minh email, quên mật khẩu, rate limiting và khóa đăng nhập bền vững.

## 2. Tài khoản demo

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Admin | `admin.demo@quiz.local` | `Test@123456` |
| Mentor | `mentor.demo@quiz.local` | `Test@123456` |
| User | `user.demo@quiz.local` | `Test@123456` |

> Chỉ dùng các tài khoản này trong database demo cô lập.

## 3. Chuẩn bị trước buổi demo

### 3.1. Yêu cầu

- .NET SDK 9.0.313.
- PostgreSQL 16+ (cần cho cú pháp `IS JSON` và collation ICU `und-x-icu`). Chọn một trong hai:
  - PostgreSQL cài trên máy (hoặc `docker run -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17`).
  - Database online miễn phí trên [Neon](https://neon.tech), region `ap-southeast-1` (Singapore).
- `psql` (đi kèm PostgreSQL) hoặc SQL Editor của Neon để chạy dữ liệu demo.
- Repository đã được clone về máy.

Mở PowerShell tại thư mục gốc repository.

### 3.2. Cấu hình connection string

Tạo file local nếu chưa có:

```powershell
Copy-Item .\QuizManagement\appsettings.Local.example.json `
          .\QuizManagement\appsettings.Local.json
```

Cập nhật `ConnectionStrings:DefaultConnection` trong `appsettings.Local.json`.

PostgreSQL trên máy:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=quizmanagement;Username=postgres;Password=postgres"
  }
}
```

Neon: dùng endpoint **direct** (không có `-pooler` trong tên host). Neon hiển thị connection string dạng URI `postgresql://user:pass@host/db`, nhưng Npgsql **không** đọc được dạng này; hãy chuyển sang dạng key/value:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=ep-xxx.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require"
  }
}
```

Nếu muốn demo email qua log thay vì SMTP thật, bỏ section `Email` hoặc đặt `Email:Host` thành chuỗi rỗng. Khi đó phải chạy ứng dụng trong môi trường Development và giữ terminal mở để lấy liên kết xác minh/reset.

### 3.3. Tạo schema bằng EF Core migrations

```powershell
dotnet tool restore
dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
```

Lệnh trên tạo database (nếu chưa có) cùng toàn bộ bảng, khóa ngoại, index và constraint. Schema được quản lý hoàn toàn bằng migration trong `DataAccessObjects/Migrations`; không còn script tạo schema viết tay.

### 3.4. Nạp dữ liệu demo

```powershell
psql "host=localhost dbname=quizmanagement user=postgres password=postgres" -f db/seed-demo.sql
```

Với Neon: mở **SQL Editor**, dán toàn bộ nội dung `db/seed-demo.sql` rồi chạy.

Kết quả mong đợi:

- Ba tài khoản `Admin`, `Mentor`, `User` được tạo.
- Có 2 môn học, 3 bộ đề, 21 câu hỏi và dữ liệu mẫu về lượt làm quiz, báo cáo câu hỏi, lịch sử đăng nhập.

Script có thể chạy lại nhiều lần: dữ liệu thuộc tài khoản demo được xóa và tạo lại, dữ liệu khác được giữ nguyên. Nếu có lỗi, toàn bộ script được rollback.

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin.demo@quiz.local` | `Test@123456` |
| Mentor | `mentor.demo@quiz.local` | `Test@123456` |
| User | `user.demo@quiz.local` | `Test@123456` |

### 3.5. Build và chạy

```powershell
dotnet restore
dotnet build QuizManagementSystem.slnx -c Release
dotnet run --project QuizManagement --launch-profile http
```

Mở: [http://localhost:5039](http://localhost:5039)

Kiểm tra nhanh trước khi bắt đầu:

- Trang đăng nhập mở được.
- Đăng nhập được bằng `user.demo@quiz.local`.
- Menu có `Môn học`, `Thống kê`, `Lịch sử`.
- Terminal không có exception kết nối database.

## 4. Kịch bản demo chính khoảng 20 phút

### Phần A — Giới thiệu hệ thống (1 phút)

Tại trang Home, giới thiệu ngắn:

> Quiz Management là hệ thống quản lý học liệu và luyện tập trắc nghiệm. Nội dung được tổ chức theo Môn học → Bộ đề → Câu hỏi → Đáp án. Hệ thống có ba vai trò User, Mentor và Admin, sử dụng ASP.NET Core MVC, Entity Framework Core và PostgreSQL.

Nêu nhanh các điểm kỹ thuật:

- Cookie authentication và policy-based authorization.
- Validation tại ViewModel, service và database constraint.
- Soft delete để giữ nguyên lịch sử làm bài.
- Kết quả quiz lưu snapshot nên không bị thay đổi khi nội dung gốc được sửa sau đó.

### Phần B — Luồng User (7 phút)

#### B1. Đăng nhập và Dashboard

Đăng nhập:

- Email: `user.demo@quiz.local`
- Mật khẩu: `Test@123456`

Tại Dashboard, chỉ ra:

- Số bài đã làm.
- Điểm trung bình.
- Lần làm gần nhất.
- Quiz gần đây và danh sách học liệu.

> Dashboard tổng hợp trực tiếp từ lịch sử của người đang đăng nhập; User chỉ thấy dữ liệu cá nhân của mình.

#### B2. Duyệt học liệu

1. Chọn `Môn học`.
2. Mở `C# Fundamentals`.
3. Chọn bộ đề `C# and OOP Basics`.

Giới thiệu bốn hướng sử dụng trên bộ đề:

- `Làm bài`.
- `Flashcard`.
- `Câu hỏi`.
- `Bảng XH`.

#### B3. Học Flashcard

1. Chọn `Flashcard`.
2. Nhấn vào thẻ hoặc phím `Space` để lật.
3. Chỉ ra đáp án đúng và phần giải thích.
4. Chọn `Đã nhớ` cho một thẻ.
5. Chọn `Cần học lại` cho một thẻ khác.

Nội dung cần nói:

> Mỗi đánh giá được gửi về server và lưu trong bảng tiến độ flashcard. Thẻ đã nhớ được giãn lịch ôn; thẻ cần học lại quay về sau 10 phút. Vì dữ liệu nằm trong PostgreSQL nên tiến độ vẫn còn sau khi đăng xuất hoặc restart ứng dụng.

Nếu màn hình báo đã hoàn thành toàn bộ thẻ đến hạn, chọn `Học lại tất cả` để tiếp tục demo.

#### B4. Làm Quiz

1. Chọn `Làm quiz`.
2. Nhập `3` câu hỏi.
3. Chỉ ra thời gian giới hạn của bộ đề và cơ chế xáo trộn câu/đáp án.
4. Chọn `Bắt đầu làm bài`.
5. Trả lời rồi chọn `Nộp bài`.

Ở trang kết quả, trình bày:

- Điểm theo thang 10 và tỷ lệ phần trăm.
- Số câu đúng và trạng thái đạt/không đạt.
- Đáp án đã chọn, đáp án đúng và phần giải thích.
- Nút `Báo cáo câu hỏi`.

Để demo báo cáo, chọn một câu hỏi C# bất kỳ, nhấn `Báo cáo câu hỏi`, chọn lý do và nhập:

```text
Báo cáo tạo trong buổi demo để kiểm tra quy trình xử lý.
```

Gửi báo cáo nhưng chưa xử lý ở bước này.

#### B5. Lịch sử, thống kê và bảng xếp hạng

1. Chọn `Lịch sử` trên menu.
2. Mở `Chi tiết` của lượt vừa làm.
3. Chọn `Thống kê` để xem tổng lượt làm, điểm trung bình, điểm cao nhất và tỷ lệ đạt.
4. Quay lại bộ đề và mở `Bảng XH`.

> Mỗi User chỉ xem lịch sử và thống kê cá nhân, còn bảng xếp hạng tổng hợp kết quả tốt nhất theo bộ đề.

Đăng xuất User.

### Phần C — Luồng Mentor (6 phút)

Đăng nhập:

- Email: `mentor.demo@quiz.local`
- Mật khẩu: `Test@123456`

#### C1. Phân quyền sở hữu nội dung

1. Chọn `Môn học`.
2. Chỉ ra Mentor có nút tạo/sửa/xóa đối với nội dung do chính Mentor sở hữu.
3. Mở `PRN222 - ASP.NET Core MVC` → `Entity Framework Core` → `Câu hỏi`.

> Mentor được đọc toàn bộ học liệu nhưng chỉ được thay đổi môn học, bộ đề và câu hỏi thuộc quyền sở hữu. Admin không bị giới hạn này.

#### C2. Import câu hỏi

1. Tại danh sách câu hỏi, chọn `Import`.
2. Dán mẫu sau vào vùng Text:

```text
Question: HTTP status nào biểu thị không tìm thấy tài nguyên?
Type: single
* 404 Not Found
- 200 OK
- 201 Created
- 500 Internal Server Error
Explanation: HTTP 404 cho biết server không tìm thấy tài nguyên được yêu cầu.
```

3. Chọn `Xem trước import`.
4. Chỉ ra số dòng hợp lệ và lỗi validation.
5. Chọn `Import 1 câu hợp lệ`.

> Import có bước preview, kiểm tra loại câu hỏi, số đáp án và đáp án đúng trước khi ghi. Toàn bộ batch được lưu theo transaction để tránh import dở dang.

#### C3. Export và quản lý câu hỏi

Tại trang `Câu hỏi`:

- Chỉ ra câu hỏi vừa import.
- Mở `Sửa` để giới thiệu form một/nhiều đáp án đúng và phần giải thích Markdown.
- Quay lại và chọn `Word`, sau đó `PDF` để trình diễn export Unicode.

Không cần xóa câu hỏi vừa import trong lúc trình bày; dữ liệu có thể được reset sau demo.

#### C4. Thống kê nội dung và xử lý báo cáo

1. Chọn `Thống kê nội dung` trên menu.
2. Chỉ ra tổng lượt làm, người dùng khác nhau, điểm trung bình và điểm cao nhất theo môn/bộ đề.
3. Chọn `Báo cáo lỗi`.
4. Tìm báo cáo seed có ghi chú `Demo báo cáo đang chờ Mentor/Admin xử lý.`
5. Có thể mở `Sửa câu hỏi`, sau đó quay lại và chọn `Đã xử lý`.

> Mentor chỉ thấy báo cáo thuộc nội dung mình sở hữu. Trạng thái báo cáo chuyển từ chờ xử lý sang đã xử lý và được lưu trong database.

Đăng xuất Mentor.

### Phần D — Luồng Admin (4 phút)

Đăng nhập:

- Email: `admin.demo@quiz.local`
- Mật khẩu: `Test@123456`

#### D1. Dashboard quản trị

Chọn `Quản trị` trên menu và giới thiệu các số liệu:

- Tổng người dùng.
- Tổng môn học.
- Tổng bộ đề.
- Tổng câu hỏi.
- Tổng lượt làm bài.

#### D2. Quản lý người dùng

1. Chọn `Quản lý người dùng`.
2. Tìm `user.demo@quiz.local`.
3. Mở `Chi tiết`.
4. Chỉ ra chức năng đổi vai trò và vô hiệu hóa/kích hoạt tài khoản.

Không thay đổi ba tài khoản demo trong buổi trình bày. Nếu muốn thao tác thật, chỉ dùng một tài khoản tạm tạo ở phần 5.

Nêu invariant bảo mật:

> Admin không thể tự hạ vai trò, tự vô hiệu hóa hoặc vô hiệu hóa Admin hoạt động cuối cùng của hệ thống.

#### D3. Lịch sử đăng nhập

1. Quay lại Dashboard quản trị.
2. Chọn `Lịch sử đăng nhập`.
3. Lọc `Thất bại` và `Thành công`.
4. Chỉ ra email, IP, kết quả và thời gian.

> Các lần đăng nhập sai được ghi bền vững trong database. Sau nhiều lần sai trong cửa sổ thời gian quy định, email và IP bị khóa tạm thời; restart ứng dụng không xóa trạng thái này.

#### D4. Quyền toàn hệ thống

- Chọn `Môn học` để chỉ ra Admin có thể quản lý toàn bộ nội dung.
- Chọn `Báo cáo lỗi` để chỉ ra Admin thấy báo cáo trên toàn hệ thống.
- Mở báo cáo User vừa gửi và chọn `Đã xử lý` nếu muốn kết thúc đầy đủ vòng đời báo cáo.

Kết luận demo:

> Hệ thống đã bao phủ toàn bộ vòng đời: Mentor tạo nội dung, User học và làm bài, User gửi phản hồi, Mentor/Admin xử lý phản hồi, còn Admin giám sát tài khoản và bảo mật đăng nhập.

## 5. Demo tùy chọn: đăng ký → xác minh email → quên mật khẩu

Chỉ thực hiện khi `Email:Host` để trống và terminal ứng dụng đang hiển thị log.

### 5.1. Đăng ký và xác minh email

1. Đăng xuất mọi tài khoản.
2. Chọn `Đăng ký`.
3. Nhập một tài khoản tạm, ví dụ:

| Trường | Giá trị mẫu |
| --- | --- |
| Tên người dùng | `live_demo_user` |
| Email | `live.demo@quiz.local` |
| Mật khẩu | `Demo@123` |
| Xác nhận | `Demo@123` |

4. Gửi form.
5. Thử đăng nhập ngay để cho thấy tài khoản chưa xác minh bị từ chối.
6. Trong terminal, tìm log `Development email to live.demo@quiz.local`.
7. Sao chép URL `/Account/VerifyEmail?...` và mở trong trình duyệt.
8. Đăng nhập lại thành công.

Điểm cần nói:

- Token xác minh có thời hạn 24 giờ.
- Token dùng ASP.NET Core Data Protection, không lưu token thô trong database.
- Cookie kiểm tra `SecurityStamp`, trạng thái vô hiệu hóa và trạng thái xác minh email.

### 5.2. Quên và đặt lại mật khẩu

1. Đăng xuất tài khoản tạm.
2. Chọn `Quên mật khẩu?`.
3. Nhập `live.demo@quiz.local`.
4. Trong terminal, lấy URL `/Account/ResetPassword?...`.
5. Đặt mật khẩu mới, ví dụ `Demo@456`.
6. Đăng nhập bằng mật khẩu mới.

Điểm cần nói:

- Mật khẩu dài từ 8 đến 100 ký tự.
- Link reset có thời hạn 1 giờ.
- Sau khi đổi mật khẩu, `SecurityStamp` thay đổi và cookie cũ không còn hợp lệ.
- Form quên mật khẩu luôn trả về thông báo chung để tránh lộ email có tồn tại hay không.

## 6. Đáp án nhanh cho bộ đề C# dùng khi demo

Do câu hỏi và đáp án được xáo trộn, chọn theo nội dung thay vì vị trí A/B/C/D.

| Câu hỏi | Đáp án đúng |
| --- | --- |
| CLR trong .NET có vai trò chính là gì? | Quản lý thực thi mã .NET, bộ nhớ và exception |
| Đặc tính cốt lõi của OOP | Đóng gói; Kế thừa; Đa hình; Trừu tượng hóa |
| Đặc điểm của `string` | `string` là immutable |
| Interface dùng để làm gì? | Mô tả hợp đồng hành vi cần triển khai |
| LINQ deferred execution | Truy vấn chạy khi được duyệt hoặc materialize |
| Ví dụ value type | `int`; `bool`; `struct` tự định nghĩa |
| Khối `finally` chạy khi nào? | Sau `try/catch` trong luồng bình thường, dù có exception hay không |

## 7. Xử lý sự cố trong lúc demo

### Không kết nối được database

- Kiểm tra PostgreSQL đang chạy (hoặc project Neon không bị tạm dừng; lần truy vấn đầu sau khi idle có thể chậm vài giây).
- Kiểm tra `DefaultConnection` trong `appsettings.Local.json` ở dạng key/value, không phải URI `postgresql://`.
- Với Neon, connection string phải có `SSL Mode=Require`.

### `db/seed-demo.sql` báo "schema not found"

Database chưa có schema. Chạy `dotnet ef database update` theo phần 3.3 rồi chạy lại script seed.

### Không nhận được email

- Với demo qua log: để `Email:Host` trống và chạy môi trường Development.
- Với SMTP thật: kiểm tra host, port, SSL, username, password và from address.

### Không thấy thẻ flashcard đến hạn

Chọn `Học lại tất cả`.

### Không import được câu hỏi

- Giữ đúng tiền tố `Question:`, `Type:`, `Explanation:`.
- Đáp án đúng bắt đầu bằng `*`.
- Đáp án sai bắt đầu bằng `-`.
- `single` phải có đúng một đáp án đúng; `multiple` có thể có nhiều đáp án đúng.

### Chạy integration test

Integration test cần một PostgreSQL mà tài khoản có quyền tạo và xóa database tạm. Mặc định test dùng `Host=localhost;Port=5432;Username=postgres;Password=postgres`. Để dùng server khác:

```powershell
$env:QUIZ_TEST_POSTGRES_CONNECTION = "Host=localhost;Port=5432;Username=postgres;Password=..."
dotnet test QuizManagementSystem.slnx -c Release
```

Không trỏ biến này vào database Neon đang dùng cho demo. Chỉ chạy unit test (không cần PostgreSQL): `dotnet test --filter "Category!=PostgresIntegration"`.

## 8. Reset dữ liệu sau buổi demo

Để đưa dữ liệu demo về đúng trạng thái mẫu, chỉ cần chạy lại `db/seed-demo.sql` (phần 3.4). Dữ liệu không thuộc tài khoản demo được giữ nguyên.

Để xóa sạch và tạo lại toàn bộ database (**xóa toàn bộ dữ liệu**):

```powershell
dotnet ef database drop --force --project DataAccessObjects --startup-project QuizManagement
dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
```

Sau đó chạy lại `db/seed-demo.sql`.

## 9. Checklist ngay trước khi trình bày

- [ ] PostgreSQL đang chạy (hoặc database Neon truy cập được).
- [ ] `dotnet ef database update` và `db/seed-demo.sql` đã chạy thành công.
- [ ] Ba tài khoản demo đăng nhập được.
- [ ] Build Release không có warning/error.
- [ ] Trang `http://localhost:5039` mở được.
- [ ] Terminal được giữ mở nếu demo email qua log.
- [ ] Trình duyệt ở mức zoom 100%.
- [ ] Đã đóng thông tin nhạy cảm và cửa sổ không liên quan.
- [ ] Đã thử trước luồng User → Mentor → Admin ít nhất một lần.

## 10. Câu kết đề xuất

> Quiz Management không chỉ quản lý câu hỏi mà còn khép kín quy trình học tập: tạo học liệu, ôn tập theo lịch, làm bài, lưu lịch sử, thống kê, phản hồi chất lượng và quản trị bảo mật. Dữ liệu quan trọng đều được lưu bền vững trong PostgreSQL và quyền truy cập được kiểm soát theo vai trò lẫn quyền sở hữu nội dung.
