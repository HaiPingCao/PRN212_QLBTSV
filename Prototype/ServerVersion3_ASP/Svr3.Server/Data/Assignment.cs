using System;
using System.Collections.Generic;

namespace Svr3.Server.Data;

public partial class Assignment
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public DateTime DueDate { get; set; }

    public bool AllowLate { get; set; }

    public bool IsClosed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
