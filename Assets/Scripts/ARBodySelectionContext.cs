using System;

public static class ARBodySelectionContext
{
    public readonly struct SelectionSnapshot
    {
        public readonly bool HasSelection;
        public readonly string BodyName;
        public readonly string Title;
        public readonly string Description;

        public SelectionSnapshot(bool hasSelection, string bodyName, string title, string description)
        {
            HasSelection = hasSelection;
            BodyName = bodyName;
            Title = title;
            Description = description;
        }
    }

    private const string DefaultLoadingMessage = "Now Loading";
    private const string DefaultInstructionMessage = "Point your camera at the marker";

    private static string selectedBodyName;
    private static string selectedTitle;
    private static string selectedDescription;

    public static bool HasSelection => !string.IsNullOrWhiteSpace(selectedBodyName);

    public static void SetSelectedBody(CelestialBody body)
    {
        if (body == null)
        {
            Clear();
            return;
        }

        SetSelectedBody(body.bodyName, body.bodyName, body.profileDescription);
    }

    public static void SetSelectedBody(string bodyName, string title = null, string description = null)
    {
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            Clear();
            return;
        }

        selectedBodyName = NormalizeBodyKey(bodyName);
        selectedTitle = string.IsNullOrWhiteSpace(title) ? FormatDisplayName(bodyName) : title.Trim();
        selectedDescription = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    }

    public static SelectionSnapshot Capture(bool clearAfterCapture)
    {
        SelectionSnapshot snapshot = new SelectionSnapshot(
            HasSelection,
            selectedBodyName,
            string.IsNullOrWhiteSpace(selectedTitle) ? FormatDisplayName(selectedBodyName) : selectedTitle,
            selectedDescription);

        if (clearAfterCapture)
        {
            Clear();
        }

        return snapshot;
    }

    public static string GetLoadingMessage()
    {
        return HasSelection
            ? $"Preparing {GetDisplayTitle()} AR view"
            : DefaultLoadingMessage;
    }

    public static string GetInstructionMessage()
    {
        return HasSelection
            ? $"Point your camera at the {GetDisplayTitle()} marker"
            : DefaultInstructionMessage;
    }

    public static void Clear()
    {
        selectedBodyName = null;
        selectedTitle = null;
        selectedDescription = null;
    }

    public static string NormalizeBodyKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('_', ' ').ToLowerInvariant();
    }

    public static string FormatDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts = value.Trim().Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            parts[i] = part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
        }

        return string.Join(" ", parts);
    }

    private static string GetDisplayTitle()
    {
        return string.IsNullOrWhiteSpace(selectedTitle)
            ? FormatDisplayName(selectedBodyName)
            : selectedTitle;
    }
}
