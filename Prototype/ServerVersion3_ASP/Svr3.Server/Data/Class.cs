using System;
using System.Collections.Generic;

namespace Svr3.Server.Data;

public partial class Class
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string Name { get; set; } = null!;

    public int SemesterId { get; set; }

    public string Major { get; set; } = null!;

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual Semester Semester { get; set; } = null!;

    public virtual User Teacher { get; set; } = null!;
}
