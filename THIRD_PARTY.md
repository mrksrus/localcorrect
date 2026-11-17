# Third-party components and credits

The LocalCorrect program is MIT, copyright Sam Markus Rus. The release zip also contains other people's work under
their own licences. Everything below is shipped unmodified; where something was left out, the section
"What was removed from LanguageTool" says exactly what.

## Components

| Component | Version | Licence | Source | Where in the package |
| --- | --- | --- | --- | --- |
| [LanguageTool](https://languagetool.org) (open-source edition: server, core, English and German rules) | 6.6, build `f3e8d91` (2025-03-27) | LGPL-2.1-or-later | [`languagetool-org/languagetool`](https://github.com/languagetool-org/languagetool/tree/v6.6), tag `v6.6` | `languagetool\` (licence: `languagetool\COPYING.txt`) |
| LanguageTool's Java libraries (125 jars, including LanguageTool's own) | see the table | Apache-2.0, BSD, MIT, EPL-1.0/LGPL, CDDL/GPL with Classpath Exception and others, per jar | Maven Central, coordinates per jar | `languagetool\libs\`, listed in [`licenses/languagetool-libraries.md`](licenses/languagetool-libraries.md) |
| [Hunspell](https://hunspell.github.io) spell-checking library, native builds inside the `hunspell-java` wrapper (the wrapper itself is Apache-2.0) | 1.7.1, as built into `com.gitlab.dumonts:hunspell` 2.1.2 | MPL-1.1 / GPL-2.0-or-later / LGPL-2.1-or-later, used here under LGPL-2.1-or-later | [`hunspell/hunspell`](https://github.com/hunspell/hunspell); wrapper and build: [`dumonts/hunspell-java`](https://gitlab.com/dumonts/hunspell-java) | `languagetool\libs\hunspell.jar` (e.g. `win32-x86-64/hunspell.dll`; `licenses\LGPL-2.1-only.txt`) |
| German spelling dictionary (igerman98 by Björn Jacke, frami extension by Franz Michael Baumann), with Jan Schreiber's German word list (public domain) | as in LanguageTool 6.6 | GPL-2.0 or GPL-3.0 | [igerman98](https://www.j3e.de/ispell/igerman98/), [germandict](https://sourceforge.net/projects/germandict/) | `languagetool\org\languagetool\resource\de\hunspell\` (`COPYING_GPLv2.txt`, `COPYING_GPLv3.txt`, `de_DE_frami_README.txt`, `de_DE.README`) |
| German part-of-speech data (Morphy, with additions by Julian von Heyl) | `german-pos-dict` 1.2.4 | CC-BY-SA-4.0 | [`languagetool-org/german-pos-dict`](https://github.com/languagetool-org/german-pos-dict) | `languagetool\libs\german-pos-dict.jar` (`licenses\CC-BY-SA-4.0.txt`) |
| English spelling and part-of-speech dictionaries (from SCOWL, AGID, 12dicts and the Moby part-of-speech list by Kevin Atkinson and others) | `english-pos-dict` 0.6 | LGPL-2.1; notices of the word sources inside | [`languagetool-org/english-pos-dict`](https://github.com/languagetool-org/english-pos-dict) | `languagetool\libs\english-pos-dict.jar`; notices in `languagetool\org\languagetool\resource\en\` (`agid-readme.txt`, `12dicts-readme.html`, `pos-readme.txt`) |
| Word frequencies inside the English and German spelling dictionaries (Spell On It, via Mozilla Gaia) | as in LanguageTool 6.6 | CC-BY-4.0 | [mozilla-b2g/gaia](https://github.com/mozilla-b2g/gaia) | inside the dictionaries above (`licenses\CC-BY-4.0.txt`; see `languagetool\README.md`) |
| [Eclipse Temurin](https://adoptium.net) Java runtime, reduced with `jlink` | 17.0.21+9 | GPL-2.0 with Classpath Exception; further notices per module | [`adoptium/jdk17u`](https://github.com/adoptium/jdk17u/tree/jdk-17.0.21+9), tag `jdk-17.0.21+9` | `jre\` (licences: `jre\legal\`) |
| .NET 8 runtime and Windows Forms | 8.0, the servicing release the build used | MIT; third-party notices | [`dotnet/runtime`](https://github.com/dotnet/runtime), [`dotnet/winforms`](https://github.com/dotnet/winforms) | inside `LocalCorrect.exe` (`licenses\dotnet-LICENSE.txt`, `licenses\dotnet-ThirdPartyNotices.txt`) |
| Word frequencies for the built-in word lists ([FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, OpenSubtitles 2018) | revision `525f9b5` | CC-BY-SA-4.0 (lists); MIT (repository code) | see "Built-in word lists" below | inside `LocalCorrect.exe` (`licenses\CC-BY-SA-4.0.txt`, `licenses\FrequencyWords-LICENSE`, `licenses\FrequencyWords-README.md`) |
| English word validity for the built-in word lists (npm `dictionary-en`, SCOWL by Kevin Atkinson et al.) | 4.0.0 | SCOWL licence | see below | used when the lists were built (`licenses\dictionary-en-LICENSE`) |
| German word validity for the built-in word lists (npm `dictionary-de`, igerman98 by Björn Jacke) | 3.0.0 | GPL-2.0-or-later (GPL-3.0 chosen by the package) | see below | used when the lists were built (`licenses\dictionary-de-LICENSE`) |

Licence texts not shipped beside a component are in `licenses\`, named by their SPDX identifier
(`LGPL-2.1-only.txt` is also the text of LGPL-2.1-or-later, `GPL-2.0-only.txt` with `Classpath-exception-2.0.txt`
is the Java runtime's licence). LanguageTool's own notices are in `languagetool\third-party-licenses\` and
`languagetool\README.md`.

LanguageTool Premium rules and the LanguageTool cloud service are not used. They are not open source and are not
part of this package. The bundled server runs on this computer and only answers on 127.0.0.1.

## What was removed from LanguageTool

`build/bundle-languagetool.sh` starts from the official `LanguageTool-6.6.zip` and only deletes whole files.
No LanguageTool file is edited and no jar is changed.

- In `org/languagetool/{resource,rules,tagging,synthesis,tokenizers}/<xx>/`, for every language other than `de`
  and `en`: every file that is not a `.class` file, except `resource/<xx>/common_words.txt`. (The server registers
  every language at start and needs those classes and that file. `META-INF/org/languagetool/language-module.properties`
  is therefore unchanged.)
- From `libs/`: `dutch-pos-dict`, `languagetool-ga-dicts`, `lucene-gosen-ipadic`, `hanlp`, `morfologik-ukrainian-lt`,
  `catalan-pos-dict`, `portuguese-pos-dict`, `spanish-pos-dict`, `french-pos-dict`, `asturian-pos-dict`,
  `morfologik-crh-lt`, `junit`, `languagetool-core-tests`, `hamcrest-core` (dictionaries of other languages and
  test libraries).
- `languagetool.jar` (desktop program), `languagetool-commandline.jar`, `testrules.bat`, `testrules.sh`, `CHANGES.txt`.

LanguageTool's `README.md` still mentions the licence files of the removed languages; those languages' data is
not in this package. The Java runtime contains only the modules `java.base`, `java.logging`, `java.xml`,
`java.naming`, `java.sql`, `java.management`, `java.desktop`, `jdk.httpserver`, `jdk.unsupported` and the modules
they require, built with `jlink --strip-debug --no-header-files --no-man-pages --compress=2`.

## Pinned downloads

| Archive | URL | SHA-256 |
| --- | --- | --- |
| LanguageTool 6.6 | `https://languagetool.org/download/LanguageTool-6.6.zip` | `53600506b399bb5ffe1e4c8dec794fd378212f14aaf38ccef9b6f89314d11631` |
| Eclipse Temurin JDK 17.0.21+9, Windows x64 | `https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.21%2B9/OpenJDK17U-jdk_x64_windows_hotspot_17.0.21_9.zip` | `98518ecf554465057ee5509462631deeee374bc368c12a411d225cd29381f84b` |

The build stops when a download does not match. `languagetool\BUNDLED-JARS.txt` lists every shipped jar with its
Maven coordinates and SHA-256.

## Source code

- LanguageTool 6.6: <https://github.com/languagetool-org/languagetool/tree/v6.6> (tag `v6.6`). The binary reports
  build `f3e8d91` (<https://github.com/languagetool-org/languagetool/tree/f3e8d914cc33e5106b3d096819e6a7f82b79f6f6>);
  the tag is the next commit, which changes only version numbers and release scripts.
- Eclipse Temurin 17.0.21+9: <https://github.com/adoptium/jdk17u/tree/jdk-17.0.21+9_adopt> (tag
  `jdk-17.0.21+9_adopt`, the tag Adoptium builds this release from), based on OpenJDK 17
  (<https://github.com/openjdk/jdk17u>).
- The dictionaries and libraries: the projects linked in the tables above and in `licenses/languagetool-libraries.md`.

The [`third-party-sources`](https://github.com/mrksrus/localcorrect/releases/tag/third-party-sources) release of
this repository keeps the source archives of the bundled copyleft components, with their SHA-256 in `SHA256SUMS`:
LanguageTool 6.6 (tag `v6.6`, including the German dictionary sources); Eclipse Temurin 17.0.21+9 (Adoptium's own
source archive, tag `jdk-17.0.21+9_adopt`); Hunspell 1.7.1 (tag `v1.7.1`) with the `hunspell-java` 2.1.2 wrapper
(its sources jar and the repository at commit `3115c92`, which holds the same native libraries and the build
scripts); `english-pos-dict` 0.6 (commit `2c68994`); and the Maven Central sources jars of
`mariadb-java-client` 3.4.1, `openregex` 1.1.1, `logback-core` and `logback-classic` 1.5.8, `jaxb-api` 2.3.1,
`jaxb-core` 2.3.0.1, `javax.activation-api` 1.2.0 and `javax.annotation-api` 1.3.2.

## Built-in word lists

The word lists are the lists shipped with [LocalCorrect for Firefox](https://github.com/mrksrus/localcorrect-browser) (`extension/data/*.lex` at `v2.0.0-rc.1`, commit `7db776278435a43a09a417e8ddd54aab37b2b383`) and reused by [LocalCorrect for Outlook](https://github.com/mrksrus/localcorrect-outlook).

- **Word frequencies.** [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, OpenSubtitles 2018, revision `525f9b560de45753a5ea01069454e72e9aa541c6`. The lists are CC BY-SA 4.0 (`licenses/CC-BY-SA-4.0.txt`). The FrequencyWords repository license for its code is MIT (`licenses/FrequencyWords-LICENSE`); the content license is CC BY-SA 4.0 (`licenses/FrequencyWords-README.md`).
- **English word validity.** npm `dictionary-en` 4.0.0, SCOWL / Kevin Atkinson et al. `licenses/dictionary-en-LICENSE`. Used when the lists were built, to decide which English words are real.
- **German word validity.** npm `dictionary-de` 3.0.0, igerman98 by Björn Jacke, GPL-2.0-or-later (GPL-3.0 chosen by the dictionary package). `licenses/dictionary-de-LICENSE`. Used when the lists were built, to decide which German words are real.
- **Algorithm.** One- and two-edit candidates ranked by frequency, after Peter Norvig's "How to Write a Spelling Corrector" and Wolf Garbe's SymSpell. The policy (umlauts, compounds, contractions, what to leave alone) matches the version 2 engine in the Firefox and Outlook projects. The C# in this repository is written for the MIT tray app.

`en.lex` and `de.lex` keep only words those dictionaries accept, with the subtitle-corpus count. See `src/LocalCorrect.Engine/data/DATA-LICENSE.md`.

Build tools: the .NET SDK. No Office binaries are bundled. The optional Ollama engine is off unless the user turns it on, and it only talks to a loopback address.
