using Core.Content;
using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>
/// The centre panel's actions for the picked point (D44): a zone's danger and journey with "set
/// out", the current settlement's entrance or the current wild zone's places and hunting grounds;
/// inside a place, meditation; in the tea house, asking around; in a
/// shop, its offers; on a hunting ground, at the arena or the job board, training (D52).
/// The place itself (name, description) is drawn where the map was. Rebuilt from the PlayerView on
/// every refresh, with the player's picks kept in <see cref="Choices"/>.
/// </summary>
public partial class PointPanel : VBoxContainer
{
    /// <summary>"Enter the settlement" was pressed on the region map.</summary>
    public event Action? EnterPressed;

    /// <summary>A point was picked from the panel itself (a place of the current wild zone).</summary>
    public event Action<MapTarget>? TargetPicked;

    private UiContext? _ui;
    private PlayerView? _view;
    private MapTarget? _target;

    public PointPanel()
    {
        AddThemeConstantOverride("separation", 12);
    }

    public void Refresh(UiContext ui, PlayerView view, MapTarget? target)
    {
        (_ui, _view, _target) = (ui, view, target);
        Rebuild();
    }

    /// <summary>Rebuilds from the last view; also after a pick that changes which controls are shown.</summary>
    private void Rebuild()
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        var (ui, view) = (_ui!, _view!);
        bool free = view.Mode == GameMode.Overworld;

        switch (_target)
        {
            case null:
                AddChild(Text(ui.Text("str.ui.select_point")));
                break;
            case ZoneTarget zone:
                ShowZone(ui, view, zone.ZoneId, free);
                break;
            case PlaceTarget place when view.Places.FirstOrDefault(p => p.PlaceId == place.PlaceId) is { } here:
                ShowPlace(ui, here, free);
                break;
            case ShopTarget shop:
                ShowShop(ui, view, shop.ShopId, free);
                break;
            case InquireTarget:
                AddChild(Duration(ui));
                AddChild(Button(ui.Text("str.ui.inquire_start"), () => ui.Send(new StartActivity(new InquireActivity(), ui.Choices.ActivityWeeks)), free));
                break;
            case HuntTarget hunt:
                ShowHunt(ui, ui.Db.Get<HuntDef>(hunt.HuntId), free);
                break;
            case ArenaTarget arena:
                ShowArena(ui, ui.Db.Get<ArenaDef>(arena.ArenaId), free);
                break;
            case JobsTarget:
                ShowJobs(ui, view, free);
                break;
        }
    }

    private void ShowZone(UiContext ui, PlayerView view, string zoneId, bool free)
    {
        if (view.Region.FirstOrDefault(z => z.ZoneId == zoneId) is not { } zone)
            return;
        AddChild(Title(ui.Name(zoneId)));
        AddChild(Text(ui.Text(SimText.RegionZoneText(zone, view))));

        if (zoneId != view.Zone.ZoneId)
        {
            if (zone.TravelWeeks is not null)
                AddChild(Button(ui.Text("str.ui.travel"), () => ui.Send(new Travel(zoneId)), free));
            return;
        }
        if (ui.Db.Get<ZoneDef>(zoneId).Settlement)
        {
            AddChild(Button(ui.Text("str.ui.enter_settlement"), () => EnterPressed?.Invoke()));
            return;
        }
        AddChild(Text(ui.Text("str.ui.places_here")));
        foreach (var place in view.Places)
            AddChild(Button(ui.Name(place.PlaceId), () => TargetPicked?.Invoke(new PlaceTarget(place.PlaceId))));
        var hunts = ui.Db.All<HuntDef>().Where(h => h.Zone == zoneId).ToList();
        if (hunts.Count == 0)
            return;
        AddChild(Text(ui.Text("str.ui.hunts_here")));
        foreach (var hunt in hunts)
            AddChild(Button(ui.Name(hunt.Id), () => TargetPicked?.Invoke(new HuntTarget(hunt.Id))));
    }

    private void ShowHunt(UiContext ui, HuntDef hunt, bool free)
    {
        AddChild(Text(ui.Text(SimText.HuntText(hunt))));
        AddChild(Text(ui.Text(SimText.TrainsText(hunt))));
        AddChild(Duration(ui));
        AddChild(Button(ui.Text("str.ui.hunt_start"), () => ui.Send(new StartActivity(new HuntActivity(hunt.Id), ui.Choices.ActivityWeeks)), free));
    }

    /// <summary>The ranks of opponents with fee and prize, the picked one, and the weeks of bouts.</summary>
    private void ShowArena(UiContext ui, ArenaDef arena, bool free)
    {
        var choices = ui.Choices;
        AddChild(Text(ui.Text(SimText.TrainsText(arena))));
        var bouts = arena.Bouts.Select((b, i) => (i.ToString(), ui.Text(SimText.BoutText(b)))).ToList();
        choices.Bout = Math.Clamp(choices.Bout, 0, bouts.Count - 1);
        AddChild(Row(new Label { Text = ui.Text("str.ui.bout") }, Options(bouts, choices.Bout.ToString(), id => choices.Bout = int.Parse(id))));
        AddChild(Duration(ui));
        AddChild(Button(ui.Text("str.ui.arena_start"), () => ui.Send(new StartActivity(new ArenaActivity(arena.Id, choices.Bout), choices.ActivityWeeks)), free));
    }

    /// <summary>Every job of the settlement with its pay, what it trains, and one button each; the weeks are shared.</summary>
    private void ShowJobs(UiContext ui, PlayerView view, bool free)
    {
        AddChild(Text(ui.Text(SimText.StonesText(view))));
        AddChild(Duration(ui));
        foreach (var job in ui.Db.All<JobDef>().Where(j => j.Zone == view.Zone.ZoneId))
        {
            var line = Text(ui.Text(SimText.JobText(job)) + "\n" + ui.Text(SimText.TrainsText(job)));
            line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(Row(line, Button(ui.Text("str.ui.work_start"), () => ui.Send(new StartActivity(new WorkActivity(job.Id), ui.Choices.ActivityWeeks)), free)));
        }
    }

    /// <summary>Meditation in a direction (comprehension also names its subject).</summary>
    private void ShowPlace(UiContext ui, PlaceView place, bool free)
    {
        var choices = ui.Choices;
        AddChild(Text(ui.Text(SimText.PlaceText(place))));

        var directions = ui.Db.All<MeditationDirectionDef>().Select(d => (d.Id, ui.Name(d.Id))).ToList();
        choices.Direction = Pick(directions, choices.Direction);
        AddChild(Row(new Label { Text = ui.Text("str.ui.direction") }, Options(directions, choices.Direction, id => { choices.Direction = id; Rebuild(); })));

        string? subject = null;
        if (choices.Direction is { } directionId && ui.Db.Get<MeditationDirectionDef>(directionId).Studies)
        {
            var subjects = StudySubjects(ui).ToList();
            subject = choices.Subject = Pick(subjects, choices.Subject);
            AddChild(Row(new Label { Text = ui.Text("str.ui.subject") }, Options(subjects, subject, id => choices.Subject = id)));
        }
        AddChild(Duration(ui));
        AddChild(Button(ui.Text("str.ui.meditate"), () =>
            ui.Send(new StartActivity(new MeditateActivity(choices.Direction!, place.PlaceId, subject is null ? null : choices.Subject), choices.ActivityWeeks)), free));
    }

    /// <summary>Carried folios first, then every element, which can be sensed without a folio (§6).</summary>
    private IEnumerable<(string Id, string Text)> StudySubjects(UiContext ui) =>
        _view!.Items.Where(i => ui.Db.Get<ItemDef>(i.ItemId).Folio is not null).Select(i => (i.ItemId, ui.Name(i.ItemId)))
            .Concat(ui.Db.All<KnowledgeDef>().Where(k => k.IsElement).Select(k => (k.Id, ui.Name(k.Id))));

    private void ShowShop(UiContext ui, PlayerView view, string shopId, bool free)
    {
        AddChild(Text(ui.Text(SimText.StonesText(view))));
        foreach (var offer in view.ShopOffers.Where(o => o.ShopId == shopId))
        {
            var line = Text(ui.Text(SimText.OfferText(offer)));
            line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(Row(line, Button(ui.Text("str.ui.buy"), () => ui.Send(new Buy(offer.ItemId)), free)));
        }
    }
}
