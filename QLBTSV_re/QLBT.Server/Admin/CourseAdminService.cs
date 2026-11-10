using Microsoft.EntityFrameworkCore;
using QLBT.Server.Data;
using QLBT.Server.Domain;
using QLBT.Server.Models;

namespace QLBT.Server.Admin;

/// <summary>
/// Courses (môn học), e.g. PRN212. Code and name can be edited at any time (classes refer to the id);
/// a course can be deleted only while no class uses it.
/// </summary>
public sealed class CourseAdminService
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

    public async Task<AdminResult> Create(string code, string name)
    {
        code = code.Trim();
        name = name.Trim();
        string? invalid = Validate(code, name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            if (await db.Courses.AnyAsync(c => c.Code == code))
            {
                return AdminResult.Fail("Course " + code + " already exists.");
            }

            Course course = new Course { Code = code, Name = name };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            AdminLog.Write("Created course " + code + " - " + name);
            return AdminResult.Success(course.Id);
        }
    }

    public async Task<AdminResult> Update(int id, string code, string name)
    {
        code = code.Trim();
        name = name.Trim();
        string? invalid = Validate(code, name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Course? course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
            if (course == null)
            {
                return AdminResult.Fail("Course no longer exists.");
            }
            if (await db.Courses.AnyAsync(c => c.Code == code && c.Id != id))
            {
                return AdminResult.Fail("Course " + code + " already exists.");
            }

            course.Code = code;
            course.Name = name;
            await db.SaveChangesAsync();
            AdminLog.Write("Updated course " + code + " - " + name);
            return AdminResult.Success(course.Id);
        }
    }

    /// <summary>Only a course that no class uses.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Course? course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
            if (course == null)
            {
                return AdminResult.Fail("Course no longer exists.");
            }
            int classes = await db.Classes.CountAsync(c => c.CourseId == id);
            if (classes > 0)
            {
                return AdminResult.Fail("Course " + course.Code + " is used by " + classes + " class(es) and cannot be deleted.");
            }

            db.Courses.Remove(course);
            await db.SaveChangesAsync();
            AdminLog.Write("Deleted course " + course.Code);
            return AdminResult.Success();
        }
    }

    private static string? Validate(string code, string name)
    {
        if (code.Length == 0)
        {
            return "Code is required.";
        }
        if (code.Length > 20)
        {
            return "Code is longer than 20 characters.";
        }
        if (code.Any(char.IsWhiteSpace))
        {
            return "Code cannot contain spaces.";
        }
        if (code.Any(ch => ch > 127))
        {
            return "Code can only use ASCII letters and digits (e.g. PRN212).";
        }
        if (name.Length == 0)
        {
            return "Name is required.";
        }
        if (name.Length > 100)
        {
            return "Name is longer than 100 characters.";
        }
        return null;
    }
}
