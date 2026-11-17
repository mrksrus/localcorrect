# LocalCorrect

LocalCorrect is a per-user Windows tray app that corrects English and German spelling in whatever program you are typing in. Select text, press a hotkey, and the selection is replaced. Outlook, a browser, and most other text fields all use the same app. Nothing is sent off the machine.

This replaces the separate Outlook add-in and browser extension with one program. It does not install into Outlook.

**Version 0.2.0-alpha.1.** The release zip also contains [LanguageTool](https://languagetool.org) 6.6 and a Java runtime, which run on this computer only (see [Credits and licences](#credits-and-licences)). LocalCorrect uses LanguageTool by default: it fixes spelling, capitalisation, grammar, punctuation and commonly confused words (`das` / `dass`, `Its` / `It's`), and leaves style suggestions alone. The built-in word lists, the same corrector as [LocalCorrect for Firefox](https://github.com/mrksrus/localcorrect-browser) version 2 and [LocalCorrect for Outlook](https://github.com/mrksrus/localcorrect-outlook), are the instant fallback: they fix typos only.

## Privacy

LocalCorrect starts the bundled LanguageTool server itself, as a separate program that answers only on 127.0.0.1 on a free port, and stops it when you exit. Your text goes to that server over the loopback connection and nowhere else. The built-in word lists ship inside the program and do not open a connection at all. There is no account and no telemetry.

The text you select is not written to the log. The log records only that something happened (for example the outcome, the language, the length, the engine that was used, how long it took in milliseconds, and the program name). The first line of the log is written as the process starts, before settings are loaded, so a startup failure still leaves a trace at `%LOCALAPPDATA%\LocalCorrect\log.txt`.

An optional local-model engine can talk to an [Ollama](https://ollama.com)-compatible server. It is off unless you select it. The address must be `127.0.0.1`, `localhost`, or `::1`. Other addresses are rejected before a request is built. The client does not use a proxy and does not follow redirects. It asks the model for its most predictable answer (temperature 0) and keeps the model loaded for 30 minutes. An answer that changes the number of lines, the length by more than 20 %, or more than a quarter of the words is refused and nothing is changed. Leave the preview on when you try it, because that model is not the one the tests cover.

## Install

Windows 10 or Windows 11, 64-bit. No administrator account. The published program includes its own .NET runtime.

The zip is about 210 MB and takes about 280 MB once installed, most of it LanguageTool (about 155 MB) and its Java runtime (about 45 MB). While LanguageTool runs it uses about 1 GB of memory.

1. Download `LocalCorrect-0.2.0-alpha.1-win-x64.zip` from the [releases](https://github.com/mrksrus/localcorrect/releases) page and unzip it.
2. The program is unsigned, so SmartScreen may say it is unrecognized. Choose **More info**, then **Run anyway**.
3. Double-click `install.cmd`. That copies LocalCorrect to `%LOCALAPPDATA%\LocalCorrect\app` and adds a Start menu shortcut. Installing over an older version replaces the `languagetool`, `jre` and `licenses` folders completely. You can also run `LocalCorrect.exe --install`.
4. A tray icon appears. Right-click it for settings, pause, the log, and exit.

Running `LocalCorrect.exe` itself, without installing, also works. Install it if you want it to stay put and to start with Windows.

Remove it with `uninstall.cmd` (or `LocalCorrect.exe --uninstall`). That removes the program, the Start menu shortcut, and the sign-in startup entry. Settings in `%APPDATA%\LocalCorrect` are kept.

## Use

Click in the text field first so the caret is there. Select the words you want fixed. Press **Ctrl+Alt+L** (change this in settings).

A small window lists the changes. **Replace** pastes them over the selection. **Cancel** or Esc leaves the text alone. You can turn that preview off.

The previous clipboard contents are put back afterwards, including formats that are not plain text when Windows exposes them as memory (HTML, RTF, PNG, and similar). A bitmap held only as a GDI handle cannot be snapshotted and is skipped.

### LanguageTool (default)

LanguageTool checks English as US English and German as German German; the language is detected per text, or forced in settings. Only spelling, capitalisation, grammar, punctuation and confused-word fixes are applied, always the first suggestion. A suggestion is skipped when it would change a link, an e-mail address, code, a quoted `>` line, a name or a word that is valid in the other language, or the first word of the selection (it may start in the middle of a sentence).

LanguageTool needs a minute or so after LocalCorrect starts before it is ready; the tray menu shows its state. Until then, and whenever it fails, does not answer within 10 seconds, or is not installed, the built-in word lists correct the text at once and the tray tells you. If the server stops it is restarted once; if it stops again within a minute the word lists are used until you change a setting or restart LocalCorrect. To save the memory, choose **Built-in word lists only** in settings; LanguageTool is then stopped.

### Built-in word lists

What they change, from the same cases as the Firefox version:

- `recieve` → `receive`, `teh` → `the`, `Dnak` → `Dank`
- `fur` → `für`, `ueber` → `über`, `gross` → `groß`
- `haus` → `Haus`, `dont` → `don't`, a lone `i` → `I`

What it leaves alone: names, links, e-mail addresses, code, file names, numbers, acronyms, command-line flags, and quoted lines that start with `>`. A word that is valid in the other language stays (`Meeting` in a German sentence). It does not correct a real word used in the wrong place (`there` / `their`). When two fixes are equally likely it picks the more common word.

If nothing is selected, LocalCorrect tells you and does not type anything. It does not correct a whole message on its own.

## Settings

Stored in `%APPDATA%\LocalCorrect\settings.json`. Open them from the tray icon.

| Setting | Default |
| --- | --- |
| Hotkey | Ctrl+Alt+L. Ctrl or Alt is required, plus a letter or F1–F12. |
| Language | Detect automatically, or force English or German. |
| Engine | LanguageTool on this computer. Or the built-in word lists only (no LanguageTool, less memory), or a local Ollama-compatible model on this computer. A 0.1.0 settings file that names the word lists is read as LanguageTool. |
| Preview | On. Shows the changes and waits for Replace or Cancel. |
| Exclusions | Process names, one per line (`outlook`, `keepass`). `.exe` is optional. |
| Start with Windows | Off. Uses the per-user Run key, not an administrator task. |
| Logging | Off. Errors are still written. The selection is not. |

## How it is built

`LocalCorrect.Engine` corrects text. `LanguageToolServer` starts, warms up, restarts and stops the bundled server; `LanguageToolEngine` turns its matches into edits and falls back to `SymSpellCorrector`, the built-in word lists. `OllamaCorrectionEngine` is the optional loopback engine. `TextPolicy` holds the protected-text and name rules both LanguageTool and the word lists use. All engines implement `ICorrectionEngine`. On Windows the server runs in a job object, so it ends with LocalCorrect even after a crash.

`LocalCorrect.Core` holds settings, the clipboard save/restore session, and the correction workflow. Those are unit-tested without Windows. `LocalCorrect.App` is the tray: a global hotkey, simulated Ctrl+C / Ctrl+V, a preview window, and the per-user installer.

The hotkey copies the selection, the engine returns a new string, and Ctrl+V pastes it. Focus is sent back to the window you were typing in before the paste.

## Build

.NET SDK 8.

```
dotnet test LocalCorrect.sln -c Release
dotnet publish src/LocalCorrect.App/LocalCorrect.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

`build/bundle-languagetool.sh <publish folder> --smoke` adds LanguageTool and the Java runtime to the published folder. It downloads the pinned archives, checks their SHA-256, trims LanguageTool to English and German, builds the runtime with `jlink`, and with `--smoke` starts the bundled server and checks a German and an English sentence. It needs bash, curl and Python 3; on Linux, set `LC_JDK` to a local JDK 17 to test the trim and the server (the Windows runtime can only be built on Windows).

Set `LOCALCORRECT_BUNDLE_DIR` to such a folder and `dotnet test` also runs `BundleIntegrationTests`: it starts the real server through `LanguageToolServer`, corrects the German and English fixtures through `LanguageToolEngine`, prints the start, warm-up and warm check times, and fails if a warm German check takes 5 seconds or more.

GitHub Actions (`.github/workflows/ci.yml`) builds and tests on `windows-latest`, runs the bundle script with `--smoke`, runs the integration test against the bundle, and uploads `LocalCorrect-<version>-win-x64.zip`. The version comes from `Directory.Build.props`.

## Roadmap

- A stronger local model, behind the same `ICorrectionEngine`, once it beats the word lists on the public cases without inventing text.
- Richer preview for long selections.
- A signed installer.
- macOS and Linux. The engine and the workflow are plain .NET. The missing pieces are a tray, a global hotkey, and a way to read and replace the selection (Accessibility on macOS, AT-SPI on Linux). Not in this version.

## Limitations

- The paste is plain text. Bold, links, and other formatting inside the selection are not kept.
- Some programs drop the selection when another window opens. The preview tries to put focus back before pasting. If a program clears the selection on focus loss, Replace can insert the text at the caret instead of over the selection. Turn the preview off if that happens, or paste from a program that keeps the selection (Outlook's editor and normal browser fields usually do, if you do not click back into the text first).
- Editors that draw their own text and do not answer Ctrl+C, such as Google Docs, do not work.
- A window running as administrator is left alone. The tray icon says so. Running LocalCorrect itself as administrator is not required and is not the setup this installer uses.
- Password boxes that are standard Windows edit controls are skipped. A browser password field is not always detectable. Do not use the hotkey on a password.
- Selections longer than 50,000 characters are refused. Words longer than 32 letters are left unchanged.
- The clipboard is borrowed for a moment. If another program needs it during that moment, that program waits or fails, and LocalCorrect then puts back what was there.
- This alpha has not been exercised inside a live copy of Outlook 2019. The automated tests cover the engine and the clipboard session, not the Outlook window.

## Credits and licences

LocalCorrect uses [LanguageTool](https://languagetool.org) 6.6, the open-source edition, under the GNU Lesser General Public License 2.1 or later. The dictionaries and data of the other languages are removed; THIRD_PARTY.md lists exactly what is left. It runs as a separate program on this computer and answers only on 127.0.0.1. LanguageTool Premium rules and the LanguageTool cloud service are not used.

LanguageTool brings its own dictionaries: the German spelling dictionary igerman98 / frami (GPL-2.0 or GPL-3.0), German part-of-speech data (CC BY-SA 4.0), and English spelling and part-of-speech data based on SCOWL, AGID and 12dicts (LGPL-2.1). It also ships about 120 Java libraries, each listed with its version and licence in [licenses/languagetool-libraries.md](licenses/languagetool-libraries.md).

The Java runtime is [Eclipse Temurin](https://adoptium.net) 17.0.21+9 (GPL-2.0 with the Classpath Exception), reduced to the modules LanguageTool needs. The program itself runs on .NET 8 and Windows Forms (MIT), included in `LocalCorrect.exe`. The built-in word lists use FrequencyWords (CC BY-SA 4.0), SCOWL and igerman98.

[THIRD_PARTY.md](THIRD_PARTY.md) names every component with its version, licence, source and place in the package, says exactly what was removed from LanguageTool, and links the source code. The licence texts are in the `licenses` folder, in `languagetool` and in `jre\legal`.

LanguageTool is a trademark of LanguageTooler GmbH. Eclipse Temurin is a trademark of the Eclipse Foundation. Java and OpenJDK are trademarks of Oracle and/or its affiliates. LocalCorrect is not affiliated with or endorsed by any of them.

**Replacing LanguageTool or Java.** The LanguageTool jars and the Java runtime are unmodified and can be replaced. Exit LocalCorrect from the tray, then, in `%LOCALAPPDATA%\LocalCorrect\app`, replace any jar in `languagetool\libs`, the whole `languagetool` folder (another LanguageTool build with `languagetool-server.jar` at the top), or the whole `jre` folder (any Java 17 or later runtime with `bin\java.exe`). The server is started as `jre\bin\java.exe -cp languagetool-server.jar org.languagetool.server.HTTPServer` from inside the `languagetool` folder, with `languagetool-server.properties` as its configuration. Installing LocalCorrect again puts the bundled versions back.

Program: [MIT](LICENSE), copyright Sam Markus Rus.
