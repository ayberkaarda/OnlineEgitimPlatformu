using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Services and repositories together on SQLite in-memory: sign-in, lockout, password migration,
/// registration, courses, notes, enrollments and the role and ownership rules end to end. Each test
/// gets a fresh database.
/// </summary>
public sealed class SqliteServiceTests : IAsyncLifetime
{
    private const string NotFound = "Ders bulunamadı.";

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAuthService Auth => _db.Get<IAuthService>();

    private ICourseService Courses => _db.Get<ICourseService>();

    private IEnrollmentService Enrollments => _db.Get<IEnrollmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task SignIn_EachRoleOnlyThroughItsOwnSignIn()
    {
        var teacher = await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
        var student = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);

        Assert.True(teacher.IsTeacher);
        Assert.Equal(DenizId, teacher.UserId);
        Assert.True(student.IsStudent);

        // A teacher's name and password do not open the student sign-in, and the other way round.
        var asStudent = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Deniz Örnek", TeacherPassword, Ct));
        var asTeacher = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Mert Deneme", StudentPassword, Ct));
        Assert.Equal(AuthService.CredentialsInvalid, asStudent.Message);
        Assert.Equal(AuthService.CredentialsInvalid, asTeacher.Message);
    }

    [Fact]
    public async Task RegisterStudent_StoresAHashAndAllowsSignIn()
    {
        await Auth.RegisterStudentAsync(" Ayşe Yılmaz ", "ayse@example.com", "Gizli.Sifre1", Ct);

        var session = await Auth.SignInStudentAsync("Ayşe Yılmaz", "Gizli.Sifre1", Ct);
        await using var context = _db.CreateContext();
        var stored = await context.Students.SingleAsync(s => s.Id == session.UserId, Ct);

        Assert.Equal("Ayşe Yılmaz", stored.FullName);
        Assert.StartsWith("PBKDF2-SHA256$", stored.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain("Gizli.Sifre1", stored.PasswordHash, StringComparison.Ordinal);
    }

    /// <summary>A taken name and a taken e-mail address get one answer, so neither can be probed.</summary>
    [Fact]
    public async Task RegisterStudent_TakenNameOrEmail_SameAnswerAndNoNewRow()
    {
        var name = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.RegisterStudentAsync("Mert Deneme", "yeni@example.com", "Gizli.Sifre1", Ct));
        var email = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.RegisterStudentAsync("Yeni Kişi", "selin@example.com", "Gizli.Sifre1", Ct));

        Assert.Equal(name.Message, email.Message);
        await using var context = _db.CreateContext();
        Assert.Equal(2, await context.Students.CountAsync(Ct));
    }

    [Fact]
    public async Task Database_UniqueIndexes_RejectDuplicateStudentsWrittenPastTheServices()
    {
        await using var context = _db.CreateContext();
        context.Students.Add(new Student { FullName = "Mert Deneme", Email = "baska@example.com", PasswordHash = "-" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Lockout_AfterFiveFailures_RefusesTheRightPasswordUntilTheLockEnds()
    {
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", "yanlis", Ct));
        }

        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct));
        Assert.Equal(AuthService.CredentialsInvalid, locked.Message);

        _db.Clock.Advance(TimeSpan.FromMinutes(14));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct));

        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var session = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);

        await using var context = _db.CreateContext();
        var student = await context.Students.SingleAsync(s => s.Id == session.UserId, Ct);
        Assert.Equal(0, student.FailedLoginCount);
        Assert.Null(student.LockoutEnd);

        // The lock of one table does not touch the account with the same id in the other table.
        await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
    }

    [Fact]
    public async Task Lockout_SuccessfulSignInResetsTheCounter()
    {
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Deniz Örnek", "yanlis", Ct));
        }

        await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Deniz Örnek", "yanlis", Ct));

        // One failure after the reset does not lock the account.
        await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
    }

    [Fact]
    public async Task LegacyPlainTextPassword_IsHashedOnTheFirstSuccessfulSignIn()
    {
        // As written by the original application: the password in plain text.
        await using (var context = _db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Egitmenler SET Sifre = '1234', SifreBicimi = 1 WHERE EgitmenID = 1", Ct);
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Deniz Örnek", "12345", Ct));
        await Auth.SignInTeacherAsync("Deniz Örnek", " 1234 ", Ct); // the original application trimmed passwords

        await using (var context = _db.CreateContext())
        {
            var stored = await context.Teachers.SingleAsync(t => t.Id == DenizId, Ct);
            Assert.StartsWith("PBKDF2-SHA256$1000$", stored.PasswordHash, StringComparison.Ordinal);
            Assert.Equal(PasswordFormat.Pbkdf2, stored.PasswordFormat);
        }

        await Auth.SignInTeacherAsync("Deniz Örnek", "1234", Ct);
    }

    [Fact]
    public async Task AccountWithoutPassword_CannotSignInWithAnyInput()
    {
        await using (var context = _db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Ogrenci SET Sifre = '', SifreBicimi = 2 WHERE OgrenciID = 1", Ct);
        }

        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", "", Ct));
        var spaces = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", "   ", Ct));
        var any = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", "herhangi", Ct));

        Assert.Equal(AuthService.InputRequired, empty.Message);
        Assert.Equal(AuthService.CredentialsInvalid, spaces.Message);
        Assert.Equal(AuthService.CredentialsInvalid, any.Message);
    }

    [Fact]
    public async Task CreateCourse_BelongsToTheSignedInTeacherOnly()
    {
        var id = await Courses.CreateAsync(Teacher(EceId), " Grafik ", " Renk ve biçim ", Ct);

        var mine = await Courses.GetTeachingAsync(Teacher(EceId), Ct);
        var theirs = await Courses.GetTeachingAsync(Teacher(DenizId), Ct);

        Assert.Equal([EceCourseId, id], mine.Select(c => c.Id));
        Assert.DoesNotContain(theirs, c => c.Id == id);
        var created = mine.Single(c => c.Id == id);
        Assert.Equal("Grafik", created.Name);
        Assert.Equal("Renk ve biçim", created.Topic);
        Assert.Equal("Ece Taslak", created.TeacherName);
        await Assert.ThrowsAsync<AccessDeniedException>(() => Courses.CreateAsync(Student(MertId), "Ders", "Konu", Ct));
    }

    [Fact]
    public async Task Catalog_ListsEveryCourseWithItsTeacher()
    {
        var catalog = await Courses.GetCatalogAsync(Student(MertId), Ct);

        Assert.Equal(["Temel Programlama / Deniz Örnek", "Web Tasarımı / Ece Taslak"], catalog.Select(c => c.Name + " / " + c.TeacherName));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Courses.GetCatalogAsync(Teacher(DenizId), Ct));
    }

    [Fact]
    public async Task Enroll_Twice_IsRejectedAndKeepsOneRow()
    {
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct));

        Assert.Equal("Bu dersi zaten seçtiniz.", error.Message);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.Enrollments.CountAsync(Ct));
    }

    [Fact]
    public async Task Database_PrimaryKey_RejectsADuplicateEnrollmentWrittenPastTheServices()
    {
        await using var context = _db.CreateContext();
        context.Enrollments.Add(new Enrollment { StudentId = MertId, CourseId = DenizCourseId });
        await context.SaveChangesAsync(Ct);

        await using var other = _db.CreateContext();
        other.Enrollments.Add(new Enrollment { StudentId = MertId, CourseId = DenizCourseId });
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task MyCourses_ShowOnlyTheStudentsOwnEnrollments()
    {
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await Enrollments.EnrollAsync(Student(SelinId), EceCourseId, Ct);

        Assert.Equal([DenizCourseId], (await Enrollments.GetMyCoursesAsync(Student(MertId), Ct)).Select(c => c.Id));
        Assert.Equal([EceCourseId], (await Enrollments.GetMyCoursesAsync(Student(SelinId), Ct)).Select(c => c.Id));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Enrollments.GetMyCoursesAsync(Teacher(DenizId), Ct));
    }

    [Fact]
    public async Task Enroll_MissingCourse_IsRefused()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), 999, Ct));

        Assert.Equal(NotFound, error.Message);
        await Assert.ThrowsAsync<AccessDeniedException>(() => Enrollments.EnrollAsync(Teacher(DenizId), DenizCourseId, Ct));
    }

    // IDOR: valid sessions must not reach other teachers' courses by passing their ids; a foreign and a
    // missing course get the same answer.
    [Fact]
    public async Task Teacher_ForeignCourse_LooksExactlyLikeAMissingOne()
    {
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        var ece = Teacher(EceId);

        var foreignStudents = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.GetStudentsAsync(ece, DenizCourseId, Ct));
        var missingStudents = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.GetStudentsAsync(ece, 999, Ct));
        var foreignDetails = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.GetDetailsAsync(ece, DenizCourseId, Ct));
        var missingDetails = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.GetDetailsAsync(ece, 999, Ct));
        var foreignNote = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.AddNoteAsync(ece, DenizCourseId, "Sızma", Ct));
        var missingNote = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.AddNoteAsync(ece, 999, "Sızma", Ct));

        Assert.All(
            new[] { foreignStudents, missingStudents, foreignDetails, missingDetails, foreignNote, missingNote },
            e => Assert.Equal(NotFound, e.Message));
        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.Notes.CountAsync(Ct));
    }

    [Fact]
    public async Task Teacher_OwnCourse_SeesStudentsAndNotes()
    {
        await Enrollments.EnrollAsync(Student(SelinId), DenizCourseId, Ct);
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await Courses.AddNoteAsync(Teacher(DenizId), DenizCourseId, " Birinci not ", Ct);
        await Courses.AddNoteAsync(Teacher(DenizId), DenizCourseId, "İkinci not", Ct);

        var details = await Courses.GetDetailsAsync(Teacher(DenizId), DenizCourseId, Ct);

        Assert.Equal(["Mert Deneme", "Selin Deneme"], await Enrollments.GetStudentsAsync(Teacher(DenizId), DenizCourseId, Ct));
        Assert.Equal(["Birinci not", "İkinci not"], details.Notes);
        Assert.Equal(["Mert Deneme", "Selin Deneme"], details.Students);
        Assert.Equal(2, details.StudentCount);
    }

    /// <summary>
    /// The catalogue is public, but a course's notes are for its students only and no student sees
    /// the names of other students.
    /// </summary>
    [Fact]
    public async Task Student_SeesNotesOfJoinedCoursesOnlyAndNoOtherStudentsNames()
    {
        await Courses.AddNoteAsync(Teacher(DenizId), DenizCourseId, "Ders notu", Ct);
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);

        var joined = await Courses.GetDetailsAsync(Student(MertId), DenizCourseId, Ct);
        var notJoined = await Courses.GetDetailsAsync(Student(SelinId), DenizCourseId, Ct);
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.GetDetailsAsync(Student(SelinId), 999, Ct));

        Assert.True(joined.NotesVisible);
        Assert.Equal(["Ders notu"], joined.Notes);
        Assert.Null(joined.Students);
        Assert.False(notJoined.NotesVisible);
        Assert.Empty(notJoined.Notes);
        Assert.Null(notJoined.Students);
        Assert.Equal(1, notJoined.StudentCount);
        Assert.Equal(NotFound, missing.Message);
    }

    [Fact]
    public async Task Notes_StudentCannotAdd()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Courses.AddNoteAsync(Student(MertId), DenizCourseId, "Not", Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Enrollments.GetStudentsAsync(Student(MertId), DenizCourseId, Ct));
    }

    [Fact]
    public async Task Setup_IsNotOfferedOnceATeacherExists()
    {
        var setup = _db.Get<ISetupService>();

        Assert.False(await setup.IsSetupRequiredAsync(Ct));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync("Yeni Öğretmen", "Kimya", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        Assert.Equal(SetupService.AlreadyDone, error.Message);
        await using var context = _db.CreateContext();
        Assert.Equal(2, await context.Teachers.CountAsync(Ct));
    }
}
