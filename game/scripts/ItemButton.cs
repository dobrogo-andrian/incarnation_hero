using Godot;

/// <summary>
/// A button of the character window that items are dragged from or dropped on: an inventory card
/// carries <see cref="DragId"/> (an item ID), a slot or the figure takes what <see cref="Accepts"/>
/// allows and hands it to <see cref="Dropped"/>. Dropping only picks the command; Core still checks it.
/// </summary>
public partial class ItemButton : Button
{
    public string? DragId { get; set; }

    public Func<string, bool> Accepts { get; set; } = _ => false;

    public Action<string> Dropped { get; set; } = _ => { };

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (DragId is null || Disabled)
            return default;
        SetDragPreview(new Label { Text = Text });
        return DragId;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && Accepts(data.AsString());

    public override void _DropData(Vector2 atPosition, Variant data) => Dropped(data.AsString());
}
