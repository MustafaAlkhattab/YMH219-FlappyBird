using System.Text.Json;

namespace FlappyBird;

internal sealed class HighScores
{
    private readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YMH219-FlappyBird");
    private readonly Dictionary<string, int> scores = [];
    private string ScoreFile => Path.Combine(directory, "scores.json");

    public HighScores()
    {
        try
        {
            if (File.Exists(ScoreFile))
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(ScoreFile));
                if (existing is not null)
                    foreach (var pair in existing) scores[pair.Key] = Math.Max(0, pair.Value);
            }
            else if (File.Exists(Path.Combine(directory, "best.txt")) &&
                     int.TryParse(File.ReadAllText(Path.Combine(directory, "best.txt")), out int old))
                scores[Key(-1, FlightDifficulty.Sefer)] = Math.Max(0, old);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public int Get(int route, FlightDifficulty mode) => scores.GetValueOrDefault(Key(route, mode));

    public bool SaveIfBetter(int route, FlightDifficulty mode, int score)
    {
        if (score <= Get(route, mode)) return true;
        scores[Key(route, mode)] = score;
        try
        {
            Directory.CreateDirectory(directory);
            var pending = ScoreFile + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(scores));
            File.Move(pending, ScoreFile, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static string Key(int route, FlightDifficulty mode) => $"{route}/{mode}";
}
