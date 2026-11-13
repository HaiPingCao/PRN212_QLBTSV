using QLBT.Server.DAO;
using QLBT.Server.Domain;
using QLBT.Server.Models;

namespace QLBT.Server.Admin;

/// <summary>Semesters. Name and dates can always be edited; a semester can be deleted only when it has no classes.</summary>
public sealed class SemesterAdminService
{
    private readonly SemesterDAO _semesters = new SemesterDAO();

    /// <summary>Newest start date first.</summary>
    public Task<List<SemesterRow>> List()
    {
        return _semesters.List();
    }

    public async Task<AdminResult> Create(string name, DateOnly startDate, DateOnly endDate)
    {
        name = name.Trim();
        string? invalid = Validate(name, startDate, endDate);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }
        if (await _semesters.NameExists(name, null))
        {
            return AdminResult.Fail("A semester named " + name + " already exists.");
        }

        int id = await _semesters.Insert(new Semester { Name = name, StartDate = startDate, EndDate = endDate });
        AdminLog.Write("Created semester " + name + " (" + startDate + " - " + endDate + ")");
        return AdminResult.Success(id);
    }

    public async Task<AdminResult> Update(int id, string name, DateOnly startDate, DateOnly endDate)
    {
        name = name.Trim();
        string? invalid = Validate(name, startDate, endDate);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }
        if (await _semesters.GetById(id) == null)
        {
            return AdminResult.Fail("The semester no longer exists.");
        }
        if (await _semesters.NameExists(name, id))
        {
            return AdminResult.Fail("A semester named " + name + " already exists.");
        }

        await _semesters.Update(id, name, startDate, endDate);
        AdminLog.Write("Updated semester " + name + " (" + startDate + " - " + endDate + ")");
        return AdminResult.Success(id);
    }

    /// <summary>Only a semester with no classes (delete its classes first).</summary>
    public async Task<AdminResult> Delete(int id)
    {
        Semester? semester = await _semesters.GetById(id);
        if (semester == null)
        {
            return AdminResult.Fail("The semester no longer exists.");
        }
        if (await _semesters.HasClasses(id))
        {
            return AdminResult.Fail("Semester " + semester.Name + " cannot be deleted because it still contains classes. Please delete those classes first.");
        }

        await _semesters.Delete(id);
        AdminLog.Write("Deleted semester " + semester.Name);
        return AdminResult.Success();
    }

    private static string? Validate(string name, DateOnly startDate, DateOnly endDate)
    {
        if (name.Length == 0)
        {
            return "Please enter a name.";
        }
        if (name.Length > 20)
        {
            return "The name must not exceed 20 characters.";
        }
        if (endDate <= startDate)
        {
            return "The end date must be later than the start date.";
        }
        return null;
    }
}
