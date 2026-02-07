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