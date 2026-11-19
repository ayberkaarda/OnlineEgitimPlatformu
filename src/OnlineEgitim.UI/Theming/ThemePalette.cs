namespace OnlineEgitim.UI.Theming;

/// <summary>
/// Named colors of one theme. Controls never use literal colors; they read a role from the current
/// palette, so the light, dark and high contrast themes stay consistent.
/// </summary>
internal sealed record ThemePalette
{
    public required string Name { get; init; }

    public bool IsDark { get; init; }

    /// <summary>Built from the Windows high contrast colors; custom painting steps back for native drawing.</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Window background behind cards: a calm grey with a faint violet tint.</summary>
    public required Color Page { get; init; }

    /// <summary>Cards, inputs, header and footer.</summary>
    public required Color Surface { get; init; }

    /// <summary>Quiet areas: disabled inputs and the footer of the message dialog.</summary>
    public required Color SurfaceAlt { get; init; }

    public required Color SurfaceHover { get; init; }

    /// <summary>Decorative hairlines between areas.</summary>
    public required Color Border { get; init; }

    /// <summary>Borders of inputs; at least 3:1 against the surface.</summary>
    public required Color BorderStrong { get; init; }

    public required Color Text { get; init; }

    public required Color TextSecondary { get; init; }

    public required Color TextMuted { get; init; }

    public required Color TextDisabled { get; init; }

    /// <summary>The deep indigo violet of the logo: primary actions, selection and the brand panel.</summary>
    public required Color Accent { get; init; }

    public required Color AccentHover { get; init; }

    public required Color AccentPressed { get; init; }

    /// <summary>Text and glyphs on <see cref="Accent"/>.</summary>
    public required Color OnAccent { get; init; }

    /// <summary>Secondary text on <see cref="Accent"/>, for example the tagline of the brand panel.</summary>
    public required Color OnAccentMuted { get; init; }

    /// <summary>Tinted background for selected rows, chosen segments and glyph badges.</summary>
    public required Color AccentSubtle { get; init; }

    /// <summary>Accent used as text or a glyph on <see cref="Surface"/>.</summary>
    public required Color AccentText { get; init; }

    /// <summary>
    /// The warm sun yellow of the logo's spark: a small mark for what the user has (the own enrollment, a code
    /// to pass on, the brand line). Decorative only; text in this family uses <see cref="WarmText"/>.
    /// </summary>
    public required Color Warm { get; init; }

    public required Color WarmSubtle { get; init; }

    public required Color WarmText { get; init; }

    public required Color Success { get; init; }

    public required Color SuccessSubtle { get; init; }

    public required Color Warning { get; init; }

    public required Color WarningSubtle { get; init; }

    public required Color Danger { get; init; }

    public required Color DangerSubtle { get; init; }

    public required Color Info { get; init; }

    public required Color InfoSubtle { get; init; }

    /// <summary>
    /// Light theme: soft violet-grey neutrals with a deep indigo violet accent and a warm sun yellow mark. The
    /// accent is far from the red danger role, so leaving a course or deleting one never looks like the
    /// primary action, and the pair stays clear of the blue and red of official education services.
    /// </summary>
    public static ThemePalette Light { get; } = new()
    {
        Name = "Light",
        Page = Hex(0xF4F3F8),
        Surface = Hex(0xFFFFFF),
        SurfaceAlt = Hex(0xF7F6FB),
        SurfaceHover = Hex(0xF0EEF8),
        Border = Hex(0xE1DEEC),
        BorderStrong = Hex(0x827E97),
        Text = Hex(0x1D1A2F),
        TextSecondary = Hex(0x47435C),
        TextMuted = Hex(0x5D5972),
        TextDisabled = Hex(0xA6A2B6),
        Accent = Hex(0x4C3BB0),
        AccentHover = Hex(0x412F9B),
        AccentPressed = Hex(0x372784),
        OnAccent = Hex(0xFFFFFF),
        OnAccentMuted = Hex(0xDEDAF6),
        AccentSubtle = Hex(0xEDEAFB),
        AccentText = Hex(0x4C3BB0),
        Warm = Hex(0xF2B630),
        WarmSubtle = Hex(0xFDF4D9),
        WarmText = Hex(0x765100),
        Success = Hex(0x1E6B46),
        SuccessSubtle = Hex(0xE6F3EC),
        Warning = Hex(0x7A4B00),
        WarningSubtle = Hex(0xFBF0D9),
        Danger = Hex(0xB42318),
        DangerSubtle = Hex(0xFCEAE8),
        Info = Hex(0x2D4F8E),
        InfoSubtle = Hex(0xE9EEF8),
    };

    /// <summary>Dark theme: the same roles stepped for a dark violet-grey surface, not an inverted light theme.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        Page = Hex(0x13121D),
        Surface = Hex(0x1C1A29),
        SurfaceAlt = Hex(0x211F30),
        SurfaceHover = Hex(0x29273B),
        Border = Hex(0x34314A),
        BorderStrong = Hex(0x7F7B98),
        Text = Hex(0xEDEBF6),
        TextSecondary = Hex(0xC3C0D6),
        TextMuted = Hex(0xA4A1BB),
        TextDisabled = Hex(0x605D77),
        Accent = Hex(0xA99CFF),
        AccentHover = Hex(0xB8ADFF),
        AccentPressed = Hex(0x988AF4),
        OnAccent = Hex(0x17113A),
        OnAccentMuted = Hex(0x2B2261),
        AccentSubtle = Hex(0x2C2752),
        AccentText = Hex(0xB9AEFF),
        Warm = Hex(0xF5C451),
        WarmSubtle = Hex(0x3A2F12),
        WarmText = Hex(0xF7D47E),
        Success = Hex(0x7FD3A0),
        SuccessSubtle = Hex(0x15302A),
        Warning = Hex(0xF0BF5C),
        WarningSubtle = Hex(0x33290F),
        Danger = Hex(0xFF9C8C),
        DangerSubtle = Hex(0x3C1B16),
        Info = Hex(0x9DB8F5),
        InfoSubtle = Hex(0x1A2440),
    };

    /// <summary>
    /// High contrast theme built from the active Windows system colors, so the user's contrast theme is
    /// respected instead of being replaced by the application's colors. Selected and hovered content is
    /// written in HighlightText on Highlight.
    /// </summary>
    public static ThemePalette HighContrast() => new()
    {
        Name = "HighContrast",
        IsHighContrast = true,
        IsDark = SystemColors.Window.GetBrightness() < 0.5f,
        Page = SystemColors.Window,
        Surface = SystemColors.Window,
        SurfaceAlt = SystemColors.Window,
        SurfaceHover = SystemColors.Highlight,
        Border = SystemColors.WindowText,
        BorderStrong = SystemColors.WindowText,
        Text = SystemColors.WindowText,
        TextSecondary = SystemColors.WindowText,
        TextMuted = SystemColors.WindowText,
        TextDisabled = SystemColors.GrayText,
        Accent = SystemColors.Highlight,
        AccentHover = SystemColors.Highlight,
        AccentPressed = SystemColors.Highlight,
        OnAccent = SystemColors.HighlightText,
        OnAccentMuted = SystemColors.HighlightText,
        AccentSubtle = SystemColors.Window,
        AccentText = SystemColors.HotTrack,
        Warm = SystemColors.WindowText,
        WarmSubtle = SystemColors.Window,
        WarmText = SystemColors.WindowText,
        Success = SystemColors.WindowText,
        SuccessSubtle = SystemColors.Window,
        Warning = SystemColors.WindowText,
        WarningSubtle = SystemColors.Window,
        Danger = SystemColors.WindowText,
        DangerSubtle = SystemColors.Window,
        Info = SystemColors.WindowText,
        InfoSubtle = SystemColors.Window,
    };

    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
