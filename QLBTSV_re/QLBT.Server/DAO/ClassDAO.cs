using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QLBT.Server.Admin;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.DAO;

/// <summary>
/// Queries on classes and class_students (enrolments) for the admin GUI. One short-lived context per call.
/// Enrolment writes return false when the DB rejects them with uq_class_students_course
/// (a student active in two classes of the same course in one semester).
/// </summary>
public sealed class ClassDAO
{
    private const string SameCourseIndex = "uq_class_students_course";

    /// <param name="semesterId">null = every semester.</param>
    public async Task<List<ClassRow>> List(int? semesterId)
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
                    TeacherId = c.TeacherId,
                    TeacherUsername = c.Teacher.Username,
                    TeacherName = c.Teacher.FullName,
                    ActiveCount = c.ClassStudents.Count(cs => cs.IsActive),
                    TotalCount = c.ClassStudents.Count
                })
                .ToListAsync();
        }
    }

    /// <summary>The class with its semester loaded, or null.</summary>
    public async Task<Class?> GetById(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Classes.AsNoTracking().Include(c => c.Semester).FirstOrDefaultAsync(c => c.Id == id);
        }
    }

    /// <summary>uq_classes: name + course + semester.</summary>
    /// <param name="excludeId">The class being edited, or null when adding.</param>
    public async Task<bool> NameExists(int semesterId, int courseId, string name, int? excludeId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Classes.AnyAsync(c => c.SemesterId == semesterId && c.CourseId == courseId && c.Name == name && c.Id != excludeId);
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

    /// <summary>Returns the new id.</summary>
    public async Task<int> Insert(Class newClass)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            db.Classes.Add(newClass);
            await db.SaveChangesAsync();
            return newClass.Id;
        }
    }

    /// <summary>
    /// Name, course and teacher in one transaction. EF treats course_id as part of an alternate key and refuses
    /// to modify it, so a course change is written with plain SQL; the DB cascades it to class_students.
    /// </summary>
    public async Task Update(int id, int courseId, string name, int teacherId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Class existing = await db.Classes.FirstAsync(c => c.Id == id);
            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
            {
                if (existing.CourseId != courseId)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE classes SET course_id = {courseId} WHERE id = {id}");
                }
                existing.Name = name;
                existing.TeacherId = teacherId;
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
    }

    /// <summary>In one transaction: submissions first (fk_submissions_class_students is NO ACTION), then the class; the DB cascades assignments and enrolments.</summary>
    public async Task Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
            {
                await db.Submissions.Where(s => s.ClassId == id).ExecuteDeleteAsync();
                await db.Classes.Where(c => c.Id == id).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
        }
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
    public async Task<List<SelectableStudent>> ListAvailableStudents(int semesterId, int courseId, int classId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.Student
                    && !u.ClassStudents.Any(cs => cs.ClassId == classId)
                    && !u.ClassStudents.Any(cs => cs.IsActive && cs.SemesterId == semesterId && cs.CourseId == courseId))
                .OrderBy(u => u.Username)
                .Select(u => new SelectableStudent { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email })
                .ToListAsync();
        }
    }

    /// <summary>The active students of a class.</summary>
    public async Task<List<int>> ActiveStudentIds(int classId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.ClassStudents.Where(cs => cs.ClassId == classId && cs.IsActive).Select(cs => cs.StudentId).ToListAsync();
        }
    }

    /// <summary>The ids from <paramref name="studentIds"/> that are students and not in the class yet.</summary>
    public async Task<List<int>> FilterNewStudents(int classId, IReadOnlyCollection<int> studentIds)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users
                .Where(u => studentIds.Contains(u.Id) && u.RoleId == (int)UserRole.Student && !u.ClassStudents.Any(cs => cs.ClassId == classId))
                .Select(u => u.Id)
                .ToListAsync();
        }
    }

    /// <summary>
    /// "username (class)" for each of <paramref name="studentIds"/> already active in another class of this course
    /// in this semester. Empty when there is no conflict.
    /// </summary>
    public async Task<List<string>> FindSameCourseConflicts(int semesterId, int courseId, IReadOnlyCollection<int> studentIds, int excludeClassId)
    {
        if (studentIds.Count == 0)
        {
            return new List<string>();
        }
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.ClassStudents.AsNoTracking()
                .Where(cs => cs.IsActive
                    && studentIds.Contains(cs.StudentId)
                    && cs.ClassId != excludeClassId
                    && cs.SemesterId == semesterId
                    && cs.CourseId == courseId)
                .OrderBy(cs => cs.Student.Username)
                .Select(cs => cs.Student.Username + " (" + cs.Class.Name + ")")
                .ToListAsync();
        }
    }

    /// <summary>The enrolment with its class and student loaded, or null.</summary>
    public async Task<ClassStudent?> GetEnrolment(int classId, int studentId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.ClassStudents.AsNoTracking()
                .Include(cs => cs.Class)
                .Include(cs => cs.Student)
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
        }
    }

    public async Task<bool> HasSubmissions(int classId, int studentId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Submissions.AnyAsync(s => s.ClassId == classId && s.StudentId == studentId);
        }
    }

    /// <summary>Active enrolments joined now. semester_id / course_id are copied from the class. False when the DB rejected it.</summary>
    public async Task<bool> AddStudents(Class target, IEnumerable<int> studentIds)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            DateTime now = DateTime.Now;
            foreach (int studentId in studentIds)
            {
                db.ClassStudents.Add(new ClassStudent
                {
                    ClassId = target.Id,
                    StudentId = studentId,
                    SemesterId = target.SemesterId,
                    CourseId = target.CourseId,
                    IsActive = true,
                    JoinedAt = now
                });
            }
            return await SaveEnrolments(db);
        }
    }

    /// <summary>Suspend: is_active = 0, left_at = now. Un-suspend: is_active = 1, left_at = null. False when the DB rejected it.</summary>
    public async Task<bool> SetSuspended(int classId, int studentId, bool suspended)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            ClassStudent member = await db.ClassStudents.FirstAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
            member.IsActive = !suspended;
            member.LeftAt = suspended ? DateTime.Now : null;
            return await SaveEnrolments(db);
        }
    }

    /// <summary>Hard delete of the enrolment.</summary>
    public async Task RemoveStudent(int classId, int studentId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            await db.ClassStudents.Where(cs => cs.ClassId == classId && cs.StudentId == studentId).ExecuteDeleteAsync();
        }
    }

    /// <summary>Someone else may have enrolled the student meanwhile: a hit on the unique index becomes false.</summary>
    private static async Task<bool> SaveEnrolments(Prn212PQlbtContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.GetBaseException().Message.Contains(SameCourseIndex))
        {
            return false;
        }
    }
}
