# Contributing

LocalCorrect is a small Windows tray app. Corrections run on the user's machine. Pull requests that keep it that way are welcome.

## Build and test

Install the .NET 8 SDK. From the repository root:

```
dotnet test LocalCorrect.sln -c Release
```

The engine, settings, and clipboard tests run on any operating system. The tray project targets `net8.0-windows` and is built on Windows (see `.github/workflows/ci.yml`).

A self-contained build:

```
dotnet publish src/LocalCorrect.App/LocalCorrect.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Adding a correction engine

Implement `ICorrectionEngine` in `LocalCorrect.Core`. The tray only calls `Correct(text, language)` with `language` of `auto`, `en`, or `de`. Keep the user's text on this computer: no telemetry, no remote APIs, and no writing the selection into the log. Loopback-only engines must refuse any other address before a request is sent.

The built-in engine is `SymSpellCorrector`. Its behavior is covered by `SymSpellLexiconTests`, which repeats the public cases from LocalCorrect for Firefox. Change those expectations only when the correction policy itself changes.

## Word lists

`src/LocalCorrect.Engine/data/*.lex` are third-party data. Their licenses are in `licenses/` and `THIRD_PARTY.md`. They are not covered by the MIT license of the program.

## What not to send in a pull request

Do not add personal mail, chat logs, or other private text as test fixtures. Synthetic sentences are enough. Do not commit secrets.
