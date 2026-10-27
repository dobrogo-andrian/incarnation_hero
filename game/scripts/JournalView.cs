using Godot;

/// <summary>The event log under the centre panel; keeps the latest lines and follows new ones.</summary>
public partial class JournalView : RichTextLabel
{
    private const int MaxLines = 400;

    public JournalView()
    {
        ScrollFollowing = true;
        SelectionEnabled = true;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    public void Add(string line)
    {
        if (GetParagraphCount() > MaxLines)
            RemoveParagraph(0);
        AddText(line + "\n");
    }
}
