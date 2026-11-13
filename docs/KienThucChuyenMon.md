# Kiến thức chuyên môn áp dụng trong dự án QLBTSV

Tài liệu này tổng hợp các kiến thức kỹ thuật được vận dụng khi xây dựng hệ thống quản lý giao – nộp – chấm bài tập (QLBTSV, môn PRN212). Mỗi mục trình bày **vấn đề**, **giải pháp đã chọn**, **lý do** và **vị trí trong mã nguồn**, để người đọc có thể đối chiếu trực tiếp với code.

Đặc tả nghiệp vụ nằm ở [Plan.md](Plan.md); sơ đồ cấu trúc dự án nằm ở [ProjectStructure.drawio](ProjectStructure.drawio).

---

## 1. Kiến trúc tổng thể

### 1.1. Mô hình Client – Server trên mạng LAN

Hệ thống gồm hai ứng dụng WPF (.NET 8) và một thư viện dùng chung:

| Thành phần | Vai trò |
|---|---|
| `QLBT.Server` | Ứng dụng WPF cho quản trị viên, **đồng thời** chạy một máy chủ HTTP (ASP.NET Core / Kestrel) ngay trong cùng tiến trình |
| `QLBT.Client` | Ứng dụng WPF cho giáo viên và sinh viên, chỉ giao tiếp với server qua HTTP |
| `QLBT.Shared` | Thư viện lớp: hằng số route (`ApiRoutes`), mã trạng thái (`ApiStatus`), các DTO và cấu hình JSON (`ApiJson`) |

**Nguyên tắc tách lớp:** Client không bao giờ truy cập cơ sở dữ liệu. Mọi quy tắc nghiệp vụ và kiểm tra quyền đều nằm ở server; client chỉ phản ánh lại các quy tắc đó trên giao diện (ẩn/khoá nút) để trải nghiệm tốt hơn, nhưng **server luôn là nơi quyết định cuối cùng**.

### 1.2. Nhúng ASP.NET Core vào ứng dụng WPF

Thay vì tách server HTTP thành một dịch vụ riêng, dự án dựng `WebApplication` bên trong ứng dụng WPF (`Asp/ServerHost.cs`) và khởi động bằng `StartAsync` (`Asp/ServerRunner.cs`).

- Dùng `StartAsync` thay vì `Run()` vì `Run()` chặn luồng hiện tại, sẽ làm treo luồng giao diện (UI thread).
- Khi tắt ứng dụng, việc dừng Kestrel được đưa sang luồng nền (`Task.Run(_runner.Stop)` trong `App.OnExit`) để tránh **deadlock** giữa quá trình dừng của Kestrel và Dispatcher của WPF.
- Log của ASP.NET Core được chuyển về tab "Server log" thông qua một `ILoggerProvider` tự viết (`Asp/UiLoggerProvider.cs`); các dòng log được đẩy sang UI bằng `Dispatcher.BeginInvoke` vì log có thể phát sinh từ bất kỳ luồng nào.

### 1.3. Từ giao thức TCP tự định nghĩa sang HTTP

Thiết kế ban đầu (xem `Prototype/ServerVersion2` và `docs/QLBT_TCP.drawio`) dùng socket TCP với giao thức tự định nghĩa. Bản thử nghiệm `Prototype/ServerVersion3_ASP` được dựng để đánh giá việc chuyển sang HTTP. Kết quả là giữ nguyên **mô hình nghiệp vụ** (cùng tập thao tác, cùng mã trạng thái) nhưng ánh xạ sang HTTP vì:

- Không phải tự xử lý đóng khung thông điệp (message framing), đa luồng kết nối, timeout.
- Có sẵn cơ chế xác thực/phân quyền, giới hạn kích thước request, streaming file, xử lý lỗi tập trung.
- Bảng ánh xạ thao tác → route được ghi trong Plan.md ("Ánh xạ sang HTTP"), còn route là hằng số trong `QLBT.Shared/ApiRoutes.cs` để client và server không bao giờ lệch nhau.

---

## 2. Thiết kế API

### 2.1. Minimal API và tổ chức endpoint

Endpoint được nhóm theo vai trò: `AuthEndpoints`, `TeacherEndpoints`, `StudentEndpoints`, `SubmissionEndpoints` (thư mục `Asp/Endpoints/`). Mỗi lớp có một hàm `Map(app)` đăng ký route, giúp `ServerHost` ngắn gọn và dễ đọc.

### 2.2. Mã trạng thái thống nhất

Mọi lỗi trả về cùng một dạng thân JSON `ApiError { status, error }`. Bảng ánh xạ:

| `ApiStatus` | HTTP | Ý nghĩa |
|---|---|---|
| `Ok` | 200 | Thành công |
| `InvalidCredentials` | 401 | Sai tên đăng nhập hoặc mật khẩu |
| `Unauthorized` | 401 | Chưa đăng nhập / phiên hết hạn → client quay về màn hình đăng nhập |
| `Forbidden` | 403 | Sai vai trò, không phải chủ sở hữu, hoặc sinh viên bị đình chỉ trong lớp |
| `NotFound` | 404 | Không tìm thấy đối tượng |
| `Rejected` | 409 | Có quyền nhưng vi phạm quy tắc (bài đã đóng, bài nộp đã chấm) |
| `Error` | 500 | Lỗi ngoài dự kiến |

Việc tách `Forbidden` (không có quyền) và `Rejected` (có quyền nhưng sai quy tắc) giúp client hiển thị thông báo chính xác hơn so với chỉ dùng mã HTTP.

### 2.3. Xử lý lỗi tập trung

`UseExceptionHandler` trong `ServerHost` bắt mọi ngoại lệ chưa xử lý, ghi log và trả về `ApiError` với thông điệp chung — **không bao giờ lộ stack trace** ra client.

### 2.4. JSON

`ApiJson.Options` cấu hình camelCase và enum dạng chuỗi (`JsonStringEnumConverter`), dùng chung cho cả server và client, nên dữ liệu dễ đọc khi gỡ lỗi và không phụ thuộc vào giá trị số của enum.

---

## 3. Xác thực, phiên làm việc và phân quyền

### 3.1. Phiên (session) thay vì JWT

`SessionStore` (`Asp/SessionAndAuthentication/SessionStore.cs`) lưu phiên trong bộ nhớ bằng `ConcurrentDictionary`:

- Mã phiên là 32 byte ngẫu nhiên từ `RandomNumberGenerator` (bộ sinh số ngẫu nhiên an toàn mật mã), mã hoá Base64 an toàn cho URL.
- **Hết hạn trượt (sliding expiration):** mỗi lần dùng, thời điểm truy cập cuối được cập nhật; quá thời gian rảnh (mặc định 120 phút, cấu hình `Session:IdleMinutes`) thì phiên bị xoá.
- Lý do không dùng JWT: JWT không thể thu hồi khi đăng xuất nếu không có danh sách đen; phiên trong bộ nhớ thu hồi được ngay (`Remove`). Đánh đổi chấp nhận được: phiên mất khi server dừng — client xử lý tình huống này bằng cách đưa người dùng về màn hình đăng nhập.
- `ConcurrentDictionary` được dùng vì Kestrel xử lý nhiều request song song trên nhiều luồng.

### 3.2. Authentication scheme tự viết

`SessionAuthenticationHandler` đọc header `Authorization: Bearer {sessionId}`, tra cứu `SessionStore` và tạo `ClaimsPrincipal` (tên, id, vai trò). Nhờ tích hợp đúng vào pipeline của ASP.NET Core, endpoint chỉ cần khai báo policy (`Policies.Teacher`, `Policies.Student`) qua `RequireAuthorization`. Handler cũng tự ghi thân lỗi `ApiError` cho các trường hợp 401/403 để định dạng lỗi luôn thống nhất.

### 3.3. Nguyên tắc bảo mật

- **Danh tính và vai trò chỉ lấy từ phiên phía server**, không bao giờ tin vào id hay vai trò client gửi lên.
- **Kiểm tra quyền sở hữu ở mọi thao tác:** giáo viên chỉ thấy lớp mình dạy; sinh viên chỉ thấy lớp mình học và bài nộp của mình.
- **Mật khẩu lưu dạng băm BCrypt** (`BCrypt.Net-Next`): có salt tích hợp và hệ số chi phí, chống tấn công từ điển/rainbow table. Đăng nhập dùng `BCrypt.Verify`.
- Tên file người dùng gửi lên được làm sạch (`FileReceiver.CleanFileName`): bỏ phần thư mục, loại ký tự không hợp lệ — chống **path traversal**.

---

## 4. Truy cập dữ liệu với Entity Framework Core

### 4.1. Database-first

Lược đồ được định nghĩa trong `db/QLBT.sql`, sau đó **scaffold** sang các entity trong `QLBT.Server/Models` bằng `dotnet ef dbcontext scaffold ... --no-onconfiguring`. Các entity sinh tự động **không được sửa tay**; muốn đổi cấu trúc thì sửa script SQL rồi scaffold lại. Tuỳ chọn `--no-onconfiguring` loại bỏ chuỗi kết nối cứng trong `OnConfiguring`, buộc chuỗi kết nối phải đến từ cấu hình.

### 4.2. Vòng đời DbContext

Chỉ có **một lớp** `Prn212PQlbtContext`, nhưng **mỗi thao tác dùng một instance ngắn hạn**:

- Endpoint HTTP: `AddDbContext` đăng ký context với vòng đời *scoped* — mỗi request một instance.
- Giao diện quản trị: mọi truy vấn nằm trong thư mục `DAO/` (`UserDAO`, `SemesterDAO`, `CourseDAO`, `ClassDAO`); mỗi phương thức DAO dùng `DbFactory.Create()` tạo context mới (`using`). Service chỉ kiểm tra dữ liệu, áp dụng quy tắc nghiệp vụ và gọi DAO. Context được tạo theo cách này vì phần quản trị phải hoạt động cả khi server HTTP đang dừng (lúc đó không có DI container).

Lý do không dùng một context dùng chung: `DbContext` **không an toàn đa luồng**, change tracker giữ dữ liệu cũ (dữ liệu "stale"), một lần `SaveChanges` lỗi sẽ làm hỏng các lần lưu sau, và bộ nhớ tăng dần theo thời gian.

### 4.3. Tối ưu truy vấn

- Truy vấn chỉ đọc dùng `AsNoTracking()` để bỏ chi phí theo dõi thay đổi.
- Chiếu dữ liệu (`Select`) thẳng sang DTO hoặc row model thay vì tải cả entity.
- Mọi thao tác I/O đều bất đồng bộ (`ToListAsync`, `SaveChangesAsync`) để không chặn luồng UI hay luồng của Kestrel.

### 4.4. Giao dịch (transaction) và thứ tự xoá

- Các thao tác nhiều bước (nộp bài, đổi môn của lớp, xoá lớp) chạy trong `BeginTransactionAsync`.
- **Quy tắc xoá:** xoá dữ liệu trong DB (trong transaction) trước, *sau đó* mới xoá file. Nếu xoá file trước mà DB lỗi, dữ liệu sẽ trỏ tới file không còn tồn tại; làm ngược lại thì trường hợp xấu nhất chỉ là một file mồ côi, vô hại.

---

## 5. Toàn vẹn dữ liệu ở mức cơ sở dữ liệu

### 5.1. Ràng buộc "một lớp mỗi môn mỗi kỳ"

Quy tắc: một sinh viên chỉ được *đang học* một lớp của cùng một môn trong một kỳ (lượt đình chỉ không tính). Quy tắc này được bảo đảm **ở hai tầng**:

1. **Tầng ứng dụng** (`ClassAdminService`): kiểm tra trước để đưa ra thông báo dễ hiểu (khi thêm sinh viên, gỡ đình chỉ, đổi môn), và màn hình chọn sinh viên ẩn luôn những sinh viên sẽ vi phạm.
2. **Tầng cơ sở dữ liệu:** bảng `class_students` sao chép `semester_id` và `course_id` từ lớp qua **khoá ngoại phức hợp** `(class_id, semester_id, course_id)` với `ON UPDATE CASCADE`, kèm **chỉ mục duy nhất có lọc** (filtered unique index) `uq_class_students_course` chỉ áp dụng cho bản ghi đang hoạt động.

Tầng ứng dụng không thể chống được hai thao tác đồng thời (race condition); chỉ ràng buộc ở DB mới bảo đảm tuyệt đối. Khi DB từ chối, lỗi `DbUpdateException` được bắt và chuyển thành thông báo thân thiện.

### 5.2. Hạn chế của EF với khoá thay thế

Vì `(Id, SemesterId, CourseId)` của `Class` được khoá ngoại phức hợp tham chiếu tới, EF coi đó là *alternate key* và không cho sửa `CourseId`. Giải pháp: cập nhật bằng `ExecuteSqlInterpolatedAsync` (có tham số hoá nên **an toàn trước SQL injection**), để DB tự lan truyền thay đổi sang `class_students` nhờ `ON UPDATE CASCADE`.

### 5.3. Lưu ý về script SQL

Chỉ mục có lọc yêu cầu `SET QUOTED_IDENTIFIER ON`, trong khi `sqlcmd` mặc định là OFF; vì vậy cả hai script đều bắt đầu bằng lệnh này. Dữ liệu thử nghiệm (`db/QLBT_test_ds.sql`) tính hạn nộp theo `GETDATE()` để các trạng thái (đang mở, quá hạn, cho nộp muộn, đóng thủ công) luôn đúng mỗi lần chạy lại.

---

## 6. Quy tắc nghiệp vụ

### 6.1. Trạng thái đóng của bài tập

```
closed = IsClosed || (now > Deadline && !AllowLate)
```

Công thức đặt tại `Domain/AssignmentRules.cs` và được **dùng chung** bởi endpoint và giao diện quản trị, tránh việc mỗi nơi tự tính một kiểu. Bài tập chỉ được chấm khi đã đóng, để điểm không bị thay đổi do sinh viên nộp lại sau khi chấm.

### 6.2. Thứ tự kiểm tra khi nộp bài

1. Là thành viên của lớp và không bị đình chỉ → nếu không: `Forbidden`.
2. Bài tập chưa đóng → nếu không: `Rejected`.
3. Đã có bài nộp: nếu đã chấm → `Rejected`; nếu chưa chấm → thay thế.
4. `IsLate = now > Deadline`.

Thứ tự này đặt kiểm tra quyền trước kiểm tra quy tắc, đúng với ý nghĩa của `Forbidden` và `Rejected`.

### 6.3. Kỳ học và lớp học

Kỳ học và lớp học sửa được bất cứ lúc nào; chỉ chặn những thao tác làm mất dữ liệu: không xoá kỳ còn lớp, không xoá môn đang được lớp dùng, không xoá người dùng đã có lớp. Một lớp không bao giờ đổi kỳ. Sinh viên đã có bài nộp thì không gỡ khỏi lớp mà dùng **đình chỉ** (`is_active = 0, left_at = now`) để giữ nguyên lịch sử điểm và bài nộp.

Đây là một lựa chọn đơn giản hoá có chủ đích: phiên bản trước có thêm các khoá theo "kỳ đã bắt đầu" (khoá ngày bắt đầu, không sửa lớp, không thêm/gỡ sinh viên), nhưng chúng làm giao diện và service phức tạp hơn nhiều so với giá trị mang lại cho một đồ án môn học.

---

## 7. Truyền tải file

### 7.1. Upload dạng luồng (streaming)

- Upload dùng `PUT` với **thân request là nội dung file thô** và tên file trong query `?fileName=`, không dùng `multipart/form-data` — đơn giản hơn và không phải đệm toàn bộ file vào bộ nhớ.
- `FileReceiver.ReceiveToTemp` chép luồng request vào file tạm `.tmp` với bộ đệm 80 KB và I/O bất đồng bộ; nếu kết nối đứt giữa chừng thì file tạm bị xoá.
- **Ghi an toàn:** file chỉ được `File.Move(..., overwrite: true)` vào vị trí thật sau khi đã nhận đủ dữ liệu và cập nhật DB thành công. Lỗi giữa chừng không làm mất file cũ.
- Kestrel giới hạn kích thước thân request (`MaxRequestBodySize`, mặc định 500 MB qua `Upload:MaxMegabytes`).

### 7.2. `Expect: 100-continue`

Client gửi header `Expect: 100-continue`: nó gửi phần header trước và chờ server đồng ý rồi mới gửi thân file. Server thực hiện **mọi kiểm tra trước khi đọc thân request**, nên nếu bị từ chối (không có quyền, bài đã đóng...) thì client không phải tải lên hàng trăm MB vô ích.

### 7.3. Download

`GET` trả file với tên gốc trong header `Content-Disposition` và hỗ trợ `Range` (`enableRangeProcessing`). Client đọc tên file từ header rồi mở hộp thoại lưu file với tên đó.

### 7.4. Lưu trữ theo ID

Đường dẫn chỉ được ghép từ các ID:

```
storage/{semesterId}/{classId}/{assignmentId}/assignment
storage/{semesterId}/{classId}/{assignmentId}/submissions/{submissionId}
```

Tên file gốc chỉ được lưu trong DB. Cách này loại bỏ hoàn toàn nguy cơ trùng tên, ký tự đặc biệt hay path traversal từ tên file do người dùng đặt.

---

## 8. Ứng dụng Client

### 8.1. Lớp bao HttpClient

`Api/ApiClient.cs` bao `HttpClient` với các quyết định sau:

- **Một `HttpClient` dùng suốt vòng đời ứng dụng** (tránh cạn socket do tạo mới liên tục).
- `SocketsHttpHandler.ConnectTimeout = 5s` để phát hiện nhanh server không phản hồi, nhưng **không** đặt timeout tổng (để upload/download file lớn không bị cắt ngang).
- **Không ném ngoại lệ khi lỗi mạng:** mọi lỗi được chuyển thành `ApiResult` với `Status = Error` và thông điệp rõ ràng. Code giao diện chỉ cần kiểm tra kết quả, không cần `try/catch` ở khắp nơi.
- Xử lý đúng phản hồi 200 có thân rỗng (`Results.Ok()` không trả thân).
- Khi nhận `Unauthorized`, tự xoá session id.

`Api/QlbtApi.cs` cung cấp mỗi thao tác một hàm có kiểu rõ ràng (ví dụ `GetMyClasses`, `SubmitAssignment`), nên giao diện không phải biết route hay chi tiết HTTP.

### 8.2. Xử lý lỗi và hết phiên tập trung

Mọi kết quả đều đi qua `UiHelper.Check`: hiển thị thông báo lỗi của server (trên dòng báo lỗi màu đỏ hoặc hộp thoại), và nếu là `Unauthorized` thì gọi `App.SessionExpired()` — đóng mọi cửa sổ và quay về màn hình đăng nhập. Nhờ vậy hành vi khi mất phiên (ví dụ server khởi động lại) nhất quán trên toàn ứng dụng.

### 8.3. Kỹ thuật WPF

- **Điều hướng nhiều cửa sổ:** `ShutdownMode = OnExplicitShutdown` để ứng dụng không tự thoát khi đóng cửa sổ đăng nhập lúc chuyển sang cửa sổ chính; ứng dụng tự quyết định khi nào tắt.
- **Code-behind mỏng:** tải dữ liệu → gán binding → gọi API/service → hiển thị lỗi. Quy tắc nghiệp vụ không nằm trong view.
- **Lập trình bất đồng bộ trên UI:** handler sự kiện dạng `async void`, mọi lời gọi mạng đều `await` để giao diện không bị treo. `BusyScope` (mẫu `IDisposable`) khoá vùng giao diện và hiển thị trạng thái trong lúc chờ, và luôn mở khoá lại kể cả khi có lỗi.
- **Chống phản hồi cũ (stale response):** khi người dùng chuyển nhanh giữa các mục, một bộ đếm yêu cầu được tăng mỗi lần tải; phản hồi về muộn của yêu cầu cũ bị bỏ qua, tránh hiển thị chi tiết của mục khác.
- **Theo dõi thay đổi chưa lưu (dirty tracking):** các cờ `_filling`, `_dirty` phân biệt thay đổi do code và do người dùng; khi đổi lớp/bài tập mà còn thay đổi chưa lưu thì hỏi xác nhận, và nếu người dùng huỷ thì khôi phục lựa chọn cũ qua `Dispatcher.BeginInvoke`.
- **Mẫu Master–Detail:** cửa sổ giáo viên chia 3 vùng (danh sách lớp → bài tập → bài nộp); cửa sổ sinh viên cho phép xem dạng cây theo lớp (`TreeView` + `HierarchicalDataTemplate`) hoặc danh sách theo hạn nộp.
- **Kiểm tra dữ liệu nhập ở client trước khi gửi** (ví dụ điểm 0–10, tối đa 2 chữ số thập phân, chấp nhận cả "8.5" và "8,5"), nhưng server vẫn kiểm tra lại.

### 8.4. Cấu hình

Địa chỉ server nằm trong `appsettings.json` của client (`ServerUrl`, Copy to Output Directory = Always); `ClientSettings` đọc file bằng `System.Text.Json` và quay về giá trị mặc định nếu file thiếu hoặc URL không hợp lệ. Cấu hình server (IP, cổng, thư mục lưu file, tự khởi động) được lưu ở `%LocalAppData%\QLBT.Server\settings.json`, tách khỏi chuỗi kết nối DB trong `appsettings.json`.

---

## 9. Ứng dụng quản trị (Server GUI)

- Thao tác trực tiếp với DB (không qua HTTP), hoạt động cả khi server HTTP đang dừng.
- Phân lớp: **View** (UserControl, code-behind mỏng) → **Admin service** (truy vấn DB + kiểm tra quy tắc, trả về `AdminResult`) → **Domain** (quy tắc dùng chung).
- `AdminResult` biểu diễn kết quả thành công/thất bại kèm thông báo, thay vì ném ngoại lệ cho các lỗi nghiệp vụ dự kiến được.
- Lỗi bất ngờ (thường là mất kết nối DB) được ghi log và hiển thị **một lần**: khi nhiều tab cùng tải và cùng lỗi, các lỗi trùng lặp chỉ được ghi log, tránh việc bật liên tiếp nhiều hộp thoại giống nhau.
- `DispatcherUnhandledException` là lưới an toàn cuối: lỗi giao diện không làm sập ứng dụng (và kéo theo sập máy chủ HTTP đang chạy cùng tiến trình).

---

## 10. Kiểm thử

- **Dữ liệu thử nghiệm có kịch bản** (`db/QLBT_test_ds.sql`): giáo viên có lớp ở kỳ đã kết thúc / đang diễn ra / chưa bắt đầu, sinh viên bị đình chỉ, sinh viên học nhiều lớp, sinh viên không thuộc lớp nào, các bài tập ở đủ mọi trạng thái.
- **Kiểm thử kịch bản tự động trên server thật:** chương trình chạy chuỗi kiểm tra PASS/FAIL qua API (đăng nhập sai/đúng, tạo bài – upload – download, nộp và nộp lại, chấm khi bài còn mở bị từ chối, chấm sau khi đóng, đăng xuất rồi gọi API nhận `Unauthorized`, server không truy cập được) và mở các cửa sổ ngoài màn hình để kiểm tra trạng thái giao diện. Dữ liệu thử nghiệm được dọn sạch qua API sau khi chạy.
- **Tái hiện lỗi giao diện bằng UI Automation / Win32:** ví dụ, để xác nhận lỗi "hộp thoại hiện nhiều lần khi DB không truy cập được", chạy một bản sao server với chuỗi kết nối sai và đếm các hộp thoại `#32770` xuất hiện, trước và sau khi sửa (5 → 1).

---

## 11. Quy trình làm việc

- **Git:** làm việc trên nhánh tính năng (`feature/...`, `fix/...`), commit message dạng mệnh lệnh, dòng đầu dưới 72 ký tự; không commit `bin/`, `obj/`, `.vs/`, dữ liệu `storage/` (xem [GitHelp.md](GitHelp.md)).
- **Tài liệu đi cùng code:** Plan.md là đặc tả; mọi thay đổi quy tắc đều cập nhật lại Plan.md. Route mới phải đồng bộ với bảng ánh xạ HTTP trong Plan.md.
- **Quy ước code:** namespace dạng file-scoped, bật `Nullable`, khai báo kiểu tường minh thay vì `var`, luôn dùng ngoặc nhọn, lớp mặc định `sealed`, chú thích `///` ngắn gọn cho thành viên public.
- **Nguyên mẫu trước, sản phẩm sau:** các hướng đi rủi ro (giao thức TCP, BCrypt, EF scaffold, chuyển sang ASP.NET Core) được thử trong `Prototype/` trước khi đưa vào giải pháp chính.
