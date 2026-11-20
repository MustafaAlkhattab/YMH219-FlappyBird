using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FlappyBird;

internal sealed class FlightCanvas : Control
{
    private readonly Game game;
    private readonly Dictionary<string, Image> art = [];
    private static readonly Color Ink = Color.FromArgb(18, 27, 39);
    private readonly Font display = new("Georgia", 56, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font metric = new("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font body = new("Segoe UI", 17, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font small = new("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Best { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool StorageOk { get; set; } = true;

    public FlightCanvas(Game game)
    {
        this.game = game;
        AccessibleName = "Uçuş alanı. Boşluk, Enter veya yukarı ok ile kanat çırpın.";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Ink;
        foreach (World world in World.Route)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "art", world.Art);
            if (File.Exists(path))
            {
                using var source = Image.FromFile(path);
                art.Add(world.Art, new Bitmap(source));
            }
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Ink);
        float scale = Math.Min(ClientSize.Width / (float)Game.Width, ClientSize.Height / (float)Game.Height);
        if (scale <= 0) return;
        g.TranslateTransform((ClientSize.Width - Game.Width * scale) / 2, (ClientSize.Height - Game.Height * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.SetClip(new Rectangle(0, 0, Game.Width, Game.Height));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var world = game.World;
        DrawWorld(g, world);
        foreach (var gate in game.Gates)
        {
            if (gate.X + Gate.Width < 0 || gate.X > Game.Width) continue;
            DrawGate(g, gate.Upper, world, true);
            DrawGate(g, gate.Lower, world, false);
        }
        DrawFloor(g, world);
        DrawBird(g, game.Bird, world);
        DrawHud(g, world);
        if (game.State != FlightState.Flying) DrawMenu(g, world);
    }

    private void DrawWorld(Graphics g, World world)
    {
        using var background = new SolidBrush(world.Shadow);
        g.FillRectangle(background, 0, 0, Game.Width, Game.Height);
        if (art.TryGetValue(world.Art, out var image))
        {
            float width = image.Height * Game.Width / (float)Game.Height;
            float offset = (image.Width - width) * (.44f + .39f * MathF.Sin(game.Travel / 2700));
            g.DrawImage(image, new Rectangle(0, 0, Game.Width, Game.Height), offset, 0, width, image.Height, GraphicsUnit.Pixel);
        }
        using var shade = new LinearGradientBrush(new Rectangle(0, 0, Game.Width, 160), Color.FromArgb(180, Ink), Color.FromArgb(0, Ink), 90);
        g.FillRectangle(shade, 0, 0, Game.Width, 160);
        using var mote = new SolidBrush(Color.FromArgb(110, world.Accent));
        for (int i = 0; i < 42; i++)
        {
            float x = (i * 131.7f - game.Travel * (.14f + (i % 4) * .035f)) % Game.Width;
            if (x < 0) x += Game.Width;
            float y = 100 + (i * 47.9f % 430) + 8 * MathF.Sin(i + game.Time * 1.45f);
            float diameter = i % 8 == 0 ? 4 : 2;
            g.FillEllipse(mote, x, y, diameter * (game.Speed / 255), diameter);
        }
    }

    private static void DrawGate(Graphics g, RectangleF rect, World world, bool upper)
    {
        if (rect.Height <= 0) return;
        using var shadow = new SolidBrush(Color.FromArgb(55, Ink));
        g.FillRectangle(shadow, rect.X + 6, rect.Y, rect.Width, rect.Height);
        using var fill = new LinearGradientBrush(rect, world.Stone, world.Shadow, 0f);
        g.FillRectangle(fill, rect);
        using var bands = new Pen(Color.FromArgb(72, world.Accent), 1.4f);
        for (float y = rect.Y + 18; y < rect.Bottom; y += 34)
        {
            g.DrawLine(bands, rect.X + 7, y, rect.Right - 9, y);
            float joint = rect.X + (y % 2 == 0 ? 24 : 53);
            g.DrawLine(bands, joint, y, joint, Math.Min(rect.Bottom, y + 13));
        }
        using var rim = new SolidBrush(world.Accent);
        float cap = upper ? rect.Bottom - 11 : rect.Top;
        g.FillRectangle(rim, rect.X, cap, rect.Width, 5);
        using var core = new SolidBrush(Color.FromArgb(145, world.Accent));
        g.FillRectangle(core, rect.X + rect.Width / 2 - 3, cap + 5, 6, 6);
    }

    private void DrawFloor(Graphics g, World world)
    {
        using var floor = new SolidBrush(world.Shadow);
        using var line = new SolidBrush(world.Accent);
        using var decoration = new Pen(Color.FromArgb(100, world.Accent), 2);
        g.FillRectangle(floor, 0, Game.Ground, Game.Width, Game.Height - Game.Ground);
        g.FillRectangle(line, 0, Game.Ground, Game.Width, 3);
        for (float x = -(game.Travel % 27); x < Game.Width; x += 27)
            g.DrawLine(decoration, x, 566, x + 9, 556);
    }

    private void DrawBird(Graphics g, Bird bird, World world)
    {
        if (game.State == FlightState.Flying)
        {
            for (int i = 1; i <= 5; i++)
            {
                using var spark = new SolidBrush(Color.FromArgb(146 - i * 21, world.Accent));
                g.FillEllipse(spark, Bird.X - 29 - i * 10, bird.Y - 3 + MathF.Sin(i + game.Time * 12) * 6, 9 - i, 6 - i);
            }
        }
        var state = g.Save();
        float bob = game.State == FlightState.Ready ? -8 : 0;
        g.TranslateTransform(Bird.X, bird.Y + bob);
        g.RotateTransform(Math.Clamp(bird.Velocity / 13, -25, 45));
        using var dark = new SolidBrush(Color.FromArgb(30, 42, 53));
        using var rust = new SolidBrush(Color.FromArgb(202, 71, 43));
        using var teal = new LinearGradientBrush(new Rectangle(-32, -28, 52, 48), Color.FromArgb(73, 208, 185), Color.FromArgb(25, 87, 107), 68);
        using var gold = new LinearGradientBrush(new Rectangle(-30, -21, 62, 43), Color.FromArgb(252, 218, 153), Color.FromArgb(199, 98, 52), 65);
        using var cream = new SolidBrush(Color.FromArgb(255, 223, 166));
        using var outline = new Pen(Color.FromArgb(32, 43, 49), 2.3f) { LineJoin = LineJoin.Round };
        float wing = game.State == FlightState.Ready ? 15 : 11 + MathF.Cos(bird.WingTime * 16) * 12;
        var tail = new PointF[] { new(-10, 4), new(-43, -4), new(-36, 7), new(-44, 15), new(-13, 15) };
        g.FillPolygon(teal, tail);
        g.DrawPolygon(outline, tail);
        using var scarf = new GraphicsPath();
        scarf.AddBezier(-10, -4, -20, -14, -26, -7, -44, -16);
        scarf.AddBezier(-44, -16, -32, 2, -28, 8, -7, 7);
        scarf.CloseFigure();
        g.FillPath(rust, scarf);
        g.FillEllipse(gold, -24, -13, 51, 29);
        g.DrawEllipse(outline, -24, -13, 51, 29);
        g.FillEllipse(cream, 10, -17, 23, 22);
        var beak = new PointF[] { new(28, -5), new(43, 2), new(28, 7) };
        g.FillPolygon(gold, beak);
        g.DrawPolygon(outline, beak);
        using var wingShape = new GraphicsPath();
        wingShape.AddBezier(8, 4, -4, -5, -15, -wing, -34, -wing);
        wingShape.AddBezier(-34, -wing, -30, wing * .68f, -11, 19, 8, 4);
        wingShape.CloseFigure();
        g.FillPath(teal, wingShape);
        g.DrawPath(outline, wingShape);
        g.FillEllipse(dark, 14, -15, 17, 16);
        g.FillEllipse(Brushes.White, 20, -12, 7, 6);
        g.FillEllipse(Brushes.White, 24, -5, 3, 3);
        g.Restore(state);
    }

    private void DrawHud(Graphics g, World world)
    {
        using var accent = new SolidBrush(world.Accent);
        using var panel = new SolidBrush(Color.FromArgb(218, Ink));
        g.FillRectangle(panel, 23, 17, 222, 55);
        g.DrawString(world.Name, body, Brushes.White, 35, 22);
        for (int i = 0; i < 3; i++)
        {
            using var progress = new SolidBrush(i <= game.WorldIndex ? world.Accent : Color.FromArgb(95, 111, 129));
            g.FillRectangle(progress, 36 + i * 41, 53, 33, 3);
        }
        g.FillRectangle(panel, 720, 17, 281, 65);
        DrawStat(g, "SKOR", game.Score.ToString("00"), 735, accent);
        DrawStat(g, "REKOR", Best.ToString("00"), 810, accent);
        DrawStat(g, "HIZ", $"{game.Speed / 260:0.00}×", 893, accent);
    }

    private void DrawStat(Graphics g, string label, string value, int x, Brush accent)
    {
        g.DrawString(label, small, accent, x, 22);
        g.DrawString(value, metric, Brushes.White, x, 38);
    }

    private void DrawMenu(Graphics g, World world)
    {
        var panel = new RectangleF(275, 157, 474, 235);
        using var dark = new SolidBrush(Color.FromArgb(240, Ink));
        using var accent = new SolidBrush(world.Accent);
        using var outline = new Pen(Color.FromArgb(135, world.Accent));
        g.FillRectangle(dark, panel);
        g.DrawRectangle(outline, panel.X, panel.Y, panel.Width, panel.Height);
        g.FillRectangle(accent, panel.X, panel.Y, panel.Width, 3);
        using var center = new StringFormat { Alignment = StringAlignment.Center };
        string caption = game.State switch
        {
            FlightState.Ended => "OYUN BİTTİ",
            FlightState.Paused => "DURAKLATILDI",
            _ => "FIRAT · ARCADE"
        };
        string title = game.State switch
        {
            FlightState.Ended => "Game Over",
            FlightState.Paused => "Uçuş molası",
            _ => "Fırat Flight"
        };
        string subtitle = game.State switch
        {
            FlightState.Ended => $"{game.Crash}  ·  Skor {game.Score}",
            FlightState.Paused => world.Name,
            _ => world.Description
        };
        g.DrawString(caption, small, accent, new RectangleF(panel.X, 178, panel.Width, 26), center);
        g.DrawString(title, display, Brushes.White, new RectangleF(panel.X, 201, panel.Width, 83), center);
        g.DrawString(subtitle, body, Brushes.White, new RectangleF(panel.X, 288, panel.Width, 32), center);
        g.DrawString("Boşluk / Enter · " + (game.State == FlightState.Paused ? "Devam et" : "Kanat çırp"), small, accent,
            new RectangleF(panel.X, 335, panel.Width, 25), center);
        if (!StorageOk)
            g.DrawString("Rekor bu oturumda bellekte saklanıyor.", small, Brushes.White, new RectangleF(panel.X, 361, panel.Width, 25), center);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var background in art.Values) background.Dispose();
            display.Dispose();
            metric.Dispose();
            body.Dispose();
            small.Dispose();
        }
        base.Dispose(disposing);
    }
}
