using System;
using System.Collections.Generic;

namespace QLBT.Server.Models;

public partial class Class
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string Name { get; set; } = null!;

    public int SemesterId { get; set; }

    public int CourseId { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual Course Course { get; set; } = null!;

    public virtual Semester Semester { get; set; } = null!;

    public virtual User Teacher { get; set; } = null!;
}
