# Architektur

Dieses Repository enthält ein Blazor-Feature ("YouTube Player"), das mit einer strikt store-getriebenen Architektur umgesetzt ist.
Ziel sind vorhersagbare Zustandsübergänge, eine zentrale Datenquelle und eine saubere Trennung zwischen UI und Side-Effects.

## Prinzipien

### Single Source of Truth
Der komplette Feature-State lebt im `YouTubePlayerState` innerhalb des `YouTubePlayerStore`.
Die UI verändert Feature-Daten niemals direkt.

### Unidirektionaler Datenfluss
User- und Interop-Ereignisse werden in **Actions** übersetzt.
Die Verarbeitung erfolgt im Store in zwei Schritten:

1. **Reduce** – reine Zustandsänderung (kein I/O)
2. **Effects** – Side-Effects (DB, JS-Interop, asynchrone Aufrufe), optional mit Folge-Actions

### UI ist Dispatch-only
Razor-Komponenten:
- rendern ausschließlich auf Basis des Store-States
- dispatchen Actions bei User-Interaktionen
- halten nur lokalen UI-State (z. B. Drawer offen/geschlossen)

### Side-Effects liegen im Store
Persistenz (Playlist-/Video-CRUD), JS-Interop (YouTube-Iframe) und sonstige asynchrone Logik liegen in den Store-Effects.
Drawer und Pages greifen nicht direkt auf die Datenbank zu.

---

## Ordnerübersicht (Feature)

Typische Struktur:

- `Features/YouTubePlayer/Store/`
    - `YouTubePlayerStore.cs` – verwaltet State, Dispatch, Reducer und Effects
- `Features/YouTubePlayer/Components/`
    - `CreatePlaylistDrawer.razor` – MudDrawer (Temporary) für Playlist-Erstellung
    - `AddVideoDrawer.razor` – MudDrawer (Temporary) für Video-Hinzufügung
    - `ImportExportToolbar.razor` – Export/Import-Buttons + Persist-Fehleranzeige
    - `ImportDrawer.razor` – MudDrawer (End) mit Datei-Upload und JSON-Paste
- `Features/YouTubePlayer/State/`
    - `YouTubePlayerState.cs` (Root-State)
    - Sub-States (z. B. `PlaylistsState`, `QueueState`, `PlayerState`)
    - `YtAction.cs` (Actions)
    - `ActionOrigin.cs` (Enum: `User`, `System`, `TimeTravel`)
    - `QueueSnapshot.cs` (Immutable Snapshot mit parallelem `VideoPositions`-Array + Playback-Felder)
    - `UndoPolicy.cs` (Bestimmt, welche Actions undoable/boundary/playback-transient sind)
    - `RepeatMode.cs` (Enum: `Off`, `All`, `One`)
    - `PlaybackDecision.cs` (Discriminated Union: `AdvanceTo`, `Stop`, `NoOp`)
    - `PlaybackReasons.cs` (`AdvanceReason`, `StopReason` Enums)
    - `PlaybackNavigation.cs` (Reine Strategie-Funktionen für Shuffle/Next/Prev/Repair)
    - `Notification.cs` (Notification-Modell + Severity)
    - `OperationError.cs` (Kategorisierte Fehler + OperationContext)
    - `Result.cs` (Result-Pattern für erwartbare Fehler)
    - `ImportExportState.cs` (Discriminated Union State Machine: Idle → Export/Import-Lifecycle)
    - `PersistenceState.cs` (Dirty-Flag, letzter Persist-Versuch, Fehlermeldung)
- `Features/YouTubePlayer/ImportExport/`
    - `ExportDtoV1.cs` (DTOs: `ExportEnvelopeV1`, `ExportPlaylistDto`, `ExportVideoDto`)
    - `ExportMapper.cs` (Domain-Modelle → DTOs, sortiert Videos nach Position)
    - `ExportSerializer.cs` (JSON-Serialisierung mit camelCase und Null-Handling)
    - `ImportPolicy.cs` (`ImportMode.ReplaceAll`, `IdStrategy.TrustIncoming`, `ImportOptions`)
    - `ImportExportErrors.cs` (Discriminated Union: `ImportError`, `ExportError`)
- `Features/YouTubePlayer/Models/`
    - Domänenmodelle (z. B. `Playlist`, `VideoItem`)
- `wwwroot/js/`
    - `youtube-player-interop.js` (YouTube IFrame API)
    - `sortable-interop.js` (SortableJS)
    - `export-interop.js` (Blob-Download für JSON-Export)

---

## State-Modell

### Root-State: `YouTubePlayerState`
Enthält:
- `Playlists`: loading / empty / loaded (Liste der Playlists)
- `Queue`: ausgewählte Playlist, sortierte Videoliste, aktueller Index
- `Player`: Player-Zustand (empty / loading / buffering / playing / paused)
- `Notifications`: `ImmutableList<Notification>` — aktive Benachrichtigungen für die UI
- `LastError`: `OperationError?` — letzter aufgetretener Fehler (für Debugging)
- `ImportExport`: Discriminated Union State Machine für den Import/Export-Lifecycle
- `Persistence`: Dirty-Flag, letzter Persist-Versuch, Fehlermeldung

Alle UI-Entscheidungen werden aus diesen Werten abgeleitet.

### Immutability
- State-Slices sind `record`-Typen
- Collections sind `ImmutableList<T>`
- Änderungen erzeugen neue Instanzen via `with`-Expressions

### Queue
- `SelectedPlaylistId`: `Guid?`
- `Videos`: `ImmutableList<VideoItem>` (sortiert nach `Position`)
- `CurrentIndex`: `int?` (aktuelle Auswahl)
- `CurrentItemId`: `Guid?` — ID-basiertes Tracking (stabil über Sortierungen hinweg)
- `Past`: `ImmutableList<QueueSnapshot>` — Undo-History (max. 30 Einträge)
- `Future`: `ImmutableList<QueueSnapshot>` — Redo-History
- `CanUndo` / `CanRedo`: abgeleitete Properties für UI-Binding

**Playback-Navigation:**
- `RepeatMode`: `Off` | `All` | `One`
- `ShuffleEnabled`: `bool`
- `ShuffleOrder`: `ImmutableList<Guid>` — Permutation der Video-IDs (aktuelles Video an Index 0)
- `PlaybackHistory`: `ImmutableList<Guid>` — Stack-Semantik für Previous im Shuffle-Modus (max. 100)
- `ShuffleSeed`: `int` — deterministischer Seed für Fisher-Yates-Shuffle

### Player
Repräsentiert den Wiedergabe-Lifecycle und wird ausschließlich über Actions aus der JS-Interop aktualisiert
(z. B. `PlayerStateChanged`, `VideoEnded`).

---

## Actions

Actions sind der einzige Eingang in den Store.

### Commands (Intent)
Von der UI ausgelöst:
- `Initialize`
- `SelectPlaylist(playlistId)`
- `SelectVideo(index, autoplay)`
- `CreatePlaylist(...)`
- `AddVideo(...)`
- `SortChanged(oldIndex, newIndex)`
- `ShuffleSet(enabled, seed?)` — Shuffle ein-/ausschalten
- `RepeatSet(mode)` — Repeat-Modus wechseln (Off → All → One)
- `NextRequested` — zum nächsten Video springen
- `PrevRequested` — zum vorherigen Video springen
- `UndoRequested` — Letzte Queue-Änderung rückgängig machen
- `RedoRequested` — Rückgängig gemachte Änderung wiederherstellen

### Results (Loaded / Abgeleitet)
Von Effects ausgelöst:
- `PlaylistsLoaded(playlists)`
- `PlaylistLoaded(playlist)`
- `PlaybackAdvanced(toItemId, reason)` — Effect-Routing
- `PlaybackStopped(reason)` — Effect-Routing

### Import/Export
Von der UI ausgelöst:
- `ExportRequested` — Export starten
- `ImportRequested(jsonContent)` — Import mit JSON-Inhalt starten

Von Effects ausgelöst (Lifecycle):
- `ExportPrepared(envelope)` — DTO serialisiert, bereit für Download
- `ExportSucceeded` — Download abgeschlossen
- `ExportFailed(ExportError)` — Export fehlgeschlagen
- `ImportParsed(envelope)` — JSON erfolgreich geparst
- `ImportValidated(envelope)` — Validierung bestanden
- `ImportApplied(playlists, selectedPlaylistId)` — Domain-Modelle erstellt, bereit zum Anwenden
- `ImportSucceeded(playlistCount, videoCount)` — Import abgeschlossen
- `ImportFailed(ImportError)` — Import fehlgeschlagen

### Persistenz
- `PersistRequested` — DB-Schreibvorgang anfordern
- `PersistSucceeded` — DB-Schreibvorgang erfolgreich
- `PersistFailed(message, inner)` — DB-Schreibvorgang fehlgeschlagen

### Error Handling & Notifications
- `OperationFailed(OperationError)` — kategorisierter Fehler mit Kontext
- `ShowNotification(Notification)` — zeigt Benachrichtigung in der UI
- `DismissNotification(CorrelationId)` — entfernt Benachrichtigung (manuell oder auto-dismiss)

### Action-Kategorien
**State-ändernde Actions:**
- Reducer gibt neuen State zurück
- Beispiele: `SelectVideo`, `PlaylistLoaded`, `SortChanged`

**Effect-only Actions:**
- Reducer gibt unveränderten State zurück (`state`)
- Logik läuft ausschließlich in Effects
- Beispiele: `CreatePlaylist`, `AddVideo`
- Dispatchen nachfolgende Result-Actions

Faustregel:
- **Commands** beschreiben Nutzerintentionen
- **Results** beschreiben abgeschlossene I/O-Operationen

---

## Store-Pipeline

### Dispatch
`Dispatch(action)` schreibt Actions in einen `Channel<YtAction>`.
Die Verarbeitung erfolgt seriell in einer Background-Task, um Race-Conditions zu vermeiden.

### Reduce (Pure)
Der Reducer erzeugt einen neuen, unveränderlichen `YouTubePlayerState`.
Keine DB-Zugriffe, keine JS-Aufrufe, keine asynchronen Abhängigkeiten.

**Phasenstruktur:**

1. `UndoRequested` / `RedoRequested` → direkt an `ReduceUndo` / `ReduceRedo`
2. Alle anderen Actions:
   - Pre-Snapshot des aktuellen QueueState erstellen
   - `ReduceStandard` ausführen (enthält exhaustive pattern matching)
   - `ApplyHistoryPolicy` anwenden (Undo-History aktualisieren oder zurücksetzen)
   - Falls `Videos`-Referenz geändert → `RepairPlaybackStructures` aufrufen (ShuffleOrder, History, Index-Sync)
   - `Validate()` anwenden (Bounds-Check für CurrentIndex, verwaiste CurrentItemId bereinigen)

**Exhaustive Pattern Matching (in `ReduceStandard`):**

```csharp
var newState = action switch
{
    YtAction.SelectVideo sv => HandleSelectVideo(state, sv),
    YtAction.CreatePlaylist => state, // Nur Effect
    _ => throw new UnreachableException(...)
};
```

Der Compiler zwingt zur Behandlung aller Actions.

### Effects (Async)
Effects laufen nach dem Reduce-Schritt und dürfen:
- Services aufrufen (DB, HTTP)
- JS-Interop nutzen
- weitere Actions dispatchen

**Effect-Gating:** `UndoRequested` und `RedoRequested` überspringen Effects komplett — Undo/Redo ist rein reducer-basiert, ohne DB-Persistenz oder JS-Interop.

**Export-Effect (`RunExportEffect`):**
1. Guard: Prüft, ob Playlists geladen sind
2. Map: `ExportMapper.ToEnvelope(playlists, selectedPlaylistId)` — Domain-Modelle → DTOs
3. Serialize: `ExportSerializer.Serialize(envelope)` — JSON mit camelCase
4. Dispatch: `ExportPrepared(envelope)` → State-Transition
5. Download: JS-Interop `ExportInterop.downloadFile(fileName, json)` — Blob-basierter Browser-Download
6. Dispatch: `ExportSucceeded` → Reset auf `Idle`

**Persist-Effect (`RunPersistEffect`):**
1. Guard: Prüft `IsDirty`-Flag und geladene Playlists
2. Snapshot: Klont den gesamten Playlist-Baum aus dem State
3. Replace: `ReplaceAllPlaylistsAsync(snapshot)` — atomarer DB-Austausch
4. Dispatch: `PersistSucceeded` → `IsDirty = false`, Zeitstempel setzen
5. Bei Fehler: `PersistFailed` → `IsDirty` bleibt `true` (Retry möglich)

### Lifecycle
Der Store implementiert `IDisposable`:
- `CancellationToken` stoppt die Background-Verarbeitung
- Channel wird geschlossen
- Sauberes Shutdown ohne Race Conditions

---

## UI-Komponenten

### Page (`YouTubePlayer.razor`)
Aufgaben:
- Initialisierung des Stores nach JS-Interop-Setup
- Rendern von Playlists, Queue und Controls aus dem Store-State
- Dispatch von Actions bei Klicks
- Weiterleitung von JS-Callbacks in Store-Actions

Erlaubter lokaler UI-State:
- Drawer-Flags
- SortableJS-Lifecycle-Flags

### Benachrichtigungen (MudBlazor ISnackbar)
Notifications werden über MudBlazors `ISnackbar`-Service direkt in der Page-Komponente angezeigt:
- `OnStoreStateChanged` erkennt neue Notifications im Store-State
- Severity-Mapping: `NotificationSeverity` → MudBlazor `Severity`
- Deduplizierung über `HashSet<Guid>` (`_shownNotifications`)
- Nach Anzeige wird `DismissNotification` dispatcht, um die Notification aus dem Store zu entfernen

### Drawer (MudDrawer Variant.Temporary)
Drawer sind reine Eingabekomponenten und nutzen MudBlazors `MudDrawer` mit `Variant.Temporary`:
- sammeln Nutzereingaben
- senden `EventCallback` an Parent-Komponente
- Parent übersetzt Requests in Actions und dispatcht
- persistieren keine Daten

**Warum kein direktes Dispatch?**
- Wiederverwendbarkeit (nicht an Store gekoppelt)
- Separation of Concerns
- Bessere Testbarkeit

### ImportExportToolbar (`ImportExportToolbar.razor`)
Dispatch-only Toolbar-Komponente:
- Export-Button → `EventCallback` an Parent (dispatcht `ExportRequested`)
- Import-Button → öffnet ImportDrawer
- Persist-Fehleranzeige: Chip mit Retry-Button wenn `IsDirty && LastPersistError != null`
- Buttons deaktiviert basierend auf State (`ExportInProgress`, `ImportParsing`)

### ImportDrawer (`ImportDrawer.razor`)
MudDrawer (Anchor.End, 400px) mit zwei Eingabemodi:
- **Datei-Upload**: `InputFile` mit `.json`-Filter, max. 5 MB
- **JSON-Paste**: `MudTextField` mit 8 Zeilen für manuelles Einfügen
- Fortschrittsbalken während `ImportParsing`
- Fehler-Alert bei `ImportFailed`, Erfolgs-Alert bei `ImportSucceeded` (zeigt Playlist/Video-Counts)
- Cancel/Import-Buttons, Eingabe wird als `EventCallback<string>` an Parent gesendet

---

## JavaScript-Interop

### YouTube Player
`youtube-player-interop.js`:
- lädt die YouTube IFrame API
- erstellt und steuert den Player
- leitet State-Änderungen an .NET weiter

JS ruft `[JSInvokable]`-Methoden auf, die in Actions übersetzt werden.

### Export-Interop
`export-interop.js`:
- erstellt einen `Blob` aus dem JSON-Inhalt
- erzeugt eine temporäre Download-URL via `URL.createObjectURL`
- triggert den Browser-Download über ein unsichtbares `<a>`-Element
- bereinigt URL und Element nach dem Download

Aufgerufen aus dem Export-Effect: `ExportInterop.downloadFile(fileName, json)`

### SortableJS
SortableJS verändert das DOM direkt.
Daher:
- kontrollierte Initialisierung und Zerstörung
- Sortierereignisse werden in Actions übersetzt
- Persistenz erfolgt in Store-Effects

---

## Typische Abläufe

### App-Start
1. UI initialisiert JS-Interop
2. UI dispatcht `Initialize`
3. Effect lädt Playlists → `PlaylistsLoaded`
4. Optional: erste Playlist auswählen → `SelectPlaylist`
5. Effect lädt Playlist → `PlaylistLoaded`
6. Optional: erstes Video auswählen → `SelectVideo`
7. Effect ruft JS `loadVideo` auf

### Video auswählen
1. UI dispatcht `SelectVideo`
2. Reducer aktualisiert Queue und Player-State
3. Effect ruft JS `loadVideo` auf

### Undo/Redo
1. User klickt Undo-Button → UI dispatcht `UndoRequested`
2. Reducer nimmt letzten Snapshot aus `Past`, erstellt Snapshot des aktuellen Zustands für `Future`
3. `QueueState` wird aus dem Snapshot wiederhergestellt (inkl. `VideoPositions`)
4. Effects werden übersprungen (Effect-Gating)
5. UI aktualisiert sich: vorheriger Video-Index / Sortierung ist wiederhergestellt

### Nächstes / Vorheriges (Playback-Navigation)
1. UI dispatcht `NextRequested` (oder `PrevRequested`)
2. Reducer ruft `PlaybackNavigation.ComputeNext` (oder `ComputePrev`) auf — reine Funktion
3. Entscheidung: `AdvanceTo(itemId)` → `CurrentIndex`/`CurrentItemId` setzen, `Player = Loading`
4. Entscheidung: `Stop` → `Player = Paused` (Queue-Ende, RepeatMode.Off)
5. Entscheidung: `NoOp` → keine Änderung (z. B. Prev am Anfang der sequenziellen Queue)
6. Effect ruft JS `loadVideo` auf (nutzt `LoadCurrentVideoFromState`)
7. Undo-History bleibt erhalten (Playback-transiente Actions ändern Past/Future nicht)

### Shuffle-Umschaltung
1. UI dispatcht `ShuffleSet(true, seed?)` (oder `false`)
2. Aktivierung: Reducer ruft `GenerateShuffleOrder` auf (Fisher-Yates, aktuelles Video an Index 0), leert PlaybackHistory
3. Deaktivierung: leert ShuffleOrder + PlaybackHistory
4. `CurrentItemId` und `CurrentIndex` bleiben unverändert

### Repeat-Zyklus
1. UI dispatcht `RepeatSet(mode)` — wechselt Off → All → One
2. Reducer aktualisiert `RepeatMode`
3. RepeatOne: `ComputeNext` gibt `AdvanceTo(current)` zurück, JS startet über `seekTo(0)` neu

### Video beendet (Auto-Advance)
1. JS feuert `OnVideoEnded` → UI dispatcht `VideoEnded`
2. Effect dispatcht `NextRequested` (ersetzt altes index-basiertes `SelectNextVideoWithAutoplay`)
3. Vollständige Shuffle/Repeat-Logik greift automatisch

### Export
1. User klickt Export-Button → UI dispatcht `ExportRequested`
2. Reducer setzt `ImportExportState` auf `ExportInProgress`
3. Effect: `ExportMapper.ToEnvelope()` → DTOs erzeugen
4. Effect: `ExportSerializer.Serialize()` → JSON erzeugen
5. Effect dispatcht `ExportPrepared(envelope)`
6. Effect: JS-Interop `ExportInterop.downloadFile()` → Browser-Download
7. Effect dispatcht `ExportSucceeded` → Reducer setzt State auf `Idle`

### Import
1. User wählt Datei oder fügt JSON ein → UI dispatcht `ImportRequested(jsonContent)`
2. Reducer setzt `ImportExportState` auf `ImportParsing`
3. Effect: `ExportSerializer.Deserialize()` → JSON parsen
4. Effect dispatcht `ImportParsed(envelope)` → State: `ImportParsed`
5. Effect: Schema-Version und Feld-Constraints validieren
6. Effect dispatcht `ImportValidated(envelope)` → State: `ImportValidated`
7. Effect: DTOs → Domain-Modelle konvertieren
8. Effect dispatcht `ImportApplied(playlists, selectedPlaylistId)` → Reducer ersetzt gesamten Playlist-State
9. Persist-Effect: Dirty-Flag erkannt → `ReplaceAllPlaylistsAsync()` → DB-Snapshot-Replace
10. Effect dispatcht `ImportSucceeded(playlistCount, videoCount)` → Notification mit Counts

### Playlist erstellen
1. Drawer sendet `EventCallback<CreatePlaylistRequest>`
2. Page dispatcht `CreatePlaylist`
3. Reducer gibt State unverändert zurück
4. Effect schreibt in DB
5. Effect dispatcht `PlaylistsLoaded` (lädt neu)
6. Effect dispatcht `SelectPlaylist` (wählt neue Playlist)

---

## Playback-Navigation

### Strategie-Funktionen (`PlaybackNavigation`)
Alle Navigations-Entscheidungen werden von reinen statischen Funktionen berechnet — kein I/O, vollständig unit-testbar:

| Funktion | Zweck |
|----------|-------|
| `ComputeNext(queue)` | Nächstes Video basierend auf RepeatMode + ShuffleEnabled. Pusht aktuelles Video in PlaybackHistory. |
| `ComputePrev(queue)` | Vorheriges Video. Shuffle: poppt aus PlaybackHistory. Sequenziell: Index - 1. |
| `GenerateShuffleOrder(videos, currentItemId, seed)` | Fisher-Yates-Permutation. Aktuelles Video an Index 0. Deterministisch (gleicher Seed = gleiches Ergebnis). |
| `RepairPlaybackStructures(queue)` | Wird nach Queue-Mutationen aufgerufen. Filtert ungültige IDs aus ShuffleOrder/PlaybackHistory, hängt neue Videos an, synchronisiert CurrentIndex von CurrentItemId. |

### PlaybackDecision (Rückgabetyp)
Discriminated Union:
- `AdvanceTo(Guid ItemId)` — zu bestimmtem Video navigieren
- `Stop` — Queue-Ende (RepeatMode.Off)
- `NoOp` — keine Aktion (z. B. Prev am Anfang)

### Queue-Mutations-Resilienz
Wenn Videos hinzugefügt/entfernt werden (z. B. `PlaylistLoaded`, `AddVideo`), stellt `RepairPlaybackStructures` sicher:
- ShuffleOrder enthält nur gültige Video-IDs (entfernte IDs herausgefiltert)
- Neue Videos werden an bestehende ShuffleOrder angehängt (wenn nicht leer)
- PlaybackHistory enthält nur gültige IDs
- CurrentItemId wird geleert, wenn das Video entfernt wurde
- CurrentIndex wird von CurrentItemId synchronisiert

---

## Fehlerbehandlung

### Result Pattern
Erwartbare Fehlerfälle werden über `Result<T>` abgebildet statt über Exceptions:
- `Result<T>.Success(value)` — erfolgreiche Operation
- `Result<T>.Failure(OperationError)` — kategorisierter Fehler mit Kontext

### Fehlerkategorien (`ErrorCategory`)
| Kategorie | Bedeutung | UI-Severity |
|-----------|-----------|-------------|
| `Validation` | Eingabevalidierung fehlgeschlagen (z. B. ungültige YouTube-URL) | Warning |
| `NotFound` | Ressource nicht gefunden oder Zustandskonflikt | Warning |
| `Transient` | Netzwerk-/Timeout-Fehler — potenziell wiederholbar | Warning |
| `External` | JS-Interop- oder externe API-Fehler | Error |
| `Unexpected` | Unerwartete Bugs / unbehandelte Exceptions | Error |

### OperationContext
Jede Operation erzeugt einen `OperationContext` mit:
- `Operation`: Name der Operation (z. B. `"AddVideo"`)
- `CorrelationId`: eindeutige ID zur Log-Korrelation
- `PlaylistId`, `VideoId`, `Index`: optionale Entity-Referenzen

### Fehlerfluss
1. Effect führt Operation aus
2. Bei Fehler: `OperationError` mit Kategorie und Kontext erzeugen
3. `OperationFailed(error)` dispatchen → Reducer speichert `LastError`
4. `ShowNotification(notification)` dispatchen → UI zeigt Toast
5. Strukturiertes Logging mit Correlation-ID und Entity-IDs

### Benachrichtigungen
- MudBlazors `ISnackbar`-Service rendert Notifications als Toast-Meldungen
- Farbkodiert nach Severity (Success: grün, Info: blau, Warning: gelb, Error: rot)
- Auto-Dismiss und manuelles Schließen werden von MudBlazor verwaltet
- `DismissNotification`-Action entfernt die Notification aus dem Store-State

### YouTube-URL-Validierung
`ExtractYouTubeId` validiert mehrere URL-Formate:
- `youtube.com/watch?v=ID`
- `youtu.be/ID`
- `youtube.com/embed/ID`
- `IsValidYouTubeId`: prüft 11-Zeichen YouTube-ID-Format

Ungültige URLs werden als `Validation`-Fehler mit nutzerfreundlicher Meldung behandelt.

### Logging-Strategie
- `ILogger<YouTubePlayerStore>` für alle Store-Effects
- Log-Level abgeleitet aus `ErrorCategory` (Warning/Error)
- Erfolgreiche Operationen auf `Information`-Level
- Strukturierte Properties: `Operation`, `Category`, `CorrelationId`, `PlaylistId`, `VideoId`, `Exception`
- Nutzersichtbare Meldungen explizit getrennt von technischen Log-Details

---

## Undo/Redo

### Snapshot-Modell
Undo/Redo operiert ausschließlich auf `QueueState` (nicht Player, nicht Playlists) über ein Past/Present/Future-Snapshot-Modell.

**`QueueSnapshot`** erfasst:
- `Videos` — Referenz auf die aktuelle `ImmutableList<VideoItem>`
- `VideoPositions` — paralleles Array mit den `Position`-Werten zum Snapshot-Zeitpunkt
- `SelectedPlaylistId`, `CurrentIndex`
- `RepeatMode`, `ShuffleEnabled`, `CurrentItemId`, `ShuffleOrder`, `PlaybackHistory`, `ShuffleSeed`

**Warum `VideoPositions`?** `HandleSortChanged` mutiert `VideoItem.Position` in-place auf geteilten Referenzen. Ohne separate Positionserfassung würden vergangene Snapshots durch spätere Sortierungen korrumpiert.

### UndoPolicy
Bestimmt, wie die History auf Actions reagiert:

| Regel | Actions | Verhalten |
|-------|---------|-----------|
| **Undoable** | `SelectVideo`, `SortChanged` | Snapshot wird zu `Past` hinzugefügt, `Future` wird geleert |
| **Boundary** | `PlaylistLoaded`, `SelectPlaylist`, `ImportApplied` | `Past` und `Future` werden komplett geleert |
| **Playback Transient** | `NextRequested`, `PrevRequested`, `ShuffleSet`, `RepeatSet`, `PlaybackAdvanced`, `PlaybackStopped` | History bleibt unverändert (kein Eintrag, kein Clearing) |
| **Sonstige** | Alle anderen | History bleibt unverändert |

### Limits
- Maximale History-Tiefe: **30 Einträge** (`QueueState.HistoryLimit`)
- Älteste Einträge werden bei Überschreitung entfernt (FIFO-Trim)

### Effect-Gating
`UndoRequested` und `RedoRequested` überspringen `RunEffects` komplett — keine DB-Writes, kein JS-Interop. Undo/Redo ist eine reine Reducer-Operation.

---

## Tests

### Testprojekt: `BlazorStore.Tests`
xUnit-basiertes Testprojekt mit Zugriff auf `internal`-Member via `InternalsVisibleTo`.

| Testdatei | Fokus |
|-----------|-------|
| `ExportPipelineTests` | Export-Mapper (Feld-Mapping, Sortierung), Serializer (JSON, camelCase, Roundtrip) |
| `ImportExportReducerTests` | State-Machine-Transitionen (Export/Import-Lifecycle), Dirty-Flag, Undo-Integration (ImportApplied als Boundary) |
| `UndoPolicyTests` | `IsUndoable()`, `IsBoundary()`, `IsPlaybackTransient()` für alle Action-Typen |
| `QueueSnapshotTests` | Roundtrip, Positionswiederherstellung nach Mutation, Playback-Feld-Erhaltung |
| `UndoRedoReducerTests` | Undo/Redo-Kernlogik, History-Limit, Boundary-Clearing, Multi-Step, NextRequested-Passthrough |
| `EffectGatingTests` | Keine Side-Effects bei Undo/Redo-Actions |
| `PlaybackNavigationTests` | Reine Strategie-Funktionen: Shuffle-Generierung, Next/Prev für alle Modi, Repair, Grenzfälle |
| `ShuffleRepeatReducerTests` | Volle Reducer-Pipeline: ShuffleSet, RepeatSet, NextRequested, PrevRequested, Undo-Erhaltung |
| `PlaybackIntegrationTests` | End-to-End: Sequenzielle/Repeat/Shuffle-Abläufe, Roundtrips, Queue-Mutationen, deterministische Permutation |

- **Reducer**: Unit-Tests (State + Action → neuer State)
- **Effects**: Tests mit gemockten Services und verifizierten Folge-Actions
- **Drawer**: Component-Tests ohne Store-Abhängigkeit

---

## Debug-Features

Im `DEBUG`-Modus verfügbar:
- **State-History**: Letzte 50 Transitionen (Timestamp, Action, Before/After)
- Zugriff via `Store.GetHistory()`
- Nützlich für Time-Travel-Debugging