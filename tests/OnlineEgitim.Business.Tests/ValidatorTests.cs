using FluentValidation;
using OnlineEgitim.Business.Validation;

namespace OnlineEgitim.Business.Tests;

/// <summary>
/// The central validators: boundaries, the order in which problems are reported (the first one is
/// shown), and the Turkish texts, which are the ones the services showed before the move.
/// </summary>
public sealed class ValidatorTests
{
    private const string Password = "Gizli.Sifre1";

    [Fact]
    public void Messages_KeepTheirTurkishTexts()
    {
        Assert.Equal("Lütfen tüm alanları doldurun.", ValidationMessages.InputRequired);
        Assert.Equal("Lütfen tüm alanları doldurunuz.", ValidationMessages.RegisterFieldsRequired);
        Assert.Equal("Lütfen geçerli bir e-posta adresi girin.", ValidationMessages.EmailInvalid);
        Assert.Equal("Şifreler uyuşmuyor.", ValidationMessages.PasswordsDoNotMatch);
        Assert.Equal("Lütfen öğrencinin adını girin.", ValidationMessages.StudentNameRequired);
        Assert.Equal("Lütfen tüm alanları doldurun.", ValidationMessages.CourseFieldsRequired);
        Assert.Equal("Lütfen bir kurs seçin ve not içeriğini girin.", ValidationMessages.NoteRequired);
        Assert.Equal(
            "Ad soyad yalnızca harf, kesme işareti, nokta, kısa çizgi ve tek boşluk içerebilir ve en fazla 100 karakter olabilir.",
            ValidationMessages.NameInvalid);
        Assert.Equal("Şifre en az 8, en fazla 64 karakter olmalıdır.", ValidationMessages.PasswordInvalid);
        Assert.Equal("Uzmanlık alanı en fazla 100 karakter olabilir.", ValidationMessages.SpecialtyInvalid);
        Assert.Equal("Kurs adı en fazla 100 karakter olabilir.", ValidationMessages.CourseNameTooLong);
        Assert.Equal("Konu en fazla 200 karakter olabilir.", ValidationMessages.TopicTooLong);
        Assert.Equal("Not en fazla 500 karakter olabilir.", ValidationMessages.NoteTooLong);
    }

    public static TheoryData<string?, string?, string?, string?> RegistrationCases => new()
    {
        { null, "a@b.co", Password, ValidationMessages.RegisterFieldsRequired },
        { "   ", "a@b.co", Password, ValidationMessages.RegisterFieldsRequired },
        { "Ada Yılmaz", "  ", Password, ValidationMessages.RegisterFieldsRequired },
        { "Ada Yılmaz", "a@b.co", "   ", ValidationMessages.RegisterFieldsRequired },
        { "Ada  Yılmaz", "a@b.co", Password, ValidationMessages.NameInvalid },
        { "Ada 3", "yanlis", "kisa", ValidationMessages.NameInvalid },
        { new string('a', 101), "a@b.co", Password, ValidationMessages.NameInvalid },
        { "Ada Yılmaz", "yanlis", "kisa", ValidationMessages.EmailInvalid },
        { "Ada Yılmaz", "a b@c.co", Password, ValidationMessages.EmailInvalid },
        { "Ada Yılmaz", new string('a', 95) + "@b.com", Password, ValidationMessages.EmailInvalid },
        { "Ada Yılmaz", "a@b.co", "1234567", ValidationMessages.PasswordInvalid },
        { "Ada Yılmaz", "a@b.co", new string('x', 65), ValidationMessages.PasswordInvalid },
        { "Ada Yılmaz", "a@b.co", "12345678", null },
        { " " + new string('a', 100) + " ", " " + new string('a', 94) + "@b.com ", new string('x', 64), null },
    };

    [Theory]
    [MemberData(nameof(RegistrationCases))]
    public void Registration_ReportsTheFirstProblem(string? name, string? email, string? password, string? expected) =>
        AssertFirst(new RegistrationInputValidator(), RegistrationInput.Create(name, email, password), expected);

    public static TheoryData<string?, string?, string?> SignInCases => new()
    {
        { null, Password, ValidationMessages.InputRequired },
        { "  ", Password, ValidationMessages.InputRequired },
        { "Ada", null, ValidationMessages.InputRequired },
        { "Ada", string.Empty, ValidationMessages.InputRequired },
        { new string('a', 101), Password, ValidationMessages.InputRequired },
        { "Ada", new string('x', 101), ValidationMessages.InputRequired },

        // Sign-in accepts what an existing account can hold, also passwords outside the new-password rules.
        { "Ada", "1", null },
        { "Ada", " ", null },
        { new string('a', 100), new string('x', 100), null },
    };

    [Theory]
    [MemberData(nameof(SignInCases))]
    public void SignIn_OneAnswerForEveryProblem(string? name, string? password, string? expected) =>
        AssertFirst(new SignInInputValidator(), SignInInput.Create(name, password), expected);

    public static TheoryData<string?, string?, string?, string?, string?> FirstTeacherCases => new()
    {
        { null, "Fizik", Password, Password, ValidationMessages.InputRequired },
        { "Ada Kurucu", " ", Password, Password, ValidationMessages.InputRequired },
        { "Ada Kurucu", "Fizik", " ", " ", ValidationMessages.InputRequired },
        { "Ada 1", new string('f', 101), "a", "b", ValidationMessages.NameInvalid },
        { "Ada Kurucu", new string('f', 101), "a", "b", ValidationMessages.SpecialtyInvalid },
        { "Ada Kurucu", "Fi\u0001zik", Password, Password, ValidationMessages.SpecialtyInvalid },
        { "Ada Kurucu", "Fizik", "kisa", "kisa2", ValidationMessages.PasswordsDoNotMatch },
        { "Ada Kurucu", "Fizik", "kisa", "kisa", ValidationMessages.PasswordInvalid },
        { "Ada Kurucu", new string('f', 100), new string('x', 64), new string('x', 64), null },
    };

    [Theory]
    [MemberData(nameof(FirstTeacherCases))]
    public void FirstTeacher_ReportsTheFirstProblem(string? name, string? specialty, string? password, string? repeat, string? expected) =>
        AssertFirst(new FirstTeacherInputValidator(), FirstTeacherInput.Create(name, specialty, password, repeat), expected);

    public static TheoryData<string?, string?, string?, string?, string?> NewPasswordCases => new()
    {
        { null, "ABCD-EFGH", Password, Password, ValidationMessages.InputRequired },
        { "Ada", " ", Password, Password, ValidationMessages.InputRequired },
        { "Ada", "ABCD-EFGH", " ", " ", ValidationMessages.InputRequired },
        { "Ada", new string('A', 129), Password, Password, ValidationMessages.InputRequired },
        { new string('a', 101), "ABCD-EFGH", "a", "b", ValidationMessages.NameInvalid },
        { "Ada", "ABCD-EFGH", Password, Password + "x", ValidationMessages.PasswordsDoNotMatch },
        { "Ada", "ABCD-EFGH", "kisa", "kisa", ValidationMessages.PasswordInvalid },

        // A recovery keeps legacy names that a new registration would refuse.
        { "ada  2", new string('A', 128), Password, Password, null },
    };

    [Theory]
    [MemberData(nameof(NewPasswordCases))]
    public void NewPassword_ReportsTheFirstProblem(string? name, string? proof, string? password, string? repeat, string? expected) =>
        AssertFirst(new NewPasswordInputValidator(), NewPasswordInput.Create(name, proof, password, repeat), expected);

    [Theory]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("Mert Deneme", true)]
    [InlineData("eski  ad 7", true)]
    public void StudentName_PresentAndFitting(string? name, bool valid) =>
        AssertFirst(new StudentNameInputValidator(), StudentNameInput.Create(name), valid ? null : ValidationMessages.StudentNameRequired);

    [Fact]
    public void StudentName_LongerThanAnAccountName_IsRefused() =>
        AssertFirst(new StudentNameInputValidator(), StudentNameInput.Create(new string('a', 101)), ValidationMessages.StudentNameRequired);

    public static TheoryData<string?, string?, string?> CourseCases => new()
    {
        { null, "Konu", ValidationMessages.CourseFieldsRequired },
        { "Ders", "  ", ValidationMessages.CourseFieldsRequired },
        { new string('d', 101), new string('k', 201), ValidationMessages.CourseNameTooLong },
        { "Ders\tAdı", "Konu", ValidationMessages.CourseNameTooLong },
        { "Ders", new string('k', 201), ValidationMessages.TopicTooLong },
        { "Ders", "Satır\nsonu", ValidationMessages.TopicTooLong },
        { new string('d', 100), new string('k', 200), null },
    };

    [Theory]
    [MemberData(nameof(CourseCases))]
    public void Course_ReportsTheFirstProblem(string? name, string? topic, string? expected) =>
        AssertFirst(new CourseInputValidator(), CourseInput.Create(name, topic), expected);

    public static TheoryData<int, string?, string?> NoteCases => new()
    {
        { 0, "Not", ValidationMessages.NoteRequired },
        { -1, "Not", ValidationMessages.NoteRequired },
        { 3, " \r\n ", ValidationMessages.NoteRequired },
        { 3, new string('n', 501), ValidationMessages.NoteTooLong },
        { 3, "Zil\u0007", ValidationMessages.NoteTooLong },
        { 3, "Birinci satır\r\n\tİkinci satır", null },
        { 3, new string('n', 500), null },
    };

    [Theory]
    [MemberData(nameof(NoteCases))]
    public void Note_ReportsTheFirstProblem(int courseId, string? content, string? expected) =>
        AssertFirst(new NoteInputValidator(), NoteInput.Create(courseId, content), expected);

    [Fact]
    public void ValidateOrThrow_ThrowsTheFirstMessageOnly()
    {
        IValidator<RegistrationInput> validator = new RegistrationInputValidator();

        var error = Assert.Throws<BusinessRuleException>(() => validator.ValidateOrThrow(RegistrationInput.Create("Ada 3", "yanlis", "kisa")));

        Assert.Equal(ValidationMessages.NameInvalid, error.Message);
        validator.ValidateOrThrow(RegistrationInput.Create("Ada Yılmaz", "ada@example.com", Password));
    }

    private static void AssertFirst<T>(IValidator<T> validator, T input, string? expected)
    {
        var result = validator.Validate(input);
        if (expected is null)
        {
            Assert.True(result.IsValid, result.IsValid ? null : result.Errors[0].ErrorMessage);
            return;
        }

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Errors[0].ErrorMessage);
    }
}
