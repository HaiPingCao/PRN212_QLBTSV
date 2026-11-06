-- =============================================
-- QLBT - Test dataset (dữ liệu thử nghiệm)
--
-- Chạy SAU QLBT.sql:
--   sqlcmd -S "(local)" -E -C -f 65001 -i QLBT.sql
--   sqlcmd -S "(local)" -E -C -f 65001 -i QLBT_test_ds.sql
--
-- Chạy lại được: xóa toàn bộ dữ liệu (trừ roles) rồi nạp lại.
--
-- Mật khẩu mọi tài khoản: 123456  (BCrypt, cost 11)
--
-- Kì học dùng ngày cố định (SU26 đã kết thúc, FA26 đang diễn ra, SP27 chưa bắt đầu).
-- Hạn nộp / thời gian nộp của kì FA26 tính theo GETDATE() để các trạng thái
-- (đang mở, quá hạn, cho nộp muộn, đóng thủ công) luôn đúng khi chạy lại.
-- Đường dẫn file (file_path) là đường dẫn tương đối trong thư mục storage/.
--
-- Tình huống kiểm thử:
--   GV001  3 lớp: SE1801 (SU26, đã xong), SE1802 (FA26), SE1804 (SP27, chưa bắt đầu)
--   GV002  2 lớp FA26: SE1803, IA1801
--   GV003  2 lớp: AI1801 (SU26), AI1802 (FA26)
--   GV004  chỉ có 1 lớp SP27 chưa có sinh viên
--   HE180006 (id 10) bị đình chỉ ở SE1802, vẫn có điểm Lab 1 trước khi bị đình chỉ
--   HE180013 (id 17) bị đình chỉ ở SE1803, đã có bài nộp được chấm
--   HE180001 (id 5) học 3 lớp (SE1801, SE1802, AI1802) để xem dạng cây
--   HE180020 (id 24) không thuộc lớp nào
--
--   Bài tập (FA26):
--     4  hết hạn, không cho nộp muộn  -> đóng theo hạn, đã chấm
--     5  hết hạn, cho nộp muộn        -> vẫn mở, có bài muộn, chưa chấm được
--     6  còn hạn                      -> đang mở
--     7  còn hạn, không có file đề
--     8  hết hạn, cho nộp muộn, gv đóng thủ công -> đang chấm dở
--     9  vừa hết hạn, không cho nộp muộn -> đóng, chưa chấm
--     10 còn hạn nhưng gv đóng thủ công sớm
--     11 hết hạn -> đã chấm
--     12 còn hạn
--     13 còn hạn, chưa có bài nộp
--     14 bài tập của lớp SP27 (kì chưa bắt đầu)
-- =============================================

USE PRN212_P_QLBT;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM submissions;
DELETE FROM assignments;
DELETE FROM class_students;
DELETE FROM classes;
DELETE FROM semesters;
DELETE FROM users;

DECLARE @pw VARCHAR(255) = '$2a$11$jvU.fitNzXmECtWWP28bVOEw0r5ozlh4uCxfpCF/bkFxNpZOZI1Mu';  -- 123456
DECLARE @now DATETIME = GETDATE();
DECLARE @eod DATETIME = DATEADD(MINUTE, 1439, CAST(CAST(@now AS DATE) AS DATETIME));      -- hôm nay 23:59

-- =============================================
-- USERS
-- =============================================

SET IDENTITY_INSERT users ON;

INSERT INTO users (id, username, full_name, email, password_hash, role_id) VALUES
    -- Giáo viên
    (1,  'GV001',    N'Nguyễn Văn Hùng',     'hungnv@qlbt.edu.vn',      @pw, 1),
    (2,  'GV002',    N'Trần Thị Mai',        'maitt@qlbt.edu.vn',       @pw, 1),
    (3,  'GV003',    N'Lê Hoàng Nam',        'namlh@qlbt.edu.vn',       @pw, 1),
    (4,  'GV004',    N'Phạm Thu Hà',         'hapt@qlbt.edu.vn',        @pw, 1),
    -- Sinh viên
    (5,  'HE180001', N'Nguyễn Minh Anh',     'anhnm.he180001@qlbt.edu.vn', @pw, 2),
    (6,  'HE180002', N'Trần Quốc Bảo',       'baotq.he180002@qlbt.edu.vn', @pw, 2),
    (7,  'HE180003', N'Lê Thị Cẩm Tú',       'tultc.he180003@qlbt.edu.vn', @pw, 2),
    (8,  'HE180004', N'Phạm Đức Duy',        'duypd.he180004@qlbt.edu.vn', @pw, 2),
    (9,  'HE180005', N'Hoàng Gia Huy',       'huyhg.he180005@qlbt.edu.vn', @pw, 2),
    (10, 'HE180006', N'Vũ Ngọc Lan',         'lanvn.he180006@qlbt.edu.vn', @pw, 2),
    (11, 'HE180007', N'Đặng Thành Long',     'longdt.he180007@qlbt.edu.vn', @pw, 2),
    (12, 'HE180008', N'Bùi Thị Hồng Nhung',  'nhungbth.he180008@qlbt.edu.vn', @pw, 2),
    (13, 'HE180009', N'Đỗ Hải Phong',        'phongdh.he180009@qlbt.edu.vn', @pw, 2),
    (14, 'HE180010', N'Ngô Thanh Quyên',     'quyennt.he180010@qlbt.edu.vn', @pw, 2),
    (15, 'HE180011', N'Dương Văn Sơn',       'sondv.he180011@qlbt.edu.vn', @pw, 2),
    (16, 'HE180012', N'Lý Thị Thảo',         'thaolt.he180012@qlbt.edu.vn', @pw, 2),
    (17, 'HE180013', N'Trịnh Công Thắng',    'thangtc.he180013@qlbt.edu.vn', @pw, 2),
    (18, 'HE180014', N'Mai Phương Thùy',     'thuymp.he180014@qlbt.edu.vn', @pw, 2),
    (19, 'HE180015', N'Hồ Đức Trung',        'trunghd.he180015@qlbt.edu.vn', @pw, 2),
    (20, 'HE180016', N'Phan Thị Uyên',       'uyenpt.he180016@qlbt.edu.vn', @pw, 2),
    (21, 'HE180017', N'Châu Quang Vinh',     'vinhcq.he180017@qlbt.edu.vn', @pw, 2),
    (22, 'HE180018', N'Tạ Thị Xuân',         'xuantt.he180018@qlbt.edu.vn', @pw, 2),
    (23, 'HE180019', N'Đinh Hoàng Yến',      'yendh.he180019@qlbt.edu.vn', @pw, 2),
    (24, 'HE180020', N'Kiều Minh Khôi',      'khoikm.he180020@qlbt.edu.vn', @pw, 2);

SET IDENTITY_INSERT users OFF;

-- =============================================
-- SEMESTERS
-- =============================================

SET IDENTITY_INSERT semesters ON;

INSERT INTO semesters (id, name, start_date, end_date) VALUES
    (1, N'SU26', '2026-05-04', '2026-08-23'),   -- đã kết thúc
    (2, N'FA26', '2026-09-07', '2026-12-27'),   -- đang diễn ra
    (3, N'SP27', '2027-01-11', '2027-05-02');   -- chưa bắt đầu

SET IDENTITY_INSERT semesters OFF;

-- =============================================
-- CLASSES
-- =============================================

SET IDENTITY_INSERT classes ON;

INSERT INTO classes (id, teacher_id, name, semester_id, major) VALUES
    (1, 1, N'SE1801', 1, N'Kỹ thuật phần mềm'),
    (2, 3, N'AI1801', 1, N'Trí tuệ nhân tạo'),
    (3, 1, N'SE1802', 2, N'Kỹ thuật phần mềm'),
    (4, 2, N'SE1803', 2, N'Kỹ thuật phần mềm'),
    (5, 3, N'AI1802', 2, N'Trí tuệ nhân tạo'),
    (6, 2, N'IA1801', 2, N'An toàn thông tin'),
    (7, 1, N'SE1804', 3, N'Kỹ thuật phần mềm'),
    (8, 4, N'GD1801', 3, N'Thiết kế đồ họa');

SET IDENTITY_INSERT classes OFF;

-- =============================================
-- CLASS_STUDENTS
-- =============================================

INSERT INTO class_students (class_id, student_id, is_active, joined_at, left_at) VALUES
    -- SE1801 (SU26)
    (1, 5,  1, '2026-04-28', NULL),
    (1, 6,  1, '2026-04-28', NULL),
    (1, 7,  1, '2026-04-28', NULL),
    (1, 8,  1, '2026-04-28', NULL),
    (1, 9,  1, '2026-04-28', NULL),
    (1, 10, 1, '2026-04-28', NULL),
    -- AI1801 (SU26)
    (2, 11, 1, '2026-04-28', NULL),
    (2, 12, 1, '2026-04-28', NULL),
    (2, 13, 1, '2026-04-28', NULL),
    (2, 14, 1, '2026-04-28', NULL),
    -- SE1802 (FA26) - id 10 bị đình chỉ
    (3, 5,  1, '2026-09-01', NULL),
    (3, 6,  1, '2026-09-01', NULL),
    (3, 7,  1, '2026-09-01', NULL),
    (3, 8,  1, '2026-09-01', NULL),
    (3, 9,  1, '2026-09-01', NULL),
    (3, 10, 0, '2026-09-01', '2026-10-05'),
    (3, 15, 1, '2026-09-01', NULL),
    -- SE1803 (FA26) - id 17 bị đình chỉ
    (4, 11, 1, '2026-09-01', NULL),
    (4, 12, 1, '2026-09-01', NULL),
    (4, 13, 1, '2026-09-01', NULL),
    (4, 14, 1, '2026-09-01', NULL),
    (4, 16, 1, '2026-09-01', NULL),
    (4, 17, 0, '2026-09-01', '2026-10-12'),
    -- AI1802 (FA26)
    (5, 5,  1, '2026-09-01', NULL),
    (5, 11, 1, '2026-09-01', NULL),
    (5, 18, 1, '2026-09-01', NULL),
    (5, 19, 1, '2026-09-01', NULL),
    (5, 20, 1, '2026-09-01', NULL),
    -- IA1801 (FA26)
    (6, 6,  1, '2026-09-01', NULL),
    (6, 12, 1, '2026-09-01', NULL),
    (6, 21, 1, '2026-09-01', NULL),
    (6, 22, 1, '2026-09-01', NULL),
    -- SE1804 (SP27, chưa bắt đầu)
    (7, 15, 1, '2026-10-20', NULL),
    (7, 16, 1, '2026-10-20', NULL),
    (7, 23, 1, '2026-10-20', NULL);
    -- GD1801 (SP27): chưa có sinh viên

-- =============================================
-- ASSIGNMENTS
-- =============================================

DECLARE @due4  DATETIME = DATEADD(DAY, -30, @eod);
DECLARE @due5  DATETIME = DATEADD(DAY, -7,  @eod);
DECLARE @due6  DATETIME = DATEADD(DAY, 7,   @eod);
DECLARE @due7  DATETIME = DATEADD(DAY, 21,  @eod);
DECLARE @due8  DATETIME = DATEADD(DAY, -14, @eod);
DECLARE @due9  DATETIME = DATEADD(DAY, -2,  @eod);
DECLARE @due10 DATETIME = DATEADD(DAY, 3,   @eod);
DECLARE @due11 DATETIME = DATEADD(DAY, -10, @eod);
DECLARE @due12 DATETIME = DATEADD(DAY, 10,  @eod);
DECLARE @due13 DATETIME = DATEADD(DAY, 5,   @eod);

SET IDENTITY_INSERT assignments ON;

INSERT INTO assignments (id, class_id, title, description, file_name, file_path, due_date, allow_late, is_closed, created_at) VALUES
    -- SE1801 (SU26)
    (1,  1, N'Lab 1 - Cài đặt môi trường .NET',
            N'Cài đặt Visual Studio 2022, .NET 8 SDK và chụp màn hình chương trình Hello World.',
            N'Lab1_CaiDatMoiTruong.pdf', N'1/1/1/assignment',
            '2026-05-20 23:59', 0, 0, '2026-05-06 08:00'),
    (2,  1, N'Đồ án cuối kỳ - Quản lý bán hàng',
            N'Xây dựng ứng dụng WPF quản lý bán hàng sử dụng Entity Framework Core. Nộp file zip gồm mã nguồn và báo cáo.',
            N'DoAn_QuanLyBanHang.docx', N'1/1/2/assignment',
            '2026-08-10 23:59', 1, 1, '2026-07-01 09:00'),
    -- AI1801 (SU26)
    (3,  2, N'Bài tập Python cơ bản',
            N'Giải 10 bài tập về vòng lặp, hàm và danh sách trong Python.',
            N'BaiTap_Python.ipynb', N'1/2/3/assignment',
            '2026-06-15 23:59', 0, 0, '2026-06-01 10:00'),
    -- SE1802 (FA26)
    (4,  3, N'Lab 1 - Lập trình hướng đối tượng C#',
            N'Cài đặt các lớp Hình học (Hình tròn, Hình chữ nhật) với kế thừa và đa hình.',
            N'Lab1_OOP.pdf', N'2/3/4/assignment',
            @due4, 0, 0, DATEADD(DAY, -14, @due4)),
    (5,  3, N'Lab 2 - LINQ và Collections',
            N'Truy vấn danh sách sinh viên bằng LINQ: lọc, sắp xếp, nhóm theo lớp.',
            N'Lab2_LINQ.pdf', N'2/3/5/assignment',
            @due5, 1, 0, DATEADD(DAY, -14, @due5)),
    (6,  3, N'Lab 3 - WPF cơ bản',
            N'Thiết kế form đăng nhập bằng WPF, sử dụng Grid và StackPanel.',
            N'Lab3_WPF.zip', N'2/3/6/assignment',
            @due6, 0, 0, DATEADD(DAY, -14, @due6)),
    (7,  3, N'Assignment - Quản lý thư viện',
            N'Xây dựng ứng dụng quản lý thư viện (sách, độc giả, mượn trả). Yêu cầu chi tiết xem mô tả này, không có file đề.',
            NULL, NULL,
            @due7, 0, 0, DATEADD(DAY, -2, @now)),
    -- SE1803 (FA26)
    (8,  4, N'Bài tập 1 - Cấu trúc dữ liệu',
            N'Cài đặt danh sách liên kết đơn và các thao tác thêm, xóa, tìm kiếm.',
            N'BT1_CTDL.pdf', N'2/4/8/assignment',
            @due8, 1, 1, DATEADD(DAY, -10, @due8)),
    (9,  4, N'Bài tập 2 - Thuật toán sắp xếp',
            N'So sánh thời gian chạy của Bubble sort, Quick sort và Merge sort.',
            N'BT2_SapXep.pdf', N'2/4/9/assignment',
            @due9, 0, 0, DATEADD(DAY, -10, @due9)),
    (10, 4, N'Bài tập 3 - Đệ quy',
            N'Giải bài toán Tháp Hà Nội và tính dãy Fibonacci bằng đệ quy.',
            N'BT3_DeQuy.pdf', N'2/4/10/assignment',
            @due10, 0, 1, DATEADD(DAY, -7, @due10)),
    -- AI1802 (FA26)
    (11, 5, N'Lab 1 - Hồi quy tuyến tính',
            N'Dự đoán giá nhà bằng mô hình hồi quy tuyến tính với scikit-learn.',
            N'Lab1_HoiQuy.ipynb', N'2/5/11/assignment',
            @due11, 0, 0, DATEADD(DAY, -14, @due11)),
    (12, 5, N'Lab 2 - Mạng nơ-ron',
            N'Xây dựng mạng nơ-ron đơn giản phân loại chữ số viết tay MNIST.',
            N'Lab2_MangNoRon.ipynb', N'2/5/12/assignment',
            @due12, 1, 0, DATEADD(DAY, -14, @due12)),
    -- IA1801 (FA26)
    (13, 6, N'Lab 1 - Mã hóa đối xứng',
            N'Cài đặt mã hóa và giải mã AES, so sánh các chế độ ECB và CBC.',
            N'Lab1_AES.pdf', N'2/6/13/assignment',
            @due13, 0, 0, DATEADD(DAY, -3, @now)),
    -- SE1804 (SP27)
    (14, 7, N'Bài tập chuẩn bị - Ôn tập C#',
            N'Ôn lại kiến thức C# cơ bản trước khi bắt đầu môn học.',
            N'OnTap_CSharp.pdf', N'3/7/14/assignment',
            '2027-01-25 23:59', 0, 0, DATEADD(DAY, -1, @now));

SET IDENTITY_INSERT assignments OFF;

-- =============================================
-- SUBMISSIONS
-- =============================================

SET IDENTITY_INSERT submissions ON;

INSERT INTO submissions (id, assignment_id, class_id, student_id, file_name, file_path, submit_count, submitted_at, score, comment, is_late) VALUES
    -- A1 - SE1801 (SU26), đã chấm hết; id 10 không nộp
    (1,  1, 1, 5,  N'HE180001_Lab1.zip', N'1/1/1/submissions/1', 1, '2026-05-18 20:15', 9.00, N'Làm tốt, ảnh chụp rõ ràng.', 0),
    (2,  1, 1, 6,  N'HE180002_Lab1.zip', N'1/1/1/submissions/2', 1, '2026-05-19 21:40', 8.50, N'Đạt yêu cầu.', 0),
    (3,  1, 1, 7,  N'HE180003_Lab1.pdf', N'1/1/1/submissions/3', 1, '2026-05-20 10:05', 7.00, N'Thiếu ảnh chụp phiên bản SDK.', 0),
    (4,  1, 1, 8,  N'HE180004_Lab1.zip', N'1/1/1/submissions/4', 1, '2026-05-17 14:30', 6.50, NULL, 0),
    (5,  1, 1, 9,  N'HE180005_Lab1.zip', N'1/1/1/submissions/5', 2, '2026-05-19 23:10', 9.50, N'Rất tốt.', 0),
    -- A2 - SE1801 (SU26), cho nộp muộn, đã đóng và chấm hết
    (6,  2, 1, 5,  N'HE180001_DoAn.zip', N'1/1/2/submissions/6',  1, '2026-08-09 22:00', 8.75, N'Giao diện đẹp, báo cáo đầy đủ.', 0),
    (7,  2, 1, 6,  N'HE180002_DoAn.zip', N'1/1/2/submissions/7',  2, '2026-08-12 09:30', 7.00, N'Nộp muộn 2 ngày.', 1),
    (8,  2, 1, 7,  N'HE180003_DoAn.zip', N'1/1/2/submissions/8',  1, '2026-08-10 23:45', 8.00, NULL, 0),
    (9,  2, 1, 8,  N'HE180004_DoAn.zip', N'1/1/2/submissions/9',  1, '2026-08-08 16:20', 5.50, N'Chức năng thống kê chưa chạy.', 0),
    (10, 2, 1, 9,  N'HE180005_DoAn.zip', N'1/1/2/submissions/10', 1, '2026-08-09 19:00', 9.00, N'Tốt.', 0),
    (11, 2, 1, 10, N'HE180006_DoAn.rar', N'1/1/2/submissions/11', 1, '2026-08-15 08:00', 6.00, N'Nộp muộn 5 ngày, thiếu báo cáo.', 1),
    -- A3 - AI1801 (SU26)
    (12, 3, 2, 11, N'HE180007_Python.ipynb', N'1/2/3/submissions/12', 1, '2026-06-14 21:00', 8.00, NULL, 0),
    (13, 3, 2, 12, N'HE180008_Python.ipynb', N'1/2/3/submissions/13', 1, '2026-06-15 23:30', 7.50, N'Bài 9 sai kết quả.', 0),
    (14, 3, 2, 13, N'HE180009_Python.ipynb', N'1/2/3/submissions/14', 3, '2026-06-10 15:45', 9.25, N'Code gọn gàng.', 0),
    (15, 3, 2, 14, N'HE180010_Python.zip',   N'1/2/3/submissions/15', 1, '2026-06-13 11:10', 0.00, N'Nộp nhầm bài của môn khác.', 0),
    -- A4 - SE1802, đóng theo hạn, đã chấm; id 9, 15 không nộp
    (16, 4, 3, 5,  N'HE180001_Lab1_OOP.zip', N'2/3/4/submissions/16', 1, DATEADD(DAY, -2,  @due4), 9.00, N'Áp dụng đa hình đúng.', 0),
    (17, 4, 3, 6,  N'HE180002_Lab1_OOP.zip', N'2/3/4/submissions/17', 2, DATEADD(DAY, -1,  @due4), 8.00, NULL, 0),
    (18, 4, 3, 7,  N'HE180003_Lab1_OOP.zip', N'2/3/4/submissions/18', 1, DATEADD(HOUR, -2, @due4), 7.50, N'Thiếu lớp Hình tam giác.', 0),
    (19, 4, 3, 8,  N'HE180004_Lab1_OOP.zip', N'2/3/4/submissions/19', 1, DATEADD(DAY, -3,  @due4), 6.00, N'Chưa ghi đè phương thức ToString.', 0),
    (20, 4, 3, 10, N'HE180006_Lab1_OOP.zip', N'2/3/4/submissions/20', 1, DATEADD(DAY, -1,  @due4), 7.00, NULL, 0),
    -- A5 - SE1802, hết hạn nhưng cho nộp muộn -> vẫn mở, chưa chấm
    (21, 5, 3, 5,  N'HE180001_Lab2_LINQ.zip', N'2/3/5/submissions/21', 1, DATEADD(DAY, -1, @due5), NULL, NULL, 0),
    (22, 5, 3, 6,  N'HE180002_Lab2_LINQ.zip', N'2/3/5/submissions/22', 2, DATEADD(DAY, 2,  @due5), NULL, NULL, 1),
    (23, 5, 3, 7,  N'HE180003_Lab2_LINQ.zip', N'2/3/5/submissions/23', 1, DATEADD(DAY, 6,  @due5), NULL, NULL, 1),
    (24, 5, 3, 15, N'HE180011_Lab2_LINQ.zip', N'2/3/5/submissions/24', 1, DATEADD(DAY, -2, @due5), NULL, NULL, 0),
    -- A6 - SE1802, còn hạn
    (25, 6, 3, 5,  N'HE180001_Lab3_WPF.zip', N'2/3/6/submissions/25', 1, DATEADD(DAY, -1,  @now), NULL, NULL, 0),
    (26, 6, 3, 8,  N'HE180004_Lab3_WPF.zip', N'2/3/6/submissions/26', 3, DATEADD(HOUR, -2, @now), NULL, NULL, 0),
    -- A8 - SE1803, gv đóng thủ công, đang chấm dở
    (27, 8, 4, 11, N'HE180007_BT1.zip', N'2/4/8/submissions/27', 1, DATEADD(DAY, -1,  @due8), 8.50, N'Tốt.', 0),
    (28, 8, 4, 12, N'HE180008_BT1.zip', N'2/4/8/submissions/28', 1, DATEADD(HOUR, -1, @due8), 7.00, N'Hàm xóa chưa xử lý phần tử đầu.', 0),
    (29, 8, 4, 13, N'HE180009_BT1.zip', N'2/4/8/submissions/29', 1, DATEADD(DAY, 2,   @due8), NULL, NULL, 1),
    (30, 8, 4, 14, N'HE180010_BT1.zip', N'2/4/8/submissions/30', 2, DATEADD(DAY, -2,  @due8), NULL, NULL, 0),
    (31, 8, 4, 17, N'HE180013_BT1.zip', N'2/4/8/submissions/31', 1, DATEADD(DAY, -1,  @due8), 4.00, N'Chương trình không biên dịch được.', 0),
    -- A9 - SE1803, đóng theo hạn, chưa chấm
    (32, 9, 4, 11, N'HE180007_BT2.zip', N'2/4/9/submissions/32', 1, DATEADD(DAY, -1,     @due9), NULL, NULL, 0),
    (33, 9, 4, 12, N'HE180008_BT2.zip', N'2/4/9/submissions/33', 1, DATEADD(MINUTE, -30, @due9), NULL, NULL, 0),
    (34, 9, 4, 16, N'HE180012_BT2.zip', N'2/4/9/submissions/34', 1, DATEADD(DAY, -2,     @due9), NULL, NULL, 0),
    -- A10 - SE1803, còn hạn nhưng gv đóng sớm
    (35, 10, 4, 13, N'HE180009_BT3.zip', N'2/4/10/submissions/35', 1, DATEADD(DAY, -1, @now), NULL, NULL, 0),
    -- A11 - AI1802, đóng theo hạn, đã chấm; id 20 không nộp
    (36, 11, 5, 5,  N'HE180001_HoiQuy.ipynb', N'2/5/11/submissions/36', 1, DATEADD(DAY, -1,  @due11), 9.50, N'Phân tích dữ liệu kỹ.', 0),
    (37, 11, 5, 11, N'HE180007_HoiQuy.ipynb', N'2/5/11/submissions/37', 1, DATEADD(HOUR, -3, @due11), 8.00, NULL, 0),
    (38, 11, 5, 18, N'HE180014_HoiQuy.ipynb', N'2/5/11/submissions/38', 2, DATEADD(DAY, -2,  @due11), 6.75, N'Chưa chuẩn hóa dữ liệu.', 0),
    (39, 11, 5, 19, N'HE180015_HoiQuy.zip',   N'2/5/11/submissions/39', 1, DATEADD(DAY, -1,  @due11), 7.25, NULL, 0),
    -- A12 - AI1802, còn hạn
    (40, 12, 5, 18, N'HE180014_MangNoRon.ipynb', N'2/5/12/submissions/40', 1, DATEADD(DAY, -1,  @now), NULL, NULL, 0),
    (41, 12, 5, 20, N'HE180016_MangNoRon.zip',   N'2/5/12/submissions/41', 1, DATEADD(HOUR, -3, @now), NULL, NULL, 0);

SET IDENTITY_INSERT submissions OFF;

COMMIT TRANSACTION;
GO

-- =============================================
-- Tóm tắt
-- =============================================

SELECT 'users' AS [table], COUNT(*) AS [rows] FROM users
UNION ALL SELECT 'semesters',      COUNT(*) FROM semesters
UNION ALL SELECT 'classes',        COUNT(*) FROM classes
UNION ALL SELECT 'class_students', COUNT(*) FROM class_students
UNION ALL SELECT 'assignments',    COUNT(*) FROM assignments
UNION ALL SELECT 'submissions',    COUNT(*) FROM submissions;
GO
