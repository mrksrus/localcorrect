# LanguageTool's Java libraries

One row per jar shipped in `languagetool\` (LanguageTool 6.6, after the trim described in
[THIRD_PARTY.md](../THIRD_PARTY.md)). The jars are unmodified copies from the official
`LanguageTool-6.6.zip`. `languagetool\BUNDLED-JARS.txt`, written by `build/bundle-languagetool.sh`, lists the
same jars with their SHA-256; the build fails when the jars and this table differ.

How the rows were filled: Maven coordinates come from the jar's own `META-INF/maven/.../pom.properties`.
37 jars carry no Maven metadata; they were identified on Maven Central by their SHA-1 (exact file match).
The licence comes from the jar's own `pom.xml`, or from its parent pom on Maven Central when the jar's pom
does not name one, or from the pom on Maven Central for the jars without metadata.
LanguageTool's own list (`languagetool\third-party-licenses\README.txt`) is shipped too, but it names
older versions.

Licence texts: `licenses\` in this package (file names are the SPDX identifiers) and
`languagetool\third-party-licenses\`. Copyright notices of each library are inside its jar.

¹ The pom says only "BSD". The variant is taken from the project's licence file for that version
(Morfologik: `languagetool\third-party-licenses\Morfologik-license.txt`; units of measurement: the
`unitsofmeasurement` repositories at tags `1.0`, `1.1`, `1.3`). All are BSD-3-Clause.

"Eclipse Distribution License 1.0" is the BSD-3-Clause licence (SPDX `BSD-3-Clause`).

| Jar | Maven coordinates | Version | Licence | Coordinates from | Licence as declared |
| --- | --- | --- | --- | --- | --- |
| `languagetool-server.jar` | `org.languagetool:languagetool-server` | 6.6 | LGPL-2.1-or-later | jar pom | GNU Lesser General Public License (pom) |
| `libs/HdrHistogram.jar` | `org.hdrhistogram:HdrHistogram` | 2.1.12 | CC0-1.0 OR BSD-2-Clause | jar pom | Public Domain, per Creative Commons CC0; BSD-2-Clause (pom) |
| `libs/LatencyUtils.jar` | `org.latencyutils:LatencyUtils` | 2.0.3 | CC0-1.0 | jar pom | Public Domain, per Creative Commons CC0 (pom) |
| `libs/aho-corasick-double-array-trie.jar` | `com.hankcs:aho-corasick-double-array-trie` | 1.2.2 | Apache-2.0 | jar pom | Apache License Version 2.0 (pom) |
| `libs/angus-activation.jar` | `org.eclipse.angus:angus-activation` | 2.0.2 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | EDL 1.0 (parent org.eclipse.angus:angus-activation-project:2.0.2) |
| `libs/annotations.jar` | `com.intellij:annotations` | 12.0 | Apache-2.0 | Maven Central (SHA-1) | Apache License 2 (pom) |
| `libs/bcrypt.jar` | `at.favre.lib:bcrypt` | 0.10.2 | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent at.favre.lib:bcrypt-parent:0.10.2) |
| `libs/bytes.jar` | `at.favre.lib:bytes` | 1.5.0 | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent at.favre.lib:common-parent:12) |
| `libs/caffeine.jar` | `com.github.ben-manes.caffeine:caffeine` | 3.2.0 | Apache-2.0 | Maven Central (SHA-1) | Apache License, Version 2.0 (pom) |
| `libs/checker-qual.jar` | `org.checkerframework:checker-qual` | 3.43.0 | MIT | Maven Central (SHA-1) | The MIT License (pom) |
| `libs/commons-cli.jar` | `commons-cli:commons-cli` | 1.9.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:72) |
| `libs/commons-codec.jar` | `commons-codec:commons-codec` | 1.17.1 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:71) |
| `libs/commons-collections4.jar` | `org.apache.commons:commons-collections4` | 4.4 | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent org.apache.commons:commons-parent:48) |
| `libs/commons-digester.jar` | `commons-digester:commons-digester` | 2.1 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent org.apache.commons:commons-parent:17) |
| `libs/commons-io.jar` | `commons-io:commons-io` | 2.17.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:74) |
| `libs/commons-lang3.jar` | `org.apache.commons:commons-lang3` | 3.17.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:73) |
| `libs/commons-logging.jar` | `commons-logging:commons-logging` | 1.3.4 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:72) |
| `libs/commons-pool2.jar` | `org.apache.commons:commons-pool2` | 2.12.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:62) |
| `libs/commons-text.jar` | `org.apache.commons:commons-text` | 1.12.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:69) |
| `libs/commons-validator.jar` | `commons-validator:commons-validator` | 1.9.0 | Apache-2.0 | jar pom | Apache-2.0 (parent org.apache.commons:commons-parent:70) |
| `libs/ecs-logging-core.jar` | `co.elastic.logging:ecs-logging-core` | 1.3.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent co.elastic.logging:ecs-logging-java-parent:1.3.2) |
| `libs/english-pos-dict.jar` | `org.languagetool:english-pos-dict` | 0.6 | LGPL-2.1 | jar pom | GNU Lesser General Public License, Version 2.1, February 1999 (pom) |
| `libs/error_prone_annotations.jar` | `com.google.errorprone:error_prone_annotations` | 2.28.0 | Apache-2.0 | jar pom | Apache 2.0 (pom) |
| `libs/failureaccess.jar` | `com.google.guava:failureaccess` | 1.0.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent com.google.guava:guava-parent:26.0-android) |
| `libs/fastutil-core.jar` | `it.unimi.dsi:fastutil-core` | 8.5.14 | Apache-2.0 | Maven Central (SHA-1) | Apache License, Version 2.0 (pom) |
| `libs/german-pos-dict.jar` | `de.danielnaber:german-pos-dict` | 1.2.4 | CC-BY-SA-4.0 | jar pom | Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0) (pom) |
| `libs/grpc-api.jar` | `io.grpc:grpc-api` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-context.jar` | `io.grpc:grpc-context` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-core.jar` | `io.grpc:grpc-core` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-netty-shaded.jar` | `io.grpc:grpc-netty-shaded` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-protobuf-lite.jar` | `io.grpc:grpc-protobuf-lite` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-protobuf.jar` | `io.grpc:grpc-protobuf` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-stub.jar` | `io.grpc:grpc-stub` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/grpc-util.jar` | `io.grpc:grpc-util` | 1.68.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/gson.jar` | `com.google.code.gson:gson` | 2.11.0 | Apache-2.0 | jar pom | Apache-2.0 (pom) |
| `libs/guava.jar` | `com.google.guava:guava` | 33.3.1-jre | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent com.google.guava:guava-parent:33.3.1-jre) |
| `libs/hppc.jar` | `com.carrotsearch:hppc` | 0.8.2 | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent com.carrotsearch:hppc-parent:0.8.2) |
| `libs/hunspell.jar` | `com.gitlab.dumonts:hunspell` | 2.1.2 | Apache-2.0 | jar pom | Apache License 2.0 (pom) |
| `libs/icu4j.jar` | `com.ibm.icu:icu4j` | 75.1 | Unicode-3.0 | jar pom | Unicode-3.0 (parent com.ibm.icu:icu4j-root:75.1) |
| `libs/indriya.jar` | `tech.units:indriya` | 1.3 | BSD-3-Clause ¹ | jar pom | BSD (pom) |
| `libs/istack-commons-runtime.jar` | `com.sun.istack:istack-commons-runtime` | 4.1.2 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | Eclipse Distribution License - v 1.0 (parent com.sun.istack:istack-commons:4.1.2) |
| `libs/j2objc-annotations.jar` | `com.google.j2objc:j2objc-annotations` | 3.0.0 | Apache-2.0 | jar pom | Apache License, Version 2.0 (pom) |
| `libs/jackson-annotations.jar` | `com.fasterxml.jackson.core:jackson-annotations` | 2.18.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/jackson-core.jar` | `com.fasterxml.jackson.core:jackson-core` | 2.18.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/jackson-databind.jar` | `com.fasterxml.jackson.core:jackson-databind` | 2.18.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/jakarta.activation-api.jar` | `jakarta.activation:jakarta.activation-api` | 2.1.3 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | EDL 1.0 (pom) |
| `libs/jakarta.activation.jar` | `com.sun.activation:jakarta.activation` | 1.2.2 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | EDL 1.0 (parent com.sun.activation:all:1.2.2) |
| `libs/jakarta.xml.bind-api.jar` | `jakarta.xml.bind:jakarta.xml.bind-api` | 2.3.3 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | Eclipse Distribution License - v 1.0 (parent jakarta.xml.bind:jakarta.xml.bind-api-parent:2.3.3) |
| `libs/java-diff-utils.jar` | `io.github.java-diff-utils:java-diff-utils` | 4.12 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent io.github.java-diff-utils:java-diff-utils-parent:4.12) |
| `libs/javax.activation-api.jar` | `javax.activation:javax.activation-api` | 1.2.0 | CDDL-1.1 OR GPL-2.0-only WITH Classpath-exception-2.0 | jar pom | CDDL/GPLv2+CE (parent com.sun.activation:all:1.2.0) |
| `libs/javax.annotation-api.jar` | `javax.annotation:javax.annotation-api` | 1.3.2 | CDDL-1.1 OR GPL-2.0-only WITH Classpath-exception-2.0 | jar pom | CDDL + GPLv2 with classpath exception (pom) |
| `libs/jaxb-api.jar` | `javax.xml.bind:jaxb-api` | 2.3.1 | CDDL-1.1 OR GPL-2.0-only WITH Classpath-exception-2.0 | jar pom | CDDL 1.1; GPL2 w/ CPE (parent javax.xml.bind:jaxb-api-parent:2.3.1) |
| `libs/jaxb-core.jar` | `org.glassfish.jaxb:jaxb-core` | 2.3.0.1 | CDDL-1.1 OR GPL-2.0-only WITH Classpath-exception-2.0 | jar pom | CDDL+GPL License (parent com.sun.xml.bind.mvn:jaxb-parent:2.3.0.1) |
| `libs/jaxb-impl.jar` | `com.sun.xml.bind:jaxb-impl` | 2.3.3 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | Eclipse Distribution License - v 1.0 (parent com.sun.xml.bind.mvn:jaxb-bundles:2.3.3) |
| `libs/jaxb-runtime.jar` | `org.glassfish.jaxb:jaxb-runtime` | 4.0.5 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | Eclipse Distribution License - v 1.0 (parent com.sun.xml.bind.mvn:jaxb-runtime-parent:4.0.5) |
| `libs/jcl-over-slf4j.jar` | `org.slf4j:jcl-over-slf4j` | 2.0.7 | Apache-2.0 | jar pom | Apache License, Version 2.0 (pom) |
| `libs/jcommander.jar` | `com.beust:jcommander` | 1.78 | Apache-2.0 | Maven Central (SHA-1) | Apache License, Version 2.0 (pom) |
| `libs/jna-platform.jar` | `net.java.dev.jna:jna-platform` | 5.12.1 | LGPL-2.1-or-later OR Apache-2.0 | Maven Central (SHA-1) | LGPL-2.1-or-later; Apache-2.0 (pom) |
| `libs/jna.jar` | `net.java.dev.jna:jna` | 5.12.1 | LGPL-2.1-or-later OR Apache-2.0 | Maven Central (SHA-1) | LGPL-2.1-or-later; Apache-2.0 (pom) |
| `libs/json.jar` | `org.json:json` | 20240303 | Public domain (JSON-java) | jar pom | Public Domain (pom) |
| `libs/jsonic.jar` | `net.arnx:jsonic` | 1.2.11 | Apache-2.0 | Maven Central (SHA-1) | The Apache Software License, Version 2.0 (pom) |
| `libs/jspecify.jar` | `org.jspecify:jspecify` | 1.0.0 | Apache-2.0 | Maven Central (SHA-1) | The Apache License, Version 2.0 (pom) |
| `libs/jsr305.jar` | `com.google.code.findbugs:jsr305` | 3.0.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/jwordsplitter.jar` | `de.danielnaber:jwordsplitter` | 4.7 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/language-detector.jar` | `com.optimaize.languagedetector:language-detector` | 0.6 | Apache-2.0 | jar pom | Apache 2 (pom) |
| `libs/languagetool-core.jar` | `org.languagetool:languagetool-core` | 6.6 | LGPL-2.1-or-later | jar pom | GNU Lesser General Public License (pom) |
| `libs/languagetool-gui-commons.jar` | `org.languagetool:languagetool-gui-commons` | 6.6 | LGPL-2.1-or-later | jar pom | GNU Lesser General Public License (pom) |
| `libs/languagetool-tools.jar` | `org.languagetool:languagetool-tools` | 6.6 | LGPL-2.1-or-later | jar pom | GNU Lesser General Public License (pom) |
| `libs/lettuce-core.jar` | `io.lettuce:lettuce-core` | 6.5.4.RELEASE | MIT | jar pom | MIT (pom) |
| `libs/linguistics.grammardb.spell.languagetool.jar` | `io.github.belarus:linguistics.grammardb.spell.languagetool` | 1.0.2 | CC-BY-SA-4.0 | Maven Central (SHA-1) | CC-BY-SA-4.0 license (pom) |
| `libs/listenablefuture.jar` | `com.google.guava:listenablefuture` | 9999.0-empty-to-avoid-conflict-with-guava | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent com.google.guava:guava-parent:26.0-android) |
| `libs/logback-classic.jar` | `ch.qos.logback:logback-classic` | 1.5.8 | EPL-1.0 OR LGPL-2.1-only | jar pom | Eclipse Public License - v 1.0; GNU Lesser General Public License (parent ch.qos.logback:logback-parent:1.5.8) |
| `libs/logback-core.jar` | `ch.qos.logback:logback-core` | 1.5.8 | EPL-1.0 OR LGPL-2.1-only | jar pom | Eclipse Public License - v 1.0; GNU Lesser General Public License (parent ch.qos.logback:logback-parent:1.5.8) |
| `libs/logback-ecs-encoder.jar` | `co.elastic.logging:logback-ecs-encoder` | 1.3.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent co.elastic.logging:ecs-logging-java-parent:1.3.2) |
| `libs/lucene-backward-codecs.jar` | `org.apache.lucene:lucene-backward-codecs` | 5.5.5 | Apache-2.0 | Maven Central (SHA-1) | Apache 2 (parent org.apache.lucene:lucene-parent:5.5.5) |
| `libs/lucene-core.jar` | `org.apache.lucene:lucene-core` | 5.5.5 | Apache-2.0 | Maven Central (SHA-1) | Apache 2 (parent org.apache.lucene:lucene-parent:5.5.5) |
| `libs/mariadb-java-client.jar` | `org.mariadb.jdbc:mariadb-java-client` | 3.4.1 | LGPL-2.1 | jar pom | LGPL-2.1 (pom) |
| `libs/micrometer-core.jar` | `io.micrometer:micrometer-core` | 1.7.12 | Apache-2.0 | Maven Central (SHA-1) | The Apache Software License, Version 2.0 (pom) |
| `libs/micrometer-registry-prometheus.jar` | `io.micrometer:micrometer-registry-prometheus` | 1.7.12 | Apache-2.0 | Maven Central (SHA-1) | The Apache Software License, Version 2.0 (pom) |
| `libs/morfologik-fsa-builders.jar` | `org.carrot2:morfologik-fsa-builders` | 2.1.9 | BSD-3-Clause ¹ | Maven Central (SHA-1) | BSD (parent org.carrot2:morfologik-parent:2.1.9) |
| `libs/morfologik-fsa.jar` | `org.carrot2:morfologik-fsa` | 2.1.9 | BSD-3-Clause ¹ | Maven Central (SHA-1) | BSD (parent org.carrot2:morfologik-parent:2.1.9) |
| `libs/morfologik-speller.jar` | `org.carrot2:morfologik-speller` | 2.1.9 | BSD-3-Clause ¹ | Maven Central (SHA-1) | BSD (parent org.carrot2:morfologik-parent:2.1.9) |
| `libs/morfologik-stemming.jar` | `org.carrot2:morfologik-stemming` | 2.1.9 | BSD-3-Clause ¹ | Maven Central (SHA-1) | BSD (parent org.carrot2:morfologik-parent:2.1.9) |
| `libs/morfologik-tools.jar` | `org.carrot2:morfologik-tools` | 2.1.9 | BSD-3-Clause ¹ | Maven Central (SHA-1) | BSD (parent org.carrot2:morfologik-parent:2.1.9) |
| `libs/morphology-el.jar` | `org.ioperm:morphology-el` | 1.0.0 | Apache-2.0 AND CC-BY-SA-4.0 | jar pom | Apache License, Version 2.0; Creative Commons Attribution-ShareAlike 4.0 International License (pom) |
| `libs/mybatis.jar` | `org.mybatis:mybatis` | 3.5.16 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (parent org.mybatis:mybatis-parent:42) |
| `libs/netty-buffer.jar` | `io.netty:netty-buffer` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-codec.jar` | `io.netty:netty-codec` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-common.jar` | `io.netty:netty-common` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-handler.jar` | `io.netty:netty-handler` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-resolver.jar` | `io.netty:netty-resolver` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-transport-native-unix-common.jar` | `io.netty:netty-transport-native-unix-common` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/netty-transport.jar` | `io.netty:netty-transport` | 4.1.118.Final | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent io.netty:netty-parent:4.1.118.Final) |
| `libs/opennlp-chunk-models.jar` | `edu.washington.cs.knowitall:opennlp-chunk-models` | 1.5 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/opennlp-postag-models.jar` | `edu.washington.cs.knowitall:opennlp-postag-models` | 1.5 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/opennlp-tokenize-models.jar` | `edu.washington.cs.knowitall:opennlp-tokenize-models` | 1.5 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/opennlp-tools.jar` | `org.apache.opennlp:opennlp-tools` | 1.9.4 | Apache-2.0 | jar pom | Apache License, Version 2.0 (parent org.apache.opennlp:opennlp:1.9.4) |
| `libs/openregex.jar` | `edu.washington.cs.knowitall:openregex` | 1.1.1 | LGPL (version not stated; the pom links the current LGPL, 3.0) | jar pom | LGPL (GNU Lesser General Public License) (pom) |
| `libs/opentelemetry-api.jar` | `io.opentelemetry:opentelemetry-api` | 1.42.1 | Apache-2.0 | Maven Central (SHA-1) | The Apache License, Version 2.0 (pom) |
| `libs/opentelemetry-context.jar` | `io.opentelemetry:opentelemetry-context` | 1.42.1 | Apache-2.0 | Maven Central (SHA-1) | The Apache License, Version 2.0 (pom) |
| `libs/opentelemetry-semconv.jar` | `io.opentelemetry:opentelemetry-semconv` | 1.30.1-alpha | Apache-2.0 | Maven Central (SHA-1) | The Apache License, Version 2.0 (pom) |
| `libs/perfmark-api.jar` | `io.perfmark:perfmark-api` | 0.27.0 | Apache-2.0 | Maven Central (SHA-1) | Apache 2.0 (pom) |
| `libs/proto-google-common-protos.jar` | `com.google.api.grpc:proto-google-common-protos` | 2.41.0 | Apache-2.0 | jar pom | Apache-2.0 (pom) |
| `libs/protobuf-java.jar` | `com.google.protobuf:protobuf-java` | 3.25.5 | BSD-3-Clause | Maven Central (SHA-1) | BSD-3-Clause (parent com.google.protobuf:protobuf-parent:3.25.5) |
| `libs/reactive-streams.jar` | `org.reactivestreams:reactive-streams` | 1.0.4 | MIT-0 | Maven Central (SHA-1) | MIT-0 (pom) |
| `libs/reactor-core.jar` | `io.projectreactor:reactor-core` | 3.6.6 | Apache-2.0 | Maven Central (SHA-1) | Apache License, Version 2.0 (pom) |
| `libs/resilience4j-circuitbreaker.jar` | `io.github.resilience4j:resilience4j-circuitbreaker` | 1.7.1 | Apache-2.0 | Maven Central (SHA-1) | Apache-2.0 (pom) |
| `libs/resilience4j-core.jar` | `io.github.resilience4j:resilience4j-core` | 1.7.1 | Apache-2.0 | Maven Central (SHA-1) | Apache-2.0 (pom) |
| `libs/resilience4j-micrometer.jar` | `io.github.resilience4j:resilience4j-micrometer` | 1.7.1 | Apache-2.0 | Maven Central (SHA-1) | Apache-2.0 (pom) |
| `libs/segment.jar` | `net.loomchild:segment` | 2.0.3 | MIT | jar pom | MIT License (pom) |
| `libs/simpleclient.jar` | `io.prometheus:simpleclient` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_common.jar` | `io.prometheus:simpleclient_common` | 0.10.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_guava.jar` | `io.prometheus:simpleclient_guava` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_hotspot.jar` | `io.prometheus:simpleclient_hotspot` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_httpserver.jar` | `io.prometheus:simpleclient_httpserver` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_tracer_common.jar` | `io.prometheus:simpleclient_tracer_common` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_tracer_otel.jar` | `io.prometheus:simpleclient_tracer_otel` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/simpleclient_tracer_otel_agent.jar` | `io.prometheus:simpleclient_tracer_otel_agent` | 0.16.0 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/slf4j-api.jar` | `org.slf4j:slf4j-api` | 2.0.16 | MIT | jar pom | MIT License (parent org.slf4j:slf4j-parent:2.0.16) |
| `libs/txw2.jar` | `org.glassfish.jaxb:txw2` | 4.0.5 | BSD-3-Clause (Eclipse Distribution License 1.0) | jar pom | Eclipse Distribution License - v 1.0 (parent com.sun.xml.bind.mvn:jaxb-txw-parent:4.0.5) |
| `libs/unit-api.jar` | `javax.measure:unit-api` | 1.0 | BSD-3-Clause ¹ | jar pom | BSD (pom) |
| `libs/uom-lib-common.jar` | `tech.uom.lib:uom-lib-common` | 1.1 | BSD-3-Clause ¹ | jar pom | BSD (parent tech.uom.lib:uom-lib:1.1) |
| `libs/vavr-match.jar` | `io.vavr:vavr-match` | 0.10.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/vavr.jar` | `io.vavr:vavr` | 0.10.2 | Apache-2.0 | jar pom | The Apache Software License, Version 2.0 (pom) |
| `libs/waffle-jna.jar` | `com.github.waffle:waffle-jna` | 3.3.0 | MIT | jar pom | MIT (parent com.github.waffle:waffle-parent:3.3.0) |
