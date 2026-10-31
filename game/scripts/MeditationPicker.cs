using Core.Content;
using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// What "meditate" offers once the body and qi are whole (D73): every direction but recovery, each
/// started at the zone's best place it is allowed at and run until interrupted; comprehension names
/// its subject, a carried folio or an element. A direction with no allowed place here (open ground of
/// a dangerous zone, no shelter known) is shown disabled with the reason.
/// </summary>
public partial class MeditationPicker : PopupPanel
{
    private readonly VBoxContainer _content = new() { CustomMinimumSize = new Vector2(380, 0) };

    public MeditationPicker()
    {
        _content.AddThemeConstantOverride("separation", 6);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.2f, 0.22f),
            BorderColor = new Color(0.45f, 0.45f, 0.5f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        AddChild(_content);
    }

    public void Open(UiContext ui, PlayerView view, Control under)
    {
        foreach (var child in _content.GetChildren())
            child.QueueFree();
        _content.AddChild(new Label { Text = ui.Text("str.ui.meditate_pick") });
        _content.AddChild(new Label
        {
            Text = ui.Text("str.ui.meditate_pick_hint"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
        });

        string recovery = ui.Db.Get<RunConfigDef>(RunConfigDef.RunId).RecoveryMeditation;
        foreach (var direction in ui.Db.All<MeditationDirectionDef>().Where(d => d.Id != recovery))
        {
            string? place = MeditationSystem.BestPlace(view, ui.Db, direction.Id);
            var subjects = direction.Studies ? PointPanel.StudySubjects(ui, view).ToList() : [(Id: (string?)null, Text: "")];
            foreach (var (subject, subjectText) in subjects)
            {
                string text = subject is null ? ui.Name(direction.Id) : $"{ui.Name(direction.Id)}: {subjectText}";
                var button = Widgets.Button(text, () => Start(ui, direction.Id, place!, subject), place is not null);
                button.Alignment = HorizontalAlignment.Left;
                if (place is null)
                    button.TooltipText = ui.Text("str.ui.meditate_no_place");
                _content.AddChild(button);
            }
        }

        ResetSize();
        Position = (Vector2I)(under.GlobalPosition + new Vector2(0, under.Size.Y + 4));
        Popup();
    }

    private void Start(UiContext ui, string direction, string place, string? subject)
    {
        Hide();
        ui.Send(new StartActivity(new MeditateActivity(direction, place, subject), null));
    }
}
