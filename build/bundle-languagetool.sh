#!/usr/bin/env bash
# Adds the trimmed LanguageTool 6.6, a jlink Java runtime and the server config to a published app folder.
# Usage: build/bundle-languagetool.sh <publish-dir> [--smoke]
# Env:   LC_CACHE  download cache (default build/cache)
#        LC_JDK    use this local JDK instead of the pinned Windows Temurin (Linux checks only)
set -euo pipefail

LT_URL=https://languagetool.org/download/LanguageTool-6.6.zip
LT_SHA=53600506b399bb5ffe1e4c8dec794fd378212f14aaf38ccef9b6f89314d11631
JDK_URL=https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.21%2B9/OpenJDK17U-jdk_x64_windows_hotspot_17.0.21_9.zip
JDK_SHA=98518ecf554465057ee5509462631deeee374bc368c12a411d225cd29381f84b
MODULES=java.base,java.logging,java.xml,java.naming,java.sql,java.management,java.desktop,jdk.httpserver,jdk.unsupported
DROP_LIBS="dutch-pos-dict languagetool-ga-dicts lucene-gosen-ipadic hanlp morfologik-ukrainian-lt catalan-pos-dict
  portuguese-pos-dict spanish-pos-dict french-pos-dict asturian-pos-dict morfologik-crh-lt junit languagetool-core-tests
  hamcrest-core"

repo=$(cd "$(dirname "$0")/.." && pwd)
out=${1:?usage: bundle-languagetool.sh <publish-dir> [--smoke]}
smoke=${2:-}
cache=${LC_CACHE:-$repo/build/cache}
work=$cache/work
mkdir -p "$cache" "$out"
out=$(cd "$out" && pwd)

# Python unpacks the zips and reads jar metadata: Git Bash on Windows has no reliable unzip.
py=python3
"$py" -c '' 2>/dev/null || py=python

fail() { echo "bundle: $*" >&2; exit 1; }

fetch() { # url sha file
  local file=$cache/$3
  if [[ ! -f $file ]] || ! echo "$2  $file" | sha256sum -c --status; then
    curl -fsSL --retry 3 -o "$file.part" "$1"
    mv "$file.part" "$file"
  fi
  echo "$2  $file" | sha256sum -c --status || fail "SHA-256 mismatch for $3"
}

unpack() { "$py" -c 'import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$1" "$2"; }

rm -rf "$work" "$out/languagetool" "$out/jre"
mkdir -p "$work"

# 1. LanguageTool, trimmed to English and German. No LanguageTool file is edited, only whole files are removed.
fetch "$LT_URL" "$LT_SHA" LanguageTool-6.6.zip
unpack "$cache/LanguageTool-6.6.zip" "$work"
lt=$work/LanguageTool-6.6
for area in resource rules tagging synthesis tokenizers; do
  for dir in "$lt/org/languagetool/$area"/*/; do
    dir=${dir%/} lang=$(basename "$dir")
    [[ $lang == de || $lang == en ]] && continue
    # The server registers every language at start and needs its classes and common_words.txt.
    find "$dir" -type f ! -name '*.class' ! -path "$lt/org/languagetool/resource/$lang/common_words.txt" -delete
    find "$dir" -mindepth 1 -type d -empty -delete
  done
done
for lib in $DROP_LIBS; do
  [[ -f $lt/libs/$lib.jar ]] || fail "expected libs/$lib.jar in LanguageTool 6.6"
  rm "$lt/libs/$lib.jar"
done
rm "$lt/languagetool.jar" "$lt/languagetool-commandline.jar" "$lt/testrules."* "$lt/CHANGES.txt"
mv "$lt" "$out/languagetool"

# 2. Java runtime from the pinned JDK's own jlink (keeps legal/ and release).
if [[ -n ${LC_JDK:-} ]]; then
  jdk=$LC_JDK
else
  fetch "$JDK_URL" "$JDK_SHA" temurin-17.0.21_9-windows-x64.zip
  unpack "$cache/temurin-17.0.21_9-windows-x64.zip" "$work"
  jdk=$work/jdk-17.0.21+9
fi
"$jdk/bin/jlink" --add-modules "$MODULES" --strip-debug --no-header-files --no-man-pages --compress=2 \
  --output "$out/jre"
cp "$repo/languagetool-server.properties" "$out/"
rm -rf "$work"

# 3. Jar list with Maven coordinates, compared with the licence table.
"$py" - "$out/languagetool" "$repo/licenses/languagetool-libraries.md" <<'EOF'
import hashlib, pathlib, re, sys, zipfile
root, table = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
rows = []
for jar in sorted(list(root.glob("*.jar")) + list(root.glob("libs/*.jar"))):
    coords = "-"
    with zipfile.ZipFile(jar) as z:
        props = [n for n in z.namelist() if n.startswith("META-INF/maven/") and n.endswith("/pom.properties")]
        props.sort(key=lambda n: n.split("/")[3] != jar.stem)  # the jar's own pom first, then shaded ones
        if props:
            p = dict(l.split("=", 1) for l in z.read(props[0]).decode().splitlines() if "=" in l and not l.startswith("#"))
            coords = f"{p.get('groupId','?')}:{p.get('artifactId','?')} {p.get('version','?')}"
    rel = jar.relative_to(root).as_posix()
    rows.append(f"{rel}\t{coords}\t{hashlib.sha256(jar.read_bytes()).hexdigest()}")
(root / "BUNDLED-JARS.txt").write_text("\n".join(rows) + "\n", newline="\n")
bundled = {r.split("\t")[0] for r in rows}
listed = set(re.findall(r"^\| `([^`]+\.jar)` \|", table.read_text(encoding="utf-8"), re.M))
if bundled != listed:
    sys.exit(f"bundle: licence table differs from the jars. Missing rows: {sorted(bundled - listed)}; extra rows: {sorted(listed - bundled)}")
print(f"bundle: {len(bundled)} jars, all in the licence table")
EOF

# 4. Windows path limit headroom: %LOCALAPPDATA%\LocalCorrect\app\ plus these must stay under 260.
long=$(cd "$out" && find languagetool jre -print | awk 'length($0) > 150')
[[ -z $long ]] || fail "paths longer than 150 characters: $long"

# 5. Smoke test: the bundled runtime must start the server and correct German and English.
if [[ $smoke == --smoke ]]; then
  java=$out/jre/bin/java
  [[ -x $java.exe ]] && java=$java.exe
  port=18081
  log=$cache/smoke-server.log
  (cd "$out/languagetool" && exec "$java" -Xmx768m -Dfile.encoding=UTF-8 -Djava.net.preferIPv4Stack=true \
    -cp languagetool-server.jar org.languagetool.server.HTTPServer --port $port \
    --config ../languagetool-server.properties) >"$log" 2>&1 &
  pid=$!
  stop() {
    if [[ -r /proc/$pid/winpid ]]; then taskkill //F //T //PID "$(cat /proc/$pid/winpid)" >/dev/null 2>&1 || true; fi
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  }
  trap stop EXIT
  up=
  for _ in $(seq 1 180); do
    curl -fs "http://127.0.0.1:$port/v2/languages" >/dev/null 2>&1 && { up=1; break; }
    kill -0 "$pid" 2>/dev/null || break
    sleep 0.5
  done
  [[ -n $up ]] || { cat "$log"; fail "server did not start"; }
  check() { # language text expected...
    local lang=$1 res; shift
    # The text goes through a UTF-8 file: Windows would re-encode a command-line argument to curl.exe.
    printf '%s' "$1" > "$cache/smoke-text.txt"; shift
    res=$(cd "$cache" && curl -fsS --max-time 240 -w '\n%{time_total}' --data-urlencode "language=$lang" \
      --data-urlencode "text@smoke-text.txt" "http://127.0.0.1:$port/v2/check") \
      || { cat "$log"; fail "$lang check failed"; }
    for want in "$@"; do
      [[ $res == *"\"value\":\"$want\""* ]] || { echo "$res"; fail "$lang: no suggestion \"$want\""; }
    done
    echo "bundle: $lang corrected in ${res##*$'\n'} s"
  }
  check de-DE "Ich habe heute keine zeit, aber morgen könen wir uns treffen." Zeit können
  check en-US "I will recieve the parcel tomorrow and we have alot of work." receive "a lot"
  check de-DE "Wir haben leider keine zeit für das Treffen." Zeit
fi

echo "bundle: $(cd "$out" && du -sh --apparent-size languagetool jre | tr '\n' ' ')"
