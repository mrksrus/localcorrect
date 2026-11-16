# Third-party data and credits

The LocalCorrect program is MIT, copyright Sam Markus Rus. The word lists are not MIT. They are the lists shipped with [LocalCorrect for Firefox](https://github.com/mrksrus/localcorrect-browser) (`extension/data/*.lex` at `v2.0.0-rc.1`, commit `7db776278435a43a09a417e8ddd54aab37b2b383`) and reused by [LocalCorrect for Outlook](https://github.com/mrksrus/localcorrect-outlook).

- **Word frequencies.** [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, OpenSubtitles 2018, revision `525f9b560de45753a5ea01069454e72e9aa541c6`. The lists are CC BY-SA 4.0 (`licenses/CC-BY-SA-4.0.txt`). The FrequencyWords repository license for its code is MIT (`licenses/FrequencyWords-LICENSE`); the content license is CC BY-SA 4.0 (`licenses/FrequencyWords-README.md`).
- **English word validity.** npm `dictionary-en` 4.0.0, SCOWL / Kevin Atkinson et al. `licenses/dictionary-en-LICENSE`. Used when the lists were built, to decide which English words are real.
- **German word validity.** npm `dictionary-de` 3.0.0, igerman98 by Björn Jacke, GPL-2.0-or-later (GPL-3.0 chosen by the dictionary package). `licenses/dictionary-de-LICENSE`. Used when the lists were built, to decide which German words are real.
- **Algorithm.** One- and two-edit candidates ranked by frequency, after Peter Norvig's "How to Write a Spelling Corrector" and Wolf Garbe's SymSpell. The policy (umlauts, compounds, contractions, what to leave alone) matches the version 2 engine in the Firefox and Outlook projects. The C# in this repository is written for the MIT tray app.

`en.lex` and `de.lex` keep only words those dictionaries accept, with the subtitle-corpus count. See `src/LocalCorrect.Engine/data/DATA-LICENSE.md`.

Build tools: the .NET SDK. No Office binaries are bundled. The optional Ollama engine is off unless the user turns it on, and it only talks to a loopback address.
