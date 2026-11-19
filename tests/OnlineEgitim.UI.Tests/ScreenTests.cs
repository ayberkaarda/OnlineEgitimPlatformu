using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using OnlineEgitim.UI.Theming;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>
/// Every screen is built with test doubles and laid out without being shown: window handles are created
/// for real layout and tab order, but no window becomes visible and no message window opens.
/// </summary>
public sealed class ScreenTests
{
    public static TheoryData<string> ScreenNames => [.. TestForms.Names];

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_HasTheApplicationIdentity(string name) => Run(() =>
    {
        using var form = TestForms.Create(name);

        Assert.StartsWith(AppText.Title + " - ", form.Text, StringComparison.Ordinal);
        Assert.Same(AppAssets.Icon, form.Icon);
        Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
        Assert.True(form.MinimumSize.Width > 0 && form.MinimumSize.Height > 0, "A resizable screen needs a minimum size.");
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
        Assert.Null(form.BackgroundImage);
        Assert.False(form.Visible);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_UsesThemedControlsAndAKeyboardWayOut(string name) => Run(() =>
    {
        using var form = TestForms.Create(name);
        var controls = Descendants(form).ToList();

        var buttons = controls.OfType<Button>().ToList();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.IsType<ThemedButton>(button));
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(button.Text), $"{button.Name} has no text."));
        Assert.All(controls.OfType<ComboBox>(), combo => Assert.IsType<ThemedComboBox>(combo));
        Assert.All(controls.OfType<ListBox>(), list => Assert.IsType<ThemedListBox>(list));
        Assert.All(controls.OfType<CheckBox>(), check => Assert.IsType<ThemedCheckBox>(check));
        Assert.All(controls.OfType<RadioButton>(), radio => Assert.IsType<SegmentButton>(radio));
        Assert.All(controls.OfType<NumericUpDown>(), number => Assert.IsType<NumberField>(number.Parent));
        Assert.All(controls.OfType<TextBox>().Where(box => box.Parent is not NumericUpDown), box => Assert.IsType<TextField>(box.Parent));
        Assert.All(controls.OfType<ThemedListBox>(), list => Assert.False(string.IsNullOrEmpty(list.EmptyText), $"{list.Name} has no empty text."));
        Assert.NotNull(form.CancelButton); // Esc closes every screen (a panel signs out).
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_LaysOutInsideItsWindow(string name) => Run(() =>
    {
        using var form = TestForms.Create(name);

        LayoutAssert.FitsInside(form);
        Assert.False(form.Visible);
    });

    public static TheoryData<string, int, int, float> ScreensOnSmallScreens
    {
        get
        {
            var data = new TheoryData<string, int, int, float>();
            foreach (var (width, height) in new[] { (1024, 600), (1366, 768) })
            {
                foreach (var scale in LayoutAssert.FontScales)
                {
                    foreach (var name in TestForms.Names)
                    {
                        data.Add(name, width, height, scale);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    /// On a small working area and with larger text the window still opens inside the screen, its minimum size
    /// too, and every control stays reachable: the body scrolls while the header and the footer stay in view,
    /// also while a two-line message is shown.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScreensOnSmallScreens))]
    public void Screen_FitsTheWorkingAreaAndKeepsEveryControlReachable(string name, int screenWidth, int screenHeight, float fontScale) => Run(() =>
    {
        var previous = Theme.Current;
        var previousScale = Theme.RenderScale;
        var previousArea = AppDialog.WorkingAreaOf;
        Theme.Use(previous, fontScale);
        var area = new Rectangle(0, 0, screenWidth, screenHeight);
        AppDialog.WorkingAreaOf = _ => area;
        try
        {
            using var form = TestForms.Create(name);
            LayoutAssert.CreateHandles(form);

            Assert.True(form.Width <= area.Width && form.Height <= area.Height, $"{name} opens at {form.Size}, larger than the working area {area.Size}.");
            Assert.True(form.MinimumSize.Width <= area.Width && form.MinimumSize.Height <= area.Height, $"{name} requires {form.MinimumSize}, larger than the working area {area.Size}.");

            foreach (var size in new[] { form.Size, form.MinimumSize })
            {
                form.Size = size;
                form.ShowStatus(StatusKind.Warning, "Lütfen bir kurs seçin." + Environment.NewLine + "İkinci satır.");
                form.PerformLayout();

                try
                {
                    LayoutAssert.FitsInside(form);
                }
                catch (Xunit.Sdk.XunitException ex)
                {
                    throw new Xunit.Sdk.XunitException($"{name} at {form.Size}: {ex.Message}");
                }

                form.ClearStatus();
            }
        }
        finally
        {
            AppDialog.WorkingAreaOf = previousArea;
            Theme.Use(previous, previousScale);
        }
    });

    [Theory]
    [InlineData(nameof(GirisForm), "txtAd", "btnCikis")]
    [InlineData(nameof(OgretmenGirisForm), "txtAd", "btnIptal")]
    [InlineData(nameof(KayitForm), "txtAd", "btnIptal")]
    [InlineData(nameof(IlkKurulumForm), "txtAnahtar", "btnIptal")]
    [InlineData(nameof(HesapKurtarmaForm), "rbOgrenci", "btnIptal")]
    [InlineData(nameof(SifreDegistirForm), "txtMevcut", "btnIptal")]
    [InlineData(nameof(MainForm), "txtAra", "btnCikis")]
    [InlineData(nameof(KursDetayForm), "lstbNotlar", "btnKapat")]
    [InlineData(nameof(KursNotlariForm), "lstKurslar", "btnGeri")]
    [InlineData(nameof(OgretmenForm), "lstKurslar", "btnCikis")]
    [InlineData(nameof(KursEklemeForm), "txtKursAd", "btnKapat")]
    [InlineData(nameof(KurtarmaKoduForm), "txtAd", "btnKapat")]
    [InlineData(nameof(YoneticiForm), "txtAra", "btnCikis")]
    public void TabOrder_StartsAtTheFirstInputAndEndsInTheFooter(string name, string first, string last) => Run(() =>
    {
        using var form = TestForms.Create(name);
        LayoutAssert.CreateHandles(form);

        var order = TabOrder(form);

        Assert.Equal(first, order[0].Name);
        Assert.Equal(last, order[^1].Name);
        Assert.Equal(order.Count, order.Distinct().Count());
    });

    [Theory]
    [InlineData(nameof(GirisForm), new[] { "txtAd", "txtSifre", "btnOgrenciGiris", "btnKayit", "btnKurtarma", "btnOgretmenGiris", "btnCikis" })]
    [InlineData(nameof(IlkKurulumForm), new[] { "txtAnahtar", "txtAd", "txtUzmanlik", "txtSifre", "txtSifreTekrar", "btnOlustur", "btnIptal" })]
    [InlineData(nameof(HesapKurtarmaForm), new[] { "rbOgrenci", "txtAd", "txtKod", "txtSifre", "txtSifreTekrar", "btnKurtar", "btnIptal" })]
    [InlineData(nameof(MainForm), new[]
    {
        "txtAra", "txtKonu", "txtOgretmen", "cmbKayit", "chkBosKontenjan", "btnAra", "btnTemizle", "lstKurslar",
        "btnKursDetay", "btnDersSec", "btnAyril", "btnKurslarim", "btnSifre", "btnCikis",
    })]
    [InlineData(nameof(OgretmenForm), new[]
    {
        "lstKurslar", "btnYeniKurs", "btnDuzenle", "btnSil", "lstKayitliOgrenciler", "txtNotEkle", "btnNotEkle", "btnKurtarmaKodu", "btnSifre", "btnCikis",
    })]
    [InlineData(nameof(KursEklemeForm), new[] { "txtKursAd", "txtKonu", "chkSinirsiz", "btnEkle", "btnKapat" })]
    [InlineData(nameof(KurtarmaKoduForm), new[] { "txtAd", "btnUret", "btnKapat" })]
    [InlineData(nameof(YoneticiForm), new[]
    {
        "txtAra", "cmbRol", "btnAra", "lstHesaplar", "btnEtkinlestir", "btnYoneticilikten", "btnDevreDisi",
        "rbOgretmen", "txtAd", "txtDetay", "btnOlustur", "btnSifirlamaKodu", "txtKod", "btnSifremiDegistir", "btnCikis",
    })]
    public void TabOrder_FollowsTheReadingOrder(string name, string[] expected) => Run(() =>
    {
        using var form = TestForms.Create(name);
        LayoutAssert.CreateHandles(form);

        var names = TabOrder(form).Select(control => control.Name).Where(expected.Contains).ToList();

        Assert.Equal(expected, names);
    });

    [Fact]
    public void FieldCaptions_NameTheirInputs() => Run(() =>
    {
        using var form = TestForms.Create(nameof(GirisForm));

        Assert.Equal("Ad soyad", Find<TextBox>(form, "txtAd").AccessibleName);
        Assert.Equal("Şifre", Find<TextBox>(form, "txtSifre").AccessibleName);
        Assert.False(Descendants(form).OfType<TextField>().Any(field => field.CanSelect), "A text field frame must hand the focus to its text box.");
    });

    [Fact]
    public void Validation_IsShownInlineWithoutADialog() => Run(() =>
    {
        using var form = TestForms.StudentPanel();
        var dialogs = NoDialogs.Requested.Count;

        Assert.True(form.EnrollAsync().IsCompletedSuccessfully);

        Assert.True(form.StatusBar.IsShown);
        Assert.Equal(StatusKind.Warning, form.StatusBar.Kind);
        Assert.Equal("Lütfen bir ders seçiniz.", form.StatusBar.Text);
        Assert.Equal(form.StatusBar.Text, form.StatusBar.AccessibleName);
        Assert.Equal(dialogs, NoDialogs.Requested.Count);
    });

    /// <summary>A refusal of the service (a business rule) appears in the screen's message strip, not in a window.</summary>
    [Fact]
    public void RuleRefusal_IsShownInline() => Run(() =>
    {
        using var form = TestForms.TeacherPanel();
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;
        var dialogs = NoDialogs.Requested.Count;

        Assert.True(form.DeleteCourseAsync().IsCompletedSuccessfully); // the fake has no details: "Ders bulunamadı."

        Assert.Equal((StatusKind.Warning, "Ders bulunamadı."), (form.StatusBar.Kind, form.StatusBar.Text));
        Assert.Equal(dialogs, NoDialogs.Requested.Count);
    });

    [Fact]
    public void NextWrite_ClearsThePreviousMessage() => Run(() =>
    {
        var enrollments = new ControlledEnrollmentService();
        using var form = new MainForm(new ControlledCourseService(), enrollments, new NoFormsFactory(), Sessions.Student);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Assert.True(form.EnrollAsync().IsCompletedSuccessfully);
        Assert.True(form.StatusBar.IsShown);
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;

        _ = form.EnrollAsync(); // Never completes; the old warning must be gone meanwhile.

        Assert.Equal(1, enrollments.EnrollCalls);
        Assert.False(form.StatusBar.IsShown);
    });

    [Fact]
    public void LeaveQuestion_GoesThroughTheDialogHelper() => Run(() =>
    {
        var enrollments = new ControlledEnrollmentService();
        using var form = new MainForm(new ControlledCourseService(), enrollments, new NoFormsFactory(), Sessions.Student);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;
        var before = NoDialogs.Requested.Count;

        Assert.True(form.LeaveAsync().IsCompletedSuccessfully);

        Assert.Equal("Onay: " + MainForm.LeaveQuestion("Temel Programlama"), Assert.Single(NoDialogs.Requested.Skip(before)));
        Assert.Empty(enrollments.LeaveCalls); // The fixture answers "No".
    });

    [Fact]
    public void Lists_SayWhenTheyLoadAndWhenTheyAreEmpty() => Run(() =>
    {
        var courses = new ControlledCourseService { HoldSearches = true };
        using var form = new MainForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Student);
        var list = Find<ThemedListBox>(form, "lstKurslar");

        var search = form.SearchAsync(0);
        Assert.True(list.IsLoading);
        courses.Searches[0].Answer.SetResult(ControlledCourseService.Page([]));

        Assert.True(search.IsCompletedSuccessfully);
        Assert.False(list.IsLoading);
        Assert.Empty(list.Items);
        Assert.StartsWith("Aramanıza uyan kurs yok.", list.EmptyText, StringComparison.Ordinal);
    });

    [Fact]
    public void TeacherStudents_SayWhetherACourseIsChosen() => Run(() =>
    {
        var enrollments = new ControlledEnrollmentService();
        using var form = new OgretmenForm(new ControlledCourseService(), enrollments, new NoFormsFactory(), Sessions.Teacher);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        var students = Find<ThemedListBox>(form, "lstKayitliOgrenciler");
        Assert.Equal(OgretmenForm.ChooseACourse, students.EmptyText);

        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;
        Assert.True(students.IsLoading);
        enrollments.StudentRequests[0].Answer.SetResult([]);

        Assert.False(students.IsLoading);
        Assert.Equal(OgretmenForm.NoStudents, students.EmptyText);
    });

    [Fact]
    public void MessageDialog_AnswersWithKeysAndIsNeverShown() => Run(() =>
    {
        using var dialog = new MessageDialog(MainForm.LeaveQuestion("Temel Programlama"), "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        Assert.False(dialog.Visible);
        Assert.Equal(DialogResult.Yes, Assert.IsType<ThemedButton>(dialog.AcceptButton).DialogResult);
        Assert.Equal(DialogResult.No, Assert.IsType<ThemedButton>(dialog.CancelButton).DialogResult);
        Assert.Equal(AppText.Title, dialog.Text);
        Assert.True(dialog.ClientSize.Width > 0 && dialog.ClientSize.Height > 0);
        Assert.Equal("Bir sorun oluştu", MessageDialog.Heading(AppText.Title, MessageBoxIcon.Error));
        Assert.Equal("Uyarı", MessageDialog.Heading("Uyarı", MessageBoxIcon.Warning));
    });

    /// <summary>The sign-in screen shows why the previous session ended, for example after a password change.</summary>
    [Fact]
    public void SignIn_ShowsTheNoticeOfTheEndedSession() => Run(() =>
    {
        using var form = new GirisForm(new PendingAuthService(), new NoFormsFactory());

        form.ShowNotice(SifreDegistirForm.Done);

        Assert.Equal((StatusKind.Info, SifreDegistirForm.Done), (form.StatusBar.Kind, form.StatusBar.Text));
    });

    internal static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Focusable controls in the order Tab visits them.</summary>
    private static List<Control> TabOrder(Form form)
    {
        var order = new List<Control>();
        Control? current = null;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (CanSelectWhenShown(current))
            {
                order.Add(current);
            }
        }

        return order;
    }

    /// <summary>
    /// Whether Tab can stop on the control once the window is shown: an enabled tab stop that is not a
    /// container, label, frame or badge, not hidden by its screen and not the inner box of a number field.
    /// </summary>
    private static bool CanSelectWhenShown(Control control) =>
        control.TabStop
        && control.Enabled
        && !FormLayout.IsHidden(control)
        && control.Parent is not NumericUpDown
        && control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or TextField or NumberField or GlyphBadge or InfoBar or BrandRule);
}
