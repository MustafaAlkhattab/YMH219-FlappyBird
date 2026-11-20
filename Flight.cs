namespace FlappyBird;

internal enum FlightState { Ready, Flying, Paused, Ended }
internal enum FlightDifficulty { Sefer, Ustalık }

internal sealed record World(string Name, string Description, string Art, Color Accent, Color Stone, Color Shadow, float Gap, float Speed, float Wind)
{
    public override string ToString() => Name;

    public static IReadOnlyList<World> Route { get; } =
    [
        new("Fırat Boğazı", "Geniş geçitler, sakin hava", "river.jpg",
            Color.FromArgb(255, 213, 147), Color.FromArgb(74, 111, 113), Color.FromArgb(21, 52, 59), 212, 260, 0),
        new("Kızıl Geçit", "Daha dar geçitler, hızlanan rota", "ruins.jpg",
            Color.FromArgb(255, 198, 111), Color.FromArgb(169, 81, 61), Color.FromArgb(73, 28, 41), 185, 305, 50),
        new("Ay Yaylası", "Hareketli kapılar ve dalgalı rüzgâr", "moon.jpg",
            Color.FromArgb(192, 220, 255), Color.FromArgb(83, 90, 158), Color.FromArgb(33, 27, 69), 158, 350, 105)
    ];
}

internal sealed class Bird
{
    public const float X = 222;
    public float Y { get; private set; } = 268;
    public float Velocity { get; private set; }
    public float WingTime { get; private set; } = 1;
    public RectangleF Bounds => new(X - 19, Y - 12, 38, 24);

    public void Flap()
    {
        Velocity = -378;
        WingTime = 0;
    }

    public void Tick(float dt, float wind)
    {
        Velocity = Math.Min(700, Velocity + (1070 + wind) * dt);
        Y += Velocity * dt;
        WingTime += dt;
    }
}

internal sealed class Gate(float x, float center, float gap, bool moving, float phase)
{
    public const float Width = 82;
    public float X { get; private set; } = x;
    public bool Passed { get; set; }
    private float offset;
    public float Center => center + offset;
    public RectangleF Upper => new(X, 0, Width, Center - gap / 2);
    public RectangleF Lower => new(X, Center + gap / 2, Width, Game.Ground - Center - gap / 2);

    public void Tick(float dt, float speed, float time)
    {
        X -= speed * dt;
        if (moving) offset = 28 * MathF.Sin(time * 1.7f + phase);
    }
}

internal sealed class Game
{
    public const int Width = 1024, Height = 576, Ground = 546, RegionLength = 5;
    private readonly Random random;
    private readonly List<Gate> gates = [];
    private int startWorld;
    private FlightDifficulty difficulty;
    private float lastCenter;

    public Game(Random? random = null)
    {
        this.random = random ?? Random.Shared;
        Reset();
    }

    public Bird Bird { get; private set; } = new();
    public IReadOnlyList<Gate> Gates => gates;
    public FlightState State { get; private set; }
    public int Score { get; private set; }
    public float Time { get; private set; }
    public float Travel { get; private set; }
    public int WorldIndex => startWorld < 0 ? Math.Min(2, Score / RegionLength) : startWorld;
    public World World => FlappyBird.World.Route[WorldIndex];
    public float Speed => World.Speed + Math.Min(Score * 5, 135) + (difficulty == FlightDifficulty.Ustalık ? 52 : 0);
    public string Crash { get; private set; } = "";

    public void Reset(int region = -1, FlightDifficulty mode = FlightDifficulty.Sefer)
    {
        startWorld = region;
        difficulty = mode;
        Bird = new();
        Score = 0;
        Time = Travel = 0;
        Crash = "";
        State = FlightState.Ready;
        gates.Clear();
        lastCenter = 270;
        for (int i = 0; i < 3; i++) AddGate(555 + 360 * i);
    }

    private void AddGate(float x)
    {
        float gap = World.Gap - (difficulty == FlightDifficulty.Ustalık ? 28 : 0) - Math.Min(18, Math.Max(0, Score - 10));
        lastCenter = Math.Clamp(lastCenter + random.Next(-86, 87), gap / 2 + 92, Ground - gap / 2 - 92);
        gates.Add(new(x, lastCenter, gap, WorldIndex == 2, random.NextSingle() * MathF.Tau));
    }

    public void Flap()
    {
        if (State is FlightState.Paused or FlightState.Ended) return;
        State = FlightState.Flying;
        Bird.Flap();
    }

    public void TogglePause()
    {
        if (State == FlightState.Flying) State = FlightState.Paused;
        else if (State == FlightState.Paused) State = FlightState.Flying;
    }

    public void Tick(float dt)
    {
        if (State != FlightState.Flying) return;
        float speed = Speed;
        Time += dt;
        Travel += speed * dt;
        Bird.Tick(dt, World.Wind * MathF.Sin(Time * 2.8f));
        foreach (Gate gate in gates) gate.Tick(dt, speed, Time);

        var bounds = Bird.Bounds;
        if (bounds.Top <= 0 || bounds.Bottom >= Ground)
        {
            Finish(bounds.Top <= 0 ? "Tavana çarptın." : "Zemine çarptın.");
            return;
        }
        if (gates.Any(g => bounds.IntersectsWith(g.Upper) || bounds.IntersectsWith(g.Lower)))
        {
            Finish("Kapıya çarptın.");
            return;
        }
        foreach (Gate gate in gates.Where(g => !g.Passed && g.X + Gate.Width < bounds.Left))
        {
            gate.Passed = true;
            Score++;
        }
        if (gates[0].X + Gate.Width < 0)
        {
            gates.RemoveAt(0);
            AddGate(gates[^1].X + Math.Clamp(Speed * 1.12f, 272, 405));
        }
    }

    private void Finish(string reason)
    {
        Crash = reason;
        State = FlightState.Ended;
    }
}
