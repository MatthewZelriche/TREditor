using System;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class EditorToolRegistryTests
{
    private const string FirstId = "test.first";
    private const string SecondId = "test.second";

    [TestCase]
    [RequireGodotRuntime]
    public void NothingIsActiveUntilAToolIsActivated()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        var tool = new RecordingTool(FirstId);
        registry.Register(tool);

        AssertThat(registry.ActiveTool).IsNull();

        registry.MouseDown(default);
        registry.MouseDrag(default);
        registry.MouseUp(default);

        AssertThat(string.Join(",", tool.Calls)).IsEqual(string.Empty);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RegistrationRejectsDuplicateAndEmptyIdentifiers()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        registry.Register(new RecordingTool(FirstId));

        AssertThrown(() => registry.Register(new RecordingTool(FirstId)))
            .IsInstanceOf<ArgumentException>();
        AssertThrown(() => registry.Register(new RecordingTool(null!)))
            .IsInstanceOf<ArgumentException>();
        AssertThrown(() => registry.Register(new RecordingTool("  ")))
            .IsInstanceOf<ArgumentException>();
        AssertThrown(() => registry.Register(null!)).IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ActivatingAnUnknownToolFails()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);

        AssertThrown(() => registry.Activate(FirstId)).IsInstanceOf<ArgumentException>();
        AssertThrown(() => registry.Activate(null!)).IsInstanceOf<ArgumentException>();
        AssertThat(registry.ActiveTool).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TheActiveToolReceivesEveryMousePhaseWithTheContext()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        var tool = new RecordingTool(FirstId);
        registry.Register(tool);
        registry.Activate(FirstId);
        var input = new EditorMouseInput(null!, new Vector2(12.0f, 34.0f));

        registry.MouseDown(input);
        registry.MouseDrag(input);
        registry.MouseUp(input);

        AssertThat(registry.ActiveTool).IsSame(tool);
        AssertThat(string.Join(",", tool.Calls)).IsEqual("down,drag,up");
        AssertThat(tool.LastInput.ScreenPosition).IsEqual(new Vector2(12.0f, 34.0f));
        AssertThat(tool.Contexts.Count).IsEqual(3);
        foreach (IEditorToolContext context in tool.Contexts)
            AssertThat(context).IsSame(application.ToolContext);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SwitchingToolsCancelsThePreviousOneOnlyOnChange()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        var first = new RecordingTool(FirstId);
        var second = new RecordingTool(SecondId);
        registry.Register(first);
        registry.Register(second);

        registry.Activate(FirstId);
        registry.Activate(FirstId);
        AssertThat(string.Join(",", first.Calls)).IsEqual(string.Empty);

        registry.Activate(SecondId);
        AssertThat(registry.ActiveTool).IsSame(second);
        AssertThat(string.Join(",", first.Calls)).IsEqual("cancel");
        AssertThat(string.Join(",", second.Calls)).IsEqual(string.Empty);

        registry.MouseDown(default);
        AssertThat(string.Join(",", first.Calls)).IsEqual("cancel");
        AssertThat(string.Join(",", second.Calls)).IsEqual("down");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CancelAndDeactivateReachTheActiveTool()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        var tool = new RecordingTool(FirstId);
        registry.Register(tool);

        registry.CancelActiveTool();
        registry.Deactivate();
        AssertThat(string.Join(",", tool.Calls)).IsEqual(string.Empty);

        registry.Activate(FirstId);
        registry.CancelActiveTool();
        registry.Deactivate();

        AssertThat(registry.ActiveTool).IsNull();
        AssertThat(string.Join(",", tool.Calls)).IsEqual("cancel,cancel");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeactivateReturnsToTheDefaultTool()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = application.Tools;
        var other = new RecordingTool("test.other");
        registry.Register(other);
        registry.Activate(other.Id);

        registry.Deactivate();

        AssertThat(registry.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
        AssertThat(string.Join(",", other.Calls)).IsEqual("cancel");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AFailedCancelStillCompletesTheSwitch()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var registry = new EditorToolRegistry(application.ToolContext);
        var failing = new RecordingTool(FirstId) { FailOnCancel = true };
        var second = new RecordingTool(SecondId);
        registry.Register(failing);
        registry.Register(second);
        registry.Activate(FirstId);

        AssertThrown(() => registry.Activate(SecondId)).IsInstanceOf<InvalidOperationException>();

        AssertThat(registry.ActiveTool).IsSame(second);
        registry.MouseDown(default);
        AssertThat(string.Join(",", second.Calls)).IsEqual("down");
    }

    private sealed class RecordingTool(string id) : IEditorTool
    {
        public string Id => id;

        public List<string> Calls { get; } = [];

        public List<IEditorToolContext> Contexts { get; } = [];

        public EditorMouseInput LastInput { get; private set; }

        public bool FailOnCancel { get; init; }

        public void OnMouseDown(IEditorToolContext context, EditorMouseInput input) =>
            Record("down", context, input);

        public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input) =>
            Record("drag", context, input);

        public void OnMouseUp(IEditorToolContext context, EditorMouseInput input) =>
            Record("up", context, input);

        public void Cancel(IEditorToolContext context)
        {
            Calls.Add("cancel");
            Contexts.Add(context);
            if (FailOnCancel)
                throw new InvalidOperationException("Cancel failed.");
        }

        private void Record(string call, IEditorToolContext context, EditorMouseInput input)
        {
            Calls.Add(call);
            Contexts.Add(context);
            LastInput = input;
        }
    }
}
