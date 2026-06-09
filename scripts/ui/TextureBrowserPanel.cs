#nullable enable

using System.Collections.Generic;
using Godot;

// TODO: Will be overhauled at some point, likely.
public partial class TextureBrowserPanel : PanelContainer
{
    private readonly TextureBrowserState _state = new();
    private readonly Dictionary<string, Button> _buttonsByAssetId = [];
    private readonly HashSet<string> _pendingPreviewIds = [];
    private EditorSession? _session;
    private TextureRect _activePreview = null!;
    private Label _activePath = null!;
    private Label _rootPath = null!;
    private Label _status = null!;
    private LineEdit _search = null!;
    private HFlowContainer _grid = null!;
    private FileDialog _folderDialog = null!;
    private int _visibleAssetCount;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _activePreview = GetNode<TextureRect>("Margin/Column/Active/Preview");
        _activePath = GetNode<Label>("Margin/Column/Active/Details/Path");
        _rootPath = GetNode<Label>("Margin/Column/RootPath");
        _status = GetNode<Label>("Margin/Column/Status");
        _search = GetNode<LineEdit>("Margin/Column/Search");
        _grid = GetNode<HFlowContainer>("Margin/Column/Scroll/Grid");
        _folderDialog = GetNode<FileDialog>("FolderDialog");

        GetNode<Button>("Margin/Column/Actions/ChooseFolder").Pressed += OpenFolderDialog;
        GetNode<Button>("Margin/Column/Actions/ClearFolder").Pressed += ClearFolder;
        GetNode<Button>("Margin/Column/Actions/Refresh").Pressed += RefreshCatalog;
        _search.TextChanged += OnSearchChanged;
        _folderDialog.DirSelected += OnFolderSelected;

        RebuildGrid();
    }

    public override void _Process(double delta)
    {
        UpdatePreviews();
    }

    private void OpenFolderDialog()
    {
        if (_session?.TextureRootSettings.RootPath is string rootPath)
            _folderDialog.CurrentDir = rootPath;
        _folderDialog.PopupCenteredRatio(0.75f);
    }

    private void OnFolderSelected(string directory)
    {
        if (_session == null || !_session.TrySetTextureRoot(directory))
        {
            _status.Text = "The selected texture folder is unavailable.";
            return;
        }

        RebuildGrid();
    }

    private void ClearFolder()
    {
        _session?.ClearTextureRoot();
        RebuildGrid();
    }

    private void RefreshCatalog()
    {
        _session?.RefreshTextureCatalog();
        RebuildGrid();
    }

    private void OnSearchChanged(string searchText)
    {
        _state.SetSearchText(searchText);
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        foreach (Node child in _grid.GetChildren())
            child.QueueFree();
        _buttonsByAssetId.Clear();
        _pendingPreviewIds.Clear();

        if (_session == null)
        {
            UpdateSummary();
            return;
        }

        IReadOnlyList<TextureAsset> visibleAssets = _state.Filter(_session.TextureCatalog.Assets);
        _visibleAssetCount = visibleAssets.Count;
        foreach (TextureAsset asset in visibleAssets)
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(116, 116),
                ExpandIcon = true,
                Text = "Loading...",
                TooltipText = asset.AssetId,
                ToggleMode = true,
                ButtonPressed = asset.AssetId == _session.TextureCatalog.ActiveAssetId,
            };
            button.Pressed += () => SelectAsset(asset.AssetId);
            _grid.AddChild(button);
            _buttonsByAssetId.Add(asset.AssetId, button);
            _pendingPreviewIds.Add(asset.AssetId);
        }

        UpdatePreviews();
        UpdateSummary();
    }

    private void SelectAsset(string assetId)
    {
        if (_session?.TextureCatalog.TrySetActiveAsset(assetId) != true)
            return;

        foreach ((string id, Button button) in _buttonsByAssetId)
            button.ButtonPressed = id == assetId;
        UpdateActiveTexture();
    }

    private void UpdatePreviews()
    {
        if (_session == null)
            return;

        if (_pendingPreviewIds.Count > 0)
        {
            List<string> resolved = [];
            foreach (string assetId in _pendingPreviewIds)
            {
                Button button = _buttonsByAssetId[assetId];
                if (!_session.TextureCatalog.TryGetPreview(assetId, out var preview))
                    continue;

                button.Icon = preview.Resource;
                button.Text = preview.State switch
                {
                    QueuedResourceState.Pending => "Loading...",
                    QueuedResourceState.Failed => "Failed",
                    _ => "",
                };
                if (preview.State != QueuedResourceState.Pending)
                    resolved.Add(assetId);
            }
            foreach (string assetId in resolved)
            {
                _pendingPreviewIds.Remove(assetId);
            }
        }

        UpdateActiveTexture();
    }

    private void UpdateActiveTexture()
    {
        string? activeId = _session?.TextureCatalog.ActiveAssetId;
        _activePath.Text = activeId ?? "No active texture";
        _activePreview.Texture = null;
        if (activeId != null && _session!.TextureCatalog.TryGetPreview(activeId, out var preview))
        {
            _activePreview.Texture = preview.Resource;
        }
    }

    private void UpdateSummary()
    {
        string? root = _session?.TextureRootSettings.RootPath;
        _rootPath.Text = root ?? "Texture folder: Not configured";
        if (root != null)
            _rootPath.Text = $"Texture folder: {root}";

        IReadOnlyList<TextureAsset> assets = _session?.TextureCatalog.Assets ?? [];
        int errors = _session?.TextureCatalog.Errors.Count ?? 0;
        _status.Text = _state.GetStatus(root, assets.Count, _visibleAssetCount, errors);
    }
}
