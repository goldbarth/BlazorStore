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
- `Features/YouTubePlayer/State/`
    - `YouTubePlayerState.cs` (root state)
    - sub-states (e.g. `PlaylistsState`, `QueueState`, `PlayerState`)
    - `YtAction.cs` (actions)
- `Features/YouTubePlayer/Models/`
    - domain models (e.g. `Playlist`, `VideoItem`)
- `wwwroot/js/`
    - `youtube-player-interop.js` (YouTube IFrame API interop)
    - `sortable-interop.js` (SortableJS integration)

---

## State Model

### Root State: `YouTubePlayerState`
Contains:
- `Playlists`: loading / empty / loaded (list of playlists)
- `Queue`: selected playlist, ordered list of videos, current index
- `Player`: player status (empty / loading / buffering / playing / paused)

The UI derives all view decisions from these values.

### Immutability
- State slices are `record` types
- Collections are `ImmutableList<T>`
- Changes produce new instances via `with` expressions

### Queue
- `SelectedPlaylistId`: `Guid?`
- `Videos`: `ImmutableList<VideoItem>` (sorted by `Position`)
- `CurrentIndex`: `int?` (current selection)

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

### Results (Loaded / Derived)
Triggered by effects:
- `PlaylistsLoaded(playlists)`
- `PlaylistLoaded(playlist)`
- optional error reporting (e.g. `OperationFailed(message)`)

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

**Exhaustive Pattern Matching:**

```csharp
var newState = action switch 
{ 
    YtAction.SelectVideo sv => HandleSelectVideo(state, sv), 
    YtAction.CreatePlaylist => state, // Effect-only _ => throw new UnreachableException(...) 
};
```

The compiler enforces handling of all actions.

### Effects (Async)
Effects are triggered after reducing.
They may:
- call services (DB, HTTP)
- call JS interop
- dispatch additional actions

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

### Drawers
Drawers are input components:
- collect user input
- emit `EventCallback` to parent component
- parent translates requests into actions and dispatches
- do not persist data

**Why no direct dispatch?**
- Reusability (not coupled to store)
- Separation of concerns
- Better testability

---

## JS Interop

### YouTube Player
`youtube-player-interop.js`:
- loads the YouTube IFrame API
- creates and controls the player
- forwards state changes to .NET

JS calls into .NET via `[JSInvokable]` methods, which are translated into actions.

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

### Create Playlist
1. Drawer emits `EventCallback<CreatePlaylistRequest>`
2. Page dispatches `CreatePlaylist`
3. Reducer returns unchanged state
4. Effect writes to DB
5. Effect dispatches `PlaylistsLoaded` (reloads)
6. Effect dispatches `SelectPlaylist` (selects new playlist)

---

## Error Handling
Errors should not throw from UI handlers.
Instead, the store can dispatch `OperationFailed(message)` and the UI can render feedback.

---

## Testing Notes
- **Reducer**: unit-testable (state + action → new state)
- **Effects**: testable via mocked services and asserted follow-up actions
- **Drawers**: component tests without store dependency

---

## Debug Features

Available in `DEBUG` mode:
- **State History**: Last 50 transitions (timestamp, action, before/after)
- Access via `Store.GetHistory()`
- Useful for time-travel debugging