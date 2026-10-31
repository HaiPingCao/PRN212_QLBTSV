using System;
using System.Collections.Generic;

namespace TestEF.Models;

public partial class Semester
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}
