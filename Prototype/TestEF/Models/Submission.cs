using System;
using System.Collections.Generic;

namespace TestEF.Models;

public partial class Submission
{
    public int Id { get; set; }

    public int AssignmentId { get; set; }

    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public int SubmitCount { get; set; }

    public DateTime SubmittedAt { get; set; }

    public decimal? Score { get; set; }

    public string? Comment { get; set; }

    public bool IsLate { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual ClassStudent ClassStudent { get; set; } = null!;
}
