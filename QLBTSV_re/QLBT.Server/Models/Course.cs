using System;
using System.Collections.Generic;

namespace QLBT.Server.Models;

public partial class Course
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}
