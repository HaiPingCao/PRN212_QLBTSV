namespace Svr3.Shared;

public sealed class LoginRequest
{
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";
}

public sealed class LoginResponse
{
    public string SessionId { get; set; } = "";

    public UserDto User { get; set; } = new UserDto();
}

public sealed class UserDto
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public UserRole Role { get; set; }
}

public sealed class ClassDto
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Major { get; set; } = "";

    public string Semester { get; set; } = "";

    /// <summary>Active (not suspended) students only.</summary>
    public int StudentCount { get; set; }
}

public sealed class SubmissionDto
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string StudentUsername { get; set; } = "";

    public string StudentName { get; set; } = "";

    public DateTime SubmittedAt { get; set; }

    public bool IsLate { get; set; }

    public decimal? Score { get; set; }

    public string FileName { get; set; } = "";
}

public sealed class StudentAssignmentDto
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public string ClassName { get; set; } = "";

    public string Title { get; set; } = "";

    public DateTime DueDate { get; set; }

    /// <summary>IsClosed || (now > DueDate && !AllowLate), computed by the server.</summary>
    public bool IsClosed { get; set; }

    public bool HasFile { get; set; }

    /// <summary>The student is suspended in this class.</summary>
    public bool ClassSuspended { get; set; }
}

public sealed class UploadResult
{
    public string FileName { get; set; } = "";

    public long Length { get; set; }
}

public sealed class SubmitResult
{
    public int SubmissionId { get; set; }

    public bool IsLate { get; set; }

    public int SubmitCount { get; set; }

    public long Length { get; set; }
}
