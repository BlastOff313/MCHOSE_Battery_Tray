# Installer and release workflow

The repository contains the Inno Setup script and a GitHub Actions workflow. The compiled installer is a Windows build artifact; this source archive does not contain a fabricated or untested `Setup.exe`.

## Local build (Windows x64)

Install the .NET 10 SDK and Inno Setup 6, then run:

```powershell
.\build.ps1
```

Expected output:

`dist\MCHOSE-Battery-Tray-Setup-10.0.1.exe`

## GitHub build

1. Push the repository to GitHub.
2. Open **Actions → Build Windows installer → Run workflow** for a test build.
3. Download the `MCHOSE-Battery-Tray-Setup` artifact from the workflow run.
4. For a public release, push a version tag such as `v10.0.1`. The same workflow builds the installer and attaches it to a GitHub Release.

The installer uses a per-user install (`%LOCALAPPDATA%\Programs`) and registers an uninstall entry under the current user's installed-apps registry. It does not require elevation. App settings and logs are intentionally preserved when the program is uninstalled.

The installer is not digitally signed with an Authenticode certificate. SmartScreen reputation warnings may appear for a newly published build.
