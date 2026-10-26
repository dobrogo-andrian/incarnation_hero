using Core.State;

namespace Core.Sim;

/// <summary>A typed request from the UI or a bot. Each command is valid in exactly one mode.</summary>
public abstract record Command
{
    public abstract GameMode Mode { get; }
}

/// <summary>Let time pass without any activity.</summary>
public sealed record WaitWeeks(int Weeks) : Command
{
    public override GameMode Mode => GameMode.Overworld;
}
