using Godot;

/// <summary>The event log at the bottom of the screen; keeps the latest lines and follows new ones.</summary>
public partial class JournalView : RichTextLabel
{
    private const int MaxLines = 400;

    public JournalView()
    {
        ScrollFollowing = true;
        SelectionEnabled = true;
        CustomMinimumSize = new Vector2(0, 170);
    }

    public void Add(string line)
    {
        if (GetParagraphCount() > MaxLines)
            RemoveParagraph(0);
        AddText(line + "\n");
    }
}
