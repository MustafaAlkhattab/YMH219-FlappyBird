using System.Diagnostics;

namespace FlappyBird;

internal sealed class FlightWindow : Form
{
    private readonly Game game = new();
    private readonly HighScores scores = new();
    private readonly FlightCanvas view;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    private readonly Stopwatch clock = new();
    private readonly HashSet<Keys> held = [];
    private readonly ComboBox routes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 165, AccessibleName = "Uçuş rotası" };
    private readonly ComboBox difficulties = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 101, AccessibleName = "Zorluk" };
    private readonly Button pause = MakeButton("Duraklat", false);
    private readonly FlowLayoutPanel toolbar = new();
    private readonly TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
    private bool fullscreen;
    private FormWindowState previousWindowState;
    private Rectangle previousBounds;
    private bool recordStorageOk = true;
    private double accumulator;
    private int Route => routes.SelectedIndex - 1;
    private FlightDifficulty Difficulty => (FlightDifficulty)Math.Max(0, difficulties.SelectedIndex);

    public FlightWindow()
    {
        Text = "Fırat Flight";
        ClientSize = new(1160, 735);
        MinimumSize = new(720, 478);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        BackColor = Color.FromArgb(16, 24, 34);
        ForeColor = Color.White;
        Font = new("Segoe UI", 10f);
        view = new(game) { Dock = DockStyle.Fill, Margin = Padding.Empty };

        routes.Items.Add("Tam rota");
        routes.Items.AddRange(World.Route.Cast<object>().ToArray());
        routes.SelectedIndex = 0;
        difficulties.Items.AddRange(["Sefer", "Ustalık"]);
        difficulties.SelectedIndex = 0;
        var restart = MakeButton("Yeni uçuş", true);
        var full = MakeButton("Tam ekran · F11", false);
        toolbar.Dock = DockStyle.Fill;
        toolbar.WrapContents = true;
        toolbar.Padding = new(12, 8, 0, 0);
        toolbar.Margin = Padding.Empty;
        routes.Margin = difficulties.Margin = new(4, 9, 8, 3);
        toolbar.Controls.AddRange([Caption("Rota"), routes, Caption("Zorluk"), difficulties, restart, pause, full]);
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 90));
        layout.Controls.Add(view, 0, 0);
        layout.Controls.Add(toolbar, 0, 1);
        Controls.Add(layout);
        view.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { view.Focus(); Fly(); } };
        restart.Click += (_, _) => Reset();
        pause.Click += (_, _) => { game.TogglePause(); view.Focus(); RefreshHud(); };
        full.Click += (_, _) => ToggleFullscreen();
        routes.SelectedIndexChanged += (_, _) => Reset(focus: false);
        difficulties.SelectedIndexChanged += (_, _) => Reset(focus: false);
        KeyDown += OnFlightKeyDown;
        KeyUp += (_, e) => held.Remove(e.KeyCode);
        Deactivate += (_, _) =>
        {
            if (game.State == FlightState.Flying) game.TogglePause();
            held.Clear();
            RefreshHud();
        };
        timer.Tick += Tick;
        Shown += (_, _) => { Reset(); clock.Start(); timer.Start(); };
        RefreshHud();
    }

    private static Label Caption(string title) => new()
    {
        AutoSize = true, Text = title, Margin = new(8, 13, 0, 0), ForeColor = Color.FromArgb(188, 201, 215)
    };

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text, Height = 36, AutoSize = true, Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat,
            ForeColor = primary ? Color.FromArgb(33, 27, 20) : Color.White,
            BackColor = primary ? Color.FromArgb(255, 210, 141) : Color.FromArgb(38, 50, 64),
            Padding = new(8, 0, 8, 0), Margin = new(4, 3, 4, 3)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(102, 116, 130);
        return button;
    }

    private void OnFlightKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None || routes.DroppedDown || difficulties.DroppedDown) return;
        if (!view.Focused && e.KeyCode is Keys.Space or Keys.Up or Keys.Enter) return;
        if (e.KeyCode is not (Keys.Space or Keys.Enter or Keys.Up or Keys.P or Keys.R)) return;
        e.SuppressKeyPress = true;
        if (!held.Add(e.KeyCode)) return;
        switch (e.KeyCode)
        {
            case Keys.P: game.TogglePause(); RefreshHud(); break;
            case Keys.R: Reset(); break;
            default: Fly(); break;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (fullscreen && keyData == Keys.Escape))
        {
            ToggleFullscreen();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            previousWindowState = WindowState;
            previousBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var screen = Screen.FromControl(this);
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = screen.Bounds;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = previousBounds;
            WindowState = previousWindowState;
        }
        fullscreen = !fullscreen;
        view.Focus();
    }

    private void Reset(bool focus = true)
    {
        game.Reset(Route, Difficulty);
        accumulator = 0;
        recordStorageOk = true;
        if (focus) view.Focus();
        RefreshHud();
    }

    private void Fly()
    {
        if (game.State == FlightState.Ended) Reset();
        if (game.State == FlightState.Paused) game.TogglePause();
        else game.Flap();
        RefreshHud();
    }

    private void Tick(object? sender, EventArgs e)
    {
        double elapsed = Math.Min(.1, clock.Elapsed.TotalSeconds);
        clock.Restart();
        if (game.State != FlightState.Flying) return;
        int previous = game.Score;
        accumulator += elapsed;
        const double step = 1d / 120;
        while (accumulator >= step)
        {
            game.Tick((float)step);
            accumulator -= step;
        }
        if (game.Score > previous) recordStorageOk = scores.SaveIfBetter(Route, Difficulty, game.Score) && recordStorageOk;
        RefreshHud();
    }

    private void RefreshHud()
    {
        pause.Enabled = game.State is FlightState.Flying or FlightState.Paused;
        pause.Text = game.State == FlightState.Paused ? "Devam et" : "Duraklat";
        view.Best = scores.Get(Route, Difficulty);
        view.StorageOk = recordStorageOk;
        view.Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Dispose();
        clock.Stop();
        base.OnFormClosed(e);
    }
}
