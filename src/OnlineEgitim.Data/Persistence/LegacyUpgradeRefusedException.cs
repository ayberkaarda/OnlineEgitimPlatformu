using System.Globalization;

namespace OnlineEgitim.Data.Persistence;

/// <summary>Why a database of the original application was not upgraded. The values are stable codes.</summary>
public enum LegacyUpgradeProblem
{
    /// <summary>Some of the five tables are missing.</summary>
    PartialSchema = 1,

    /// <summary>The enrollment table is not keyed by exactly OgrenciID and DersID; the count is the number of key columns.</summary>
    EnrollmentKeyColumns = 2,

    /// <summary>Student and course pairs that are enrolled more than once.</summary>
    DuplicateEnrollments = 3,

    /// <summary>Teachers with a missing, padded or multi-line sign-in name or a missing specialty.</summary>
    UnclearTeacherIdentities = 4,

    /// <summary>Teacher sign-in names used by more than one teacher.</summary>
    DuplicateTeacherNames = 5,

    /// <summary>Students with a missing, padded or multi-line sign-in name or e-mail address.</summary>
    UnclearStudentIdentities = 6,

    /// <summary>Student sign-in names used by more than one student.</summary>
    DuplicateStudentNames = 7,

    /// <summary>E-mail addresses used by more than one student.</summary>
    DuplicateStudentEmails = 8,

    /// <summary>Courses without a name or without an existing teacher.</summary>
    InvalidCourses = 9,
}

/// <summary>One reason of a refused upgrade and the number of rows (or key columns) it concerns.</summary>
public sealed record LegacyUpgradeIssue(LegacyUpgradeProblem Problem, int Count)
{
    /// <summary>Stable, lower case code of <see cref="Problem"/> as it is written to the log.</summary>
    public string Code => Problem switch
    {
        LegacyUpgradeProblem.PartialSchema => "legacy_upgrade.schema_partial",
        LegacyUpgradeProblem.EnrollmentKeyColumns => "legacy_upgrade.enrollment_key_columns",
        LegacyUpgradeProblem.DuplicateEnrollments => "legacy_upgrade.enrollment_duplicate",
        LegacyUpgradeProblem.UnclearTeacherIdentities => "legacy_upgrade.teacher_identity_unclear",
        LegacyUpgradeProblem.DuplicateTeacherNames => "legacy_upgrade.teacher_name_duplicate",
        LegacyUpgradeProblem.UnclearStudentIdentities => "legacy_upgrade.student_identity_unclear",
        LegacyUpgradeProblem.DuplicateStudentNames => "legacy_upgrade.student_name_duplicate",
        LegacyUpgradeProblem.DuplicateStudentEmails => "legacy_upgrade.student_email_duplicate",
        LegacyUpgradeProblem.InvalidCourses => "legacy_upgrade.course_invalid",
        _ => "legacy_upgrade.unknown",
    };

    /// <summary><c>code=count</c>, the only form in which an issue is logged.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Code}={Count}");
}

/// <summary>
/// The upgrade of a legacy database stopped before changing anything. <see cref="Issues"/> carries only
/// fixed problem kinds and counts, never row contents, so it is safe to show and to log; the message
/// is for developers and is never shown or logged by the application.
/// </summary>
public sealed class LegacyUpgradeRefusedException : InvalidOperationException
{
    public LegacyUpgradeRefusedException()
        : this([], "The legacy database was not upgraded. No changes were made.")
    {
    }

    public LegacyUpgradeRefusedException(string message)
        : this([], message)
    {
    }

    public LegacyUpgradeRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Issues = [];
    }

    public LegacyUpgradeRefusedException(IReadOnlyList<LegacyUpgradeIssue> issues, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
    }

    public IReadOnlyList<LegacyUpgradeIssue> Issues { get; }
}
