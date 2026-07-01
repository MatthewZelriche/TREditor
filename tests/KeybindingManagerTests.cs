using Godot;

public sealed class KeybindingManagerTests
{
    [Fact]
    public void Constructor_LoadsOverridesAndAppliesEveryAction()
    {
        FakeStore store = new(
            new Dictionary<string, string>
            {
                [KeybindingActions.CameraForward] = KeybindingCodec.Encode(
                    new KeyInputBinding(Key.Up)
                ),
                [KeybindingActions.ToolSelect] = KeybindingCodec.Unbound,
            }
        );
        FakeRuntime runtime = new();

        KeybindingManager manager = new(store, runtime);

        Assert.Equal(
            new KeyInputBinding(Key.Up),
            manager.GetBinding(KeybindingActions.CameraForward)
        );
        Assert.Null(manager.GetBinding(KeybindingActions.ToolSelect));
        Assert.Equal(KeybindingCatalog.All.Count, runtime.Applied.Count);
    }

    [Fact]
    public void Constructor_InvalidOverrideFallsBackToDefaultAndWarns()
    {
        FakeStore store = new(
            new Dictionary<string, string> { [KeybindingActions.CameraForward] = "invalid" }
        );
        List<string> warnings = [];

        KeybindingManager manager = new(store, new FakeRuntime(), warnings.Add);

        Assert.Equal(
            KeybindingCatalog.Get(KeybindingActions.CameraForward).DefaultBinding,
            manager.GetBinding(KeybindingActions.CameraForward)
        );
        Assert.Single(warnings);
    }

    [Fact]
    public void Constructor_DuplicateOverrideLeavesLaterActionUnbound()
    {
        string duplicate = KeybindingCodec.Encode(
            KeybindingCatalog.Get(KeybindingActions.FileSave).DefaultBinding
        );
        FakeStore store = new(
            new Dictionary<string, string> { [KeybindingActions.ToolSelect] = duplicate }
        );
        List<string> warnings = [];

        KeybindingManager manager = new(store, new FakeRuntime(), warnings.Add);

        Assert.Null(manager.GetBinding(KeybindingActions.ToolSelect));
        Assert.Single(warnings);
    }

    [Fact]
    public void SetBinding_ReportsConflictWithoutChangingState()
    {
        FakeStore store = new();
        KeybindingManager manager = new(store, new FakeRuntime());
        InputBinding save = manager.GetBinding(KeybindingActions.FileSave)!;

        KeybindingChangeResult result = manager.SetBinding(KeybindingActions.ToolSelect, save);

        Assert.Equal(KeybindingChangeStatus.Conflict, result.Status);
        Assert.Equal(KeybindingActions.FileSave, result.ConflictingActionId);
        Assert.Null(manager.GetBinding(KeybindingActions.ToolSelect));
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void SetBinding_NormalizesKeypadEnterBeforeConflictDetection()
    {
        KeybindingManager manager = new(new FakeStore(), new FakeRuntime());

        KeybindingChangeResult result = manager.SetBinding(
            KeybindingActions.ToolSelect,
            new KeyInputBinding(Key.KpEnter)
        );

        Assert.Equal(KeybindingChangeStatus.Conflict, result.Status);
        Assert.Equal(KeybindingActions.Confirm, result.ConflictingActionId);
    }

    [Fact]
    public void SetBinding_ConfirmedConflictMovesBindingAtomically()
    {
        FakeStore store = new();
        FakeRuntime runtime = new();
        KeybindingManager manager = new(store, runtime);
        List<string> changed = [];
        manager.BindingChanged += changed.Add;
        InputBinding save = manager.GetBinding(KeybindingActions.FileSave)!;

        KeybindingChangeResult result = manager.SetBinding(
            KeybindingActions.ToolSelect,
            save,
            replaceConflict: true
        );

        Assert.True(result.WasApplied);
        Assert.Null(manager.GetBinding(KeybindingActions.FileSave));
        Assert.Equal(save, manager.GetBinding(KeybindingActions.ToolSelect));
        Assert.Equal(KeybindingCodec.Unbound, store.Saved[KeybindingActions.FileSave]);
        Assert.Equal(KeybindingCodec.Encode(save), store.Saved[KeybindingActions.ToolSelect]);
        Assert.Equal([KeybindingActions.FileSave, KeybindingActions.ToolSelect], changed);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void SetBinding_SaveFailureRollsBackRuntimeAndState()
    {
        FakeStore store = new() { FailSave = true };
        FakeRuntime runtime = new();
        KeybindingManager manager = new(store, runtime);
        runtime.Applied.Clear();
        InputBinding original = manager.GetBinding(KeybindingActions.CameraForward)!;

        KeybindingChangeResult result = manager.SetBinding(
            KeybindingActions.CameraForward,
            new KeyInputBinding(Key.Up)
        );

        Assert.Equal(KeybindingChangeStatus.PersistenceFailed, result.Status);
        Assert.Equal(original, manager.GetBinding(KeybindingActions.CameraForward));
        Assert.Empty(runtime.Applied);
    }

    [Fact]
    public void ClearResetAndResetAll_UpdateOverridesAndRuntime()
    {
        FakeStore store = new();
        KeybindingManager manager = new(store, new FakeRuntime());

        Assert.True(manager.SetBinding(KeybindingActions.CameraForward, null).WasApplied);
        Assert.Null(manager.GetBinding(KeybindingActions.CameraForward));
        Assert.Equal(KeybindingCodec.Unbound, store.Saved[KeybindingActions.CameraForward]);

        Assert.True(manager.ResetBinding(KeybindingActions.CameraForward).WasApplied);
        Assert.Equal(
            KeybindingCatalog.Get(KeybindingActions.CameraForward).DefaultBinding,
            manager.GetBinding(KeybindingActions.CameraForward)
        );
        Assert.DoesNotContain(KeybindingActions.CameraForward, store.Saved.Keys);

        Assert.True(
            manager
                .SetBinding(KeybindingActions.ToolSelect, new KeyInputBinding(Key.Key1))
                .WasApplied
        );
        Assert.True(manager.ResetAll().WasApplied);
        Assert.Null(manager.GetBinding(KeybindingActions.ToolSelect));
        Assert.Empty(store.Saved);
    }

    private sealed class FakeStore : IKeybindingStore
    {
        private readonly IReadOnlyDictionary<string, string> _loaded;

        public FakeStore(IReadOnlyDictionary<string, string>? loaded = null)
        {
            _loaded = loaded ?? new Dictionary<string, string>();
        }

        public bool FailSave { get; set; }
        public int SaveCount { get; private set; }
        public Dictionary<string, string> Saved { get; private set; } = [];

        public IReadOnlyDictionary<string, string> Load(out string? warning)
        {
            warning = null;
            return _loaded;
        }

        public bool TrySave(IReadOnlyDictionary<string, string> overrides, out string? error)
        {
            SaveCount++;
            if (FailSave)
            {
                error = "save failed";
                return false;
            }

            Saved = new Dictionary<string, string>(overrides);
            error = null;
            return true;
        }
    }

    private sealed class FakeRuntime : IKeybindingRuntime
    {
        public List<(string ActionId, InputBinding? Binding)> Applied { get; } = [];

        public void Apply(string actionId, InputBinding? binding) =>
            Applied.Add((actionId, binding));
    }
}
