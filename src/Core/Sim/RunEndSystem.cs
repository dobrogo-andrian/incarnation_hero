using Core.State;

namespace Core.Sim;

/// <summary>The single way a run ends, whatever the cause.</summary>
public static class RunEndSystem
{
    public static void End(GameState state, RunEndCause cause, List<SimEvent> events)
    {
        state.Mode = GameMode.RunEnd;
        state.EndCause = cause;
        state.Activity = null;
        state.Event = null;
        events.Add(new RunEnded(cause));
    }
}
