using System.Collections.Generic;
using System.Text;
using Godot;

public partial class LicensesDialog : Window
{
    private ItemList _entryList;
    private RichTextLabel _licenseText;

    private IReadOnlyList<ThirdPartyLicenseEntry> _entries = [];

    public override void _Ready()
    {
        Title = "Third-Party Licenses";
        Size = new Vector2I(900, 600);
        MinSize = new Vector2I(640, 420);
        Unresizable = false;
        CloseRequested += Hide;

        _entryList = GetNode<ItemList>("Margin/Content/EntryList");
        _licenseText = GetNode<RichTextLabel>("Margin/Content/Details/Scroll/LicenseText");

        _entryList.ItemSelected += OnEntrySelected;

        try
        {
            ThirdPartyLicenseReport report = ThirdPartyLicenseCatalog.Load();
            _entries = report.Entries;
            PopulateEntryList();
            if (_entries.Count > 0)
            {
                _entryList.Select(0);
                OnEntrySelected(0);
            }
        }
        catch (System.Exception exception)
        {
            _licenseText.Text = exception.Message;
            GD.PushError(exception.Message);
        }
    }

    private void PopulateEntryList()
    {
        string currentCategory = null;
        foreach (ThirdPartyLicenseEntry entry in _entries)
        {
            if (entry.Category != currentCategory)
            {
                currentCategory = entry.Category;
                _entryList.AddItem($"— {currentCategory} —");
                int categoryIndex = _entryList.ItemCount - 1;
                _entryList.SetItemDisabled(categoryIndex, true);
            }

            _entryList.AddItem(entry.DisplayName);
        }
    }

    private void OnEntrySelected(long index)
    {
        ThirdPartyLicenseEntry entry = GetEntryForListIndex((int)index);
        if (entry == null)
            return;

        _licenseText.Text = BuildEntryText(entry);
    }

    private ThirdPartyLicenseEntry GetEntryForListIndex(int listIndex)
    {
        int entryIndex = -1;
        for (int index = 0; index <= listIndex; index++)
        {
            if (!_entryList.IsItemDisabled(index))
                entryIndex++;
        }

        if (entryIndex < 0 || entryIndex >= _entries.Count)
            return null;

        return _entries[entryIndex];
    }

    internal static string BuildEntryText(ThirdPartyLicenseEntry entry)
    {
        StringBuilder builder = new();
        builder.AppendLine(entry.DisplayName);
        builder.AppendLine(new string('=', entry.DisplayName.Length));

        if (!string.IsNullOrWhiteSpace(entry.License))
            builder.AppendLine($"License: {entry.License}");

        if (!string.IsNullOrWhiteSpace(entry.SourcePath))
            builder.AppendLine($"Source: {entry.SourcePath}");

        if (!string.IsNullOrWhiteSpace(entry.LicenseFile))
            builder.AppendLine($"License file: {entry.LicenseFile}");

        if (!string.IsNullOrWhiteSpace(entry.SourceUrl))
            builder.AppendLine($"More info: {entry.SourceUrl}");

        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(entry.LicenseText))
            builder.Append(entry.LicenseText);
        else if (!string.IsNullOrWhiteSpace(entry.SourceUrl))
            builder.Append("Full license text is available at the URL above.");
        else
            builder.Append("No license text available.");

        return builder.ToString();
    }
}
