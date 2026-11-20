using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace FlappyBird;

public partial class Form1 : Form
{
    private readonly Game game = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Label score = new() { AutoSize = true, ForeColor = Color.White, Font = new("Segoe UI", 12, FontStyle.Bold) };
    private readonly Panel canvas = new GameCanvas();
    private bool spaceHeld;
    private int best;
    private readonly string scorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YMH219-FlappyBird", "best.txt");
    public Form1()
    {
        InitializeComponent(); Controls.Clear();
        Text = "Fırat Flight | YMH219 Flappy Bird";
        ClientSize = new(840, 690); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(15, 23, 42); KeyPreview = true;
        var title = new Label { Text = "FIRAT FLIGHT", Location = new(24, 17), Size = new(400, 38), ForeColor = Color.White, Font = new("Segoe UI", 23, FontStyle.Bold) };
        score.Location = new(570, 28);
        canvas.SetBounds(20, 75, 800, 520); canvas.Paint += DrawGame;
        var restart = MakeButton("Yeni oyun  [R]", 20, (_, _) => { game.Reset(); canvas.Invalidate(); });
        var pause = MakeButton("Duraklat / Devam  [P]", 215, (_, _) => game.Pause());
        var hint = new Label { Text = "BOŞLUK: uç  •  R: yeniden başlat  •  P: duraklat\nMustafa Alkhatta · 250541607", ForeColor = Color.FromArgb(203, 213, 225), Location = new(440, 613), Size = new(380, 58), Font = new("Segoe UI", 10) };
        Controls.AddRange([title, score, canvas, restart, pause, hint]);
        try { if (File.Exists(scorePath)) int.TryParse(File.ReadAllText(scorePath), out best); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Space) { if (!spaceHeld) game.Flap(); spaceHeld = true; e.Handled = e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.R) game.Reset();
            if (e.KeyCode == Keys.P) game.Pause();
        };
        KeyUp += (_, e) => { if (e.KeyCode == Keys.Space) spaceHeld = false; };
        Deactivate += (_, _) => { if (game.Status == GameStatus.Running) game.Pause(); spaceHeld = false; };
        timer.Tick += (_, _) =>
        {
            var dt = Math.Min((float)clock.Elapsed.TotalSeconds, .04f); clock.Restart(); game.Update(dt);
            if (game.Score > best)
            {
                best = game.Score;
                try { Directory.CreateDirectory(Path.GetDirectoryName(scorePath)!); File.WriteAllText(scorePath, best.ToString()); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            score.Text = $"SKOR  {game.Score}    REKOR  {best}"; canvas.Invalidate();
        };
        timer.Start();
    }
    private static Button MakeButton(string text, int x, EventHandler action)
    {
        var b = new Button { Text = text, Location = new(x, 615), Size = new(185, 44), BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.FromArgb(15, 23, 42), FlatStyle = FlatStyle.Flat, Font = new("Segoe UI", 10, FontStyle.Bold) };
        b.Click += action; return b;
    }
    private void DrawGame(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var sky = new LinearGradientBrush(new Rectangle(0, 0, 800, 520), Color.FromArgb(25, 50, 83), Color.FromArgb(72, 120, 145), 90f);
        g.FillRectangle(sky, 0, 0, 800, 520);
        using var cloud = new SolidBrush(Color.FromArgb(45, Color.White));
        foreach (int x in new[] { 70, 320, 610 }) { g.FillEllipse(cloud, x, 95, 120, 28); g.FillEllipse(cloud, x + 24, 76, 65, 44); }
        foreach (var pipe in game.Pipes)
        {
            using var fill = new SolidBrush(Color.FromArgb(34, 197, 94)); using var edge = new Pen(Color.FromArgb(16, 100, 75), 4);
            g.FillRectangle(fill, pipe.Top); g.FillRectangle(fill, pipe.Bottom);
            g.DrawRectangle(edge, pipe.X, 0, Pipe.Width, pipe.GapY);
            g.DrawRectangle(edge, pipe.X, pipe.GapY + Pipe.Gap, Pipe.Width, Game.Ground - pipe.GapY - Pipe.Gap);
        }
        using var ground = new SolidBrush(Color.FromArgb(17, 35, 47)); g.FillRectangle(ground, 0, Game.Ground, 800, 40);
        var b = game.Bird.Bounds;
        using var yellow = new SolidBrush(Color.FromArgb(250, 204, 21)); g.FillEllipse(yellow, b);
        g.FillEllipse(Brushes.White, b.X + 19, b.Y + 4, 13, 13); g.FillEllipse(Brushes.Black, b.X + 26, b.Y + 7, 4, 6);
        g.FillPolygon(Brushes.Orange, [new PointF(b.Right - 1, b.Y + 13), new PointF(b.Right + 12, b.Y + 18), new PointF(b.Right - 1, b.Y + 21)]);
        if (game.Status != GameStatus.Running)
        {
            using var overlay = new SolidBrush(Color.FromArgb(195, 15, 23, 42)); g.FillRectangle(overlay, 160, 165, 480, 150);
            using var heading = new Font("Segoe UI", 25, FontStyle.Bold); using var detail = new Font("Segoe UI", 12);
            using var center = new StringFormat { Alignment = StringAlignment.Center };
            var text = game.Status switch { GameStatus.Over => "GAME OVER", GameStatus.Paused => "DURAKLATILDI", _ => "UÇMAYA HAZIR MISIN?" };
            g.DrawString(text, heading, Brushes.White, new RectangleF(160, 188, 480, 50), center);
            g.DrawString(game.Status == GameStatus.Paused ? "Devam etmek için P tuşuna bas." : "Başlamak için BOŞLUK tuşuna bas.", detail, Brushes.White, new RectangleF(160, 254, 480, 40), center);
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { timer.Stop(); timer.Dispose(); clock.Stop(); base.OnFormClosed(e); }
}

internal sealed class GameCanvas : Panel
{
    public GameCanvas() { DoubleBuffered = true; ResizeRedraw = true; TabStop = false; }
}
