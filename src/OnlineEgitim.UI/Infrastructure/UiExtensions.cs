namespace OnlineEgitim.UI.Infrastructure;

internal static class UiExtensions
{
    /// <summary>
    /// Runs an asynchronous UI action with a wait cursor. Business rule violations are shown as
    /// warnings; anything else is reported through <see cref="ErrorReporter"/>.
    /// </summary>
    public static async Task RunAsync(this Form form, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(action);

        form.UseWaitCursor = true;
        try
        {
            await action();
        }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorReporter.Show(ex, form);
        }
        finally
        {
            form.UseWaitCursor = false;
        }
    }

    /// <summary>
    /// Like <see cref="RunAsync"/>, but through <paramref name="operation"/>: while a run is pending a
    /// new call is ignored and <paramref name="controls"/> stay disabled, so a write cannot be started twice.
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

        return operation.RunAsync(() => form.RunAsync(action), controls);
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
