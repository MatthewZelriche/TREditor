using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class FileMenuTests
{
    private const string ScenePath = "user://file_menu_save.res";

    [TestCase]
    [RequireGodotRuntime]
    public void FileMenuDispatchesSceneAndDialogActions()
    {
        RemoveSceneFile();
        try
        {
            PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
            Root root = AutoFree(rootScene.Instantiate<Root>());
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

            Application application = root.CurrentApplication!;
            AssertThat(application).IsNotNull();
            AppMenuBar menuBar = root.GetNode<AppMenuBar>("UI/AppMenuBar");
            PopupMenu fileMenu = menuBar.GetNode<PopupMenu>("%FileMenu");
            FileDialog openDialog = root.GetNode<FileDialog>("%OpenSceneDialog");
            FileDialog saveDialog = root.GetNode<FileDialog>("%SaveSceneDialog");
            openDialog.UseNativeDialog = false;
            saveDialog.UseNativeDialog = false;

            Scene initialScene = application.CurrentScene!;
            EmitFileAction(fileMenu, AppMenuBar.AppMenuAction.NewDocument);
            AssertBool(ReferenceEquals(initialScene, application.CurrentScene)).IsFalse();

            EmitFileAction(fileMenu, AppMenuBar.AppMenuAction.OpenDocument);
            AssertBool(openDialog.Visible).IsTrue();
            openDialog.Hide();

            EmitFileAction(fileMenu, AppMenuBar.AppMenuAction.SaveDocument);
            AssertBool(saveDialog.Visible).IsTrue();
            saveDialog.Hide();

            EmitFileAction(fileMenu, AppMenuBar.AppMenuAction.SaveDocumentAs);
            AssertBool(saveDialog.Visible).IsTrue();
            saveDialog.Hide();

            string scenePath = ProjectSettings.GlobalizePath(ScenePath);
            application.SaveSceneAs(scenePath);
            DirAccess.RemoveAbsolute(scenePath);
            EmitFileAction(fileMenu, AppMenuBar.AppMenuAction.SaveDocument);
            AssertBool(Godot.FileAccess.FileExists(scenePath)).IsTrue();
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    private static void EmitFileAction(
        PopupMenu fileMenu,
        AppMenuBar.AppMenuAction action
    ) => fileMenu.EmitSignal(PopupMenu.SignalName.IdPressed, (long)action);

    private static void RemoveSceneFile()
    {
        string scenePath = ProjectSettings.GlobalizePath(ScenePath);
        if (Godot.FileAccess.FileExists(scenePath))
            DirAccess.RemoveAbsolute(scenePath);
    }
}
