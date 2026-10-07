# Building QuickSpraySelector

Use Windows PowerShell 5 or later. The recommended build compiles against your installed game and BepInEx files; these dependencies are not redistributed with the source or release package.

## Required inputs

- Bomb Rush Cyberfunk's `Bomb Rush Cyberfunk_Data/Managed` folder.
- Your mod manager profile's `BepInEx/core` folder.
- Roslyn's `csc.exe`, for example the compiler in Microsoft.Net.Compilers.Toolset's `tasks/net472` folder. Obtain it from Microsoft's NuGet package or your development tools.

## Build

From the repository root:

```powershell
./build.ps1 -GameManagedPath "GAME_MANAGED_FOLDER" -BepInExCorePath "BEPINEX_CORE_FOLDER" -CompilerPath "CSC_EXE_PATH"
```

Replace the placeholders with your own local paths. The result is `output/QuickSpraySelector-1.0.0.zip`, with the compiled plugin at `Thunderstore/plugins/QuickSpraySelector/QuickSpraySelector.dll`.

The ZIP can be uploaded to Thunderstore or attached to a GitHub release. The game DLLs, BepInEx libraries and compiler stay on your machine.

## Verification

After building:

```powershell
./tests/verify.ps1 -CompilerPath "CSC_EXE_PATH"
./tests/verify-native.ps1 -CompilerPath "CSC_EXE_PATH" -GameManagedPath "GAME_MANAGED_FOLDER" -BepInExCorePath "BEPINEX_CORE_FOLDER"
```

The first command checks navigation, remembered choices and motion calculations. The second checks the installed game's native graffiti finishing flow and the packaged plugin's resources. They write separate test executables to `output`; they do not replace the plugin DLL.

## IDE project

`QuickSpraySelector.csproj` accepts MSBuild properties `GameManagedPath` and `BepInExCorePath`. A .NET Framework 4.6 reference pack is required; the project declares Microsoft's reference-assemblies package for SDK builds. The PowerShell build remains the tested release path.
