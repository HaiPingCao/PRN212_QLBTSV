using System.Diagnostics;
using System.Security.Cryptography;
using Svr3.Shared;

namespace Svr3.Client;

/// <summary>
/// End-to-end checks for authentication and file transfer. Uses the ids from db/QLBT_test_ds.sql,
/// so load that dataset first. Running it changes data (submission 25 is replaced, submit_count grows).
/// </summary>
public sealed class Scenario
{
    private const string Password = "123456";

    // From QLBT_test_ds.sql
    private const string TeacherOwner = "GV001";      // teaches SE1802 (class 3)
    private const string TeacherOther = "GV002";      // does not teach SE1802
    private const string Student = "HE180001";        // active in SE1802
    private const string StudentSuspended = "HE180006"; // suspended in SE1802
    private const int OpenAssignment = 6;             // SE1802, deadline in the future
    private const int ClosedAssignment = 4;           // SE1802, past deadline, no late
    private const int OtherStudentSubmission = 17;    // HE180002, A4
    private const int SuspendedOwnSubmission = 20;    // HE180006, A4 (no file on disk in the dataset)

    private readonly Uri _baseUrl;
    private readonly string _workDir;
    private readonly int _bigFileMb;
    private int _passed;
    private int _failed;

    public Scenario(Uri baseUrl, string workDir, int bigFileMb)
    {
        _baseUrl = baseUrl;
        _workDir = workDir;
        _bigFileMb = bigFileMb;
    }

    /// <returns>Number of failed checks.</returns>
    public async Task<int> Run()
    {
        Directory.CreateDirectory(_workDir);
        Console.WriteLine("Server: " + _baseUrl);
        Console.WriteLine("Work dir: " + _workDir);

        await Authentication();
        string assignmentHash = await TeacherUploadsAssignment();
        int submissionId = await StudentDownloadsAndSubmits(assignmentHash);
        await Permissions(submissionId);

        Console.WriteLine();
        Console.WriteLine("Passed: " + _passed + ", failed: " + _failed);
        return _failed;
    }

    private async Task Authentication()
    {
        Section("Authentication");
        using (ApiClient client = NewClient())
        {
            ApiResult<UserDto> anonymous = await client.Get<UserDto>(ApiRoutes.Me);
            Expect("No session -> Unauthorized", anonymous, ApiStatus.Unauthorized);

            ApiResult<LoginResponse> wrong = await client.Login(Student, "wrong-password");
            Expect("Wrong password -> InvalidCredentials", wrong, ApiStatus.InvalidCredentials);

            ApiResult<LoginResponse> unknown = await client.Login("nobody", Password);
            Expect("Unknown user -> InvalidCredentials", unknown, ApiStatus.InvalidCredentials);

            ApiResult<LoginResponse> login = await client.Login(Student, Password);
            Expect("Student login", login, ApiStatus.Ok);
            Check("Login returns role Student", login.Body?.User.Role == UserRole.Student, login.Body?.User.Role.ToString());

            ApiResult<UserDto> me = await client.Get<UserDto>(ApiRoutes.Me);
            Expect("GetProfile", me, ApiStatus.Ok);
            Check("Profile name is Vietnamese text", me.Body?.FullName == "Nguyễn Minh Anh", me.Body?.FullName);

            ApiResult<List<ClassDto>> teacherOnly = await client.Get<List<ClassDto>>(ApiRoutes.TeacherClasses);
            Expect("Student calls teacher endpoint -> Forbidden", teacherOnly, ApiStatus.Forbidden);

            string oldSession = client.SessionId!;
            ApiResult<object> logout = await client.Logout();
            Expect("Logout", logout, ApiStatus.Ok);
            Check("Logout clears SessionId", client.SessionId == null, null);

            // Replay the revoked session id: the server must reject it.
            using (ApiClient stale = NewClient())
            {
                stale.UseSession(oldSession);
                ApiResult<UserDto> afterLogout = await stale.Get<UserDto>(ApiRoutes.Me);
                Expect("Revoked session -> Unauthorized", afterLogout, ApiStatus.Unauthorized);
                Check("Unauthorized clears SessionId", stale.SessionId == null, null);
            }
        }
    }

    private async Task<string> TeacherUploadsAssignment()
    {
        Section("Teacher uploads assignment file");
        string localFile = CreateRandomFile("assignment.bin", 3);
        string uploadName = "Đề bài Lab 3 - WPF.txt";

        using (ApiClient teacher = await LoginAs(TeacherOwner))
        {
            ApiResult<List<ClassDto>> classes = await teacher.Get<List<ClassDto>>(ApiRoutes.TeacherClasses);
            Expect("GetMyClasses", classes, ApiStatus.Ok);
            Check("GV001 has 3 classes", classes.Body?.Count == 3, classes.Body?.Count.ToString());

            ApiResult<UploadResult> upload = await teacher.UploadFile<UploadResult>(
                ApiRoutes.WithId(ApiRoutes.TeacherAssignmentFile, OpenAssignment), localFile, uploadName);
            Expect("Upload assignment file (3 MB)", upload, ApiStatus.Ok);
            Check("Server received all bytes", upload.Body?.Length == new FileInfo(localFile).Length, upload.Body?.Length.ToString());
        }

        using (ApiClient other = await LoginAs(TeacherOther))
        {
            ApiResult<UploadResult> notOwner = await other.UploadFile<UploadResult>(
                ApiRoutes.WithId(ApiRoutes.TeacherAssignmentFile, OpenAssignment), localFile, uploadName);
            Expect("Other teacher uploads -> Forbidden", notOwner, ApiStatus.Forbidden);
        }

        return Hash(localFile);
    }

    private async Task<int> StudentDownloadsAndSubmits(string assignmentHash)
    {
        Section("Student downloads and submits");
        using (ApiClient student = await LoginAs(Student))
        {
            ApiResult<List<StudentAssignmentDto>> list = await student.Get<List<StudentAssignmentDto>>(ApiRoutes.StudentAssignments);
            Expect("GetMyAssignments", list, ApiStatus.Ok);
            StudentAssignmentDto? open = list.Body?.FirstOrDefault(a => a.Id == OpenAssignment);
            StudentAssignmentDto? closed = list.Body?.FirstOrDefault(a => a.Id == ClosedAssignment);
            Check("A6 listed as open", open != null && !open.IsClosed, null);
            Check("A4 listed as closed", closed != null && closed.IsClosed, null);

            ApiResult<DownloadedFile> download = await student.Download(
                ApiRoutes.WithId(ApiRoutes.StudentAssignmentFile, OpenAssignment), Path.Combine(_workDir, "student"));
            Expect("Download assignment file", download, ApiStatus.Ok);
            Check("Downloaded file name kept (UTF-8)", download.Body?.FileName == "Đề bài Lab 3 - WPF.txt", download.Body?.FileName);
            Check("Downloaded bytes match upload (SHA-256)", download.Body != null && Hash(download.Body.Path) == assignmentHash, null);

            // Large submission, timed.
            string bigFile = CreateRandomFile("submission.bin", _bigFileMb);
            Stopwatch watch = Stopwatch.StartNew();
            ApiResult<SubmitResult> submit = await student.UploadFile<SubmitResult>(
                ApiRoutes.WithId(ApiRoutes.StudentSubmission, OpenAssignment), bigFile, "HE180001_Lab3_WPF.zip");
            watch.Stop();
            Expect("Submit " + _bigFileMb + " MB (" + Speed(_bigFileMb, watch) + ")", submit, ApiStatus.Ok);
            Check("Submission not late", submit.Body != null && !submit.Body.IsLate, null);
            Check("Existing ungraded submission replaced (same id 25)", submit.Body?.SubmissionId == 25, submit.Body?.SubmissionId.ToString());
            int submissionId = submit.Body?.SubmissionId ?? 0;
            string bigHash = Hash(bigFile);

            watch.Restart();
            ApiResult<DownloadedFile> own = await student.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, submissionId), Path.Combine(_workDir, "student"));
            watch.Stop();
            Expect("Download own submission (" + Speed(_bigFileMb, watch) + ")", own, ApiStatus.Ok);
            Check("Submission bytes match (SHA-256)", own.Body != null && Hash(own.Body.Path) == bigHash, null);

            // Interrupted upload: announce 8 MB, die after 1 MB. The old file must survive.
            ApiResult<SubmitResult>? broken = null;
            bool threw = false;
            try
            {
                using (BreakingStream source = new BreakingStream(8 * 1024 * 1024, 1024 * 1024))
                {
                    broken = await student.Upload<SubmitResult>(
                        ApiRoutes.WithId(ApiRoutes.StudentSubmission, OpenAssignment), "broken.zip", source, source.Length);
                }
            }
            catch (HttpRequestException)
            {
                threw = true;
            }
            Check("Interrupted upload fails", threw || (broken != null && !broken.IsOk), broken?.ToString());

            ApiResult<DownloadedFile> afterBreak = await student.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, submissionId), Path.Combine(_workDir, "after-break"));
            Expect("Download after interrupted upload", afterBreak, ApiStatus.Ok);
            Check("Old submission kept intact", afterBreak.Body != null && Hash(afterBreak.Body.Path) == bigHash, afterBreak.Body?.FileName);

            string small = CreateRandomFile("small.bin", 1);
            ApiResult<SubmitResult> toClosed = await student.UploadFile<SubmitResult>(
                ApiRoutes.WithId(ApiRoutes.StudentSubmission, ClosedAssignment), small, "late.zip");
            Expect("Submit to closed assignment -> Rejected", toClosed, ApiStatus.Rejected);

            ApiResult<DownloadedFile> foreign = await student.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, OtherStudentSubmission), Path.Combine(_workDir, "student"));
            Expect("Download other student's submission -> Forbidden", foreign, ApiStatus.Forbidden);

            return submissionId;
        }
    }

    private async Task Permissions(int submissionId)
    {
        Section("Teacher and suspended student access");
        using (ApiClient teacher = await LoginAs(TeacherOwner))
        {
            ApiResult<List<SubmissionDto>> submissions = await teacher.Get<List<SubmissionDto>>(
                ApiRoutes.WithId(ApiRoutes.TeacherSubmissions, OpenAssignment));
            Expect("GetSubmissions", submissions, ApiStatus.Ok);
            Check("New submission listed", submissions.Body?.Any(s => s.Id == submissionId) == true, null);

            ApiResult<DownloadedFile> download = await teacher.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, submissionId), Path.Combine(_workDir, "teacher"));
            Expect("Owner teacher downloads submission", download, ApiStatus.Ok);
        }

        using (ApiClient other = await LoginAs(TeacherOther))
        {
            ApiResult<DownloadedFile> download = await other.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, submissionId), Path.Combine(_workDir, "other"));
            Expect("Other teacher downloads submission -> Forbidden", download, ApiStatus.Forbidden);

            ApiResult<List<SubmissionDto>> list = await other.Get<List<SubmissionDto>>(
                ApiRoutes.WithId(ApiRoutes.TeacherSubmissions, OpenAssignment));
            Expect("Other teacher lists submissions -> Forbidden", list, ApiStatus.Forbidden);
        }

        using (ApiClient suspended = await LoginAs(StudentSuspended))
        {
            ApiResult<DownloadedFile> assignment = await suspended.Download(
                ApiRoutes.WithId(ApiRoutes.StudentAssignmentFile, OpenAssignment), Path.Combine(_workDir, "suspended"));
            Expect("Suspended student downloads assignment -> Forbidden", assignment, ApiStatus.Forbidden);

            string small = CreateRandomFile("small.bin", 1);
            ApiResult<SubmitResult> submit = await suspended.UploadFile<SubmitResult>(
                ApiRoutes.WithId(ApiRoutes.StudentSubmission, OpenAssignment), small, "x.zip");
            Expect("Suspended student submits -> Forbidden", submit, ApiStatus.Forbidden);

            // Allowed to see old work; the dataset has no file on disk, so NotFound (not Forbidden) is the pass.
            ApiResult<DownloadedFile> old = await suspended.Download(
                ApiRoutes.WithId(ApiRoutes.SubmissionFile, SuspendedOwnSubmission), Path.Combine(_workDir, "suspended"));
            Expect("Suspended student reaches own old submission (file missing -> NotFound)", old, ApiStatus.NotFound);
        }
    }

    private ApiClient NewClient()
    {
        return new ApiClient(_baseUrl);
    }

    private async Task<ApiClient> LoginAs(string username)
    {
        ApiClient client = NewClient();
        ApiResult<LoginResponse> login = await client.Login(username, Password);
        Expect("Login " + username, login, ApiStatus.Ok);
        return client;
    }

    private string CreateRandomFile(string name, int megabytes)
    {
        string path = Path.Combine(_workDir, name);
        byte[] buffer = new byte[1024 * 1024];
        using (FileStream file = File.Create(path))
        {
            for (int i = 0; i < megabytes; i++)
            {
                RandomNumberGenerator.Fill(buffer);
                file.Write(buffer, 0, buffer.Length);
            }
        }
        return path;
    }

    private static string Hash(string path)
    {
        using (FileStream file = File.OpenRead(path))
        {
            return Convert.ToHexString(SHA256.HashData(file));
        }
    }

    private static string Speed(int megabytes, Stopwatch watch)
    {
        double seconds = Math.Max(watch.Elapsed.TotalSeconds, 0.001);
        return (megabytes / seconds).ToString("0.0") + " MB/s";
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("== " + title);
    }

    private void Expect(string name, ApiResult result, ApiStatus expected)
    {
        Check(name, result.Status == expected, result.ToString());
    }

    private void Check(string name, bool ok, string? detail)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine("[PASS] " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("[FAIL] " + name + (detail == null ? "" : "  -> " + detail));
        }
    }
}

/// <summary>Reports <paramref name="length"/> bytes but throws after <paramref name="failAfter"/>, like a dropped connection.</summary>
public sealed class BreakingStream : Stream
{
    private readonly long _length;
    private readonly long _failAfter;
    private long _position;

    public BreakingStream(long length, long failAfter)
    {
        _length = length;
        _failAfter = failAfter;
    }

    public override bool CanRead
    {
        get { return true; }
    }

    public override bool CanSeek
    {
        get { return false; }
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override long Length
    {
        get { return _length; }
    }

    public override long Position
    {
        get { return _position; }
        set { throw new NotSupportedException(); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _failAfter)
        {
            throw new IOException("Simulated connection drop.");
        }
        int n = (int)Math.Min(count, _failAfter - _position);
        Array.Fill(buffer, (byte)0xAB, offset, n);
        _position += n;
        return n;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
