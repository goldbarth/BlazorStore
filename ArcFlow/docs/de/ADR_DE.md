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