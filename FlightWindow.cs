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
    private readonly DeckButton routes = new(DeckAction.Route, "Tam rota", "Uçuş rotasını değiştir");
    private readonly DeckButton difficulties = new(DeckAction.Difficulty, "Sefer", "Uçuş zorluğunu değiştir");
    private readonly DeckButton restart = new(DeckAction.Restart, "Yeni uçuş", "Yeni uçuş · R");
    private readonly DeckButton pause = new(DeckAction.Pause, "Duraklat", "Duraklat veya devam et · P");
    private readonly DeckButton screen = new(DeckAction.Screen, "Tam ekran", "Tam ekranı değiştir · F11");
    private readonly ContextMenuStrip routeMenu = CreateMenu();
    private readonly ContextMenuStrip difficultyMenu = CreateMenu();
    private readonly FlightDeck deck;
    private bool fullscreen;
    private FormWindowState previousWindowState;
    private Rectangle previousBounds;
    private bool recordStorageOk = true;
    private double accumulator;
    private int route = -1;
    private FlightDifficulty difficulty;

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

        AddMenuOption(routeMenu, "Tam rota", () => route = -1);
        for (int i = 0; i < World.Route.Count; i++)
        {
            int selected = i;
            AddMenuOption(routeMenu, World.Route[i].Name, () => route = selected);
        }
        foreach (FlightDifficulty option in Enum.GetValues<FlightDifficulty>())
            AddMenuOption(difficultyMenu, option.ToString(), () => difficulty = option);

        deck = new(routes, difficulties, restart, pause, screen);
        view.Controls.Add(deck);
        view.SizeChanged += (_, _) => ArrangeDeck();
        Controls.Add(view);
        ArrangeDeck();
        view.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { view.Focus(); Fly(); } };
        restart.Click += (_, _) => Reset();
        pause.Click += (_, _) => { game.TogglePause(); view.Focus(); RefreshHud(); };
        screen.Click += (_, _) => ToggleFullscreen();
        routes.Click += (_, _) => OpenMenu(routeMenu, routes, route + 1);
        difficulties.Click += (_, _) => OpenMenu(difficultyMenu, difficulties, (int)difficulty);
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

    private void ArrangeDeck()
    {
        deck.Arrange(view.ClientSize);
        view.GameplayBottom = deck.Top - 8 * view.DeviceDpi / 96f;
        view.Invalidate();
    }

    private static ContextMenuStrip CreateMenu() => new()
    {
        BackColor = Color.FromArgb(36, 48, 66), ForeColor = Color.White,
        Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false },
        Font = new("Segoe UI", 10)
    };

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 44, 61);
        public override Color MenuItemSelected => Color.FromArgb(59, 75, 101);
        public override Color MenuItemBorder => Color.FromArgb(129, 151, 178);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    }

    private void AddMenuOption(ContextMenuStrip menu, string title, Action select)
    {
        var item = new ToolStripMenuItem(title) { ForeColor = Color.White };
        item.Click += (_, _) =>
        {
            select();
            Reset();
        };
        menu.Items.Add(item);
    }

    private void OpenMenu(ContextMenuStrip menu, Control control, int selected)
    {
        if (game.State == FlightState.Flying) { game.TogglePause(); RefreshHud(); }
        for (int i = 0; i < menu.Items.Count; i++)
            ((ToolStripMenuItem)menu.Items[i]).Checked = i == selected;
        menu.Show(control, new Point(0, -4), ToolStripDropDownDirection.AboveRight);
    }

    private void OnFlightKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None || routeMenu.Visible || difficultyMenu.Visible) return;
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
        screen.Text = fullscreen ? "Pencereye dön" : "Tam ekran";
        view.Focus();
        ArrangeDeck();
    }

    private void Reset(bool focus = true)
    {
        game.Reset(route, difficulty);
        routes.Text = route < 0 ? "Tam rota" : World.Route[route].Name;
        difficulties.Text = difficulty.ToString();
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
        if (game.Score > previous) recordStorageOk = scores.SaveIfBetter(route, difficulty, game.Score) && recordStorageOk;
        RefreshHud();
    }

    private void RefreshHud()
    {
        pause.Enabled = game.State is FlightState.Flying or FlightState.Paused;
        pause.Text = game.State == FlightState.Paused ? "Devam et" : "Duraklat";
        deck.SetTheme(game.World.Accent);
        view.Best = scores.Get(route, difficulty);
        view.StorageOk = recordStorageOk;
        view.Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Dispose();
        clock.Stop();
        routeMenu.Dispose();
        difficultyMenu.Dispose();
        base.OnFormClosed(e);
    }
}
