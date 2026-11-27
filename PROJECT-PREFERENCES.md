# Project preferences: LocalCorrect for Windows

Earlier decisions LC-01 to LC-13 live in the Outlook add-in (`mrksrus/localcorrect-outlook`) and the browser extension. The ones still in force for this tray app are restated here.

## Accepted decisions

- W-01 (from LC-01/LC-11/LC-12). English and German correction runs entirely on this computer: no cloud engine, no telemetry, selected text is never logged. Clear typos must actually be fixed; leave names, links, code, numbers and quoted `>` lines alone. Language is detected per text, with a setting to force English or German.
- W-02, 2026-11-17. One Windows tray app replaces the Outlook add-in and the browser extension as the product the user works on.
- W-03, 2026-11-17. LanguageTool (open-source edition, LGPL-2.1, unmodified, English and German only) is bundled with the app together with a trimmed Java runtime. The app starts it in the background on 127.0.0.1, keeps it warm and stops it on exit. It is the default engine. The built-in word lists remain as the instant fallback while it starts or if it fails. The optional Ollama engine stays (loopback only).
- W-04, 2026-11-17. Licensing must be correct and visible: a dedicated "Credits and licences" section in the README and an accurate THIRD_PARTY.md naming every bundled component, its version, licence and source. Ship the licence texts. For LanguageTool, point to the exact upstream source of the bundled version and keep its jars replaceable (LGPL). "LanguageTool" is used only as "uses LanguageTool", never as this app's name. LanguageTool Premium rules and its cloud AI are not open source and are not used.
- W-05, 2026-11-17. The user should not have to select text first: with nothing selected, the hotkey corrects the current paragraph. The UI (preview and settings) gets a visual redesign.
- W-06, 2026-11-17. Prefer abstention over a wrong edit for suggestion types that change meaning or style: apply spelling, capitalisation, grammar, punctuation and confused-word fixes; do not apply style suggestions.
- W-07, 2026-11-17. LanguageTool checks English as `en-US` and German as `de-DE`; there is no variant setting. British spellings are changed to the American form.
- W-08, 2026-11-17. The source archives of the bundled GPL and LGPL components (LanguageTool, Java runtime) are uploaded to a third-party-sources release in this repository and linked from THIRD_PARTY.md. No written offer; nothing added to the app zip.
- W-09, 2026-11-17. With nothing selected, a paragraph that contains links, images, tables or inline formatting is not replaced; the app asks for a selection.
- W-10, 2026-11-17. The hotkey does nothing in terminals, code editors and Excel (no copy, no paste, only a tray message), so it cannot interrupt a command, paste a line at the caret or overwrite a cell or formula.
- W-11, 2026-11-27. What the engine is unsure about is shown, not hidden: the preview marks such words in blue and offers the suggestions; nothing unsure is applied without the user choosing it. A system-wide underline overlay is a possible later experiment, not decided.

## Observed implementation

0.1.0-alpha.1: .NET 8 WinForms tray app with the word lists as its only offline engine and an optional Ollama engine, behind `ICorrectionEngine`; selection read and replaced through simulated Ctrl+C / Ctrl+V with the clipboard restored; plain-text paste only.

0.2.0-alpha.1:
- Bundle: `build/bundle-languagetool.sh` (pinned LanguageTool 6.6 and Temurin 17.0.21+9, trimmed to English/German, jlink runtime, jar-licence table check, smoke test) run in CI; zip about 210 MB; `languagetool-server.properties` turns on pipeline caching.
- Engines: `LanguageToolServer` (port on 127.0.0.1, warm-up of both languages, one restart, job object kill-on-close) and `LanguageToolEngine` (category allow-list, first suggestion, abstention rules, speller suggestions more than two edits away dropped for lowercase words and at sentence starts); word lists are the fallback; Ollama has temperature 0, keep_alive and a rewrite guard.
- No selection: after a 1.2 s first-copy wait, `NoSelectionPolicy` keys select the paragraph (or line); formatted paragraphs are refused; `ProcessFilter.NeverCorrect` is the W-10 list.
- UI: the preview shows the text with changes in place (struck through / underlined); settings are two columns of sections that scroll when they do not fit above the taskbar; system font and colours, sizes in font heights.
- Third-party sources: `third-party-sources` release (W-08) with LanguageTool, Temurin, Hunspell/hunspell-java and the copyleft Maven jars' sources.

0.3.0-alpha.1:
- Engines: two capitals and then lowercase (`THe`) are fixed when LanguageTool suggests the form with the second letter lowered (word lists: when that form is valid); plurals of acronyms (`IDs`) excluded; the first of the first three suggestions that passes the policy is applied; lowercase and sentence-start speller fixes may be three edits away for words of nine letters or more.
- Doubts: `CorrectionResult.Doubts` (LanguageTool only) for matches held back by the name policy, the distance limit or the fragment-start rule; `PreviewSegments.Compose` builds the pasted text from the edits and the picked suggestions; the preview delegate returns the text to paste or null.
- UI: doubts in blue with a wavy underline (rich edit `CFU_UNDERLINEWAVE` in the text colour), hand cursor, menu with up to three suggestions and "Keep as typed", F8 for the next doubt; heading "N changes, M unsure"; Replace off with nothing to paste.

## Open decisions

- Live acceptance on Windows (Outlook, browsers) needs evidence from a real Windows machine; CI runs the unit tests on `windows-latest` only.
