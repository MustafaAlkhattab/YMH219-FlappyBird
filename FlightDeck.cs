using System.Drawing.Drawing2D;

namespace FlappyBird;

internal enum DeckAction { Route, Difficulty, Restart, Pause, Music, Screen }

internal sealed class FlightDeck : Panel
{
    private static readonly Color Surface = Color.FromArgb(22, 31, 45);
    private readonly DeckButton[] buttons;
    private Color accent = Color.Wheat;

    public FlightDeck(params DeckButton[] buttons)
    {
        this.buttons = buttons;
        BackColor = Surface;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Controls.AddRange(buttons);
    }

    public void Arrange(Size client)
    {
        if (client.Width < 1 || client.Height < 1) return;
        int unit(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        int inset = unit(12), gap = unit(7), buttonHeight = unit(44);
        int width = Math.Min(unit(1050), Math.Max(1, client.Width - unit(32)));
        int columns = width >= unit(850) ? buttons.Length : 3;
        int rows = (buttons.Length + columns - 1) / columns;
        int height = rows * buttonHeight + (rows - 1) * gap + inset * 2;
        SuspendLayout();
        SetBounds((client.Width - width) / 2, client.Height - height - unit(12), width, height);
        int available = width - inset * 2 - gap * (columns - 1);
        for (int row = 0; row < rows; row++)
        {
            int items = Math.Min(columns, buttons.Length - row * columns);
            int rowStart = inset + (columns - items) * (available / columns + gap) / 2;
            for (int col = 0; col < items; col++)
            {
                int x = rowStart + col * gap + available * col / columns;
                int cell = available * (col + 1) / columns - available * col / columns;
                buttons[row * columns + col].SetBounds(x, inset + row * (buttonHeight + gap), cell, buttonHeight);
            }
        }
        ResumeLayout(performLayout: false);
    }

    public void SetTheme(Color next)
    {
        if (accent == next) return;
        accent = next;
        foreach (var button in buttons) button.SetTheme(next);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var edge = new Pen(Color.FromArgb(100, accent));
        e.Graphics.DrawRectangle(edge, 0, 0, Width - 1, Height - 1);
        using var line = new SolidBrush(Color.FromArgb(205, accent));
        e.Graphics.FillRectangle(line, Width / 2 - 22, 0, 44, 2);
        base.OnPaint(e);
    }
}

internal sealed class DeckButton : Button
{
    private readonly DeckAction action;
    private Color accent = Color.Wheat;
    private bool hover, down;

    public DeckButton(DeckAction action, string title, string description)
    {
        this.action = action;
        Text = title;
        AccessibleDescription = description;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        Font = new("Segoe UI", 10, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = down = false; Invalidate(); };
        MouseDown += (_, _) => { down = true; Invalidate(); };
        MouseUp += (_, _) => { down = false; Invalidate(); };
    }

    public void SetTheme(Color next)
    {
        accent = next;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.FromArgb(22, 31, 45));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool primary = action == DeckAction.Restart;
        Color fill = !Enabled ? Color.FromArgb(36, 44, 57)
            : primary ? ColorBlend(accent, down ? Color.Black : Color.White, down ? .16f : hover ? .22f : 0)
            : down ? Color.FromArgb(31, 45, 61)
            : hover ? Color.FromArgb(57, 72, 94) : Color.FromArgb(36, 48, 66);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(Focused || hover ? accent : Color.FromArgb(69, 84, 105));
        var plate = new RectangleF(1, 1, Width - 3, Height - 3);
        if (plate.Width < 4 || plate.Height < 4) return;
        g.FillRoundedRectangle(brush, plate, new SizeF(8, 8));
        g.DrawRoundedRectangle(border, plate, new SizeF(8, 8));
        Color foreground = !Enabled ? Color.FromArgb(143, 155, 174) : primary ? Color.FromArgb(19, 28, 41) : Color.White;
        var state = g.Save();
        float scale = DeviceDpi / 96f;
        g.TranslateTransform(12 * scale, Height / 2f);
        g.ScaleTransform(scale, scale);
        using (var pen = new Pen(primary || !Enabled ? foreground : accent, 1.6f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        })
            DrawIcon(g, pen);
        g.Restore(state);
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(34 * scale), 0, Math.Max(1, Width - (int)(42 * scale)), Height), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DrawIcon(Graphics g, Pen pen)
    {
        switch (action)
        {
            case DeckAction.Route:
            case DeckAction.Difficulty:
                g.DrawLines(pen, [new Point(1, -3), new Point(7, 3), new Point(13, -3)]);
                break;
            case DeckAction.Restart:
                g.DrawArc(pen, 1, -6, 13, 13, -110, 285);
                g.DrawLines(pen, [new Point(1, -7), new Point(2, -2), new Point(7, -4)]);
                break;
            case DeckAction.Pause:
                if (Text == "Devam et") g.DrawPolygon(pen, [new Point(3, -6), new Point(12, 0), new Point(3, 6)]);
                else { g.DrawLine(pen, 4, -5, 4, 5); g.DrawLine(pen, 10, -5, 10, 5); }
                break;
            case DeckAction.Music:
                g.DrawPolygon(pen, [new Point(1, -2), new Point(4, -2), new Point(8, -5), new Point(8, 5), new Point(4, 2), new Point(1, 2)]);
                if (Text == "Müzik açık") g.DrawArc(pen, 6, -5, 8, 10, -67, 134);
                else g.DrawLine(pen, 10, -3, 14, 3);
                break;
            case DeckAction.Screen:
                g.DrawLines(pen, [new Point(1, -2), new Point(1, -6), new Point(5, -6)]);
                g.DrawLines(pen, [new Point(9, -6), new Point(13, -6), new Point(13, -2)]);
                g.DrawLines(pen, [new Point(13, 2), new Point(13, 6), new Point(9, 6)]);
                g.DrawLines(pen, [new Point(5, 6), new Point(1, 6), new Point(1, 2)]);
                break;
        }
    }

    private static Color ColorBlend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));
}
