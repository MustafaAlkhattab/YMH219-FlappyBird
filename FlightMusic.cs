using NAudio;
using NAudio.Vorbis;
using NAudio.Wave;

namespace FlappyBird;

internal sealed class FlightMusic : IDisposable
{
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "audio", "flowerbed_fields.ogg");
    private WaveOutEvent? output;
    private RepeatTrack? track;
    private readonly bool found;
    private bool failed;

    public FlightMusic() => found = File.Exists(path);

    public bool Enabled { get; private set; } = true;
    public bool Available => found && !failed;
    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;

    public void Toggle() => Enabled = !Enabled;

    public void Update(bool inFlight)
    {
        try
        {
            if (!Enabled || !inFlight)
            {
                if (IsPlaying) output?.Pause();
                return;
            }
            if (IsPlaying || !Available) return;
            if (output is null)
            {
                track = new RepeatTrack(File.ReadAllBytes(path));
                output = new WaveOutEvent { DesiredLatency = 160, NumberOfBuffers = 3 };
                output.Init(track);
                output.Volume = .25f;
                output.PlaybackStopped += (_, e) => { if (e.Exception is not null) failed = true; };
            }
            output.Play();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidDataException or InvalidOperationException or MmException)
        {
            failed = true;
            Dispose();
        }
    }

    public void Dispose()
    {
        output?.Dispose();
        output = null;
        track?.Dispose();
        track = null;
    }

    private sealed class RepeatTrack(byte[] source) : IWaveProvider, IDisposable
    {
        private VorbisWaveReader reader = new(new MemoryStream(source, writable: false));
        public WaveFormat WaveFormat => reader.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int filled = 0;
            while (filled < count)
            {
                int read = reader.Read(buffer, offset + filled, count - filled);
                if (read == 0)
                {
                    reader.Dispose();
                    reader = new VorbisWaveReader(new MemoryStream(source, writable: false));
                    read = reader.Read(buffer, offset + filled, count - filled);
                    if (read == 0) break;
                }
                filled += read;
            }
            return filled;
        }

        public void Dispose() => reader.Dispose();
    }
}
