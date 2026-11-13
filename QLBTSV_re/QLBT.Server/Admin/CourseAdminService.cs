using QLBT.Server.DAO;
using QLBT.Server.Domain;
using QLBT.Server.Models;

namespace QLBT.Server.Admin;

/// <summary>
/// Courses (môn học), e.g. PRN212. Code and name can be edited at any time (classes refer to the id);
/// a course can be deleted only while no class uses it.
/// </summary>
public sealed class CourseAdminService
{
    private readonly CourseDAO _courses = new CourseDAO();

    public Task<List<CourseRow>> List()
    {
        return _courses.List();
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
        if (await _courses.CodeExists(code, null))
        {
            return AdminResult.Fail("A course with the code " + code + " already exists.");
        }

        int id = await _courses.Insert(new Course { Code = code, Name = name });
        AdminLog.Write("Created course " + code + " - " + name);
        return AdminResult.Success(id);
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
        if (await _courses.GetById(id) == null)
        {
            return AdminResult.Fail("The course no longer exists.");
        }
        if (await _courses.CodeExists(code, id))
        {
            return AdminResult.Fail("A course with the code " + code + " already exists.");
        }

        await _courses.Update(id, code, name);
        AdminLog.Write("Updated course " + code + " - " + name);
        return AdminResult.Success(id);
    }

    /// <summary>Only a course that no class uses.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        Course? course = await _courses.GetById(id);
        if (course == null)
        {
            return AdminResult.Fail("The course no longer exists.");
        }
        int classes = await _courses.CountClasses(id);
        if (classes > 0)
        {
            return AdminResult.Fail("Course " + course.Code + " cannot be deleted because it is used by " + classes + " class(es).");
        }

        await _courses.Delete(id);
        AdminLog.Write("Deleted course " + course.Code);
        return AdminResult.Success();
    }

    private static string? Validate(string code, string name)
    {
        if (code.Length == 0)
        {
            return "Please enter a course code.";
        }
        if (code.Length > 20)
        {
            return "The course code must not exceed 20 characters.";
        }
        if (code.Any(char.IsWhiteSpace))
        {
            return "The course code must not contain spaces.";
        }
        if (code.Any(ch => ch > 127))
        {
            return "The course code may contain only English letters and digits (for example, PRN212).";
        }
        if (name.Length == 0)
        {
            return "Please enter a name.";
        }
        if (name.Length > 100)
        {
            return "The name must not exceed 100 characters.";
        }
        return null;
    }
}
