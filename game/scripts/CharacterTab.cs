using Core.Character;
using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The "character" tab: stats with their aspects, a ranked stat (D48) with its rank, scale and overflow
/// towards the next rank as bars, and for purity (D49) its deficit towards the rank below as a red bar
/// filling leftwards; then resources and injuries. A ranked stat's numbers live in the bars' tooltip.
/// </summary>
public partial class CharacterTab : VBoxContainer, IViewPart
{
    private static readonly Color ScaleColor = new(0.45f, 0.7f, 0.5f);
    private static readonly Color OverflowColor = new(0.9f, 0.6f, 0.25f);
    private static readonly Color DeficitColor = new(0.8f, 0.25f, 0.25f);

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        foreach (var stat in view.Stats)
        {
            AddChild(Explained(ui, [SimText.StatTitle(stat)]));
            if (stat.Standing is { } standing)
                AddChild(Bars(ui.Lines([.. SimText.StandingLines(stat), .. SimText.StandingHelp(stat)]), standing));
        }
        AddChild(new HSeparator());
        AddChild(Explained(ui, SimText.ConditionLines(view)));
    }

    /// <summary>
    /// A stat without overflow (D50) has its scale alone. Otherwise the scale takes most of the row; the overflow bar to its right fills towards the breakthrough,
    /// and for a stat with a deficit (D49) the deficit bar to its left fills towards the collapse; on the
    /// first rank, with nowhere to fall, it stays empty.
    /// </summary>
    private static HBoxContainer Bars(string tooltip, StatStanding standing)
    {
        var scale = Bar(standing.Pool, standing.Filled, ScaleColor, tooltip);
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

    private static ProgressBar Bar(double max, double value, Color color, string tooltip)
    {
        var bar = new ProgressBar
        {
            MaxValue = max,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltip,
            MouseFilter = MouseFilterEnum.Pass,
        };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        return bar;
    }
}
