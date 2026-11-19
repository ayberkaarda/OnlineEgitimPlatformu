using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>
/// The screens use the neutral product name and the application's own artwork; nothing suggests a
/// government ministry, as the original logo did. The input fields have the limits of the services.
/// </summary>
public sealed class BrandingTests
{
    private static readonly string[] ForbiddenTexts = ["Milli Eğitim", "Bakanlığı", "Bakanlik", "MEB", "T.C."];

    [Fact]
    public void Forms_UseTheNeutralTitleAndOwnIcon() => Run(() =>
    {
        var auth = new PendingAuthService();
        var courses = new ControlledCourseService();
        var enrollments = new ControlledEnrollmentService();
        using var signIn = new GirisForm(auth, new NoFormsFactory());
        using var teacherSignIn = new OgretmenGirisForm(auth);
        using var register = new KayitForm(auth);
        using var setup = new IlkKurulumForm(new PendingSetupService());
        using var student = new MainForm(courses, enrollments, new NoFormsFactory(), Sessions.Student);
        using var details = new KursDetayForm(courses, Sessions.Student, 1);
        using var mine = new KursNotlariForm(enrollments, Sessions.Student);
        using var teacher = new OgretmenForm(courses, enrollments, new NoFormsFactory(), Sessions.Teacher);
        using var addCourse = new KursEklemeForm(courses, Sessions.Teacher);

        foreach (Form form in new Form[] { signIn, teacherSignIn, register, setup, student, details, mine, teacher, addCourse })
        {
            Assert.StartsWith("Online Eğitim - ", form.Text, StringComparison.Ordinal);
            Assert.Same(AppAssets.Icon, form.Icon);
            foreach (var text in AllTexts(form))
            {
                Assert.All(ForbiddenTexts, forbidden => Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase));
            }
        }

        Assert.Same(AppAssets.Background, Find<PictureBox>(signIn, "pictureBox1").Image);
        Assert.Same(AppAssets.Logo, Find<PictureBox>(signIn, "pictureBox2").Image);
        Assert.Same(AppAssets.Logo, Find<PictureBox>(teacher, "pictureBox1").Image);
    });

    [Fact]
    public void EmbeddedResources_AreOnlyTheApplicationsOwnArtwork()
    {
        var names = typeof(AppAssets).Assembly.GetManifestResourceNames().Order(StringComparer.Ordinal);

        Assert.Equal(
            ["OnlineEgitim.UI.Assets.app.ico", "OnlineEgitim.UI.Assets.background.png", "OnlineEgitim.UI.Assets.logo.png"],
            names);
        Assert.Equal(new Size(256, 256), AppAssets.Logo.Size);
    }

    [Fact]
    public void Fields_FollowTheServiceLimitsAndHidePasswords() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var signIn = new GirisForm(auth, new NoFormsFactory()) { Prompts = new RecordingPrompts() };
        using var teacherSignIn = new OgretmenGirisForm(auth);
        using var register = new KayitForm(auth);
        using var addCourse = new KursEklemeForm(new ControlledCourseService(), Sessions.Teacher);

        // Sign-in accepts every password an existing account can have, registration only new ones.
        var longest = new string('x', PasswordPolicy.SignInMaxLength);
        Find<TextBox>(signIn, "txtAd").Text = "Mert Deneme";
        Find<TextBox>(signIn, "txtSifre").Text = longest;
        _ = signIn.SignInAsync();
        Assert.Equal(longest, auth.LastPassword);
        Assert.Equal(PasswordPolicy.SignInMaxLength, Find<TextBox>(teacherSignIn, "txtSifre").MaxLength);
        Assert.Equal(PasswordPolicy.MaxLength, Find<TextBox>(register, "txtSifre").MaxLength);
        Assert.Equal(InputRules.EmailMaxLength, Find<TextBox>(register, "txtMail").MaxLength);
        Assert.Equal(InputRules.CourseNameMaxLength, Find<TextBox>(addCourse, "txtKursAd").MaxLength);

        // The original sign-in fields were multi-line, which shows a password in clear text.
        foreach (var field in new[] { Find<TextBox>(signIn, "txtSifre"), Find<TextBox>(teacherSignIn, "txtSifre") })
        {
            Assert.False(field.Multiline);
            Assert.Equal('*', field.PasswordChar);
        }

        // A course is always the signed-in teacher's: the teacher fields cannot be edited.
        Assert.True(Find<TextBox>(addCourse, "txtOgretici").ReadOnly);
        Assert.True(Find<TextBox>(addCourse, "txtUzmanlik").ReadOnly);
        Assert.Equal("Deniz Örnek", Find<TextBox>(addCourse, "txtOgretici").Text);
    });

    private static IEnumerable<string> AllTexts(Control root)
    {
        yield return root.Text;
        foreach (Control child in root.Controls)
        {
            foreach (var text in AllTexts(child))
            {
                yield return text;
            }
        }
    }
}
