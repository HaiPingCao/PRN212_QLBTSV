# QLBT

Phần mềm có 2 bản: Server và Client. Giao tiếp qua TCP trong mạng LAN (không mã hóa).

## Server

Quản lý bằng GUI ngay trên máy chủ (thao tác trực tiếp với CSDL, không qua mạng).

- Sinh viên
- Giáo viên
- Lớp
  - Thêm mới
  - Xóa, sửa nếu kì chưa bắt đầu
  - Thêm sinh viên vào lớp (chỉ khi kì chưa bắt đầu; sv lỡ đăng ký thì không được thêm)
  - Đánh dấu đình chỉ sv (theo từng lớp, coi như xóa)
  - Không bao giờ đổi kì học của lớp
- Kì học
  - Thêm mới
  - Xóa, sửa nếu kì chưa bắt đầu
  - Đã bắt đầu: khóa ngày bắt đầu, ngày kết thúc chỉ được lùi về sau
- File lưu trữ
  - Thư mục đặt theo ID, không theo tên (đổi tên trong CSDL không làm mất file):

    ```txt
    storage/
      {semesterId}/
        {classId}/
          {assignmentId}/
            assignment            ← file đề bài
            submissions/
              {submissionId}          ← file bài nộp
    ```

  - Tên file gốc lưu trong CSDL
  - Xóa bài tập = xóa thư mục `{assignmentId}/`

## Client

Chia ra UI cho sv và gv riêng.

### Giáo viên

- Lấy DS lớp đang đứng
  - DS các lớp (Mã lớp, Chuyên ngành, sĩ số)
- Xem thông tin lớp (Tên, MSSV, trạng thái đình chỉ)
- Giao bài tập
  - Tải đề bài lên máy chủ
  - Đặt hạn nộp
  - Chọn cho phép / không cho phép nộp muộn
- Sửa bài tập
  - Đổi tên, thay đề
  - Gia hạn
  - Đóng / mở nộp bài thủ công
- Xóa bài tập (xóa luôn toàn bộ bài nộp và file)
- Xem bài nộp của sinh viên
  - Tải bài nộp từ máy chủ
- Chấm điểm và nhận xét BT của sinh viên (chỉ khi bài tập đã đóng)
- Xóa bài nộp + điểm của sv (để sv được nộp lại)

### Sinh viên

- Xem thông tin cá nhân
- Xem danh sách bài tập (Lớp, Tên bài, Hạn nộp)
  - Sắp xếp theo lớp, hiển thị dạng cây
  - Sắp xếp theo hạn
- Xem bài tập, tải đề bài
- Nộp bài
  - 1 file / bài; cần nhiều file thì nén thành archive (zip)
  - Nộp lại được, file mới thay file cũ
- Xem danh sách bài nộp (chọn trong danh sách bài tập)
  - Xem điểm
  - Tải lại bài nộp
  - Xem nhận xét gv

## Quy tắc

### Trạng thái đóng

```
closed = IsClosed || (now > Deadline && !AllowLate)
```

- `IsClosed`: gv đóng thủ công
- `AllowLate`: gv chọn khi giao / sửa bài tập
- Cho phép nộp muộn thì quá hạn vẫn chưa đóng → gv phải đóng thủ công mới chấm được

### Nộp bài

Server kiểm tra theo thứ tự:

1. Sv thuộc lớp và không bị đình chỉ trong lớp đó
2. Bài tập chưa đóng
3. Đã có bài nộp và đã có điểm → từ chối. Chưa có điểm → thay file cũ
4. `IsLate = now > Deadline`

- File nhận vào được ghi ra `{submissionId}.tmp`, nhận đủ mới thay file cũ (mất kết nối giữa chừng thì giữ bài cũ)

### Chấm điểm

- Chỉ chấm khi bài tập đã đóng
- Gia hạn / mở lại → tạm dừng chấm, điểm đã chấm giữ nguyên
- Chấm lại = ghi đè điểm và nhận xét

### Gia hạn / mở lại

- Sv chưa nộp hoặc bài nộp chưa chấm: được nộp
- Sv đã có điểm: không được thay bài, trừ khi gv xóa bài nộp + điểm của sv đó

### Đình chỉ (theo lớp)

- Không nhận bài tập mới, không nộp, không tải đề của lớp đó
- Vẫn xem được điểm, nhận xét và bài nộp cũ

### Xóa

- Xóa trong CSDL trước (transaction), xóa file sau

## Giao thức

Mỗi request 1 kết nối TCP. Khung dữ liệu: `int` độ dài JSON → JSON → byte file (nếu có).

### Request

```txt
- Type        tên thao tác
- SessionId   null nếu chưa đăng nhập
- Body        dữ liệu theo Type
[byte file]   chỉ khi gửi file lên; độ dài nằm trong Body
```

### Response

```txt
- Type        giống request
- Status      Ok | InvalidCredentials | Unauthorized | Forbidden | NotFound | Rejected | Error
- Error       thông báo lỗi hoặc null
- SessionId   chỉ có khi Login
- Body        dữ liệu theo Type, null nếu lỗi
[byte file]   chỉ khi tải file về
```

- Server xác định vai trò và quyền từ SessionId, không tin dữ liệu client gửi
- `Unauthorized` → client xóa SessionId, quay về màn hình đăng nhập
- `Forbidden` → đã đăng nhập nhưng không có quyền (sai vai trò, không phải chủ sở hữu, bị đình chỉ)
- `Rejected` → có quyền nhưng vi phạm quy tắc (bài tập đã đóng, bài nộp đã chấm, kì đã bắt đầu)
- Logout: client luôn xóa SessionId, kể cả khi server không phản hồi
- Mật khẩu lưu dạng hash trên server (vd. BCrypt.Net-Next)

### Thao tác

| Type                 | Ai     | Request                                                         | Response                                                      | File                |
| -------------------- | ------ | --------------------------------------------------------------- | ------------------------------------------------------------- | ------------------- |
| `Login`              | cả hai | username, password                                              | thông tin user, vai trò, sessionId                            |                     |
| `Logout`             | cả hai |                                                                 |                                                               |                     |
| `GetProfile`         | cả hai |                                                                 | thông tin cá nhân                                             |                     |
| `GetMyClasses`       | gv     |                                                                 | [classId, mã lớp, chuyên ngành, sĩ số]                        |                     |
| `GetClassStudents`   | gv     | classId                                                         | [studentId, tên, MSSV, đình chỉ]                              |                     |
| `CreateAssignment`   | gv     | classId, tên, hạn, allowLate, fileName, length                  | assignmentId                                                  | → server            |
| `UpdateAssignment`   | gv     | assignmentId, tên, hạn, allowLate, isClosed, [fileName, length] |                                                               | → server (tùy chọn) |
| `DeleteAssignment`   | gv     | assignmentId                                                    |                                                               |                     |
| `GetSubmissions`     | gv     | assignmentId                                                    | [submissionId, sv, thời gian nộp, isLate, điểm]               |                     |
| `GradeSubmission`    | gv     | submissionId, điểm, nhận xét                                    |                                                               |                     |
| `DeleteSubmission`   | gv     | submissionId                                                    |                                                               |                     |
| `GetMyAssignments`   | sv     |                                                                 | [assignmentId, lớp, tên, hạn, isClosed, lớp bị đình chỉ]      |                     |
| `DownloadAssignment` | sv     | assignmentId                                                    | fileName, length                                              | ← server            |
| `SubmitAssignment`   | sv     | assignmentId, fileName, length                                  | submissionId, isLate                                          | → server            |
| `GetMySubmission`    | sv     | assignmentId                                                    | submissionId, thời gian nộp, isLate, điểm, nhận xét, fileName |                     |
| `DownloadSubmission` | cả hai | submissionId                                                    | fileName, length                                              | ← server            |

- Mọi thao tác đều kiểm tra quyền sở hữu: gv chỉ truy cập lớp mình dạy, sv chỉ truy cập lớp mình học và bài nộp của mình
