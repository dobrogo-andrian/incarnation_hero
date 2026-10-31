using Core.Character;
using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The stats in the character window: stats with their aspects, a ranked stat (D48) with its rank, scale and overflow
/// towards the next rank as bars, and for purity (D49) its deficit towards the rank below as a red bar
/// filling leftwards; a resource that wears the stat away (toxins on purity) is drawn over the end of
/// its scale in violet instead of among the resources (<see cref="ConditionView"/>). A ranked stat's
/// numbers live in the bars' tooltip.
/// </summary>
public partial class StatsView : VBoxContainer, IViewPart
{
    private static readonly Color ScaleColor = new(0.45f, 0.7f, 0.5f);
    private static readonly Color OverflowColor = new(0.9f, 0.6f, 0.25f);
    private static readonly Color DeficitColor = new(0.8f, 0.25f, 0.25f);
    private static readonly Color ErosionColor = new(0.6f, 0.3f, 0.85f);

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        foreach (var stat in view.Stats)
        {
            AddChild(Explained(ui, [SimText.StatTitle(stat)]));
            if (stat.Standing is not { } standing)
                continue;
            var eroding = view.Resources.Where(r => stat.ErodedBy.Contains(r.ResourceId)).ToList();
            string tooltip = ui.Lines([.. eroding.Select(SimText.ResourceText), .. SimText.StandingLines(stat), .. SimText.StandingHelp(stat)]);
            AddChild(Bars(tooltip, standing, eroding));
        }
    }

    /// <summary>A resource drawn over a stat's scale (<see cref="DrawErosion"/>) instead of among the others.</summary>
    public static bool ShownOnAStat(PlayerView view, string resourceId) => view.Stats.Any(s => s.ErodedBy.Contains(resourceId));

    /// <summary>
    /// A stat without overflow (D50) has its scale alone. Otherwise the scale takes most of the row; the overflow bar to its right fills towards the breakthrough,
    /// and for a stat with a deficit (D49) the deficit bar to its left fills towards the collapse; on the
    /// first rank, with nowhere to fall, it stays empty.
    /// </summary>
    private static HBoxContainer Bars(string tooltip, StatStanding standing, IReadOnlyList<ResourceView> eroding)
    {
        var scale = Bar(standing.Pool, standing.Filled, ScaleColor, tooltip);
        foreach (var resource in eroding)
            scale.Draw += () => DrawErosion(scale, resource);
        if (standing.DecayPerWeek is null)
            return Row(scale);
        scale.SizeFlagsStretchRatio = 3;
        var overflow = Bar(standing.Breakthrough ?? Math.Max(standing.Overflow, 1), standing.Overflow, OverflowColor, tooltip);
        if (standing.HealPerWeek is null)
            return Row(scale, overflow);
        var deficit = Bar(standing.Collapse ?? 1, standing.Deficit, DeficitColor, tooltip);
        deficit.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
        return Row(deficit, scale, overflow);
    }

    /// <summary>
    /// The share of the resource against its maximum, drawn over the scale's filled end (or its start
    /// when the scale is nearly empty): how much of the stat it is eating into.
    /// </summary>
    private static void DrawErosion(ProgressBar scale, ResourceView resource)
    {
        if (resource.Current <= 0 || resource.Max.Value <= 0)
            return;
        float width = scale.Size.X * (float)Math.Min(1, resource.Current / resource.Max.Value);
        float filled = scale.Size.X * (float)(scale.Value / scale.MaxValue);
        float start = Math.Max(0, filled - width);
        scale.DrawRect(new Rect2(start, 0, width, scale.Size.Y), ErosionColor);
    }
}
