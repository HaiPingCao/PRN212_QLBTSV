using Microsoft.EntityFrameworkCore;
using QLBT.Server.Admin;
using QLBT.Server.Models;

namespace QLBT.Server.DAO;

/// <summary>Queries on the courses table for the admin GUI. One short-lived context per call.</summary>
public sealed class CourseDAO
{
    public async Task<List<CourseRow>> List()
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Courses.AsNoTracking()
                .OrderBy(c => c.Code)
                .Select(c => new CourseRow
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    ClassCount = c.Classes.Count
                })
                .ToListAsync();
        }
    }

    public async Task<Course?> GetById(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }
    }

    /// <param name="excludeId">The course being edited, or null when adding.</param>
    public async Task<bool> CodeExists(string code, int? excludeId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Courses.AnyAsync(c => c.Code == code && c.Id != excludeId);
        }
    }

    public async Task<int> CountClasses(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Classes.CountAsync(c => c.CourseId == id);
        }
    }

    /// <summary>Returns the new id.</summary>
    public async Task<int> Insert(Course course)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            return course.Id;
        }
    }

    public async Task Update(int id, string code, string name)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Course course = await db.Courses.FirstAsync(c => c.Id == id);
            course.Code = code;
            course.Name = name;
            await db.SaveChangesAsync();
        }
    }

    public async Task Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            await db.Courses.Where(c => c.Id == id).ExecuteDeleteAsync();
        }
    }
}
