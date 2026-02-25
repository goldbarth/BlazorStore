
# 📐 Architecture Decision Records (ADR-Light)

> Compact documentation of the key architectural decisions in this project — not as a formal RFC, but as traceable reasoning.

---

## ADR-001: Store Architecture over MVVM

**Decision**
Central store with unidirectional data flow instead of the classic MVVM pattern.

**Reasoning**
MVVM is familiar to me from WPF development and works well there. For this project, I deliberately wanted to learn a different architecture based on a single source of truth that enforces explicit, traceable state transitions. The store approach makes state changes testable and predictable — especially with async flows and JS interop.

**Consequences**
More boilerplate (actions, reducers, effects), but a clear separation of state logic and side effects. Every change is traceable and reproducible.

---

## ADR-002: Explicit JS Interop over Blazor Abstractions

**Decision**
JavaScript APIs (YouTube IFrame, SortableJS) are integrated via explicit interop calls — not through Blazor wrappers or third-party components.

**Reasoning**
Blazor wrappers often hide internal state that doesn't live in the store. With two simultaneous state sources (Blazor + JS), race conditions and hard-to-trace bugs emerge. Explicit interop ensures that JS serves only as an execution layer while all state remains in the store.

**Consequences**
More manual interop code, but no hidden state between C# and JavaScript. Every JS-side effect flows back into the store as an action.

---

## ADR-003: Immutable Records for State Slices

**Decision**
Feature state is modeled as C# `record` types — changes always produce new instances via `with` expressions.

**Reasoning**
Immutable state prevents accidental mutation outside the reducer. Change detection becomes trivial (reference comparison instead of deep compare), and the foundation for future features like undo/redo is built in from the start.

**Consequences**
Slightly more allocation from new instances, which is irrelevant at this project's scale. In return: guaranteed correct state transitions and simpler debugging.

---

## ADR-004: SortableJS Outside of Blazor Diffing

**Decision**
Drag & drop runs entirely through SortableJS directly on the DOM — not through Blazor components or MudBlazor DnD.

**Reasoning**
Drag & drop is a DOM problem, not a UI state problem. SortableJS works directly on the DOM without virtual DOM overhead, delivers clean `oldIndex`/`newIndex` events, and requires no continuous syncing during movement. A single event at the end of the drag is enough to update the store. Component-based solutions would trigger re-renders on every mouse move and introduce additional race conditions with Blazor's diffing.

**Consequences**
Blazor "knows nothing" about DOM manipulation during the drag — only the `onEnd` event flows into the store as an action. This requires deliberate lifecycle handling, but keeps the data flow clean and performant.

---

## ADR-005: ImmutableList for State Collections

**Decision**
Collections in state (e.g. `Videos`, `Playlists`) are modeled as `ImmutableList<T>` instead of `List<T>`.

**Reasoning**
`ImmutableList` enforces immutable collections and prevents accidental mutations outside the reducer. Every change creates a new collection instance, simplifying change detection and eliminating race conditions during concurrent access. The slightly higher allocation overhead is negligible at this project's scale.

**Consequences**
- Reducers must explicitly call `.ToImmutableList()` after mutations
- Collections are guaranteed threadsafe for read access
- Foundation for future features like undo/redo is established

---

## ADR-006: Channel-based Action Queue

**Decision**
Actions are serialized through a `Channel<YtAction>` instead of `SemaphoreSlim`.

**Reasoning**
`Channel<T>` is more idiomatic for producer-consumer patterns in modern .NET and provides built-in backpressure mechanisms. Action processing runs in a dedicated background task that can be cleanly stopped via `CancellationToken`. This prevents race conditions and guarantees FIFO ordering.

**Consequences**
- All actions are processed serially (no parallelism)
- Clean lifecycle management via `IDisposable`
- Easier testability through deterministic behavior

---

## ADR-007: Exhaustive Pattern Matching in Reducer

**Decision**
The reducer uses exhaustive pattern matching with `UnreachableException` for unhandled actions.

**Reasoning**
The compiler enforces explicit handling of all action types. New actions cannot be accidentally "forgotten". Actions that only trigger side-effects (e.g. `CreatePlaylist`, `AddVideo`) explicitly return unchanged state. This makes the intent clear in code.

**Consequences**
- Compiler-guaranteed action completeness
- Clear documentation of which actions change state and which don't
- Runtime exception for forgotten actions (instead of silent ignore)

---

## ADR-008: Result Pattern for Error Handling

**Decision**
Expected failure cases in store operations are handled via a `Result<T>` pattern instead of exceptions.

**Reasoning**
Exceptions are meant for unexpected errors. Validation failures (e.g. invalid YouTube URL), missing resources, or external API errors are *expected* and should not interrupt normal control flow. The Result pattern allows explicit distinction between `Success(T)` and `Failure(OperationError)` with categorized errors (`Validation`, `NotFound`, `Transient`, `External`, `Unexpected`).

**Consequences**
- Effects can handle errors specifically instead of relying solely on try-catch
- Error categories enable differentiated UI responses (Warning vs. Error)
- `OperationContext` with correlation IDs allows log correlation across async flows
- Technical error details remain separated from user-facing messages

---

## ADR-009: Notification System with MudBlazor ISnackbar

**Decision**
User feedback is managed through a centralized notification system in store state — rendered via MudBlazor's `ISnackbar` service.

**Reasoning**
Error messages, warnings, and success messages are UI state and belong in the store. Instead of `alert()` calls or component-local error states, an `ImmutableList<Notification>` is maintained in `YouTubePlayerState`. The page component injects `ISnackbar` and bridges store notifications to MudBlazor snackbars in the `OnStoreStateChanged` handler. Deduplication is done via `HashSet<Guid>`. After display, `DismissNotification` is dispatched to remove the notification from the store. Originally a custom `NotificationPanel` component with manual timers and CSS animations was used — this was removed in favor of the native MudBlazor solution.

**Consequences**
- Notifications are part of the unidirectional data flow
- No hidden UI state for error messages
- Severity mapping: `Validation`/`NotFound`/`Transient` → Warning, `External`/`Unexpected` → Error
- Notifications can be verified in tests via state inspection
- MudBlazor handles rendering, animation, and auto-dismiss — no custom timer code needed

---

## ADR-010: Structured Logging with Operation Context

**Decision**
All store effects log in a structured manner via `ILogger<YouTubePlayerStore>` with an `OperationContext` containing operation name, correlation ID, and entity IDs.

**Reasoning**
In async flows (DB → JS interop → follow-up actions), correlating log entries without explicit context is nearly impossible. The `OperationContext` is created for every operation and contains a unique `CorrelationId` along with optional `PlaylistId`, `VideoId`, and `Index`. Log levels are derived from `ErrorCategory` (Warning/Error). Successful operations are logged at `Information` level.

**Consequences**
- Every operation is traceable via its correlation ID
- Technical log details and user-facing messages are explicitly separated
- Log entries contain structured properties for machine-readable analysis
- For `Unexpected` errors, the correlation ID is displayed in the notification to enable log tracing

---

## ADR-011: Snapshot-based Undo/Redo for QueueState

**Decision**
Undo/Redo is implemented exclusively for `QueueState` — via a Past/Present/Future snapshot model with `ImmutableList<QueueSnapshot>` stacks in state.

**Reasoning**
The store architecture with immutable state and pure reducers is ideally suited for time-travel features. Rather than building a generic command stack, snapshots of `QueueState` are captured before each undoable action. This is simpler, more direct, and avoids the complexity of inverse operations.

**Critical detail — `VideoPositions`:** `VideoItem` is a mutable class (not a record). `HandleSortChanged` mutates `Position` in-place on shared references. Without a separate `VideoPositions` array in the snapshot, past snapshots would be silently corrupted by later sorts. The parallel array captures `Position` values at snapshot time and restores them on undo.

**UndoPolicy** determines behavior per action:
- `SelectVideo`, `SortChanged` → undoable (snapshot pushed to Past)
- `PlaylistLoaded`, `SelectPlaylist` → boundary (complete history reset)
- All others → history unchanged

**Effect gating:** `UndoRequested`/`RedoRequested` skip `RunEffects` entirely — undo/redo is purely reducer-based, with no DB persistence or JS interop.

**Consequences**
- Only queue mutations are undoable — player state and playlist management remain outside
- History limit of 30 entries prevents memory issues
- New undoable actions only require an update to `UndoPolicy.IsUndoable()`
- Comprehensive test coverage (27 tests) ensures correctness

---

## ADR-012: MudLayout Migration for Layout and Navigation

**Decision**
The entire app layout is migrated to MudBlazor's `MudLayout` system — with `MudAppBar`, `MudDrawer` (Variant.Mini) for the sidebar, and `MudNavMenu`/`MudNavLink` for navigation. Page-level drawers (CreatePlaylist, AddVideo) use `MudDrawer` with `Variant.Temporary`.

**Reasoning**
The original layout was based on manual HTML structure with custom CSS for sidebar, navigation, and toggle logic. MudBlazor's `MudDrawer` with `Variant.Temporary` requires a `MudLayout` context to function correctly (positioning, overlay, slide animation). Instead of workarounds (conditional rendering, `display:none`), the entire layout was migrated to MudLayout. This aligns the project with MudBlazor best practices and simultaneously resolves the drawer positioning issues.

**Consequences**
- `MainLayout.razor` uses `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`
- Navigation via `MudNavMenu`/`MudNavLink` with Material Icons instead of manual SVG icons
- Sidebar: `DrawerVariant.Mini` (60px collapsed, 250px expanded) with text fading via scoped CSS
- Page-level drawers: `DrawerVariant.Temporary` with correct overlay and slide animation
- `div.app-root` wrapper enables `::deep` access to MudBlazor internal classes
- Less custom CSS code, as MudBlazor handles layout and responsive behavior

---

## ADR-013: Permutation-based Shuffle with Pure Strategy Functions

**Decision**
Shuffle, repeat, and playback navigation are implemented as pure static functions in `PlaybackNavigation`, returning `PlaybackDecision` (discriminated union: `AdvanceTo` / `Stop` / `NoOp`) along with an updated `QueueState`. Shuffle uses Fisher-Yates permutation with a deterministic seed. Previous in shuffle mode uses a `PlaybackHistory` stack (LIFO).

**Reasoning**
Playback navigation involves multiple interacting dimensions (shuffle on/off, three repeat modes, queue mutations). Implementing this logic inside the reducer would make it hard to test in isolation. By extracting `ComputeNext`, `ComputePrev`, `GenerateShuffleOrder`, and `RepairPlaybackStructures` as pure static functions, each can be unit-tested independently with deterministic inputs. The `PlaybackDecision` return type makes branching in the reducer explicit (no side-channel state).

Key design choices:
- **Permutation, not random pick**: `GenerateShuffleOrder` produces a full permutation upfront (every video plays exactly once before repeating), rather than picking randomly at each step. This prevents duplicates and allows Previous to retrace the exact path.
- **PlaybackHistory stack**: In shuffle mode, every `ComputeNext` pushes the current video onto `PlaybackHistory`. `ComputePrev` pops from it. This gives the user exact history-based navigation rather than "previous in shuffle order" (which would be confusing).
- **Deterministic seed**: Same seed produces the same permutation, enabling reproducible test scenarios and potential future persistence.
- **RepairPlaybackStructures**: Called after any queue mutation (add/remove video), this function filters stale IDs and appends new videos, making the system resilient to concurrent modifications.
- **Playback-transient UndoPolicy**: `IsPlaybackTransient` ensures that navigation actions (`NextRequested`, `PrevRequested`, `ShuffleSet`, `RepeatSet`) pass through the undo system without creating history entries or clearing Future — they are orthogonal to undo/redo.

**Consequences**
- 95 tests total (53 new) with full coverage of all mode combinations and edge cases
- `QueueSnapshot` captures all 6 new fields, ensuring undo/redo preserves shuffle/repeat state
- Reducer handlers are thin: delegate to pure functions, then apply the decision
- New playback modes (e.g. shuffle algorithms, weighted repeat) can be added by extending `PlaybackNavigation` without touching the reducer
- `VideoEnded` effect now dispatches `NextRequested` instead of direct index calculation, unifying all auto-advance logic through the same path

---

## ADR-014: Import/Export with Discriminated Union State Machine and Multi-Stage Effect Pipeline

**Decision**
Import and export are implemented as multi-stage effect pipelines whose lifecycle is modeled via a discriminated union state machine (`ImportExportState`). Export uses a DTO pipeline (Domain → Mapper → Serializer → JS download). Import goes through Parse → Validate → Apply → Persist. Persistence uses a dirty-tracking mechanism with atomic DB snapshot replacement.

**Reasoning**
Import/export are inherently multi-stage processes with distinct failure points (parse errors, schema mismatch, validation errors, DB errors, JS interop errors). Rather than a monolithic try-catch block, each step is modeled as a separate action that triggers a state transition in the discriminated union. This makes progress visible to the UI (progress bar, error alerts), enables granular error handling, and keeps the reducer testable.

Key design choices:
- **Discriminated union state machine**: `ImportExportState` with 10 states (Idle, ExportInProgress, ExportSucceeded, ExportFailed, ImportParsing, ImportParsed, ImportValidated, ImportApplied, ImportSucceeded, ImportFailed). Each state is its own record type — exhaustive pattern matching enforces complete handling in UI and reducer.
- **DTO pipeline for export**: `ExportMapper` converts domain models to versioned DTOs (`ExportEnvelopeV1` with `SchemaVersion: 1`). `ExportSerializer` serializes with camelCase. JS interop (`export-interop.js`) triggers Blob-based browser download. Each step is independently testable.
- **Schema versioning**: `SchemaVersion` field in the export envelope enables future migration strategies without breaking changes to existing backups.
- **ImportPolicy (ReplaceAll + TrustIncoming)**: v1 supports only full replacement of all playlists (atomic transaction) and uses GUIDs from the import file as-is. This preserves referential identity across export → edit → re-import cycles.
- **Dirty tracking with persist effect**: `PersistenceState.IsDirty` is set on `ImportApplied`. The persist effect clones the entire playlist tree and atomically replaces the database. On failure, `IsDirty` stays `true` for retry. UI shows an error chip with a Retry button.
- **ImportApplied as undo boundary**: Import replaces the entire playlist state — an undo after that would be semantically meaningless. `UndoPolicy.IsBoundary` clears Past/Future entirely.
- **Discriminated union error types**: `ImportError` (ParseError, UnsupportedSchema, ValidationError, IdCollision, PersistenceFailed) and `ExportError` (SerializationFailed, InteropFailed) as dedicated record hierarchies — type-safe rather than generic strings.

**Consequences**
- State machine transitions are fully verifiable via reducer tests (no async I/O needed)
- UI reacts to every intermediate step (progress, error, success) via store state binding
- Export pipeline is extensible (e.g. new schema versions, additional export formats)
- Import validation is decoupled from persistence — errors are caught before DB writes
- Persist effect is reusable for other features that need dirty tracking