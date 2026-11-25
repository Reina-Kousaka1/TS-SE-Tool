# TS-SE-Tool – kritischer Audit und Next-Generation-Plan

**Stand:** 25. November 2026

**Geprüfter Fork:** Reina-Kousaka1/TS-SE-Tool, echter Fork von LIPtoH/TS-SE-Tool

**Prüfbasis:** master bei d97025f6a0a31b5952e5b524b50e0e0def630928 (Version 0.3.1.0); Vergleich mit upstream/dev-branch bei 440272bbaa5702c57edab306320aaf1ce64b4a05

**Zweck:** statische Bestandsaufnahme, Priorisierung gemeldeter Probleme und Architektur-/Testplan. Keine Neuentwicklung und keine Änderung am Originalrepository.

## 1. Prüfumfang und Aussagekraft

Der Fork wurde vor der Analyse unter unverändertem Namen erstellt. GitHub weist ihn als Fork mit Parent und Source LIPtoH/TS-SE-Tool aus; die LICENSE-Datei enthält Apache License 2.0. Der lokale master entspricht dem geprüften Basis-Commit. Die Analyse erfolgte im Fork; upstream/master wurde nicht verändert. Die Dokumentation liegt auf einem separaten Audit-Branch.

Untersucht wurden Solution und Projektdateien, WinForms- und Savegame-Code, SII-Parser/Serializer, native Entschlüsselungsschnittstelle, Profilpfade, Fahrzeuge/Zubehör, Logdaten, Abhängigkeiten, verfügbare Teststruktur sowie die gemeldeten Issues und PRs #147/#149. Für Fehlerberichte wurde der Status aus drei Blickwinkeln festgehalten:

- **Im geprüften Quellstand bestätigt:** Der Code enthält den beschriebenen gefährlichen Pfad oder eine konkrete strukturelle Begrenzung. Das bedeutet nicht automatisch, dass jede Nutzermeldung mit einem echten Spielstand reproduziert wurde.
- **Plausibel, nicht reproduziert:** Code oder Architektur machen den Bericht nachvollziehbar, aber Ursache und Reproduktionsfall sind nicht belegt.
- **Nur berichtet:** Issue-Kommentare oder Nutzermeldungen liegen vor, aber aus dem geprüften Quellstand ergibt sich noch keine bestätigte Ursache.

Der lokale master ist sehr alt. Deshalb dürfen Befunde über diesen Commit nicht ungeprüft auf die ausgelieferte Version 0.3.11.0 oder PR-Branches übertragen werden. Insbesondere wurde kein echter oder bereitgestellter Spielstand geöffnet, geändert oder für Tests verwendet. Es wurden keine Tests ausgeführt.

## 2. Repository- und Architekturübersicht

| Bereich | Befund |
|---|---|
| Lösung | Eine ältere MSBuild-Solution mit Hauptprogramm und separatem Updater. |
| Anwendung | C# WinForms, .NET Framework 4.7.2, x86/x64-Konfigurationen, UI, Profilzugriff und Savegame-Logik eng gekoppelt. Der Quellstand trägt Version 0.3.1.0. |
| Savegame-Parser | Handgeschriebener SII-Parser mit Fallunterscheidungen und vielen typisierten Klassen. SiiNunit führt erkannte Blöcke über einen Dictionary<string, dynamic>; unbekannte Blocktypen werden als Unidentified aufbewahrt. Unbekannte Attribute innerhalb eines erkannten Typs werden im master nicht allgemein verlustfrei modelliert. |
| Serializer | Handgeschriebene PrintOut-Methoden schreiben festgelegte Felder und Reihenfolgen. Der zentrale Serializer traversiert Blockbeziehungen über Dictionary-Schlüssel. Ein fehlender oder falsch geparster Schlüssel kann während der Ausgabe eine Ausnahme auslösen. |
| Dekodierung | Native SII_Decrypt.dll über P/Invoke; unterstützt im Wrapper die erkannten Formate Plaintext, encrypted, binary und 3NK. Ein fehlendes/unpassendes natives Binary oder nicht erkanntes Format führt zum Decode-Fehler. Die Format-/Versionsmatrix wird nicht als eigenständiger, testbarer Vertrag dargestellt. |
| Profile | Ermittlung lokaler Dokumente-Profile, Steam-Remote-Profile und benutzerdefinierter Pfade. Steam-Cloud-Saves werden beim Laden bewusst blockiert und der Nutzer wird zum Deaktivieren der Cloud aufgefordert. |
| Fahrzeug-/Busdaten | Allgemeine vehicle- und Fahrzeug-Zubehörtypen; Bus-Statistiken, Busstopps und Busjob-Log sind vorhanden. Eine eigenständige Coach-/Busfahrzeugklasse oder vollständig nachgewiesene Coaches-DLC-Roundtrips wurden nicht gefunden. |
| Analytics-Daten | Profit_log_Entry enthält unter anderem revenue, wage, maintenance, fuel, distance und timestamp_day; Delivery-Log-Einträge enthalten eine Liste und einen Cache-Zähler. Wie vollständig und wie lange diese historischen Reihen in aktuellen Spielversionen erhalten bleiben, ist nicht belegt. |
| Abhängigkeiten | SharpZipLib, SQL Server Compact/ErikEJ, Salient.Data sowie separate ColorPicker- und native SII-Decrypt-Dateien. Teile werden als lokale DLL-/HintPath-Abhängigkeiten referenziert. Die tatsächliche Wartbarkeit, Herkunft und Architekturkompatibilität jeder Binärdatei ist vor einer Migration einzeln zu inventarisieren. |
| Tests/CI | Im geprüften master wurde kein separates Testprojekt, kein Testordner und keine GitHub-Actions-Workflowdatei gefunden. PR #147 ergänzt einen In-App-Selbsttest, aber keinen Ersatz für automatisierte Parser-/Serializer-Regressionstests. |
| Lizenz | LICENSE: Apache-2.0. Bei Übernahme von Code, Assets oder Binärdateien aus PRs sind Copyright-, Lizenz- und Provenienzangaben separat zu prüfen. |

Der Quellstand auf master stammt vom 24. März 2022. upstream/dev-branch liegt 160 Commits weiter und enthält laut Git-Vergleich 131 geänderte Dateien (netto ungefähr +12.524/-4.346 Zeilen), ist aber zuletzt am 5. Dezember 2023 geändert worden. Auch dieser Branch belegt daher keine Kompatibilität mit den 2026 gemeldeten Save-Versionen. Der Inhalt wurde punktuell verglichen, nicht als fertiger Kandidat vollständig erneut geprüft.

## 3. Priorisierte Fehler- und Risikoliste

P0 bedeutet unmittelbares Risiko für Verlust oder Beschädigung eines bestehenden Spielstands. P1 betrifft zentrale Datenkorrektheit, Spielversionen oder häufige Kernfunktionen. P2 betrifft wichtige Kompatibilitäts-, Profil-, Start- oder Stabilitätsprobleme mit begrenzterem bzw. unbestätigtem Umfang. P3 bezeichnet Produktlücken und Verbesserungen ohne belegten akuten Datenverlust. Priorität und Beweislage sind getrennte Angaben.

| Priorität | Issue / Befund | Beweislage und Bewertung |
|---|---|---|
| **P0** | [#148 – game.sii wird bei Schreibfehler auf 0 Byte gesetzt; Felder fehlen oder werden ergänzt](https://github.com/LIPtoH/TS-SE-Tool/issues/148), zusammen mit [#108 – KeyNotFoundException in PrintOut](https://github.com/LIPtoH/TS-SE-Tool/issues/108) | **Destruktiver Schreibpfad im geprüften master bestätigt.** NewWrireSaveFile öffnet game.sii mit StreamWriter(path, false) und ruft PrintOut erst im Anschluss auf. Das Öffnen kürzt die vorhandene Datei; scheitert danach Parsing/Serialisierung, bleibt die Originaldatei leer oder unvollständig. SiiNunit.PrintOut greift unter anderem auf SiiNitems[Economy.registry] zu und kann bei fehlendem Schlüssel eine Ausnahme werfen. Der im Issue beschriebene genaue v97-Auslöser, die Zahl von 322 verlorenen bzw. 454 erfundenen Attributen und der konkrete Lauf in Version 0.3.11.0 wurden hier nicht reproduziert. Die gemeldete Schadensfolge ist ernst genug, um bis zu einer nachweislich sicheren Implementierung Schreibzugriffe als nicht vertrauenswürdig einzustufen. [Schreibmethode](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/MethodsReadWrite.cs#L937-L968) · [Serializerzugriff](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/Items/SiiNunit.cs#L878-L880) |
| **P1** | [#120 – Endlosschleife bei hohem Spielerlevel](https://github.com/LIPtoH/TS-SE-Tool/issues/120) | **Unbeschränkte Schleife im master bestätigt.** Economy.getPlayerLvl und SaveFileProfileData.getPlayerLvl addieren mit int-Schwellenwerten und laufen nach der Leveltabelle in do/while(true). Bei hinreichend hohen Werten droht Integer-Überlauf; eine garantierte Abbruchbedingung fehlt. Der Codepfad ist klar, ein Lauf mit großem Level wurde nicht ausgeführt. [Economy](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/Items/Economy.cs#L1372-L1397) · [Profil](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/SaveFileProfileData.cs#L321-L348) |
| **P1** | [#142 – Farben von Zubehör werden beim Speichern zurückgesetzt](https://github.com/LIPtoH/TS-SE-Tool/issues/142) | **Plausible Modelllücke, Bericht nicht reproduziert.** Vehicle_Addon_Accessory kennt im master slot_name, slot_hookup, data_path und refund und serialisiert genau diese Felder; ein paint_color-Feld fehlt. Das ist ein konkreter Verdacht, aber vor einer endgültigen Ursachenbehauptung muss anhand einer synthetischen oder ausdrücklich bereitgestellten Zubehördatei geklärt werden, in welchem Block das Spiel die betroffene Farbe tatsächlich speichert. [Modell und Serializer](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/Items/Vehicle_Addon_Accessory.cs#L12-L123) |
| **P1** | [#140 – Ladung verschwindet nach Reparatur](https://github.com/LIPtoH/TS-SE-Tool/issues/140) | **Nur berichtet; Ursache nicht ermittelt.** Die Reparaturpfade setzen Fahrzeug-/Trailerzustand zurück. Im geprüften Ausschnitt ist kein ausreichend belegter direkter Löschpfad für cargo nachgewiesen. Ohne Vorher-/Nachher-Fixture darf weder das Reparaturverhalten noch der betroffene cargo-Link als Ursache behauptet werden. Erforderlich sind getrennte Regressionen für Truck, Trailer, aktive Lieferung, Cargo-Market und Spielversion. |
| **P1** | [#67 – beschädigte Saves/Entschlüsselung und großer cargo_model_index](https://github.com/LIPtoH/TS-SE-Tool/issues/67) | **Historischer Bericht mit aktuellem Typ-Risiko.** Der Issue-Kommentar nennt cargo_model_index = 3130117026; der master parst diesen Wert mit int.Parse in ein vorzeichenbehaftetes int. Dieser konkrete Wert liegt außerhalb des positiven Int32-Bereichs und würde nicht geparst. Der Report stammt aus alten Versionen und ist nicht reproduziert; die v0.3.11.0-Implementierung sowie die aktuelle Decoder-DLL wurden nicht geprüft. [Feld und Parser](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/Items/Player_Job.cs#L40-L48) · [Parsing](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/CustomClasses/Save/Items/Player_Job.cs#L210-L226) |
| **P1** | [#107 – Geldänderung verändert weitere Zeilen oder stürzt ab](https://github.com/LIPtoH/TS-SE-Tool/issues/107) | **Plausibles Roundtrip-Risiko, nicht reproduziert.** Erkannten Blockklassen werden bekannte Felder in festem Schema neu ausgegeben. Wenn unbekannte Attribute nicht aufgehoben werden, kann ein No-op-Roundtrip andere Inhalte ändern; die konkrete Zeilenabweichung aus dem Issue liegt nicht als prüfbare Fixture vor. |
| **P1** | [#109 – Spielstandfehler nach Bearbeitung von Kennzeichen mit SCS-Markup](https://github.com/LIPtoH/TS-SE-Tool/issues/109) | **Nur berichtet, nicht reproduziert.** Der Bericht betrifft Markup-Tags wie margin, img, ret, align und color in einem Kennzeichen. Eine feste Roundtrip-Fixture mit diesen Werten fehlt. Parser und Serializer müssen Markup als Nutzdaten behandeln und dürfen es nicht normalisieren oder aufteilen. |
| **P1** | [#143](https://github.com/LIPtoH/TS-SE-Tool/issues/143), [#144](https://github.com/LIPtoH/TS-SE-Tool/issues/144), [#145](https://github.com/LIPtoH/TS-SE-Tool/issues/145) – ETS2/ATS ab Version 1.60 bzw. SII v97 | **Mehrere aktuelle Nutzerberichte, lokal nicht reproduziert.** #144 enthält weitere Rückmeldungen, aber keine unabhängige Testmatrix. #145 ist weitgehend ein Duplikat. Die Versionsabdeckung bleibt ungeklärt. master ist von 2022, dev-branch von 2023; der Parser-/Decoderstand belegt daher keine Unterstützung für 1.60+. Eine Versionsanzeige oder erfolgreiche Dekodierung allein wäre kein Nachweis für verlustfreies Speichern. |
| **P2** | [#99 – Spielordner nicht gefunden](https://github.com/LIPtoH/TS-SE-Tool/issues/99), [#101 – lokale Profile als Steam-Profile beschriftet](https://github.com/LIPtoH/TS-SE-Tool/issues/101), [#131 – Profil nicht gefunden](https://github.com/LIPtoH/TS-SE-Tool/issues/131), [#135 – Standardpfad nicht gefunden](https://github.com/LIPtoH/TS-SE-Tool/issues/135) | **Profile-/Pfadprobleme gemeldet; gemischter Status.** Der Code liest einen Steam-Registrypfad, lässt einen WOW6432Node-Pfad auskommentiert, verschluckt Ausnahmen und wählt das zuletzt geänderte numerische Steam-userdata-Verzeichnis. Custom paths existieren. Das erklärt mögliche Fehlermodi, beweist aber nicht die Ursache jedes Issues. Profilauflösung sollte mehrere Installationen, Bibliotheken, Windows-Registryansichten, Dokumentumleitungen und explizite Auswahl unterstützen. [Profilermittlung](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/Forms/FormMainControlsMethods.cs#L376-L465) |
| **P2** | [#139 – Fenster öffnet sich nur im Hintergrund](https://github.com/LIPtoH/TS-SE-Tool/issues/139), [#90 – Fehler beim Bearbeiten von Geld](https://github.com/LIPtoH/TS-SE-Tool/issues/90) | **Offene Stabilitätsberichte, nicht reproduziert.** #139 liefert im Wesentlichen nur eine Prüfsumme und keinen verwertbaren Stacktrace. #90 beschreibt einen einzelnen WinForms-Fehler mit einer weiteren Rückmeldung. Diese Berichte brauchen reproduzierbare Logs, Betriebssystem-/DPI-Angaben und einen sauberen Startprofil-Test. |
| **P2** | [#82 – Start bleibt nach Updateprüfung hängen](https://github.com/LIPtoH/TS-SE-Tool/issues/82) | **Im Issue als umgebungsbedingt beantwortet, kein bestätigter aktueller Produktfehler.** Die Kommentare ordnen es einem problematischen Ordner im ETS2-Dokumentenverzeichnis zu. Trotzdem blockiert der aktuelle Startpfad beim Anzeigen eines modalen Splash-/Updatefensters synchron, bevor DetectGame weiterläuft. [Startpfad](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/FormMain.cs#L230-L250) |
| **P2** | [#42](https://github.com/LIPtoH/TS-SE-Tool/issues/42), [#81](https://github.com/LIPtoH/TS-SE-Tool/issues/81) – Steam-Cloud-Saves können nicht direkt geladen werden | **Bestätigte Produktbegrenzung, kein ungeklärter Fehler.** Der aktuelle Code deaktiviert die Load-Schaltfläche für Steam-Profile und fordert zum Abschalten der Cloud auf. Für einen lokalen Editor ist das eine sinnvolle Sicherheitsgrenze; die UX sollte Kopie/Import in ein lokales Profil erklären, ohne den Cloud-Originalstand anzutasten. [Cloud-Sperre](https://github.com/LIPtoH/TS-SE-Tool/blob/d97025f6a0a31b5952e5b524b50e0e0def630928/TS%20SE%20Tool/Forms/FormMainControlsMethods.cs#L986-L1005) |
| **P3** | Fehlende automatisierte Regressionen, Diagnoseverträge und dokumentierte unterstützte Versionen | **Im Repository-Inventar bestätigt.** Kein Testprojekt und keine CI-Workflows gefunden. Dies ist ein Prozess-/Wartungsrisiko, nicht selbst ein reproduzierbarer Spielstandfehler. |

## 4. Ursachenanalyse für die schwersten Befunde

### P0: Schreiben vor erfolgreicher Serialisierung

Der Speichercode öffnet die Originaldatei mit Überschreiben, bevor SiiNunitData.PrintOut ausgewertet wird. Eine Ausnahme beim Lookup, beim Parsing eines fehlenden/null-Werts, beim Traversieren eines Blocks oder beim Serialisieren kann nach dem Kürzen der Datei auftreten. Es gibt an dieser Stelle weder eine vollständige In-Memory-Validierung vor dem Öffnen noch einen temporären Commit mit atomarem Austausch. Deshalb kann selbst ein ansonsten erkannter Speicherfehler die Ausgangsdatei beschädigen.

Der Bericht #148 nennt game_time_initial: nil als Parseauslöser, danach einen abgebrochenen Economy-Parse und einen fehlenden registry-Schlüssel beim PrintOut. Im geprüften master wird game_time_initial als int geparst und der Parse-Catch beendet die Feldschleife mit break. Das passt strukturell zu einem frühen Abbruch, beweist aber nicht, dass alle Zwischenschritte in Version 0.3.11.0 identisch sind. Die exakte Kette bleibt eine Issue-Angabe, bis der passende Quellstand und eine synthetische Fixture dagegen geprüft wurden.

**Architekturregel:** Erst vollständig dekodieren, parsen, validieren und serialisieren. Danach eine Datei im selben Verzeichnis/Volume schreiben, flushen und gegen die alte Datei austauschen. Bei Austauschfehler muss die alte Datei lesbar und byte-identisch bleiben. Ein Backup und ein Recovery-Journal sind zusätzliche Sicherungen, kein Ersatz für diese Reihenfolge. Mehrere Dateien wie game.sii, info.sii und profile-Info brauchen einen nachvollziehbaren Transaktions-/Rollbackplan.

### P1: Verlust unbekannter Felder durch Schema-Serialisierung

Im master werden unbekannte Blocktypen als rohe Zeilen gespeichert. Für bekannte Typen bestimmt jedoch die jeweilige Klasse, welche Properties geparst und bei PrintOut ausgegeben werden. Neue Felder eines Spiels oder Mods können daher verloren gehen, obwohl der Blocktyp selbst bekannt ist. Umgekehrt kann eine Initialisierung mit Standardwerten Felder erzeugen, die im Input nicht vorhanden waren. Ein generischer „Unknown“-Fallback für Blocktypen reicht nicht für verlustfreies Roundtripping bekannter Blöcke.

Eine zukunftsfähige Engine sollte einen konkreten Syntaxbaum mit Quellreihenfolge, Rohwerten, Zeilenformat und unbekannten Feldern halten. Semantisch bekannte Werte können als typisierte Projektion angeboten werden. Beim No-op wird die Originaldarstellung erhalten; bei einer gezielten Bearbeitung werden nur die betroffenen Tokens ersetzt. Löschungen müssen explizit ausdrückbar sein, damit ein Merge-Verfahren nicht versehentlich absichtlich entfernte Attribute wieder einsetzt.

### P1: Zahlenbereiche und Fortschrittsberechnung

Der Parser legt mehrere SII-Werte als int ab und verwendet int-Schwellenwerte auch für die Levelberechnung. Diese Grenzen passen nicht automatisch zu den im Save gespeicherten unsigned/großen Zahlen. Parsegrenzen, Overflow, nil, fehlende Werte und ungewöhnlich hohe Levels müssen an der Datenmodellgrenze validiert werden. Eine begrenzte mathematische Berechnung oder checked-Operation mit aussagekräftigem Fehler ist sicherer als eine Schleife ohne Abbruchbedingung.

### P1: Spielversionen und Dekoder

Ein gespeichertes Format kann sich auf drei Ebenen ändern: Kompression/Verschlüsselung, SII-Syntax und Semantik einzelner Blöcke/Felder. Ein Decoder-Erfolg beweist deshalb nicht, dass ein Save verlustfrei roundtrip-fähig ist. Versionstests müssen ETS2 und ATS getrennt abdecken, und jede Spielversion braucht deklarierte Unterstützung pro Operation: lesen, unverändertes Schreiben und bearbeitetes Schreiben. Unbekannte oder nicht geprüfte Versionen sollten standardmäßig read-only sein.

## 5. Bewertung der Pull Requests

### PR #147 – gezielter Fix für v97-Schreibfehler und Feldverlust

[PR #147](https://github.com/LIPtoH/TS-SE-Tool/pull/147) ist offen, enthält 5 Commits (Head 2abf710e1fbc48ac6fb9036d9372d3b2c766db54) und basiert auf dev-branch bei 440272bbaa5702c57edab306320aaf1ce64b4a05, nicht auf dem alten master. Der Autor meldet No-op Roundtrips für 21 ATS-v97-Saves und einen gezielten XP-Edit; diese Tests sind im PR beschrieben, wurden in diesem Audit nicht unabhängig ausgeführt. Die Bewertung ist eine gezielte Prüfung der relevanten Parser-, Merge- und Schreibpfade, keine erneute Zeile-für-Zeile-Freigabe aller 64 geänderten Dateien. Es existiert im Basisrepository weiterhin kein automatisiertes Testprojekt; ein In-App-Selbsttest ersetzt keine isolierte, wiederholbare CI-Regression.

**Positiv:** Serialisierungstexte werden vor dem Dateizugriff erzeugt; Fehler sollen vor dem Schreiben sichtbar werden; ein Backup wird angelegt; unknown lines und zusätzliche profile user_data-Slots werden gezielter behandelt. Das adressiert Teile des Schadenspfads und zeigt eine passende Testidee.

**Blockierende Reviewpunkte:** Der untersuchte Helper schreibt in einen festen temporären Dateinamen, löscht dann die Originaldatei und verschiebt erst anschließend die temporäre Datei. Zwischen Delete und Move bleibt ein Verlustfenster; das ist kein atomarer Ersatz. Auch die Änderung mehrerer Save-Dateien ist keine Transaktion. Der generische OriginalBlockMerge kopiert ursprüngliche Zeilen, die im neuen Schema fehlen, und kann dadurch gewollte Löschungen wiederherstellen; neu erzeugte, unbekannte Nicht-Array-Felder können dagegen verworfen werden. Diese Richtlinien brauchen explizite Tests für Hinzufügen, Ändern, Entfernen, Arrays, Duplikate, Reihenfolge und Mod-Felder.

**Empfehlung:** Nicht ungeprüft übernehmen. Einzelne Parser-/Profil-Fixes als Kandidaten behandeln; vor Integration die Schreibmethode durch einen bewährten same-volume atomic replace mit Backup-/Rollbackstrategie ersetzen und die Merge-Semantik mit einem automatisierten Testprojekt festlegen. Erst dann eine selektive Portierung auf einen aktuellen, festgelegten Basisbranch prüfen.

### PR #149 – umfangreiche Daten-/Asset- und Kompatibilitätsänderungen

[PR #149](https://github.com/LIPtoH/TS-SE-Tool/pull/149) ist offen, enthält 2 Commits (Head 035eb9af1e22b9f7f3f910dd60831f9965fdbaf0) und basiert auf master bei d97025f6a0a31b5952e5b524b50e0e0def630928. Der PR umfasst 1.318 Dateien mit ungefähr +5.302/-93 Zeilen. Der überwiegende Anteil sind Spiel-/UI-Assets (.dds, .mat, .tobj, Fonts und Bilder); hinzu kommen 23 C#-Dateien, Projektänderungen und eine aktualisierte SII-Decrypt-DLL. Der Beitrag nennt ETS2/ATS-Kompatibilität bis 1.61, aber der PR-Text erklärt ATS als nicht verfügbar/nicht getestet. Ein Issue-Kommentar berichtet weiterhin, dass ATS 1.60 mit Save v97 als nicht dekodierbar endet.

**Positiv:** Der PR enthält sichtbare Erweiterungen für neue Spielstände/Spielinhalte, zusätzliche Fahrzeug-/Jobdaten und eine aktualisierte Decoderdatei. Das kann als Quelle einzelner überprüfbarer Änderungen dienen.

**Risiken:** Der Umfang erschwert Code- und Asset-Provenienzreview. Die ATS-Validierung ist laut Autor unvollständig; der berichtete v97-Fehler widerspricht einer allgemeinen Kompatibilitätsannahme. 1.318 Dateien ohne Testprojekt/CI-Abdeckung sind keine belastbare Freigabe. Es fehlen im Audit ausreichende Belege für vollständige Coaches-DLC-Abdeckung und verlustfreie Roundtrips beider Spiele.

**Empfehlung:** Nicht als Ganzes übernehmen. Assets nur nach Lizenz-/Herkunftsprüfung und konkretem Produktbedarf. Parser-, Modell- und Decoderänderungen isoliert auswählen, pro Spielversion mit synthetischen Fixtures testen und Decoder-Binaries/Symbole/Architektur separat verifizieren.

## 6. Next-Generation-Architektur

### Machbarkeit der vorgeschlagenen Technologie

**C# / .NET 10 Savegame-Engine – gute Basis.** .NET 10 ist eine LTS-Version mit Support bis November 2028 ([Microsoft Support Policy](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)). Die Engine sollte ein eigenes, UI-unabhängiges Projekt sein, zunächst als reine Bibliothek plus CLI-Testhost. Native Decoder müssen über einen versionierten Adapter mit Architekturprüfung und klarer Fehlerdiagnose eingebunden sein. Parsing, Modell, Validierung, Änderungsplan und Schreibtransaktion dürfen keine WinForms-Abhängigkeit haben.

**Elixir / OTP / Phoenix LiveView – technisch möglich, aber zunächst Experiment.** Elixir bietet Installationswege für Windows ([Elixir Install](https://elixir-lang.org/install/)); Phoenix Releases können Erlang VM, Elixir-Anwendung und Abhängigkeiten in einem Release bündeln ([Phoenix Releases](https://phoenix.hexdocs.pm/releases.html)). Das belegt grundsätzliche Machbarkeit, aber nicht die fertige Windows-Desktop-Verteilung. Der zusätzliche Runtime-Prozess, Windows-Prozesslebenszyklus, Absturzdiagnose, Updates, WebSocket-/Loopback-Sicherheit und Einbindung in MSI sind reale Integrationskosten. C# muss alleiniger Eigentümer von Save-Dateioperationen bleiben; die UI darf keine direkte Schreibabkürzung um die Engine herum erhalten.

**WebView2 – möglicher Host, nicht selbst die Desktop-Anwendung.** Ein nativer Windows-Host muss Phoenix starten/überwachen, WebView2 initialisieren und sauber beenden. WebView2 benötigt ein installiertes Runtime; Evergreen wird von Microsoft meist empfohlen und aktualisiert sich, Offline-Installationen können den Evergreen Standalone Installer verwenden. Fixed Version ist kontrollierbarer, aber laut Microsoft mehr als 250 MB ([Distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution), [Evergreen vs. Fixed](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version)). Für ein vollständig lokales Produkt sind Runtime-Abwesenheit, Updatepolitik und Offline-Setup explizite Installerentscheidungen.

**WiX MSI – machbar, aber Version/Lizenz klären.** Das aktuelle WiX-Ökosystem dokumentiert MSBuild-basierte MSI-Projekte ([WiX MSBuild](https://docs.firegiant.com/wix/tools/msbuild/)). Vor einer Produktentscheidung sind WiX-Version, Signierung, Upgrade-/Rollback-Verhalten und die jeweils geltenden Nutzungsbedingungen zu prüfen; das WiX-Projekt weist auf eine Open Source Maintenance Fee bei kommerzieller Nutzung hin ([WiX README](https://github.com/wixtoolset/wix)). MSIX kann später als optionale Distribution evaluiert werden, ist aber keine Voraussetzung für den lokalen Prototyp.

### Vorgeschlagene Schichten

1. **SaveEngine.Core (.NET 10):** SII-Tokenizer/Parser, syntaktischer Baum mit unbekannten Tokens, typisierte Projektionen, Spiel-/Formatprofile, Versionierung, Validierung und deterministische Ausgabe. Unbekannte Attribute bleiben auch in bekannten Blöcken erhalten.
2. **SaveEngine.IO:** sichere Pfadauflösung, read-only Öffnen, Snapshot-/Backupverwaltung, atomarer Schreibcommit, Recovery-Journal und kontrollierte Fehlerzustände. Der Schreibvorgang benötigt eine explizite Freigabe des Benutzers pro Ziel und speichert standardmäßig eine Kopie.
3. **Decoder-Adapter:** isoliert native Bibliotheken, prüft unterstützte Formate/Architekturen, begrenzt Puffer und meldet „nicht unterstützt“ statt einen Erfolg vorzutäuschen.
4. **Lokale API/IPC:** eng begrenzte Befehle wie Profil auflisten, Kopie öffnen, Diagnose, Vorschau und freigegebene Änderung. Der Engine-Prozess ist alleiniger Schreibpfad; keine Cloud- oder Telemetriepflicht.
5. **UI-POC:** Phoenix LiveView als lokales UI-Experiment in einem C# WebView2-Host. Nur an 127.0.0.1 binden, zufälligen Port und kurzlebiges Capability-Token verwenden, Origin/CSRF validieren, keine Netzwerkbindung nach außen und keine Dateioperation ohne Engine-API. Prozessstart, Timeout, Crash und sauberer Shutdown werden vom Host verwaltet.
6. **Packaging:** versionierte Windows-x64 MSI-Probe mit Offline-Installationspfad, klaren Datenverzeichnissen, Update-/Rollbackprüfung und Runtime-Prüfung. Kein automatischer Start oder Netzwerkdienst.

**Architekturentscheidung:** Den Nutzerwunsch respektierend wird Elixir/Phoenix als bevorzugter experimenteller UI-POC geprüft. Die erste belastbare Produktbasis bleibt die .NET-Engine. Phoenix/WebView2 wird nur nach einem kurzen technischen Spike freigegeben; wenn Offline-Verteilung, Prozesslebenszyklus oder lokale Sicherheitsgrenzen unverhältnismäßig komplex werden, sollte eine native C#-UI als klar dokumentierte Rückfalloption bewertet werden. Kein Electron und kein TypeScript sind vorgesehen.

### Coaches-DLC und Analytics

- ETS2 und ATS erhalten getrennte, versionierte Testprofile. Ein gemeinsamer generischer vehicle-Typ kann Felder teilen, beweist aber keine vollständige Bus-, Coach-, Zubehör-, Fahrer- oder Firmenflottenunterstützung.
- Coaches-DLC wird erst als unterstützt markiert, wenn repräsentative synthetische Saves für besessene Coaches, Zubehör, Garage/Zuordnung, aktiven Busjob, Busstopps und Busjob-Historie vollständig gelesen und roundtrip-getestet sind. Bis dahin: Status „teilweise/unbestätigt“.
- 7-/30-Tage-Analytics werden aus tatsächlichen Profit-/Delivery-Logs berechnet, soweit Einträge Zeitstempel und Werte enthalten. Umsatz, Löhne, Wartung und Kraftstoff dürfen nur als getrennte Quellenwerte bzw. klar definierte Summen ausgewiesen werden. Nicht vorhandene Daten werden als fehlend markiert, nie geschätzt.
- Wo der Spielstand historische Daten nicht mehr enthält, nutzt das Produkt lokale, zeitgestempelte Snapshots mit Spiel, Profilkennung, Parser-Version und Quelle. Vergleiche beginnen erst ab dem ersten Snapshot; rückwirkende Historie wird nicht erfunden. Keine Online-Dienste, Cloud-Synchronisierung oder Telemetrie.

## 7. Test- und Abnahmestrategie

Alle Tests laufen ausschließlich mit synthetischen SII-Dateien oder ausdrücklich bereitgestellten Kopien. Nie mit produktiven Spielständen. Jeder Schreibtest arbeitet in einem temporären Testverzeichnis und protokolliert den Hash der Eingabe.

| Testbereich | Konkretes Abnahmekriterium |
|---|---|
| Parser-Grundlage | Dokumentierte synthetische Fixtures für beide Spiele, mehrere SII-Versionen, komprimierte/verschlüsselte Formate und fehlerhafte Eingaben; Fehler zeigen Version, Block/Feld und Ursache und führen nie zu einem Schreibversuch. |
| No-op Roundtrip | Für jede unterstützte Fixture bleibt die semantische Struktur einschließlich unbekannter Felder, Mod-Daten, Reihenfolge und Markup erhalten. Unveränderte Textfixtures sind, wo Formatregeln es erlauben, byte-/zeilenidentisch; jede Normalisierung ist ausdrücklich spezifiziert. Wiederholtes Parsen/Schreiben ist idempotent. |
| Gezielte Bearbeitung | Ein geändertes Feld ändert nur die erwarteten Tokens; alle übrigen bekannten und unbekannten Attribute bleiben erhalten. Tests decken Hinzufügen, Ändern und absichtliches Entfernen von Einzelwerten und Arrayelementen ab. |
| Regressionen | Je eine Regression für #148/#108, #142, #140, #120, #67, #107 und #109. Für nicht reproduzierte Issues wird zuerst eine minimale synthetische Reproduktion definiert; fehlt die, bleibt der Status offen statt „behoben“. |
| Schreibsicherheit | Vor dem Commit alle Dateien vollständig serialisieren und validieren. Fehler durch gezielte Fault Injection vor/nach Temp-Schreiben, Flush, Backup, Replace und Multi-File-Commit lassen die Originale wiederherstellbar; kein Fehlerfall erzeugt eine 0-Byte-Originaldatei. Tempdateien und Recovery-Journal werden nachvollziehbar behandelt. |
| Crash/Recovery | Prozessabbruch an jedem Commit-Schritt; beim nächsten Start werden unvollständige Transaktionen erkannt und Original oder konsistente neue Version wiederhergestellt. Hashvergleich bestätigt das Ergebnis. |
| Decoder/Kompatibilität | Für jede unterstützte ETS2-/ATS-Version getrennte Fixtures und Ergebnisse für Lesen, No-op-Schreiben und bearbeitetes Schreiben. Ungetestete Versionen sind read-only. Fehlerhafte/zu große Eingaben werden begrenzt und sicher abgelehnt. |
| Coaches | Bus-/Coach-Ownership, Garage, Ausstattung und aktive/abgeschlossene Busjobs sowie Verknüpfungen roundtrippen ohne verwaiste IDs. Erst danach Feature als unterstützt kennzeichnen. |
| Analytics | Testdaten mit festen Zeitstempeln ergeben definierte 7-/30-Tage-Ergebnisse; fehlende Logs bleiben als „keine Daten“ sichtbar; lokale Snapshots lassen sich exportieren, validieren und wiederherstellen. Keine Nullwerte werden als echte Nullen interpretiert, wenn die Quelle fehlt. |
| Profile/Cloud | Lokale Pfade, mehrere Steam-Installationen, umgeleitete Dokumente und fehlende Berechtigungen erzeugen verständliche Diagnosen. Cloud-Originale werden nicht verändert; explizite Kopie in lokales Arbeitsprofil ist erforderlich. |
| UI/Installer | Offline-Neuinstallation, Upgrade, Deinstallation und Wiederherstellung; Phoenix-Prozess wird beendet; WebView2-/OTP-Fehler werden angezeigt; keine externe Listeneradresse, keine Pflichtverbindung und keine Dateiänderung ohne ausdrückliche Aktion. |

Erforderliche CI-Gates: Format-/Buildprüfung für .NET 10, Unit-/Property-/Fuzz-Tests der Parsergrenzen, Regressionstests, Windows-x64-Installer-Smoke-Test sowie Lizenz-/SBOM-Prüfung für native und verwaltete Abhängigkeiten. Die Tests müssen vor einer Stable-Freigabe auf unterstützten Versionen und synthetischen Fixtures vollständig grün sein.

## 8. Umsetzbarer Wochenendplan (27.–29. November 2026)

**Freitag, 27. November – Spezifikation und Testmaterial**

- Unterstützte Spielversionen und Statusmodell festlegen: lesen, no-op roundtrip, bearbeiten.
- Synthetische Minimalfixtures für normale Blöcke, unbekannte Attribute, nil, große Zahlen, Markup und Busdaten erstellen.
- #148-Schreibablauf und atomare Commit-Semantik als ADR festhalten; echte Spielstände bleiben unangetastet.

**Samstag, 28. November – Engine- und UI-Spike**

- Neues .NET-10-Engine-Projekt nur als isolierten Prototyp anlegen, kein kompletter Port.
- Parser-Roundtrip für synthetische SII-Blöcke und unbekannte Felder demonstrieren.
- Kleinen Phoenix-LiveView/WebView2-Lifecycle-Spike durchführen: lokaler Start, zufälliger Loopback-Port, Host-Token, Offline-Aufruf und sauberer Shutdown.
- Entscheidung dokumentieren, ob Prozess-/Runtime-Packaging den Phoenix-Weg rechtfertigt.

**Sonntag, 29. November – Sicherheits- und Freigabegates**

- Fault-Injection für Tempdatei/Replace/Abbruch mit synthetischen Dateien durchführen.
- Regressionen für große Zahlen, nil, Markup, Farben und Cargo-Verknüpfungen aus den Fixtures ableiten.
- Coaches-DLC- und Analytics-Datenquellen anhand synthetischer Blockgraphen bewerten.
- MSI-Probe oder Installationsplan erstellen; Offline- und Updatepfad dokumentieren.
- Ergebnis als Entscheidungspaket mit offenen Belegen, Versionstabelle und nächstem Implementierungsblock festhalten. Keine vollständige Neuentwicklung und keine Stable-Aussage allein aufgrund dieses Spikes.

**Abbruch-/Go-Regel:** Kein Schreibfeature wird freigegeben, solange No-op-Roundtrip, unbekannte Felder und Crash-Recovery nicht alle akzeptiert sind. Phoenix bleibt experimentell, bis die Desktop-Prozess- und Sicherheitsprüfungen bestanden sind.

## 9. Tatsächlich ausgeführte Prüfungen und Grenzen

- Fork-Existenz, Fork-Beziehung zum Original, Standardbranch, Apache-2.0-Lizenz und Git-Remotes über GitHub/Git lokal geprüft.
- master- und dev-branch-SHAs sowie Versions-/Aktualitätsabstand geprüft.
- Relevante Quellpfade für Serialisierung, Parsefehler, Levelberechnung, Profile, Steam-Cloud-Sperre, Zubehör, Bus-/Logdaten und P/Invoke-Dekodierung gelesen.
- Issues und offene PRs #147/#149 anhand GitHub-Metadaten, Beschreibungen und verfügbarer Kommentare bewertet.
- Keine echten Saves geöffnet, keine produktiven Dateien verändert, kein Runtime-Test gegen ETS2/ATS durchgeführt.
- Keine Unit-, Integration-, Build-, Crash- oder Installer-Tests ausgeführt. Im Basisrepository wurde kein Testprojekt gefunden; verfügbar war .NET SDK 8.0.425, aber kein msbuild, elixir, mix oder wix im PATH. Vorhandene Projekte zielen auf .NET Framework 4.7.2. Die aktuelle Umgebung belegt deshalb keinen erfolgreichen Build.
- Keine Änderungen am Originalrepository, kein Merge, Release oder Deployment. Keine produktiven Spielstände verwendet.

## 10. Status und nächste Schritte

- **Fork:** vorhanden, echter Fork von LIPtoH/TS-SE-Tool, Apache-2.0-Hinweise erhalten.
- **Audit-Branch:** audit/critical-next-gen-2026-11-25, basierend auf master.
- **Commit/Push:** wird in diesem Arbeitsgang nach Review des Berichts ausgeführt und anschließend anhand des Remote-Branches verifiziert.
- **Stable-Bereitschaft:** nicht gegeben. Der P0-Schreibpfad, fehlende Roundtrip-/Crash-Tests und ungeklärte ETS2/ATS-1.60+-Kompatibilität verhindern eine belastbare Stable-Aussage.
- **Offene Blocker:** kein überprüfbarer aktueller Save-v97-Testbestand, kein vollständiger Coaches-DLC-Nachweis, unbekannte Vollständigkeit der PR-Decoder-/Asset-Provenienz, keine automatisierte Test-/CI-Basis und kein lokaler .NET-10/Elixir/WiX-Buildnachweis.

### Drei wichtigste nächste Schritte

1. P0-Schreibsicherheit zuerst lösen: vollständiges Pre-Serialization, same-volume atomic replace, Backup/Recovery und Fault-Injection-Regression.
2. Eigenständiges automatisiertes Testprojekt mit synthetischen ETS2-/ATS-/v97-/Mod-/Coaches-Fixtures einführen und Unknown-Field-Roundtrip als Abnahmekriterium festschreiben.
3. PR #147 und #149 selektiv gegen aktuelle Branches prüfen; jede Parser-, Decoder- und Assetänderung separat testen, statt einen PR ungeprüft zu übernehmen.
