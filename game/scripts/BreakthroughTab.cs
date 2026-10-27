using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>
/// The "breakthrough" tab: stage scales with the forecast, foundation, knowledge and buffs; the
/// active method; and, once a scale is full, seclusion at a place here — accepting the risk of
/// death when the forecast says it is fatal (§4.5).
/// </summary>
public partial class BreakthroughTab : VBoxContainer, IViewPart
{
    public BreakthroughTab()
    {
        AddThemeConstantOverride("separation", 8);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        bool free = view.Mode == GameMode.Overworld;

        AddChild(Text(ui.Lines(SimText.CultivationLines(view, ui.Db))));
        AddMethodPicker(ui, view, free);
        foreach (var track in view.Tracks.Where(t => t.Forecast is not null))
            AddBreakthrough(ui, view, track, free);
    }

    /// <summary>Studied methods, or none; picking one sends the instant command at once.</summary>
    private void AddMethodPicker(UiContext ui, PlayerView view, bool free)
    {
        const string none = "";
        var methods = view.Knowledge.Where(k => k.KnowledgeId.StartsWith("method.", StringComparison.Ordinal))
            .Select(k => (k.KnowledgeId, ui.Name(k.KnowledgeId)))
            .Prepend((none, ui.Text("str.ui.no_method")))
            .ToList();
        var picker = Options(methods, view.ActiveMethodId ?? none, id => ui.Send(new SetActiveMethod(id == none ? null : id)));
        picker.Disabled = !free;
        AddChild(new HSeparator());
        AddChild(Row(new Label { Text = ui.Text("str.ui.method") }, picker));
    }

    private void AddBreakthrough(UiContext ui, PlayerView view, TrackView track, bool free)
    {
        var choices = ui.Choices;
        AddChild(new HSeparator());
        if (view.Places.Count == 0)
        {
            AddChild(Text(ui.Text("str.ui.no_place_here")));
            return;
        }

        var places = view.Places.Select(p => (p.PlaceId, ui.Name(p.PlaceId))).ToList();
        choices.BreakthroughPlace = Pick(places, choices.BreakthroughPlace);
        AddChild(Row(new Label { Text = ui.Text("str.ui.breakthrough_place") }, Options(places, choices.BreakthroughPlace, id => choices.BreakthroughPlace = id)));

        bool fatal = track.Forecast!.Fatal;
        if (fatal)
        {
            var accept = new CheckBox { Text = ui.Text("str.ui.accept_death_risk"), ButtonPressed = choices.AcceptDeathRisk };
            accept.Toggled += on => choices.AcceptDeathRisk = on;
            AddChild(accept);
        }
        AddChild(Button(ui.Text("str.ui.breakthrough_start"), () =>
            ui.Send(new AttemptBreakthrough(track.VectorId, choices.BreakthroughPlace!, fatal && choices.AcceptDeathRisk)), free));
    }
}
