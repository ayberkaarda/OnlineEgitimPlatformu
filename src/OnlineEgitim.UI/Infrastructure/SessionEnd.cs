namespace OnlineEgitim.UI.Infrastructure;

/// <summary>A main window of a signed-in account; closing it with <see cref="SignedOut"/> returns to the sign-in.</summary>
internal interface ISessionPanel
{
    /// <summary>Whether the session ended (signed out or refused) and the sign-in has to be shown again.</summary>
    bool SignedOut { get; }

    /// <summary>Marks the session as ended and closes the window once the current operation finished.</summary>
    void EndSession();
}

/// <summary>Ends the session of the window chain a form belongs to.</summary>
internal static class SessionEnd
{
    /// <summary>
    /// Closes the dialogs between <paramref name="form"/> and the panel that owns them and ends the
    /// panel's session; the application then shows the sign-in again.
    /// </summary>
    public static void Request(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        for (Form? current = form; current is not null; current = current.Owner)
        {
            if (current is ISessionPanel panel)
            {
                panel.EndSession();
                return;
            }

            if (current.Modal)
            {
                current.DialogResult = DialogResult.Cancel;
            }
        }
    }

    /// <summary>
    /// The common <see cref="ISessionPanel.EndSession"/>: posted, so the running operation and any dialog
    /// it opened finish first.
    /// </summary>
    public static void Close(Form panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (panel.IsHandleCreated && !panel.IsDisposed)
        {
            panel.BeginInvoke(() =>
            {
                if (!panel.IsDisposed)
                {
                    panel.Close();
                }
            });
        }
    }
}
