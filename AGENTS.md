# Repository Guidelines

## Project Structure & Module Organization

This repository is currently in the planning stage. `README.md` describes the proposed Unity AR sticker hunt and links to the Figma design. `docs/handwritten-notes/` contains photographed planning notes. There is no Unity project, application source, test suite, or asset pipeline yet. When adding the Unity project, keep its standard `Assets/`, `Packages/`, and `ProjectSettings/` directories together at the repository root, and document any added services or smart contracts in the README.

## Build, Test, and Development Commands

No build, run, formatting, or test command is configured yet. Review the project brief with `cat README.md` and inspect tracked files with `git ls-files`. Once executable code is added, document the exact Unity Editor version, platform setup, and reproducible build and test commands here and in the README. Do not assume a command works until its required project files are committed.

## Coding Style & Naming Conventions

Use descriptive names that match the app's concepts: stickers, locations, collections, and discovery. For future Unity C# code, use four-space indentation, `PascalCase` for types and public members, and `camelCase` for local variables and private fields unless the project's adopted formatter specifies otherwise. Keep scenes, scripts, and art assets in clearly named folders under `Assets/`. Keep documentation headings short and links current.

## Testing Guidelines

There is no test framework or coverage target in this repository. Add Unity Test Framework tests when gameplay or location logic is introduced, placing them in clearly named Edit Mode or Play Mode test assemblies. Name tests for the behavior they verify, and include the test command and results in the pull request. For AR features that require a device, record the device and manual verification steps.

## Commit & Pull Request Guidelines

Recent commits include `feat: update README` and `docs: add figma link`, alongside earlier short descriptive messages. Prefer a concise `type: summary` subject, such as `feat: add sticker placement`, using `docs`, `fix`, or `test` where appropriate. Pull requests should explain the change, link the relevant issue or design, list verification performed, and include screenshots or a short capture for visible Unity or AR changes. Call out required configuration without committing secrets.
