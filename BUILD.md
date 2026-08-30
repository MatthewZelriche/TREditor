# Building TREditor

## One-time GdUnit4 setup

Run these commands from the repository root after cloning. The sparse checkout keeps GdUnit4's own test
suite out of the Godot project while retaining the complete editor addon.

Windows:

```
git submodule update --init -- .gdUnit4
git -C .gdUnit4 sparse-checkout set --no-cone "/*" "!/addons/gdUnit4/test/"
mklink /D addons\gdUnit4 ..\.gdUnit4\addons\gdUnit4
```

Linux and macOS:

```sh
git submodule update --init -- .gdUnit4
git -C .gdUnit4 sparse-checkout set --no-cone '/*' '!/addons/gdUnit4/test/'
ln -s ../.gdUnit4/addons/gdUnit4 addons/gdUnit4
```

The generated `addons/gdUnit4` link is ignored by Git. If a pull or branch switch changes the `.gdUnit4`
gitlink, update the local checkout with:

```sh
git submodule update --init -- .gdUnit4
```

When updating the pinned GdUnit4 version, keep the `gdUnit4.api` and `gdUnit4.test.adapter` package versions
in the editor project aligned with `.gdUnit4/gdUnit4.csproj`.

## Vendored dependencies

A fresh checkout must initialize the vendored source submodules:

```sh
git submodule update --init -- vendor/TRMesh vendor/Gizmo3D addons/TilingContainer
```

`vendor/Gizmo3D` is pinned to an exact upstream revision of
[Gizmo3D](https://github.com/chrisizeful/Gizmo3D). The `vendor` directory carries a `.gdignore`, so the
vendored project is not scanned by Godot, and `vendor/**` is excluded from the editor project's default
C# compilation. Update the pin deliberately with `git -C vendor/Gizmo3D checkout <revision>` followed by
committing the changed gitlink.

## Build and test

```powershell
dotnet build
dotnet test tests/TREditor.Tests/TREditor.Tests.csproj
```

GdUnit4 runtime tests require `GODOT_BIN` to point to the Godot console executable installed on the current
machine:

```powershell
$env:GODOT_BIN = "C:\path\to\Godot_console.exe"
dotnet test TREditor.csproj --settings .runsettings
```

On Linux and macOS, export the same variable before running the test command:

```sh
export GODOT_BIN=/path/to/godot
dotnet test TREditor.csproj --settings .runsettings
```
