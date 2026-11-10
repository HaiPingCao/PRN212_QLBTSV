using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QLBT.Server.Data;
using QLBT.Server.Domain;
using QLBT.Server.Files;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.Admin;

/// <summary>
/// Classes, enrolment and suspension. A class = group name + course + semester (e.g. SE1802 · PRN212 · FA26).
/// A class never changes its semester. Editing, deleting, adding and removing students are only allowed
/// before the semester starts; suspending is allowed at any time.
/// Rule: a student is active in at most one class per course per semester. The DB enforces it with the
/// filtered unique index uq_class_students_course; the checks here give a readable message first.
/// </summary>
public sealed class ClassAdminService
{
    /// <param name="semesterId">null = every semester.</param>
    public async Task<List<ClassRow>> ListClasses(int? semesterId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            IQueryable<Class> query = db.Classes.AsNoTracking();
            if (semesterId != null)
            {
                query = query.Where(c => c.SemesterId == semesterId);
            }
            return await query
                .OrderByDescending(c => c.Semester.StartDate).ThenBy(c => c.Course.Code).ThenBy(c => c.Name)
                .Select(c => new ClassRow
                {
                    Id = c.Id,
                    Name = c.Name,
                    CourseId = c.CourseId,
                    CourseCode = c.Course.Code,
                    CourseName = c.Course.Name,
                    SemesterId = c.SemesterId,
                    SemesterName = c.Semester.Name,
                    SemesterStart = c.Semester.StartDate,
                    TeacherId = c.TeacherId,
                    TeacherUsername = c.Teacher.Username,
                    TeacherName = c.Teacher.FullName,
                    ActiveCount = c.ClassStudents.Count(cs => cs.IsActive),
                    TotalCount = c.ClassStudents.Count
                })
                .ToListAsync();
        }
    }

    public async Task<List<UserRow>> ListTeachers()
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.Teacher)
                .OrderBy(u => u.Username)
                .Select(u => new UserRow { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, Role = UserRole.Teacher })
                .ToListAsync();
        }
    }

    public async Task<AdminResult> Create(int semesterId, int courseId, string name, int teacherId)
    {
        name = name.Trim();
        string? invalid = Validate(name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Semester? semester = await db.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == semesterId);
            if (semester == null)
            {
                return AdminResult.Fail("Semester no longer exists.");
            }
            if (SemesterRules.HasStarted(semester))
            {
                return AdminResult.Fail("Semester " + semester.Name + " has started: new classes cannot be added.");
            }
            string? conflict = await CheckReferencesAndUnique(db, null, semesterId, courseId, name, teacherId);
            if (conflict != null)
            {
                return AdminResult.Fail(conflict);
            }

            Class newClass = new Class { SemesterId = semesterId, CourseId = courseId, Name = name, TeacherId = teacherId };
            db.Classes.Add(newClass);
            await db.SaveChangesAsync();
            AdminLog.Write("Created class " + name + " (" + semester.Name + ")");
            return AdminResult.Success(newClass.Id);
        }
    }

    /// <summary>
    /// Name, course and teacher. The semester never changes. A course change is written with plain SQL
    /// (EF treats course_id as part of an alternate key) and the DB cascades it to class_students.
    /// </summary>
    public async Task<AdminResult> Update(int id, int courseId, string name, int teacherId)
    {
        name = name.Trim();
        string? invalid = Validate(name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Class? existing = await db.Classes.Include(c => c.Semester).FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
            {
                return AdminResult.Fail("Class no longer exists.");
            }
            if (SemesterRules.HasStarted(existing.Semester))
            {
                return AdminResult.Fail("Semester " + existing.Semester.Name + " has started: class " + existing.Name + " cannot be edited.");
            }
            string? conflict = await CheckReferencesAndUnique(db, id, existing.SemesterId, courseId, name, teacherId);
            if (conflict != null)
            {
                return AdminResult.Fail(conflict);
            }

            bool courseChanged = existing.CourseId != courseId;
            if (courseChanged)
            {
                // The active students must not already be in another class of the new course this semester.
                List<int> activeIds = await db.ClassStudents.Where(cs => cs.ClassId == id && cs.IsActive).Select(cs => cs.StudentId).ToListAsync();
                string? courseConflict = await CheckSameCourse(db, existing.SemesterId, courseId, activeIds, id);
                if (courseConflict != null)
                {
                    return AdminResult.Fail("Cannot change the course. " + courseConflict);
                }
            }

            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
            {
                if (courseChanged)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE classes SET course_id = {courseId} WHERE id = {id}");
                }
                existing.Name = name;
                existing.TeacherId = teacherId;
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            AdminLog.Write("Updated class " + name + " (" + existing.Semester.Name + ")");
            return AdminResult.Success(id);
        }
    }

    /// <summary>For the delete confirmation: how much goes with the class.</summary>
    public async Task<(int Assignments, int Submissions)> CountContents(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            int assignments = await db.Assignments.CountAsync(a => a.ClassId == id);
            int submissions = await db.Submissions.CountAsync(s => s.ClassId == id);
            return (assignments, submissions);
        }
    }

    /// <summary>DB first in a transaction (submissions, then the class; the DB cascades assignments and enrolments), then the folder.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        Class? existing;
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            existing = await db.Classes.AsNoTracking().Include(c => c.Semester).FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
            {
                return AdminResult.Fail("Class no longer exists.");
            }
            if (SemesterRules.HasStarted(existing.Semester))
            {
                return AdminResult.Fail("Semester " + existing.Semester.Name + " has started: class " + existing.Name + " cannot be deleted.");
            }

            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
            {
                // fk_submissions_class_students is NO ACTION, so submissions go first.
                await db.Submissions.Where(s => s.ClassId == id).ExecuteDeleteAsync();
                await db.Classes.Where(c => c.Id == id).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
        }

        FileStorage.DeleteDirectoryQuietly(DbFactory.Storage().ClassDirectory(existing.SemesterId, existing.Id));
        AdminLog.Write("Deleted class " + existing.Name + " (" + existing.Semester.Name + ")");
        return AdminResult.Success();
    }

    public async Task<List<ClassStudentRow>> ListStudents(int classId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.ClassStudents.AsNoTracking()
                .Where(cs => cs.ClassId == classId)
                .OrderBy(cs => cs.Student.Username)
                .Select(cs => new ClassStudentRow
                {
                    StudentId = cs.StudentId,
                    Username = cs.Student.Username,
                    FullName = cs.Student.FullName,
                    Email = cs.Student.Email,
                    IsActive = cs.IsActive,
                    JoinedAt = cs.JoinedAt,
                    LeftAt = cs.LeftAt
                })
                .ToListAsync();
        }
    }

    /// <summary>
    /// Students for the Add students picker: not in the class yet, and not active in another class of the
    /// same course in the same semester.
    /// </summary>
    public async Task<List<SelectableStudent>> ListAvailableStudents(int classId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Class? target = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
            if (target == null)
            {
                return new List<SelectableStudent>();
            }

            return await db.Users.AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.Student
                    && !u.ClassStudents.Any(cs => cs.ClassId == classId)
                    && !u.ClassStudents.Any(cs => cs.IsActive && cs.SemesterId == target.SemesterId && cs.CourseId == target.CourseId))
                .OrderBy(u => u.Username)
                .Select(u => new SelectableStudent { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email })
                .ToListAsync();
        }
    }

    /// <summary>
    /// Only before the semester starts. Non-students and students already in the class are skipped.
    /// All or nothing: if any student is already active in another class of the same course this semester, nobody is added.
    /// </summary>
    public async Task<AdminResult> AddStudents(int classId, IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return AdminResult.Fail("Select at least one student.");
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Class? existing = await db.Classes.AsNoTracking().Include(c => c.Semester).FirstOrDefaultAsync(c => c.Id == classId);
            if (existing == null)
            {
                return AdminResult.Fail("Class no longer exists.");
            }
            if (SemesterRules.HasStarted(existing.Semester))
            {
                return AdminResult.Fail("Semester " + existing.Semester.Name + " has started: students cannot be added.");
            }

            List<int> toAdd = await db.Users
                .Where(u => studentIds.Contains(u.Id) && u.RoleId == (int)UserRole.Student && !u.ClassStudents.Any(cs => cs.ClassId == classId))
                .Select(u => u.Id)
                .ToListAsync();
            string? courseConflict = await CheckSameCourse(db, existing.SemesterId, existing.CourseId, toAdd, classId);
            if (courseConflict != null)
            {
                return AdminResult.Fail(courseConflict);
            }

            DateTime now = DateTime.Now;
            foreach (int studentId in toAdd)
            {
                db.ClassStudents.Add(new ClassStudent
                {
                    ClassId = classId,
                    StudentId = studentId,
                    SemesterId = existing.SemesterId,
                    CourseId = existing.CourseId,
                    IsActive = true,
                    JoinedAt = now
                });
            }
            string? saveError = await SaveEnrolments(db);
            if (saveError != null)
            {
                return AdminResult.Fail(saveError);
            }
            AdminLog.Write("Added " + toAdd.Count + " student(s) to class " + existing.Name);
            return AdminResult.Success(classId);
        }
    }

    /// <summary>Hard delete of the enrolment, only before the semester starts and only without submissions.</summary>
    public async Task<AdminResult> RemoveStudent(int classId, int studentId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            ClassStudent? member = await db.ClassStudents
                .Include(cs => cs.Class).ThenInclude(c => c.Semester)
                .Include(cs => cs.Student)
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
            if (member == null)
            {
                return AdminResult.Fail("The student is no longer in this class.");
            }
            if (SemesterRules.HasStarted(member.Class.Semester))
            {
                return AdminResult.Fail("Semester " + member.Class.Semester.Name + " has started: suspend the student instead.");
            }
            if (await db.Submissions.AnyAsync(s => s.ClassId == classId && s.StudentId == studentId))
            {
                return AdminResult.Fail(member.Student.Username + " already has submissions in this class: suspend instead.");
            }

            db.ClassStudents.Remove(member);
            await db.SaveChangesAsync();
            AdminLog.Write("Removed " + member.Student.Username + " from class " + member.Class.Name);
            return AdminResult.Success();
        }
    }

    /// <summary>Allowed at any time. Suspend: is_active = 0, left_at = now. Un-suspend: is_active = 1, left_at = null.</summary>
    public async Task<AdminResult> SetSuspended(int classId, int studentId, bool suspended)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            ClassStudent? member = await db.ClassStudents
                .Include(cs => cs.Class)
                .Include(cs => cs.Student)
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
            if (member == null)
            {
                return AdminResult.Fail("The student is no longer in this class.");
            }
            if (!suspended)
            {
                // A suspended enrolment counts as removed, so the student may have joined another class of this course since.
                string? courseConflict = await CheckSameCourse(db, member.SemesterId, member.CourseId, new[] { studentId }, classId);
                if (courseConflict != null)
                {
                    return AdminResult.Fail("Cannot un-suspend. " + courseConflict);
                }
            }

            member.IsActive = !suspended;
            member.LeftAt = suspended ? DateTime.Now : null;
            string? saveError = await SaveEnrolments(db);
            if (saveError != null)
            {
                return AdminResult.Fail(saveError);
            }
            AdminLog.Write((suspended ? "Suspended " : "Un-suspended ") + member.Student.Username + " in class " + member.Class.Name);
            return AdminResult.Success();
        }
    }

    /// <summary>
    /// Rule: a student is active in at most one class per course per semester. Suspended enrolments count as removed.
    /// Returns a message naming the students and their current class, or null when there is no conflict.
    /// </summary>
    private static async Task<string?> CheckSameCourse(Prn212PQlbtContext db, int semesterId, int courseId, IReadOnlyCollection<int> studentIds, int excludeClassId)
    {
        if (studentIds.Count == 0)
        {
            return null;
        }

        List<string> conflicts = await db.ClassStudents.AsNoTracking()
            .Where(cs => cs.IsActive
                && studentIds.Contains(cs.StudentId)
                && cs.ClassId != excludeClassId
                && cs.SemesterId == semesterId
                && cs.CourseId == courseId)
            .OrderBy(cs => cs.Student.Username)
            .Select(cs => cs.Student.Username + " (" + cs.Class.Name + ")")
            .ToListAsync();
        if (conflicts.Count == 0)
        {
            return null;
        }
        string courseCode = await db.Courses.Where(c => c.Id == courseId).Select(c => c.Code).FirstAsync();
        return "A student can join only one " + courseCode + " class per semester. Already in another class: "
            + string.Join(", ", conflicts) + ".";
    }

    /// <summary>Saves enrolment changes; a hit on uq_class_students_course (someone else enrolled the student meanwhile) becomes a message.</summary>
    private static async Task<string?> SaveEnrolments(Prn212PQlbtContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException ex) when (ex.GetBaseException().Message.Contains("uq_class_students_course"))
        {
            return "A student can join only one class per course per semester (rejected by the database). Refresh and try again.";
        }
    }

    private static async Task<string?> CheckReferencesAndUnique(Prn212PQlbtContext db, int? classId, int semesterId, int courseId, string name, int teacherId)
    {
        bool isTeacher = await db.Users.AnyAsync(u => u.Id == teacherId && u.RoleId == (int)UserRole.Teacher);
        if (!isTeacher)
        {
            return "Choose a teacher.";
        }
        string? courseCode = await db.Courses.Where(c => c.Id == courseId).Select(c => c.Code).FirstOrDefaultAsync();
        if (courseCode == null)
        {
            return "Choose a course.";
        }
        bool duplicate = await db.Classes.AnyAsync(c => c.SemesterId == semesterId && c.Name == name && c.CourseId == courseId && c.Id != classId);
        if (duplicate)
        {
            return "Class " + name + " already has a " + courseCode + " class in this semester.";
        }
        return null;
    }

    private static string? Validate(string name)
    {
        if (name.Length == 0)
        {
            return "Name is required.";
        }
        if (name.Length > 50)
        {
            return "Name is longer than 50 characters.";
        }
        return null;
    }
}
