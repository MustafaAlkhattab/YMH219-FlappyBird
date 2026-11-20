namespace FlappyBird;

public sealed class Bird
{
    public float Y { get; set; } = 230;
    public float Velocity { get; private set; }
    public RectangleF Bounds => new(150, Y, 34, 28);
    public void Flap() => Velocity = -340;
    public void Update(float dt) { Velocity += 940 * dt; Y += Velocity * dt; }
}

public sealed class Pipe(float x, float gapY)
{
    public float X { get; set; } = x;
    public float GapY { get; set; } = gapY;
    public bool Passed { get; set; }
    public const float Width = 66, Gap = 175;
    public RectangleF Top => new(X, 0, Width, GapY);
    public RectangleF Bottom => new(X, GapY + Gap, Width, Game.Ground - GapY - Gap);
}

public enum GameStatus { Ready, Running, Paused, Over }

public sealed class Game
{
    public const int Width = 800, Ground = 480;
    private readonly Random random = new();
    public Bird Bird { get; private set; } = new();
    public List<Pipe> Pipes { get; } = [];
    public GameStatus Status { get; private set; }
    public int Score { get; private set; }
    public float Speed => 175 + Math.Min(Score * 4, 100);
    public Game() => Reset();
    public void Reset()
    {
        Bird = new(); Score = 0; Status = GameStatus.Ready; Pipes.Clear();
        Pipes.Add(new(650, random.Next(95, 240)));
        Pipes.Add(new(980, random.Next(95, 240)));
    }
    public void Flap()
    {
        if (Status == GameStatus.Over) Reset();
        if (Status == GameStatus.Paused) return;
        Status = GameStatus.Running; Bird.Flap();
    }
    public void Pause() { if (Status == GameStatus.Running) Status = GameStatus.Paused; else if (Status == GameStatus.Paused) Status = GameStatus.Running; }
    public void Update(float dt)
    {
        if (Status != GameStatus.Running) return;
        Bird.Update(dt);
        foreach (var pipe in Pipes)
        {
            pipe.X -= Speed * dt;
            if (!pipe.Passed && pipe.X + Pipe.Width < Bird.Bounds.Left) { Score++; pipe.Passed = true; }
            if (pipe.X + Pipe.Width < 0)
            {
                pipe.X = Pipes.Max(p => p.X) + 330;
                pipe.GapY = random.Next(95, 240); pipe.Passed = false;
            }
            if (Bird.Bounds.IntersectsWith(pipe.Top) || Bird.Bounds.IntersectsWith(pipe.Bottom)) Status = GameStatus.Over;
        }
        if (Bird.Y < 0 || Bird.Bounds.Bottom >= Ground) Status = GameStatus.Over;
    }
}
