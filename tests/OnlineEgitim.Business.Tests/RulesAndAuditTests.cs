using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Tests;

public sealed class InputRulesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\r\n", null)]
    [InlineData(" Ayşe ", "Ayşe")]
    public void Clean_TreatsNullEmptyAndWhiteSpaceAlike(string? input, string? expected) =>
        Assert.Equal(expected, InputRules.Clean(input));

    [Theory]
    [InlineData("Ayşe Yılmaz", true)]
    [InlineData("Ali O'Neil", true)]
    [InlineData("Zeynep Ç. Kara-Deniz", true)]
    [InlineData("Ayşe  Yılmaz", false)]
    [InlineData(" Ayşe", false)]
    [InlineData("Ayşe1", false)]
    [InlineData("Ayşe\nYılmaz", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PersonName_LettersAndSingleSpaces(string? value, bool valid) =>
        Assert.Equal(valid, InputRules.IsValidPersonName(value));

    [Fact]
    public void PersonName_LengthLimit() =>
        Assert.False(InputRules.IsValidPersonName(new string('a', InputRules.PersonNameMaxLength + 1)));

    [Theory]
    [InlineData("a@example.com", true)]
    [InlineData("a@b", false)]
    [InlineData("a b@example.com", false)]
    [InlineData("@example.com", false)]
    public void Email_Shape(string value, bool valid) => Assert.Equal(valid, InputRules.IsValidEmail(value));
}

/// <summary>The written audit codes are an interface of the log: every event is pinned to its code and value.</summary>
public sealed class AuditTests
{
    public static TheoryData<AuditEvent, int, string> ExpectedCodes => new()
    {
        { AuditEvent.SignInSucceeded, 1, "auth.sign_in.succeeded" },
        { AuditEvent.SignInFailed, 2, "auth.sign_in.failed" },
        { AuditEvent.PasswordUpgraded, 3, "auth.password.upgraded" },
        { AuditEvent.StudentRegistered, 4, "auth.student.registered" },
        { AuditEvent.FirstTeacherCreated, 5, "setup.teacher.created" },
        { AuditEvent.CourseCreated, 6, "course.created" },
        { AuditEvent.NoteAdded, 7, "course.note.added" },
        { AuditEvent.Enrolled, 8, "course.enrolled" },
        { AuditEvent.AccessDenied, 9, "access.denied" },
        { AuditEvent.SetupRefused, 10, "setup.refused" },
        { AuditEvent.RecoveryCodeIssued, 11, "recovery.code.issued" },
        { AuditEvent.AccountRecovered, 12, "recovery.completed" },
        { AuditEvent.RecoveryFailed, 13, "recovery.failed" },
    };

    [Theory]
    [MemberData(nameof(ExpectedCodes))]
    public void Code_OfEveryEvent_IsTheFixedValue(AuditEvent auditEvent, int value, string code)
    {
        Assert.Equal(value, (int)auditEvent);
        Assert.Equal(code, AuditCodes.Of(auditEvent));
    }

    [Fact]
    public void Codes_CoverEveryEventOnceAndThereIsNoSeparateLockCode()
    {
        var all = Enum.GetValues<AuditEvent>();

        Assert.Equal(ExpectedCodes.Count, all.Length);
        Assert.Equal(all.Length, all.Select(AuditCodes.Of).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(all.Select(AuditCodes.Of), c => c.Contains("lock", StringComparison.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditCodes.Of((AuditEvent)0));
    }

    [Theory]
    [InlineData(AccountRole.Student, "student")]
    [InlineData(AccountRole.Teacher, "teacher")]
    [InlineData(null, "none")]
    [InlineData((AccountRole)7, "unknown")]
    public void Code_OfRole_IsTheFixedValue(AccountRole? role, string code) => Assert.Equal(code, AuditCodes.Of(role));
}
