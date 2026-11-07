<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue/main/docs/images/mue-banniere-sombre.png">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue/main/docs/images/mue-banniere-clair.png" alt="Mue — Self-updates for .NET desktop apps" width="640">
</picture>

# Mue

[Français](https://github.com/syl-craft/Mue/blob/main/README.fr.md) · **English**

Self-update for .NET desktop apps: check, download, close what blocks, install.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/01-update-flow-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/01-update-flow-light.gif" alt="Demo: the app finds a new version, the update dialog shows the notes, downloads it, then restarts on the new version" width="960">
</picture>

## Why Mue

Mue is the update engine of [DockPad](https://github.com/syl-craft/DockPad), extracted into a library once it had proven itself in production. It is now used by:

- DockPad (WPF)
- PicaLite (WPF)
- AlterVNC (Avalonia, Windows and macOS)

The core knows nothing about your UI or your update source. You plug in your own texts, log, schedule and theme. Nothing installs on its own: the user always starts the installation.

## Features

- **Velopack backend** for GitHub releases, pinned to Velopack 1.2.161.
- **Scheduled search**: first check 20 seconds after start, then permission and preference re-evaluated every 15 minutes, with at least six hours between two searches. Searches are cancelled when the app shuts down or automatic updates are turned off.
- **Download without install**: a download never applies the update by itself.
- **Version badge**: a standalone `UpdateBadge` control shows the current version and the new one (`v1.2.0 → 1.3.0`).
- **Update dialog** with release notes (Markdown), progress and state.
- **Blocking processes**: Mue lists the programs that use your app's files, lets the user pick which to close, and checks again before installing. Windows uses the Restart Manager; macOS needs 15 or later.
- **Shutdown behaviour** per backend: `Refuse`, `AskToQuit` or `Kill`. The Velopack backend defaults to `AskToQuit` on macOS and `Kill` elsewhere.
- **Pending marker**: `VerifyLaunch()` tells at the next start whether the update succeeded or rolled back.
- **English and French texts** built in, falling back key by key when your app overrides only some of them.
- **Style keys**: the dialogs use your app's brushes and button styles, with optional defaults for apps that have no theme.
- **`LatestReleaseOnly`**: ask GitHub for `/releases/latest` only, with no fallback to another release.

### New in 0.3.0

- **Explicit consent**: when the host announces what the installation does (`Summary`), the dialog always asks for confirmation before `PrepareAsync`.
- **Ignore programs** (`AskToQuit` and `Kill`): unchecking a group leaves it to the installer. **Install without closing them** gives that consent directly. A program that appeared since then still blocks.
- **Unreadable identities**: a matched executable whose identity cannot be read stays listed as `Unavailable` and the scan is marked `Incomplete`, with a warning in the dialog.
- **macOS detection** aligned with the Windows contract: several paths, optional current process, five close results, cancellation.
- **Generic texts**: `UpdateTexts.AppName` replaces `{AppName}` in the built-in texts.
- **Mue.Avalonia**: dialogs and badge for Avalonia 12.

### New in 0.4.0

- **Text keys**: `UpdateTextKeys.All` and `Missing(...)`; a language is only accepted complete.
- **WPF dark theme**: `MueTheme` (`System`, `Light`, `Dark`), `SetTheme`, dark title bar, high contrast respected. Nothing changes until the app calls Mue.
- **One list of style keys** shared by WPF and Avalonia; WCAG AA contrast checked by tests.
- **Confirmation help** when the installer may stop a program that appeared in the meantime.
- **Separate download and install**, as an option (`UpdatesDialogOptions.SeparateInstallStep`).
- **Sample app** `samples/Mue.Sample.Wpf`, with neutral captures.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/02-blockers-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/02-blockers-light.gif" alt="Demo: the dialog lists programs still open, the user leaves the preselected sessions to the installer, closes one program and installs" width="960">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/04-confirmation-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/04-confirmation-light.gif" alt="Demo: the host announces what the installation does, Esc postpones it, then the user confirms and the app restarts on the new version" width="960">
</picture>

## Packages

| Package | Targets | Content |
| --- | --- | --- |
| `Mue` | net8.0, net10.0 | Engine, states, contracts, blocker detection (Windows and macOS) |
| `Mue.Velopack` | net8.0, net10.0 | Velopack backend (exactly 1.2.161) |
| `Mue.Wpf` | net8.0-windows, net10.0-windows | WPF dialogs and `UpdateBadge` |
| `Mue.Avalonia` | net8.0, net10.0 | Avalonia 12.1.4 dialogs and `UpdateBadge` |

The core references neither WPF nor Velopack. Blocker detection is detailed below, in [Blocker detection](#blocker-detection).

## Quick start

```sh
dotnet add package Mue.Velopack
dotnet add package Mue.Wpf
```

Until Mue is published on nuget.org, add the local feed first: see [Local publishing](#local-publishing).

```csharp
using Mue;
using Mue.Velopack;

UpdateTexts.AppName = "My App";

var backend = new VelopackBackend(
    new VelopackBackendOptions("https://github.com/you/my-app") { FallbackVersion = "0.0.0" });

var service = new UpdateService(backend, new UpdateServiceOptions
{
    PendingStore = new FileUpdatePendingStore(pendingPath),
});

service.StartPolling();                        // scheduled search
var launch = service.VerifyLaunch();           // None, Succeeded or RolledBack

var dialog = new Mue.Wpf.UpdatesDialog(service, host);
dialog.ShowDialog();
```

`host` is your `IUpdateDialogHost`: it prepares your app before installation, exits it, shows errors, builds release links, and provides the blocker finder (`WindowsUpdateBlockerFinder` or `MacUpdateBlockerFinder`) and the classifier (`ProgramClassifier` by default). Call `service.Dispose()` at shutdown. On Avalonia: `new Mue.Avalonia.UpdatesDialog(service, host)`.

The badge is a plain control (`xmlns:mue="clr-namespace:Mue.Wpf;assembly=Mue.Wpf"`):

```xml
<mue:UpdateBadge VersionIndicator="{Binding Updates.VersionIndicator}"
                 Status="{Binding Updates.Status}" HasRelease="{Binding Updates.HasRelease}"
                 Command="{Binding OpenUpdates}" />
```

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/03-badge-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/03-badge-light.gif" alt="Demo: a badge showing v1.2.0 → 1.3.0 appears in the app and opens the update dialog" width="960">
</picture>

## Customize

**Texts.** English and French are built in. Implement `IUpdateTexts` and pass it to `UpdateTexts.Configure(...)`. `TryGet` says which keys you provide; the others fall back to Mue's resources according to `CurrentUICulture`. `Configure(null)` restores the built-in texts.

**Styles.** The dialogs read your resources: the styles `PrimaryButton`, `SecondaryButton`, `DangerButton` and `DialogFooter`, plus a set of `Brush.*` brushes. `MueStyleKeys.Required` lists them all and `MueStyleKeys.Missing(...)` checks what is absent. An app without a theme calls `MueStyles.AddMissingDefaults(app)`: only missing keys are added, your palette is never overwritten. Details: [Required WPF resources](#required-wpf-resources) and [Avalonia integration](#avalonia-integration).

**Behaviour.** `UpdateServiceOptions` takes a log, a schedule, a `CanSearch` predicate, a time provider and a pending store. `VelopackBackendOptions` takes `Prerelease`, `RestartArgs`, `FallbackVersion`, `UserAgent`, `ShutdownBehavior` and `LatestReleaseOnly`.

## Platforms

- Windows: engine, WPF and Avalonia dialogs, blocker detection.
- macOS 15 or later: engine, Avalonia dialogs, blocker detection (`AskToQuit` by default).
- Updates apply only to an app installed with Velopack. Otherwise the dialog points to the manual download.

## Host integration

English and French texts are built in. Configuring a provider is optional:

```csharp
UpdateTexts.AppName = "My App";
UpdateTexts.Configure(new HostUpdateTexts());
var service = new UpdateService(backend, new UpdateServiceOptions
{
    Log = log,
    Schedule = schedule,
    CanSearch = () => true,
    Time = TimeProvider.System,
    PendingStore = new FileUpdatePendingStore(profilePendingPath),
});
var dialog = new Mue.Wpf.UpdatesDialog(service, dialogHost);
dialog.ShowDialog();
```

HostUpdateTexts, backend, log, schedule and dialogHost stand for the host's implementations.
The host provides:

- **IUpdateTexts**: `TryGet` tells which keys it knows; `T`, `F`, `Formatting` and `LanguageChanged` keep their role.
  Unknown keys fall back to Mue's resources according to `CurrentUICulture`. `Configure(null)` restores the built-in texts.
  French resources ship in the packages' satellite assemblies; SmartFormat handles plurals.
  `UpdateTexts.AppName` defaults to the entry assembly name. The literal token `{AppName}` in
  Mue's resources is replaced before `T` and before `F` formatting; texts supplied by the host
  are left untouched.
- **IUpdateLog**: error logging. SilentUpdateLog suits a host without a log.
- **IUpdateSchedule**: the Automatic preference, the last search and persistence of RecordCheck.
  MemoryUpdateSchedule suits fixtures.
- **IUpdateBackend**: installation state, version, search, download and apply.
  Mue.Velopack provides VelopackBackend; FakeUpdateBackend allows trials without network.
- **IUpdateBlockerClassifier**: identifies relays and app-specific exclusions.
  ProgramClassifier treats processes as ordinary programs.
- **IUpdateDialogHost**: side-effect-free check (`CheckAsync`), preparation (`PrepareAsync`), hand-off before apply,
  recovery on failure, exit, errors, release links, log, classifier and IUpdateBlockerFinder.
  The owner is an object; the contract does not depend on WPF.

A resident host calls StartPolling, then Dispose at shutdown. The search starts after 20 seconds,
re-evaluates permission and preference every 15 minutes, and keeps six hours between searches.
No download ever triggers the update on its own.

`ShutdownBehavior` describes what the installer does: `Refuse` refuses programs that are still
open, `AskToQuit` asks them to quit, `Kill` stops them. The Velopack backend picks `AskToQuit` on
macOS and `Kill` elsewhere. DockPad picks `Refuse` for its binaries, which are built for
**Velopack 1.2.161**: do not bump this dependency implicitly.

`CheckAsync(owner, token)` returns `UpdatePreparationCheck(CanContinue, Summary)` without closing
or releasing any session. After the download: check, blocker search, dialog if needed, final check
without enrichment, then a single preparation, `BeforeApply`, `Apply`, `ExitAfterApply`.
Postponing or closing the blockers dialog does not prepare the host. The optional summary shows
in both windows; when null, its slot stays collapsed.

### Consent and ignored programs

1. `CheckAsync` announces the effects of the preparation in `Summary`, without running them.
2. With no summary and no blocker, the flow goes straight on. With no summary, a list emptied by
   **Retry** or by closing programs counts as consent, as before.
3. With a non-null summary (even empty), the dialog always opens. With no blocker, it shows
   **Confirm installation**, the summary and **Confirm and install**. Only that button counts as consent.
4. With blockers, the summary stays visible. **Close N programs and install** allows closing them.
   If **Retry** or that closing empties the list, the dialog stays open in confirmation; the user
   confirms before any preparation.
5. With `Kill` or `AskToQuit`, unchecking a group accepts leaving it to the installer.
   The button closes the checked groups, then goes on if only the ignored ones remain.
   With no group checked, **Install without closing them** gives that consent directly.
   An `Unavailable` process can be ignored too; it cannot be closed.
6. Checked programs that stay open move to the list of remaining programs, with
   **Force stop and install**. `Kill` announces a forced stop by the installer; `AskToQuit`
   announces a request to quit, with a risk of interruption without saving.
   `Refuse` always requires every blocker to be gone and does not allow ignoring.
7. The final check (`enrich: false`) only accepts the identities ignored at consent time
   (PID and start time). A new process, even with a reused PID, blocks.
   Only then: `PrepareAsync`, `BeforeApply`, `Apply`, `ExitAfterApply`.

**Options (0.4.0).** `new UpdatesDialog(service, host, new UpdatesDialogOptions { SeparateInstallStep = true })`
splits the flow in two: **Download** only downloads and stops on *New version ready*; **Restart and install**
then runs the preparation and installation. Without options, one click downloads and installs, as in 0.3.0.
`UpdatesDialogOptions.ShowBlockers` replaces the blockers display (tests). The 0.3.0 constructors are kept;
only the literal call `new UpdatesDialog(service, host, null)` becomes ambiguous at compile time.

**Confirmation help.** With `Kill` or `AskToQuit`, the confirmation window says that programs which start using
the app's files in the meantime will be stopped or asked to quit. With `Refuse`, it shows the summary only.

**One group per program.** Several instances of the same program form a single group: they are closed or
ignored together.

**Postpone**, Esc or closing the window never give consent and never call `PrepareAsync`.
`UpdateBlockersDialog` now takes an `UpdateBlockerScan`, and its `refresh` delegate returns that
same type. `scan.Incomplete` shows a warning separate from the incomplete-closing state
(`incomplete`, used by fixtures). `IgnoredBlockers` exposes the identities accepted for the final
check. `UpdatePresentation.CloseResultText` translates the five results of `CloseAsync`.

`Apply` writes the expected version before starting the backend and removes it if the backend
throws. At resident startup, call `service.VerifyLaunch()` once: `None`, `Succeeded` or
`RolledBack`, with the expected and current versions. The marker is consumed. Only textual
equality, without a `v` prefix, is checked. The default store is in memory; use
`FileUpdatePendingStore` in the profile to survive restarts.

`BlockerQuery` holds the paths to examine (folders or files), and excludes the current process
unless `IncludeCurrentProcess`. `Find(query, classifier, enrich)` returns an `UpdateBlockerScan`;
a failed detection throws. `Incomplete` flags at least one `Unavailable` process, kept when
Restart Manager reports it but it stays unreadable. Stopping it returns `AccessDenied`.
`CloseAsync` returns `Closed`, `StillRunning`, `AccessDenied`, `Gone` or `IdentityChanged`,
checking PID and start time on each attempt.

Searches receive a token cancelled by `Dispose`. An automatic search is also cancelled when
automatic updates are turned off, or at the next cycle if `CanSearch` becomes false.
Cancellation shows no error. `VelopackBackendOptions.UserAgent` defaults to `Mue`;
`LatestReleaseOnly = true` limits GitHub to `/releases/latest`, with no fallback to another
release. This option defaults to `false`, notably for DockPad.

UpdatesDialog has no parameterless constructor: the host owns the factory.
The XAML extension `{mue:T Key}` creates a OneWay binding to a singleton indexer, refreshed on
LanguageChanged. The host stays responsible for the UI thread when the language changes.

## Texts and languages

Mue ships English and French. **A language is only accepted complete**: a test checks that every built-in
language has exactly the keys of the neutral resources.

`UpdateTextKeys.All` lists every text key. `UpdateTextKeys.Missing(texts)` returns the keys your `IUpdateTexts`
does not provide. It matters to apps that supply **all** their texts (a custom language, a pseudo-language):
add `Assert.Empty(UpdateTextKeys.Missing(myTexts))` to your tests, and a key added by a new Mue version shows up
there instead of as a mixed-language window. For an app that overrides only some texts, or none, the list is
not empty by design: it is informative.

**Language chosen by the app.** Mue's built-in texts follow `CurrentUICulture`. An app that picks its language
without following Windows sets that culture and tells Mue through `LanguageChanged`:

```csharp
var texts = new AppLanguageTexts();
UpdateTexts.Configure(texts);
texts.Switch(CultureInfo.GetCultureInfo("fr"));

/// <summary>Mue keeps its own English and French texts, but follows the language chosen in the app.</summary>
sealed class AppLanguageTexts : IUpdateTexts
{
    public CultureInfo Formatting { get; private set; } = CultureInfo.CurrentUICulture;
    public event EventHandler? LanguageChanged;

    // No key provided: Mue answers with its own texts, in the CurrentUICulture language.
    public bool TryGet(string key, out string text) { text = ""; return false; }
    public string T(string key) => key;
    public string F(string key, params object?[] args) => key;

    public void Switch(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        Formatting = culture;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

## Themes

`Mue.MueStyleKeys.Required` is the single list of resources the windows read, shared by WPF and Avalonia
(`Mue.Wpf.MueStyleKeys` and `Mue.Avalonia.MueStyleKeys` point to it). The default sets use a neutral blue
accent and meet WCAG AA contrast, in light and dark.

**WPF.** Choose a theme for Mue's default values and for the title bar of its windows:

```csharp
MueStyles.AddMissingDefaults(app, MueTheme.System); // adds missing keys; System follows Windows light/dark mode
MueStyles.SetTheme(app, MueTheme.Dark);             // on your own theme switch
MueStyles.SetTheme(app, null);                      // hand everything back to the app
```

- **Until the app calls `AddMissingDefaults` or `SetTheme`, Mue.Wpf touches neither resources, nor the title bar,
  nor Windows events.** An app that supplies its whole palette and paints its own title bars keeps its rendering.
- Mue only replaces the values it added itself: a key supplied by the app is never touched.
- `System` subscribes to Windows settings changes and re-applies; `SetTheme(app, null)` unsubscribes and removes
  the values Mue added.
- Windows high contrast: with `System`, Mue fills missing keys from the system colours, never from its light or
  dark set, and leaves the title bar to Windows.
- Title bar: dark (DWM immersive dark mode) when the effective theme is dark. `MueStyles.ManageTitleBar = false`
  leaves it to the app; `MueStyles.WindowOpened` hands every Mue window to the app before it shows.
- `AddMissingDefaults(app)` without a theme now means `System` (it meant light only in 0.3.0).

**Avalonia.** The windows follow the application's `ThemeVariant`. On Windows, Avalonia paints the title bar from
`ActualThemeVariant`. **Known limit, macOS:** the title bar follows the system appearance, not the app's
`ThemeVariant`.

## Required WPF resources

Mue.Wpf uses the application's resources and ships no theme. Provide these styles:

- PrimaryButton, SecondaryButton and DangerButton: Button styles.
- DialogFooter: Border style.

Provide the following brushes, with values suited to the active theme:

```text
Brush.Surface             Brush.SurfaceCard        Brush.SurfaceMuted
Brush.Text                Brush.TextLabel          Brush.TextDetail
Brush.TextSmall           Brush.TextSubtle         Brush.Separator
Brush.Border              Brush.Accent             Brush.AccentText
Brush.AccentTint          Brush.AccentSelected     Brush.AccentBorderSoft
Brush.Danger              Brush.DangerSurface      Brush.DangerBorder
Brush.WarnTextAlt         Brush.TextDisabled       Brush.AccentTintStrong
```

Dynamic references follow the host's resource replacements. Its implicit control styles
(check box, progress bar, etc.) apply too. BoolToVisibilityConverter is declared in
UpdatesDialog: the host does not need to provide that key.

`MueStyleKeys.Required` lists the exact contract and `MueStyleKeys.Missing(app.TryFindResource)`
lets you test it. A host without a theme can call `MueStyles.AddMissingDefaults(app)`: only
missing keys are added to the application resources. No dictionary is merged into the windows.
DockPad keeps its palettes entirely.

`UpdateBadge` is a standalone button, with no imposed bar: `VersionIndicator`, `Status`,
`HasRelease`, `Command` and a `Click` event. Its transparent hit target is 40 px high and its
pill 26 px; dynamic brushes follow the host's theme.

```xml
<mue:UpdateBadge VersionIndicator="{Binding Updates.VersionIndicator}"
                 Status="{Binding Updates.Status}" HasRelease="{Binding Updates.HasRelease}"
                 Command="{Binding OpenUpdates}" />
```

## Avalonia integration

After the host's resources and theme, call `Mue.Avalonia.MueStyles.AddMissingDefaults(app)`
explicitly at startup. The keys are the same as `Mue.Wpf.MueStyleKeys.Required`; the three
buttons and `DialogFooter` are Avalonia `ControlTheme`s targeting `Button` and `Border`
respectively. The light/dark fallback only provides missing keys, in the first merged
dictionary, behind the host's resources. The windows merge no dictionary and use `DynamicResource`.

```csharp
var dialog = new Mue.Avalonia.UpdatesDialog(service, dialogHost);
await dialog.ShowDialog(owner);
```

`dialogHost` implements the generic `IUpdateDialogHost` contract; the owners it receives are
`Avalonia.Controls.Window`s. The host's control theme (Fluent or another) stays responsible for
check boxes and progress bars. The `{mue:T Key}` extension follows `UpdateTexts.LanguageChanged`
on the UI thread. `UpdateBadge` exposes the same properties, `Command`, `CommandParameter` and
`Click` as the WPF control; the whole 40 px target is clickable and the pill is 26 px high.

The `Mue.Avalonia.Tests` tests use Skia and `Avalonia.Headless.XUnit` on both targets.
They write their captures to `artifacts/avalonia/net8.0` and `artifacts/avalonia/net10.0`,
a folder ignored by git.

## Blocker detection

The core references neither WPF nor Velopack and does not target Windows. Only
WindowsUpdateBlockerFinder is Windows-only, with System.Management 8.0.0 and native calls.
UpdateBlockers keeps the pure selection and identity operations; searching and closing go
through IUpdateBlockerFinder.

On macOS 15+, `MacUpdateBlockerFinder` receives an `UpdateBlockerQuery` holding absolute paths
of `.app` bundles, folders or executables. Paths are resolved with `realpath` before filtering,
with a case-sensitive comparison. `IncludeCurrentProcess` lists the current process; closing it
is still refused. The soft close waits five seconds; the constructor
`new MacUpdateBlockerFinder(exitTimeout: TimeSpan.FromSeconds(7), log: log)` adjusts that delay.
Keep the objects returned by `Find` and use the same instance for `CloseAsync`: it keeps their
native audit token. Without that token, no signal is sent.
`Find(query, classifier, enrich)` returns an `UpdateBlockerScan`. A process whose path cannot be
read is ignored: nothing ties it to the requested paths (as on Windows, outside Restart Manager
reports). A matched executable whose identity cannot be read is listed with `Unavailable`.
These blockers are not preselected, make the scan `Incomplete` and cannot be closed.
`CloseAsync(blocker, force, token)` returns the five `UpdateBlockerCloseResult` values;
cancellation stops the wait without sending any further signal.

## Sample app

`samples/Mue.Sample.Wpf` is a « Notebook » app whose 1.3.0 update is simulated by `FakeUpdateBackend`, without
network. `Mue.Sample.Wpf.exe --capture artifacts/wpf` writes neutral captures of the Mue windows (light and dark,
English and French) into `artifacts/`, ignored by git. The sample is not packed.

## Local publishing

Install the .NET 8 and .NET 10 SDKs and WPF support on Windows, then run:

```powershell
./pack.ps1
./pack.ps1 -Feed C:\my-nuget-feed
./pack.ps1 -Feed C:\my-nuget-feed -Version 0.3.0-b3.1 -SkipTests
```

The script builds and tests the solution, then packs the four projects in Release with -m:4.
The default feed is C:\Users\Sylvain\nuget-local. In a consuming project, set
RestoreAdditionalProjectSources from an overridable MueFeed property:

```xml
<PropertyGroup>
  <MueFeed Condition="'$(MueFeed)' == ''">C:\Users\Sylvain\nuget-local</MueFeed>
  <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MueFeed)</RestoreAdditionalProjectSources>
</PropertyGroup>
```

Pass -p:MueFeed=... to dotnet commands to change the feed. Do not put an absolute local path in a
versioned nuget.config.

**A published version is never published again.** NuGet keeps packages in ~/.nuget/packages
(or NUGET_PACKAGES): replacing a .nupkg does not update consumers that already restored it.
Bump Version in Directory.Build.props and update the changelog before a new publication.
The script refuses to overwrite a version already in the feed.

## Tests

`dotnet build Mue.sln -m:4` then `dotnet test Mue.sln -m:4 --filter "Category!=RealMachine"`.
The Windows `RealMachine` tests query real processes and Restart Manager; run them on a Windows
machine outside a sandbox with `--filter "Category=RealMachine"`. They are not disabled.
The native Mac tests carry `RealMachine` and `MacNative`; they are skipped outside macOS 15+.
On a Mac, run them with `dotnet test tests/Mue.Tests/Mue.Tests.csproj -m:4 --filter "Category=MacNative"`.
HTTP tests use simulated responses, without network access.

## License

MIT, see [LICENSE](https://github.com/syl-craft/Mue/blob/main/LICENSE).
