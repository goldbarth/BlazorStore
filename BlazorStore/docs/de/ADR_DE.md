# 📐 Architecture Decision Records (ADR-Light)

> Kompakte Dokumentation der zentralen Architektur-Entscheidungen in diesem Projekt — nicht als formaler RFC, sondern als nachvollziehbare Begründung.

---

## ADR-001: Store-Architektur statt MVVM

**Entscheidung**
Zentraler Store mit unidirektionalem Datenfluss statt klassischem MVVM-Pattern.

**Begründung**
MVVM ist mir aus der WPF-Entwicklung vertraut und funktioniert dort gut. Für dieses Projekt wollte ich bewusst eine andere Architektur erlernen, die auf einer Single Source of Truth basiert und explizite, nachvollziehbare State-Transitions erzwingt. Der Store-Ansatz macht Zustandsänderungen testbar und vorhersagbar — besonders bei asynchronen Flows und JS-Interop.

**Konsequenzen**
Mehr Boilerplate (Actions, Reducer, Effects), dafür klare Trennung von Zustandslogik und Side-Effects. Jede Änderung ist nachvollziehbar und reproduzierbar.

---

## ADR-002: Explizite JS-Interop statt Blazor-Abstraktionen

**Entscheidung**
JavaScript-APIs (YouTube IFrame, SortableJS) werden über explizite Interop-Aufrufe angebunden — nicht über Blazor-Wrapper oder Drittanbieter-Komponenten.

**Begründung**
Blazor-Wrapper verstecken oft internen State, der nicht im Store lebt. Bei zwei gleichzeitigen State-Quellen (Blazor + JS) entstehen Race Conditions und schwer nachvollziehbare Bugs. Explizite Interop stellt sicher, dass JS nur als Ausführungsschicht dient und der gesamte State im Store bleibt.

**Konsequenzen**
Mehr manueller Interop-Code, aber kein Hidden State zwischen C# und JavaScript. Jeder JS-seitige Effekt fließt als Action zurück in den Store.

---

## ADR-003: Immutable Records für State-Slices

**Entscheidung**
Feature-State wird als `record`-Typ (C#) modelliert — Änderungen erzeugen immer neue Instanzen via `with`-Expressions.

**Begründung**
Immutable State verhindert versehentliche Mutation außerhalb des Reducers. Change Detection wird trivial (Referenzvergleich statt Deep-Compare), und die Grundlage für spätere Features wie Undo/Redo ist direkt gegeben.

**Konsequenzen**
Etwas mehr Allokation durch neue Instanzen, was bei der Projektgröße aber irrelevant ist. Dafür garantiert korrekte State-Transitions und einfachere Debugging-Möglichkeiten.

---

## ADR-004: SortableJS außerhalb von Blazor-Diffing

**Entscheidung**
Drag & Drop läuft komplett über SortableJS direkt am DOM — nicht über Blazor-Komponenten oder MudBlazor-DnD.

**Begründung**
Drag & Drop ist ein DOM-Problem, kein UI-State-Problem. SortableJS arbeitet direkt am DOM ohne Virtual-DOM-Overhead, liefert saubere `oldIndex`/`newIndex`-Events und braucht kein permanentes Syncen während der Bewegung. Ein einziger Event am Ende des Drags reicht, um den Store zu aktualisieren. Komponentenbasierte Lösungen würden bei jedem Mouse-Move Re-Renders auslösen und zusätzliche Race Conditions mit Blazors Diffing erzeugen.

**Konsequenzen**
Blazor "weiß" während des Drags nichts von der DOM-Manipulation — erst das `onEnd`-Event fließt als Action in den Store. Das erfordert bewusstes Lifecycle-Handling, hält aber den Datenfluss sauber und performant.

---

## ADR-005: ImmutableList für State-Collections

**Entscheidung**
Collections im State (z.B. `Videos`, `Playlists`) werden als `ImmutableList<T>` statt `List<T>` modelliert.

**Begründung**
`ImmutableList` erzwingt unveränderliche Collections und verhindert versehentliche Mutationen außerhalb des Reducers. Jede Änderung erzeugt eine neue Collection-Instanz, was Change Detection vereinfacht und Race Conditions bei parallelen Zugriffen ausschließt. Die geringfügig höhere Allokation ist bei der Projektgröße vernachlässigbar.

**Konsequenzen**
- Reducer müssen explizit `.ToImmutableList()` aufrufen nach Mutationen
- Collections sind garantiert threadsafe für Lesezugriffe
- Basis für künftige Features wie Undo/Redo ist gelegt

---

## ADR-006: Channel-basierte Action-Queue

**Entscheidung**
Actions werden über einen `Channel<YtAction>` serialisiert statt über `SemaphoreSlim`.

**Begründung**
`Channel<T>` ist idiomatischer für Producer-Consumer-Patterns in modernem .NET und bietet eingebaute Backpressure-Mechanismen. Die Action-Verarbeitung läuft in einer dedizierten Background-Task, die über `CancellationToken` sauber gestoppt werden kann. Dies verhindert Race Conditions und garantiert FIFO-Reihenfolge.

**Konsequenzen**
- Alle Actions werden seriell verarbeitet (keine Parallelität)
- Sauberes Lifecycle-Management über `IDisposable`
- Einfachere Testbarkeit durch deterministisches Verhalten

---

## ADR-007: Exhaustive Pattern Matching im Reducer

**Entscheidung**
Der Reducer verwendet exhaustive pattern matching mit `UnreachableException` für unbehandelte Actions.

**Begründung**
Der Compiler erzwingt die explizite Behandlung aller Action-Typen. Neue Actions können nicht versehentlich "vergessen" werden. Actions, die nur Side-Effects auslösen (z.B. `CreatePlaylist`, `AddVideo`), geben explizit den unveränderten State zurück. Dies macht die Absicht im Code deutlich.

**Konsequenzen**
- Compiler-garantierte Action-Vollständigkeit
- Klare Dokumentation, welche Actions State ändern und welche nicht
- Runtime-Exception bei vergessenen Actions (statt stilles Ignorieren)

---

## ADR-008: Result Pattern für Fehlerbehandlung

**Entscheidung**
Erwartbare Fehlerfälle in Store-Operationen werden über ein `Result<T>`-Pattern abgebildet statt über Exceptions.

**Begründung**
Exceptions sind für unerwartete Fehler gedacht. Validierungsfehler (z. B. ungültige YouTube-URL), nicht gefundene Ressourcen oder externe API-Fehler sind jedoch *erwartbar* und sollten den normalen Kontrollfluss nicht unterbrechen. Das Result Pattern erlaubt explizite Unterscheidung zwischen `Success(T)` und `Failure(OperationError)` mit kategorisierten Fehlern (`Validation`, `NotFound`, `Transient`, `External`, `Unexpected`).

**Konsequenzen**
- Effects können Fehler gezielt behandeln statt alles über try-catch abzufangen
- Fehlerkategorien ermöglichen differenzierte UI-Reaktionen (Warning vs. Error)
- `OperationContext` mit Correlation-IDs erlaubt Log-Korrelation über asynchrone Flows
- Technische Fehlerdetails bleiben von nutzersichtbaren Meldungen getrennt

---

## ADR-009: Benachrichtigungssystem mit MudBlazor ISnackbar

**Entscheidung**
Nutzerfeedback wird über ein zentrales Benachrichtigungssystem im Store-State gesteuert — dargestellt über MudBlazors `ISnackbar`-Service.

**Begründung**
Fehlermeldungen, Warnungen und Erfolgsmeldungen sind UI-State und gehören in den Store. Statt `alert()`-Aufrufe oder komponentenlokale Fehlerzustände wird eine `ImmutableList<Notification>` im `YouTubePlayerState` geführt. Die Page-Komponente injiziert `ISnackbar` und bridged Store-Notifications zu MudBlazor-Snackbars im `OnStoreStateChanged`-Handler. Deduplizierung erfolgt über `HashSet<Guid>`. Nach der Anzeige wird `DismissNotification` dispatcht, um die Notification aus dem Store zu entfernen. Ursprünglich wurde eine eigene `NotificationPanel`-Komponente mit manuellen Timern und CSS-Animationen verwendet — diese wurde zugunsten der nativen MudBlazor-Lösung entfernt.

**Konsequenzen**
- Notifications sind Teil des unidirektionalen Datenflusses
- Keine versteckten UI-Zustände für Fehlermeldungen
- Severity-Mapping: `Validation`/`NotFound`/`Transient` → Warning, `External`/`Unexpected` → Error
- Notifications können in Tests über den State verifiziert werden
- MudBlazor übernimmt Rendering, Animation und Auto-Dismiss — kein eigener Timer-Code nötig

---

## ADR-010: Strukturiertes Logging mit Operation Context

**Entscheidung**
Alle Store-Effects loggen strukturiert über `ILogger<YouTubePlayerStore>` mit einem `OperationContext`, der Operation, Correlation-ID und Entity-IDs enthält.

**Begründung**
Bei asynchronen Flows (DB → JS-Interop → Folge-Actions) ist eine Korrelation von Log-Einträgen ohne expliziten Kontext kaum möglich. Der `OperationContext` wird bei jeder Operation erzeugt und enthält eine eindeutige `CorrelationId` sowie optional `PlaylistId`, `VideoId` und `Index`. Log-Level werden aus der `ErrorCategory` abgeleitet (Warning/Error). Erfolgreiche Operationen werden auf `Information`-Level geloggt.

**Konsequenzen**
- Jede Operation ist über die Correlation-ID nachvollziehbar
- Technische Log-Details und nutzersichtbare Meldungen sind explizit getrennt
- Log-Einträge enthalten strukturierte Properties für maschinelle Auswertung
- Bei `Unexpected`-Fehlern wird die Correlation-ID in der Notification angezeigt, um Log-Tracing zu ermöglichen

---

## ADR-011: Snapshot-basiertes Undo/Redo für QueueState

**Entscheidung**
Undo/Redo wird ausschließlich für `QueueState` implementiert — über ein Past/Present/Future-Snapshot-Modell mit `ImmutableList<QueueSnapshot>`-Stacks im State.

**Begründung**
Die Store-Architektur mit immutablem State und reinen Reducern eignet sich ideal für Zeitreise-Features. Statt einen generischen Command-Stack zu bauen, werden Snapshots des `QueueState` vor jeder undoable Action erfasst. Dies ist einfacher, direkter und vermeidet die Komplexität inverser Operations.

**Kritisches Detail — `VideoPositions`:** `VideoItem` ist eine mutable Klasse (kein Record). `HandleSortChanged` mutiert `Position` in-place auf geteilten Referenzen. Ohne separates `VideoPositions`-Array im Snapshot würden vergangene Snapshots durch spätere Sortierungen korrumpiert. Das parallele Array erfasst die `Position`-Werte zum Snapshot-Zeitpunkt und stellt sie bei Restore wieder her.

**UndoPolicy** bestimmt das Verhalten pro Action:
- `SelectVideo`, `SortChanged` → undoable (Snapshot wird zu Past hinzugefügt)
- `PlaylistLoaded`, `SelectPlaylist` → Boundary (kompletter History-Reset)
- Alle anderen → History unverändert

**Effect-Gating:** `UndoRequested`/`RedoRequested` überspringen `RunEffects` komplett — Undo/Redo ist rein reducer-basiert, ohne DB-Persistenz oder JS-Interop.

**Konsequenzen**
- Nur Queue-Mutationen sind undo-fähig — Player-State und Playlist-Verwaltung bleiben außen vor
- History-Limit von 30 Einträgen verhindert Speicherprobleme
- Neue undoable Actions erfordern nur eine Anpassung in `UndoPolicy.IsUndoable()`
- Umfassende Testabdeckung (27 Tests) sichert die Korrektheit ab

---

## ADR-012: MudLayout-Migration für Layout und Navigation

**Entscheidung**
Das gesamte App-Layout wird auf MudBlazors `MudLayout`-System migriert — mit `MudAppBar`, `MudDrawer` (Variant.Mini) für die Sidebar und `MudNavMenu`/`MudNavLink` für die Navigation. Page-Level-Drawers (CreatePlaylist, AddVideo) nutzen `MudDrawer` mit `Variant.Temporary`.

**Begründung**
Das ursprüngliche Layout basierte auf manueller HTML-Struktur mit eigenem CSS für Sidebar, Navigation und Toggle-Logik. MudBlazors `MudDrawer` mit `Variant.Temporary` benötigt einen `MudLayout`-Kontext, um korrekt zu funktionieren (Positioning, Overlay, Slide-Animation). Statt Workarounds (conditional rendering, `display:none`) wurde das gesamte Layout auf MudLayout migriert. Dies bringt das Projekt in Einklang mit MudBlazor Best Practices und löst gleichzeitig die Drawer-Positionierungsprobleme.

**Konsequenzen**
- `MainLayout.razor` nutzt `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`
- Navigation über `MudNavMenu`/`MudNavLink` mit Material Icons statt manueller SVG-Icons
- Sidebar: `DrawerVariant.Mini` (60px collapsed, 250px expanded) mit Text-Fading via scoped CSS
- Page-Level-Drawers: `DrawerVariant.Temporary` mit korrektem Overlay und Slide-Animation
- `div.app-root`-Wrapper ermöglicht `::deep`-Zugriff auf MudBlazor-interne Klassen
- Weniger eigener CSS-Code, da MudBlazor Layouting und Responsive-Verhalten übernimmt

---

## ADR-013: Permutationsbasierter Shuffle mit reinen Strategie-Funktionen

**Entscheidung**
Shuffle, Repeat und Playback-Navigation werden als reine statische Funktionen in `PlaybackNavigation` implementiert, die `PlaybackDecision` (Discriminated Union: `AdvanceTo` / `Stop` / `NoOp`) zusammen mit einem aktualisierten `QueueState` zurückgeben. Shuffle verwendet Fisher-Yates-Permutation mit deterministischem Seed. Previous im Shuffle-Modus nutzt einen `PlaybackHistory`-Stack (LIFO).

**Begründung**
Playback-Navigation umfasst mehrere interagierende Dimensionen (Shuffle ein/aus, drei Repeat-Modi, Queue-Mutationen). Die Implementierung dieser Logik direkt im Reducer würde isoliertes Testen erschweren. Durch die Extraktion von `ComputeNext`, `ComputePrev`, `GenerateShuffleOrder` und `RepairPlaybackStructures` als reine statische Funktionen kann jede einzeln mit deterministischen Eingaben getestet werden. Der Rückgabetyp `PlaybackDecision` macht die Verzweigung im Reducer explizit (kein Seitenkanal-State).

Zentrale Design-Entscheidungen:
- **Permutation, nicht zufällige Auswahl**: `GenerateShuffleOrder` erzeugt vorab eine vollständige Permutation (jedes Video wird genau einmal gespielt, bevor es wiederholt wird), anstatt bei jedem Schritt zufällig zu wählen. Dies verhindert Duplikate und ermöglicht Previous, den exakten Pfad zurückzuverfolgen.
- **PlaybackHistory-Stack**: Im Shuffle-Modus pusht jedes `ComputeNext` das aktuelle Video auf `PlaybackHistory`. `ComputePrev` poppt davon. Dies gibt dem Nutzer exakte history-basierte Navigation statt "vorheriges in Shuffle-Reihenfolge" (was verwirrend wäre).
- **Deterministischer Seed**: Gleicher Seed erzeugt gleiche Permutation, was reproduzierbare Testszenarien und potenzielle spätere Persistenz ermöglicht.
- **RepairPlaybackStructures**: Wird nach jeder Queue-Mutation (Video hinzufügen/entfernen) aufgerufen, filtert veraltete IDs und hängt neue Videos an — macht das System resilient gegen gleichzeitige Änderungen.
- **Playback-transiente UndoPolicy**: `IsPlaybackTransient` stellt sicher, dass Navigations-Actions (`NextRequested`, `PrevRequested`, `ShuffleSet`, `RepeatSet`) das Undo-System durchlaufen, ohne History-Einträge zu erzeugen oder Future zu leeren — sie sind orthogonal zu Undo/Redo.

**Konsequenzen**
- 95 Tests insgesamt (53 neue) mit vollständiger Abdeckung aller Modus-Kombinationen und Grenzfälle
- `QueueSnapshot` erfasst alle 6 neuen Felder, sodass Undo/Redo den Shuffle/Repeat-State erhält
- Reducer-Handler sind schlank: delegieren an reine Funktionen, wenden dann die Entscheidung an
- Neue Playback-Modi (z. B. Shuffle-Algorithmen, gewichtetes Repeat) können durch Erweiterung von `PlaybackNavigation` hinzugefügt werden, ohne den Reducer anzufassen
- `VideoEnded`-Effect dispatcht jetzt `NextRequested` statt direkter Index-Berechnung, wodurch die gesamte Auto-Advance-Logik über denselben Pfad vereinheitlicht wird

---

## ADR-014: Import/Export mit Discriminated Union State Machine und mehrstufiger Effect-Pipeline

**Entscheidung**
Import und Export werden als mehrstufige Effect-Pipelines implementiert, deren Lifecycle über eine Discriminated Union State Machine (`ImportExportState`) abgebildet wird. Export verwendet eine DTO-Pipeline (Domain → Mapper → Serializer → JS-Download). Import durchläuft Parse → Validate → Apply → Persist. Persistenz erfolgt über einen Dirty-Tracking-Mechanismus mit atomarem DB-Snapshot-Replace.

**Begründung**
Import/Export sind inhärent mehrstufige Prozesse mit unterschiedlichen Fehlerpunkten (Parse-Fehler, Schema-Mismatch, Validierungsfehler, DB-Fehler, JS-Interop-Fehler). Statt eines monolithischen try-catch-Blocks werden die Schritte als separate Actions modelliert, die jeweils einen State-Übergang in der Discriminated Union auslösen. Dies macht den Fortschritt für die UI sichtbar (Progress-Bar, Fehler-Alerts), ermöglicht granulare Fehlerbehandlung und hält den Reducer testbar.

Zentrale Design-Entscheidungen:
- **Discriminated Union State Machine**: `ImportExportState` mit 10 Zuständen (Idle, ExportInProgress, ExportSucceeded, ExportFailed, ImportParsing, ImportParsed, ImportValidated, ImportApplied, ImportSucceeded, ImportFailed). Jeder Zustand ist ein eigener Record-Typ — exhaustive pattern matching erzwingt vollständige Behandlung in UI und Reducer.
- **DTO-Pipeline für Export**: `ExportMapper` konvertiert Domain-Modelle zu versionierten DTOs (`ExportEnvelopeV1` mit `SchemaVersion: 1`). `ExportSerializer` serialisiert mit camelCase. JS-Interop (`export-interop.js`) triggert Blob-basierten Browser-Download. Jeder Schritt ist unabhängig testbar.
- **Schema-Versionierung**: `SchemaVersion`-Feld im Export-Envelope ermöglicht zukünftige Migrationsstrategien ohne Breaking Changes bei bestehenden Backups.
- **ImportPolicy (ReplaceAll + TrustIncoming)**: v1 unterstützt nur vollständigen Ersatz aller Playlists (atomare Transaktion) und übernimmt GUIDs aus der Import-Datei. Dies erhält referenzielle Identität über Export → Edit → Re-Import-Zyklen.
- **Dirty-Tracking mit Persist-Effect**: `PersistenceState.IsDirty` wird bei `ImportApplied` gesetzt. Der Persist-Effect klont den gesamten Playlist-Baum und ersetzt atomar die Datenbank. Bei Fehler bleibt `IsDirty = true` für Retry. UI zeigt Fehler-Chip mit Retry-Button.
- **ImportApplied als Undo-Boundary**: Import ersetzt den gesamten Playlist-State — ein Undo danach wäre semantisch sinnlos. `UndoPolicy.IsBoundary` leert Past/Future komplett.
- **Discriminated Union Fehlertypen**: `ImportError` (ParseError, UnsupportedSchema, ValidationError, IdCollision, PersistenceFailed) und `ExportError` (SerializationFailed, InteropFailed) als eigene Record-Hierarchie — nicht über generische Strings, sondern typsicher.

**Konsequenzen**
- State-Machine-Transitionen sind komplett über Reducer-Tests verifizierbar (kein async I/O nötig)
- UI reagiert auf jeden Zwischenschritt (Progress, Fehler, Erfolg) über Store-State-Binding
- Export-Pipeline ist erweiterbar (z. B. neue Schema-Versionen, zusätzliche Export-Formate)
- Import-Validierung ist von Persistenz entkoppelt — Fehler werden vor DB-Schreibvorgängen erkannt
- Persist-Effect ist wiederverwendbar für andere Features, die Dirty-Tracking benötigen