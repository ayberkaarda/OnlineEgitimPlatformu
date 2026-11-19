using OnlineEgitim.Business;

namespace OnlineEgitim.UI.Infrastructure;

internal static class UiExtensions
{
    /// <summary>
    /// Runs an asynchronous UI action with a wait cursor. Business rule violations are shown as
    /// warnings; anything else is reported through <see cref="ErrorReporter"/>, inline on a screen with a
    /// message strip. A session that is no longer valid is reported and then ended, which returns the
    /// application to the sign-in.
    /// </summary>
    /// <param name="reportTo">Where failures are reported instead of <paramref name="form"/>, for a window that closes when its action fails.</param>
    public static async Task RunAsync(this Form form, Func<Task> action, IWin32Window? reportTo = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(action);

        form.UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (SessionExpiredException ex)
        {
            ErrorReporter.Show(ex, form);
            SessionEnd.Request(form);
        }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorReporter.Show(ex, reportTo ?? form);
        }
        finally
        {
            form.UseWaitCursor = false;
        }
    }

    /// <summary>
    /// Like <see cref="RunAsync"/>, but through <paramref name="operation"/>: while a run is pending a
    /// new call is ignored and <paramref name="controls"/> stay disabled, so a write cannot be started twice.
    /// A run that starts clears the previous inline message of the screen.
    /// </summary>
    public static Task<bool> RunExclusiveAsync(
        this Form form,
        ExclusiveOperation operation,
        Func<Task> action,
        params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        return operation.RunAsync(
            () =>
            {
                (form as IStatusSurface)?.ClearStatus();
                return form.RunAsync(action);
            },
            controls);
    }

    /// <summary>Replaces the items of a list control, keeping the update to a single repaint.</summary>
    public static void ReplaceItems<T>(this ListBox listBox, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        ArgumentNullException.ThrowIfNull(items);

        listBox.BeginUpdate();
        try
        {
            listBox.Items.Clear();
            foreach (var item in items)
            {
                listBox.Items.Add(item!);
            }
        }
        finally
        {
            listBox.EndUpdate();
        }
    }
}
