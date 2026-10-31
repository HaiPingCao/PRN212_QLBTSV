using System;
using System.Collections.Generic;

namespace Svr3.Server.Data;

public partial class ClassStudent
{
    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public bool IsActive { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
