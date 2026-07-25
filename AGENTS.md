# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity 6 project (`6000.3.10f1`). Runtime and editor code lives under `Assets/001Scripts` and `Assets/Editor`. Player logic is split into `Assets/001Scripts/Player`, enemy and flow-field logic into `Assets/001Scripts/Enemy`. Scenes are in `Assets/Scenes` and `Assets/Prefab/Scenes`. Prefabs, generated meshes, and materials are stored under `Assets/Prefab`, `Assets/GeneratedProps`, `Assets/DesertArena_Meshes`, and related asset folders. Unity configuration stays in `Packages/` and `ProjectSettings/`.

## Build, Test, and Development Commands
Open the project with Unity Hub using editor `6000.3.10f1`. Useful local commands:

```powershell
dotnet build Gigachad.sln
```

Builds the generated C# solution for quick script validation outside the editor.

```powershell
git status
```

Checks pending scene, prefab, and script changes before committing.

For formal builds and test runs, prefer Unity batch mode from a local Unity install, for example:

```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -quit
```

## Coding Style & Naming Conventions
Follow existing C# conventions: 4-space indentation, one public class per file, `PascalCase` for types, methods, and public fields, `camelCase` for private fields and locals. Keep MonoBehaviour filenames aligned with the class name. Place editor-only utilities in `Assets/Editor`. Avoid mixing unrelated gameplay code into scene setup scripts.

## Testing Guidelines
`com.unity.test-framework` is installed, but no committed `Tests` folders were found. Add new tests under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode` with clear names such as `EnemySpawnTests.cs`. Cover core gameplay logic that can regress easily: player health, spawning, enemy damage, and flow-field movement. Run Edit Mode tests in batch mode before opening a PR.

## Commit & Pull Request Guidelines
Recent commits use short, task-focused subjects, often in Vietnamese, for example `Tạo playerstats, thêm hitbox...` or `UpdateMovementPlayer`. Keep commit messages imperative and specific to the gameplay or content change. For pull requests, include:

- a short summary of the gameplay/content impact
- linked issue or task ID if one exists
- screenshots or short clips for scene, animation, or UI changes
- notes on required scene/prefab setup or test coverage

## Asset & Scene Hygiene
Do not delete or rename Unity `.meta` files. Keep generated folders such as `Library/`, `Temp/`, and `Logs/` out of commits unless explicitly needed. When editing scenes or prefabs, isolate unrelated changes before committing to reduce merge conflicts.
