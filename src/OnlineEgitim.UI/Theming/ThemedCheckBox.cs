namespace OnlineEgitim.UI.Theming;

/// <summary>
/// A check box painted with the theme: a rounded box in the accent color when checked and a focus ring
/// around the box. Space, Tab, the access key and screen readers stay those of the native check box.
/// Under high contrast the native look is kept.
/// </summary>
internal sealed class ThemedCheckBox : CheckBox, IThemeAware
{
    private const int Box = 18;
    private const int Gap = 8;

    /// <summary>Room left of the box for its focus ring.</summary>
    private const int Inset = 3;

    private bool _hover;

    public ThemedCheckBox()
    {
        AutoSize = true;
        Font = Theme.Fonts.Body;
        Cursor = Cursors.Hand;
        ApplyStyles();
    }

    /// <summary>Native drawing under high contrast, custom drawing otherwise.</summary>
    void IThemeAware.ApplyTheme(ThemeRemap remap)
    {
        ApplyStyles();
        AutoSize = false;
        AutoSize = true;
        Invalidate();
    }

    private void ApplyStyles() =>
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, !Theme.Current.IsHighContrast);

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Theme.Current.IsHighContrast)
        {
            return base.GetPreferredSize(proposedSize);
        }

        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix);
        return new Size(Theme.Px(this, Inset + Box + Gap) + text.Width + Theme.Px(this, 2), Math.Max(text.Height, Theme.Px(this, Box + 4)));
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);
        if (Theme.Current.IsHighContrast)
        {
            base.OnPaint(pevent);
            return;
        }

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));

        var size = Theme.PxF(this, Box);
        var box = new RectangleF(Theme.PxF(this, Inset), (Height - size) / 2F, size, size);
        var radius = Theme.PxF(this, 4);
        var (fill, border) = !Enabled
            ? (p.SurfaceAlt, p.Border)
            : Checked ? (_hover ? p.AccentHover : p.Accent, (Color?)null) : (p.Surface, _hover ? p.Accent : p.BorderStrong);
        Painting.FillRounded(g, box, radius, fill, border, Math.Max(1F, Theme.PxF(this, 1.5F)));
        if (Checked)
        {
            Painting.DrawGlyph(g, Glyphs.CheckMark, Theme.Fonts.Glyph(9F), Rectangle.Round(box), Enabled ? p.OnAccent : p.TextDisabled);
        }

        if (Focused && ShowFocusCues)
        {
            Painting.FillRounded(g, RectangleF.Inflate(box, Theme.PxF(this, 2), Theme.PxF(this, 2)), radius + Theme.PxF(this, 2), Color.Transparent, p.Accent, Theme.PxF(this, 1.5F));
        }

        var left = Theme.Px(this, Inset + Box + Gap);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(left, 0, Math.Max(0, Width - left), Height),
            Enabled ? p.Text : p.TextDisabled,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }
}

/// <summary>
/// One option of a small segmented choice (for example student or teacher). It stays a real
/// <see cref="RadioButton"/>, so the arrow keys and screen readers treat the options as one group.
/// Under high contrast the native button look is used.
/// </summary>
internal sealed class SegmentButton : RadioButton, IThemeAware
{
    private bool _hover;

    public SegmentButton()
    {
        Appearance = Appearance.Button;
        TextAlign = ContentAlignment.MiddleCenter;
        Font = Theme.Fonts.BodyStrong;
        Margin = new Padding(0, 0, Spacing.XS, 0);
        Size = new Size(Sizes.ButtonMinWidth, Sizes.ControlHeight);
        AutoSize = true; // Sized by GetPreferredSize, so the whole text fits at any font size.
        Cursor = Cursors.Hand;
        ApplyStyles();
    }

    void IThemeAware.ApplyTheme(ThemeRemap remap) => ApplyStyles();

    /// <summary>The width that keeps the whole text, at least the design width.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix);
        return new Size(
            Math.Max(Theme.Px(this, Sizes.ButtonMinWidth), text.Width + Theme.Px(this, 2 * Spacing.L)),
            Math.Max(Theme.Px(this, Sizes.ControlHeight), text.Height + Theme.Px(this, Spacing.S)));
    }

    private void ApplyStyles()
    {
        if (Theme.Current.IsHighContrast)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, false);
            FlatStyle = FlatStyle.System;
        }
        else
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
        }

        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);
        if (Theme.Current.IsHighContrast)
        {
            base.OnPaint(pevent);
            return;
        }

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));
        var (fill, border, text) = !Enabled
            ? (p.SurfaceAlt, p.Border, p.TextDisabled)
            : Checked
                ? (p.AccentSubtle, p.Accent, p.AccentText)
                : (_hover ? p.SurfaceHover : p.Surface, p.BorderStrong, p.Text);
        var radius = Theme.PxF(this, Radius.Control);
        var bounds = new RectangleF(0, 0, Width, Height);
        Painting.FillRounded(g, bounds, radius, fill, border, Math.Max(1F, Theme.PxF(this, Checked ? 1.5F : 1F)));
        if (Focused && ShowFocusCues)
        {
            Painting.FillRounded(g, RectangleF.Inflate(bounds, -Theme.PxF(this, 3), -Theme.PxF(this, 3)), radius, Color.Transparent, p.Accent, Theme.PxF(this, 1.5F));
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }
}
