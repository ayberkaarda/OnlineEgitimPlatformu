namespace OnlineEgitim.UI.Infrastructure;

using OnlineEgitim.Business.Services;

/// <summary>Opens the own password change of a panel.</summary>
internal static class PasswordChange
{
    /// <summary>Shows the dialog; returns whether the password changed, which ended every session of the account.</summary>
    public static bool Show(Form owner, IFormFactory forms, UserSession session)
    {
        ArgumentNullException.ThrowIfNull(forms);

        using var form = forms.Create<SifreDegistirForm>(session);
        form.ShowDialog(owner);
        return form.Changed;
    }
}
