# Architecture

This repository contains a Blazor feature ("YouTube Player") implemented with a strict, store-driven architecture.
The goal is predictable state transitions, a single source of truth, and a clean separation between UI and side effects.

## Principles

### Single Source of Truth
All feature state lives in `YouTubePlayerState` inside the `YouTubePlayerStore`.
The UI never mutates feature data directly.

### Unidirectional Data Flow
User and interop events are translated into **actions**.
Actions are processed by the store in two steps:

1. **Reduce** – pure state transition (no I/O)
2. **Effects** – side effects (DB, JS interop, async calls), optionally dispatching more actions

### UI Is Dispatch-Only
Razor components:
- render based on store state
- dispatch actions on user intent
- hold only local UI state (e.g. drawer open/close)

### Side Effects Live in the Store
Persistence (playlist/video CRUD), JS interop (YouTube iframe), and other asynchronous work is handled in store effects.
Drawers and pages do not write to the database.

---

## Folder Overview (Feature)

Typical layout:

- `Features/YouTubePlayer/Store/`
    - `YouTubePlayerStore.cs` – owns state, dispatch, reduce, and effects
- `Features/YouTubePlayer/Components/`
    - `CreatePlaylistDrawer.razor` – MudDrawer (Temporary) for playlist creation
    - `AddVideoDrawer.razor` – MudDrawer (Temporary) for adding videos
    - `ImportExportToolbar.razor` – Export/Import buttons + persist error display
    - `ImportDrawer.razor` – MudDrawer (End) with file upload and JSON paste
- `Features/YouTubePlayer/State/`
    - `YouTubePlayerState.cs` (root state)
    - sub-states (e.g. `PlaylistsState`, `QueueState`, `PlayerState`)
    - `YtAction.cs` (actions)
    - `ActionOrigin.cs` (enum: `User`, `System`, `TimeTravel`)
    - `QueueSnapshot.cs` (immutable snapshot with parallel `VideoPositions` array + playback fields)
    - `UndoPolicy.cs` (determines which actions are undoable/boundary/playback-transient)
    - `RepeatMode.cs` (enum: `Off`, `All`, `One`)
    - `PlaybackDecision.cs` (discriminated union: `AdvanceTo`, `Stop`, `NoOp`)
    - `PlaybackReasons.cs` (`AdvanceReason`, `StopReason` enums)
    - `PlaybackNavigation.cs` (pure strategy functions for shuffle/next/prev/repair)
    - `Notification.cs` (notification model + severity)
    - `OperationError.cs` (categorized errors + OperationContext)
    - `Result.cs` (Result pattern for expected failures)
    - `ImportExportState.cs` (discriminated union state machine: Idle → export/import lifecycle)
    - `PersistenceState.cs` (dirty flag, last persist attempt, error message)
- `Features/YouTubePlayer/ImportExport/`
    - `ExportDtoV1.cs` (DTOs: `ExportEnvelopeV1`, `ExportPlaylistDto`, `ExportVideoDto`)
    - `ExportMapper.cs` (domain models → DTOs, sorts videos by position)
    - `ExportSerializer.cs` (JSON serialization with camelCase and null handling)
    - `ImportPolicy.cs` (`ImportMode.ReplaceAll`, `IdStrategy.TrustIncoming`, `ImportOptions`)
    - `ImportExportErrors.cs` (discriminated union: `ImportError`, `ExportError`)
- `Features/YouTubePlayer/Models/`
    - domain models (e.g. `Playlist`, `VideoItem`)
- `wwwroot/js/`
    - `youtube-player-interop.js` (YouTube IFrame API interop)
    - `sortable-interop.js` (SortableJS integration)
    - `export-interop.js` (Blob download for JSON export)

---

## State Model

### Root State: `YouTubePlayerState`
Contains:
- `Playlists`: loading / empty / loaded (list of playlists)
- `Queue`: selected playlist, ordered list of videos, current index
- `Player`: player status (empty / loading / buffering / playing / paused)
- `Notifications`: `ImmutableList<Notification>` — active notifications for the UI
- `LastError`: `OperationError?` — last encountered error (for debugging)
- `ImportExport`: discriminated union state machine for the import/export lifecycle
- `Persistence`: dirty flag, last persist attempt, error message

The UI derives all view decisions from these values.

### Immutability
- State slices are `record` types
- Collections are `ImmutableList<T>`
- Changes produce new instances via `with` expressions

### Queue
- `SelectedPlaylistId`: `Guid?`
- `Videos`: `ImmutableList<VideoItem>` (sorted by `Position`)
- `CurrentIndex`: `int?` (current selection)
- `CurrentItemId`: `Guid?` — ID-based tracking (stable across sort operations)
- `Past`: `ImmutableList<QueueSnapshot>` — undo history (max 30 entries)
- `Future`: `ImmutableList<QueueSnapshot>` — redo history
- `CanUndo` / `CanRedo`: derived properties for UI binding

**Playback navigation:**
- `RepeatMode`: `Off` | `All` | `One`
- `ShuffleEnabled`: `bool`
- `ShuffleOrder`: `ImmutableList<Guid>` — permutation of video IDs (current at front)
- `PlaybackHistory`: `ImmutableList<Guid>` — stack semantics for Previous in shuffle mode (max 100)
- `ShuffleSeed`: `int` — deterministic seed for Fisher-Yates shuffle

### Player
Represents the playback lifecycle and is updated only via actions dispatched from JS interop
(e.g. `PlayerStateChanged`, `VideoEnded`).

---

## Actions

Actions are the only input into the store.

### Commands (Intent)
Triggered by the UI:
- `Initialize`
- `SelectPlaylist(playlistId)`
- `SelectVideo(index, autoplay)`
- `CreatePlaylist(...)`
- `AddVideo(...)`
- `SortChanged(oldIndex, newIndex)`
- `ShuffleSet(enabled, seed?)` — toggle shuffle on/off
- `RepeatSet(mode)` — cycle repeat mode (Off → All → One)
- `NextRequested` — advance to next video
- `PrevRequested` — go to previous video
- `UndoRequested` — revert last queue change
- `RedoRequested` — restore undone change

### Results (Loaded / Derived)
Triggered by effects:
- `PlaylistsLoaded(playlists)`
- `PlaylistLoaded(playlist)`
- `PlaybackAdvanced(toItemId, reason)` — effect routing
- `PlaybackStopped(reason)` — effect routing

### Import/Export
Triggered by the UI:
- `ExportRequested` — start export
- `ImportRequested(jsonContent)` — start import with JSON content

Triggered by effects (lifecycle):
- `ExportPrepared(envelope)` — DTO serialized, ready for download
- `ExportSucceeded` — download completed
- `ExportFailed(ExportError)` — export failed at any stage
- `ImportParsed(envelope)` — JSON parsed successfully
- `ImportValidated(envelope)` — validation passed
- `ImportApplied(playlists, selectedPlaylistId)` — domain models created, ready to apply
- `ImportSucceeded(playlistCount, videoCount)` — import completed
- `ImportFailed(ImportError)` — import failed at any stage

### Persistence
- `PersistRequested` — request DB write
- `PersistSucceeded` — DB write successful
- `PersistFailed(message, inner)` — DB write failed

### Error Handling & Notifications
- `OperationFailed(OperationError)` — categorized error with context
- `ShowNotification(Notification)` — displays notification in the UI
- `DismissNotification(CorrelationId)` — removes notification (manual or auto-dismiss)

### Action Categories
**State-changing actions:**
- Reducer returns new state
- Examples: `SelectVideo`, `PlaylistLoaded`, `SortChanged`

**Effect-only actions:**
- Reducer returns unchanged state (`state`)
- Logic runs exclusively in effects
- Examples: `CreatePlaylist`, `AddVideo`
- Dispatch subsequent result actions

Rule of thumb:
- **Commands** express what the user wants
- **Results** represent completed I/O

---

## Store Pipeline

### Dispatch
`Dispatch(action)` writes actions into a `Channel<YtAction>`.
Processing happens serially in a background task to avoid race conditions.

### Reduce (Pure)
The reducer returns a new immutable `YouTubePlayerState`.
No DB calls, no JS calls, no timing dependencies.

**Phase structure:**

1. `UndoRequested` / `RedoRequested` → routed directly to `ReduceUndo` / `ReduceRedo`
2. All other actions:
   - Capture pre-snapshot of current QueueState
   - Execute `ReduceStandard` (contains exhaustive pattern matching)
   - Apply `ApplyHistoryPolicy` (update or reset undo history)
   - If `Videos` reference changed → call `RepairPlaybackStructures` (fix shuffle order, history, sync index)
   - Apply `Validate()` (bounds-check CurrentIndex, clear stale CurrentItemId)

**Exhaustive Pattern Matching (in `ReduceStandard`):**

```csharp
var newState = action switch
{
    YtAction.SelectVideo sv => HandleSelectVideo(state, sv),
    YtAction.CreatePlaylist => state, // Effect-only
    _ => throw new UnreachableException(...)
};
```

The compiler enforces handling of all actions.

### Effects (Async)
Effects are triggered after reducing.
They may:
- call services (DB, HTTP)
- call JS interop
- dispatch additional actions

**Effect gating:** `UndoRequested` and `RedoRequested` skip effects entirely — undo/redo is purely reducer-based, with no DB persistence or JS interop.

**Export effect (`RunExportEffect`):**
1. Guard: check that playlists are loaded
2. Map: `ExportMapper.ToEnvelope(playlists, selectedPlaylistId)` — domain models → DTOs
3. Serialize: `ExportSerializer.Serialize(envelope)` — JSON with camelCase
4. Dispatch: `ExportPrepared(envelope)` → state transition
5. Download: JS interop `ExportInterop.downloadFile(fileName, json)` — Blob-based browser download
6. Dispatch: `ExportSucceeded` → reset to `Idle`

**Persist effect (`RunPersistEffect`):**
1. Guard: check `IsDirty` flag and loaded playlists
2. Snapshot: clone the entire playlist tree from state
3. Replace: `ReplaceAllPlaylistsAsync(snapshot)` — atomic DB replacement
4. Dispatch: `PersistSucceeded` → `IsDirty = false`, set timestamp
5. On failure: `PersistFailed` → `IsDirty` stays `true` (retry possible)

### Lifecycle
The store implements `IDisposable`:
- `CancellationToken` stops background processing
- Channel is closed
- Clean shutdown without race conditions

---

## UI Components

### Page (`YouTubePlayer.razor`)
Responsibilities:
- initialize the store after JS interop is ready
- render playlists, queue, and controls from store state
- dispatch actions on clicks
- forward JS callbacks into store actions

Allowed local UI state:
- drawer open flags
- SortableJS initialization flags

### Notifications (MudBlazor ISnackbar)
Notifications are displayed via MudBlazor's `ISnackbar` service directly in the page component:
- `OnStoreStateChanged` detects new notifications in store state
- Severity mapping: `NotificationSeverity` → MudBlazor `Severity`
- Deduplication via `HashSet<Guid>` (`_shownNotifications`)
- After display, `DismissNotification` is dispatched to remove the notification from the store

### Drawers (MudDrawer Variant.Temporary)
Drawers are input components using MudBlazor's `MudDrawer` with `Variant.Temporary`:
- collect user input
- emit `EventCallback` to parent component
- parent translates requests into actions and dispatches
- do not persist data

**Why no direct dispatch?**
- Reusability (not coupled to store)
- Separation of concerns
- Better testability

### ImportExportToolbar (`ImportExportToolbar.razor`)
Dispatch-only toolbar component:
- Export button → `EventCallback` to parent (dispatches `ExportRequested`)
- Import button → opens ImportDrawer
- Persist error display: chip with Retry button when `IsDirty && LastPersistError != null`
- Buttons disabled based on state (`ExportInProgress`, `ImportParsing`)

### ImportDrawer (`ImportDrawer.razor`)
MudDrawer (Anchor.End, 400px) with two input modes:
- **File upload**: `InputFile` with `.json` filter, max 5 MB
- **JSON paste**: `MudTextField` with 8 lines for manual input
- Progress bar during `ImportParsing`
- Error alert on `ImportFailed`, success alert on `ImportSucceeded` (shows playlist/video counts)
- Cancel/Import buttons, input sent as `EventCallback<string>` to parent

---

## JS Interop

### YouTube Player
`youtube-player-interop.js`:
- loads the YouTube IFrame API
- creates and controls the player
- forwards state changes to .NET

JS calls into .NET via `[JSInvokable]` methods, which are translated into actions.

### Export Interop
`export-interop.js`:
- creates a `Blob` from the JSON content
- creates a temporary download URL via `URL.createObjectURL`
- triggers the browser download through an invisible `<a>` element
- cleans up URL and element after download

Called from the export effect: `ExportInterop.downloadFile(fileName, json)`

### SortableJS
SortableJS mutates the DOM.
Therefore:
- initialization and teardown are controlled explicitly
- reorder events are translated into actions
- persistence happens in store effects

---

## Typical Flows

### App Start
1. UI initializes JS interop
2. UI dispatches `Initialize`
3. Effect loads playlists → `PlaylistsLoaded`
4. Optionally select first playlist → `SelectPlaylist`
5. Effect loads playlist → `PlaylistLoaded`
6. Optionally select first video → `SelectVideo`
7. Effect calls JS `loadVideo`

### Select Video
1. UI dispatches `SelectVideo`
2. Reducer updates queue and player state
3. Effect calls JS `loadVideo`

### Undo/Redo
1. User clicks Undo button → UI dispatches `UndoRequested`
2. Reducer takes last snapshot from `Past`, creates snapshot of current state for `Future`
3. `QueueState` is restored from snapshot (including `VideoPositions`)
4. Effects are skipped (effect gating)
5. UI updates: previous video index / sort order is restored

### Next / Previous (Playback Navigation)
1. UI dispatches `NextRequested` (or `PrevRequested`)
2. Reducer calls `PlaybackNavigation.ComputeNext` (or `ComputePrev`) — pure function
3. Decision: `AdvanceTo(itemId)` → set `CurrentIndex`/`CurrentItemId`, `Player = Loading`
4. Decision: `Stop` → `Player = Paused` (end of queue, RepeatMode.Off)
5. Decision: `NoOp` → no change (e.g. Prev at start of sequential queue)
6. Effect calls JS `loadVideo` (reuses `LoadCurrentVideoFromState`)
7. Undo history is preserved (playback-transient actions don't modify Past/Future)

### Shuffle Toggle
1. UI dispatches `ShuffleSet(true, seed?)` (or `false`)
2. Enabling: reducer calls `GenerateShuffleOrder` (Fisher-Yates, current item at index 0), clears PlaybackHistory
3. Disabling: clears ShuffleOrder + PlaybackHistory
4. `CurrentItemId` and `CurrentIndex` remain unchanged

### Repeat Cycle
1. UI dispatches `RepeatSet(mode)` — cycles Off → All → One
2. Reducer updates `RepeatMode`
3. RepeatOne: `ComputeNext` returns `AdvanceTo(current)`, JS restarts via `seekTo(0)`

### Video Ended (Auto-Advance)
1. JS fires `OnVideoEnded` → UI dispatches `VideoEnded`
2. Effect dispatches `NextRequested` (replacing old index-based `SelectNextVideoWithAutoplay`)
3. Full shuffle/repeat logic applies automatically

### Export
1. User clicks Export button → UI dispatches `ExportRequested`
2. Reducer sets `ImportExportState` to `ExportInProgress`
3. Effect: `ExportMapper.ToEnvelope()` → create DTOs
4. Effect: `ExportSerializer.Serialize()` → create JSON
5. Effect dispatches `ExportPrepared(envelope)`
6. Effect: JS interop `ExportInterop.downloadFile()` → browser download
7. Effect dispatches `ExportSucceeded` → reducer resets state to `Idle`

### Import
1. User selects file or pastes JSON → UI dispatches `ImportRequested(jsonContent)`
2. Reducer sets `ImportExportState` to `ImportParsing`
3. Effect: `ExportSerializer.Deserialize()` → parse JSON
4. Effect dispatches `ImportParsed(envelope)` → state: `ImportParsed`
5. Effect: validate schema version and field constraints
6. Effect dispatches `ImportValidated(envelope)` → state: `ImportValidated`
7. Effect: convert DTOs → domain models
8. Effect dispatches `ImportApplied(playlists, selectedPlaylistId)` → reducer replaces entire playlist state
9. Persist effect: dirty flag detected → `ReplaceAllPlaylistsAsync()` → DB snapshot replace
10. Effect dispatches `ImportSucceeded(playlistCount, videoCount)` → notification with counts

### Create Playlist
1. Drawer emits `EventCallback<CreatePlaylistRequest>`
2. Page dispatches `CreatePlaylist`
3. Reducer returns unchanged state
4. Effect writes to DB
5. Effect dispatches `PlaylistsLoaded` (reloads)
6. Effect dispatches `SelectPlaylist` (selects new playlist)

---

## Playback Navigation

### Strategy Functions (`PlaybackNavigation`)
All navigation decisions are computed by pure static functions — no I/O, fully unit-testable:

| Function | Purpose |
|----------|---------|
| `ComputeNext(queue)` | Next video based on RepeatMode + ShuffleEnabled. Pushes current to PlaybackHistory. |
| `ComputePrev(queue)` | Previous video. Shuffle: pops from PlaybackHistory. Sequential: index - 1. |
| `GenerateShuffleOrder(videos, currentItemId, seed)` | Fisher-Yates permutation. Current item moved to index 0. Deterministic (same seed = same result). |
| `RepairPlaybackStructures(queue)` | Called after queue mutations. Filters invalid IDs from ShuffleOrder/PlaybackHistory, appends new videos, syncs CurrentIndex from CurrentItemId. |

### PlaybackDecision (Return Type)
Discriminated union:
- `AdvanceTo(Guid ItemId)` — navigate to specific video
- `Stop` — end of queue (RepeatMode.Off)
- `NoOp` — no action (e.g. Prev at start)

### Queue Mutation Resilience
When videos are added/removed (e.g. `PlaylistLoaded`, `AddVideo`), `RepairPlaybackStructures` ensures:
- ShuffleOrder only contains valid video IDs (removed IDs filtered out)
- New videos are appended to existing ShuffleOrder (if non-empty)
- PlaybackHistory only contains valid IDs
- CurrentItemId is cleared if its video was removed
- CurrentIndex is synced from CurrentItemId

---

## Error Handling

### Result Pattern
Expected failure cases are handled via `Result<T>` instead of exceptions:
- `Result<T>.Success(value)` — successful operation
- `Result<T>.Failure(OperationError)` — categorized error with context

### Error Categories (`ErrorCategory`)
| Category | Meaning | UI Severity |
|----------|---------|-------------|
| `Validation` | Input validation failed (e.g. invalid YouTube URL) | Warning |
| `NotFound` | Resource not found or state conflict | Warning |
| `Transient` | Network/timeout errors — potentially retryable | Warning |
| `External` | JS interop or external API errors | Error |
| `Unexpected` | Unexpected bugs / unhandled exceptions | Error |

### OperationContext
Every operation creates an `OperationContext` with:
- `Operation`: operation name (e.g. `"AddVideo"`)
- `CorrelationId`: unique ID for log correlation
- `PlaylistId`, `VideoId`, `Index`: optional entity references

### Error Flow
1. Effect executes operation
2. On failure: create `OperationError` with category and context
3. Dispatch `OperationFailed(error)` → reducer stores `LastError`
4. Dispatch `ShowNotification(notification)` → UI shows toast
5. Structured logging with correlation ID and entity IDs

### Notifications
- MudBlazor's `ISnackbar` service renders notifications as toast messages
- Color-coded by severity (Success: green, Info: blue, Warning: yellow, Error: red)
- Auto-dismiss and manual close are managed by MudBlazor
- `DismissNotification` action removes the notification from store state

### YouTube URL Validation
`ExtractYouTubeId` validates multiple URL formats:
- `youtube.com/watch?v=ID`
- `youtu.be/ID`
- `youtube.com/embed/ID`
- `IsValidYouTubeId`: validates 11-character YouTube ID format

Invalid URLs are treated as `Validation` errors with user-friendly messages.

### Logging Strategy
- `ILogger<YouTubePlayerStore>` for all store effects
- Log levels derived from `ErrorCategory` (Warning/Error)
- Successful operations logged at `Information` level
- Structured properties: `Operation`, `Category`, `CorrelationId`, `PlaylistId`, `VideoId`, `Exception`
- User-facing messages explicitly separated from technical log details

---

## Undo/Redo

### Snapshot Model
Undo/Redo operates exclusively on `QueueState` (not Player, not Playlists) via a Past/Present/Future snapshot model.

**`QueueSnapshot`** captures:
- `Videos` — reference to the current `ImmutableList<VideoItem>`
- `VideoPositions` — parallel array with `Position` values at snapshot time
- `SelectedPlaylistId`, `CurrentIndex`
- `RepeatMode`, `ShuffleEnabled`, `CurrentItemId`, `ShuffleOrder`, `PlaybackHistory`, `ShuffleSeed`

**Why `VideoPositions`?** `HandleSortChanged` mutates `VideoItem.Position` in-place on shared references. Without separate position capture, past snapshots would be silently corrupted by later sorts.

### UndoPolicy
Determines how history reacts to actions:

| Rule | Actions | Behavior |
|------|---------|----------|
| **Undoable** | `SelectVideo`, `SortChanged` | Snapshot pushed to `Past`, `Future` cleared |
| **Boundary** | `PlaylistLoaded`, `SelectPlaylist`, `ImportApplied` | `Past` and `Future` cleared entirely |
| **Playback Transient** | `NextRequested`, `PrevRequested`, `ShuffleSet`, `RepeatSet`, `PlaybackAdvanced`, `PlaybackStopped` | History preserved as-is (no entry, no clearing) |
| **Other** | All others | History unchanged |

### Limits
- Maximum history depth: **30 entries** (`QueueState.HistoryLimit`)
- Oldest entries are removed when limit is exceeded (FIFO trim)

### Effect Gating
`UndoRequested` and `RedoRequested` skip `RunEffects` entirely — no DB writes, no JS interop. Undo/Redo is a pure reducer operation.

---

## Testing

### Test Project: `BlazorStore.Tests`
xUnit-based test project with access to `internal` members via `InternalsVisibleTo`.

| Test File | Focus |
|-----------|-------|
| `ExportPipelineTests` | Export mapper (field mapping, sorting), serializer (JSON, camelCase, round-trip) |
| `ImportExportReducerTests` | State machine transitions (export/import lifecycle), dirty flag, undo integration (ImportApplied as boundary) |
| `UndoPolicyTests` | `IsUndoable()`, `IsBoundary()`, `IsPlaybackTransient()` for all action types |
| `QueueSnapshotTests` | Round-trip, position restoration after mutation, playback field preservation |
| `UndoRedoReducerTests` | Core undo/redo logic, history limit, boundary clearing, multi-step, NextRequested passthrough |
| `EffectGatingTests` | No side-effects on undo/redo actions |
| `PlaybackNavigationTests` | Pure strategy functions: shuffle generation, next/prev for all mode combinations, repair, edge cases |
| `ShuffleRepeatReducerTests` | Full reducer pipeline: ShuffleSet, RepeatSet, NextRequested, PrevRequested, undo preservation |
| `PlaybackIntegrationTests` | End-to-end: sequential/repeat/shuffle sequences, round-trips, queue mutations, deterministic permutation |

- **Reducer**: unit-testable (state + action → new state)
- **Effects**: testable via mocked services and asserted follow-up actions
- **Drawers**: component tests without store dependency

---

## Debug Features

Available in `DEBUG` mode:
- **State History**: Last 50 transitions (timestamp, action, before/after)
- Access via `Store.GetHistory()`
- Useful for time-travel debugging