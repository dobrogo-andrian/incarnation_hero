using Godot;

/// <summary>
/// The player's presentation settings, kept between runs in <c>user://settings.cfg</c>. Only how
/// the screen shows things: Core never reads them.
/// </summary>
public sealed class UiSettings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "ui";
    private const string SpeedKey = "speed";

    /// <summary>Weeks shown per second at each step of the speed slider (D47); the last one shows them at once.</summary>
    public static readonly double[] Speeds = [4, 10, 25, 60, double.PositiveInfinity];

    /// <summary>The slider step: an index into <see cref="Speeds"/>; ≈ 0.1 s a week by default.</summary>
    public int Speed { get; private set; } = 1;

    public double WeeksPerSecond => Speeds[Speed];

    public static UiSettings Load()
    {
        var file = new ConfigFile();
        var settings = new UiSettings();
        if (file.Load(FilePath) == Error.Ok)
            settings.Speed = Math.Clamp((int)file.GetValue(Section, SpeedKey, settings.Speed), 0, Speeds.Length - 1);
        return settings;
    }

    public void SetSpeed(int speed)
    {
        Speed = Math.Clamp(speed, 0, Speeds.Length - 1);
        var file = new ConfigFile();
        file.SetValue(Section, SpeedKey, Speed);
        file.Save(FilePath);
    }
}
