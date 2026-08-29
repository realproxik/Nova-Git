# NovaGit Desktop

A native Windows desktop interface for real Git repositories, built with C# and Windows Forms.

## Run

Install [Git for Windows](https://git-scm.com/download/win), then run:

```powershell
dotnet run
```

The desktop app lets you open or initialize repositories, inspect status and diffs, stage/unstage changes, commit, create/switch branches, and browse the commit log. Its **Object Explorer** reads every reachable and packed Git object, with filters for `commit`, `tree`, `blob`, and annotated `tag`, including delta-base metadata for packed deltas.

Use **Remote** → **Connect GitHub / remote** to add or update the `origin` URL. The same menu provides Fetch, Pull, and Push; first push automatically sets the branch upstream.

It calls `git` using structured process arguments (not a shell), so Git retains complete support for objects, packs, deltas, remotes, merges, tags, hooks, and other Git features.

The **Blog** tab writes Markdown drafts to `blog/` in the selected repository, ready to stage and commit. The **Account** button supports GitHub's OAuth device flow with the `read:user` scope; create your own GitHub OAuth App and enter its public client ID when prompted. GitHub handles credentials in its browser page; NovaGit never requests passwords or personal access tokens. Tokens remain in memory only.

## GitHub automation

GitHub Actions builds NovaGit on Windows for pushes and pull requests, checks vulnerable NuGet dependencies, and uploads a Windows artifact. Pushing a tag such as `v1.0.0` creates a standalone Windows release ZIP automatically. Dependabot checks NuGet packages and workflow actions weekly.

If the desktop app encounters an unhandled error, it records the details at `%LocalAppData%\NovaGit\logs\crash.log`.

## Windows installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php), then build the installer:

```powershell
powershell -ExecutionPolicy Bypass -File Installer\Build-Installer.ps1 -Version 1.0.0
```

The generated installer is placed in `artifacts\installer`. It shows installation rules and the MIT License, lets people create a desktop icon or add NovaGit to their user PATH, and installs the complete published application.

## Crypto CLI

The previous security utilities remain available by supplying a command:

```powershell
dotnet run -- hash-file README.md --sha256
dotnet run -- password-hash "example-password"
```
