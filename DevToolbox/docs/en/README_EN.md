# 🧰 DevToolbox

> A Blazor Server portfolio project focused on **clean architecture**, **store-driven state management**, and **controlled JavaScript interop**.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat-square&logo=csharp)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite)

---

## 💡 Motivation

I didn't want another todo-app portfolio. Instead, I asked myself: *How would I build a real-world feature with persistence, async workflows, and external API integration cleanly in Blazor?*

DevToolbox is my answer — a project where I make deliberate architectural decisions, document them, and demonstrate them through concrete features. The focus is not on feature quantity, but on **depth and quality** of implementation.

## 🔭 Scope of this Project

DevToolbox is a **portfolio showcase**, not a production tool. The project grows organically — new features are only added when they are architecturally interesting and bring real complexity.

### 🎵 YouTube Playlist Manager (Architecture Demonstrator)

The main feature deliberately combines several challenges in one:

- **Playlist Management** — Create and organize custom playlists
- **Integrated Player** — YouTube IFrame API via controlled JS interop
- **Drag & Drop** — SortableJS with deliberate lifecycle handling in Blazor
- **Persistence** — Local storage via SQLite (EF Core)
- **Explicit State Management** — Store-driven data flow through Actions, Reducers, and Effects

> More tools will follow when they bring something architecturally new to the table.

## 🧠 Architecture — TL;DR

The project uses a **strict store architecture** with unidirectional data flow:

```
UI → Action → Reducer → State → UI
                ↓
            Effects (DB, JS Interop)
```

| Principle | Implementation |
|-----------|---------------|
| **Single Source of Truth** | All feature state lives in the store |
| **Pure Reducers** | No DB access, no JS, no async logic |
| **Side-Effect Isolation** | DB and JS interop exclusively in Effects |
| **UI is dispatch-only** | Components read state and dispatch actions — no direct manipulation |

This architecture is deliberately stricter than necessary for a project of this size. The goal is to demonstrate that I don't just know these principles — I apply them consistently.

## 🎯 Why this matters

This project shows how I approach software development:

- **Make architectural decisions deliberately** instead of copy-pasting from tutorials
- **Take state management seriously** — even when Blazor doesn't "need" Redux
- **Use JS interop in a controlled way** — no hidden state between C# and JavaScript
- **Document code** — not just what, but why

> In short: The feature isn't the showcase — the way it's built is.

## 📚 Documentation

- 🇬🇧 [Architecture (English)](DevToolbox/docs/en/ARCHITECTURE_EN.md)
- 🇩🇪 [Architektur (Deutsch)](DevToolbox/docs/de/ARCHITECTURE_DE.md)
- 🇬🇧 [Architecture Decision Records (English)](DevToolbox/docs/en/ADR_EN.md)
- 🇩🇪 [Architecture Decision Records (Deutsch)](DevToolbox/docs/de/ADR_DE.md)

## 🗺️ Roadmap

> No deadlines, no promises — just the direction this project is heading.

**Current Focus**
- Stabilize and harden the store architecture
- Document architectural decisions (ADRs)

**Next**
- Error handling strategy — unified pattern for store errors and UI feedback
- Undo/redo for queue actions — demonstrates time-travel capability of the store architecture
- Shuffle/repeat modes — extend existing queue logic with playback strategies
- Polish UI — responsiveness, edge cases, micro-interactions

**On the Radar**
- Playback persistence — restore player state (position, active track) across sessions
- Cross-feature communication — event bus or shared state between future feature modules

## 🛠️ Tech Stack

| Technology | Version | Usage |
|------------|---------|-------|
| **.NET** | 10.0 | Backend & Frontend Framework |
| **C#** | 14.0 | Programming Language |
| **Blazor Server** | — | Interactive Web UI |
| **Entity Framework Core** | 10.0.2 | ORM for Database Access |
| **SQLite** | 10.0.2 | Local Database |
| **MudBlazor** | 8.15.0 | UI Component Library |
| **ASP.NET Core MVC** | — | Routing & Navigation |

## 📁 Project Structure

```
DevToolbox/
├── Components/             # Reusable Blazor components
│   ├── Layout/             # NavMenu, MainLayout
│   ├── Pages/              # Home, Error, NotFound
│   └── App.razor           # Root component
├── Data/                   # Data access layer
│   ├── ApplicationDbContext.cs
│   └── EntityMapping/      # Fluent API configurations
├── Features/               # Feature modules (self-contained per feature)
│   └── YouTubePlayer/
│       ├── Components/     # Feature-specific UI components
│       ├── Models/         # Domain models
│       ├── State/          # State slices + actions
│       ├── Store/          # Store + reducer + effects
│       └── YouTubePlayer.razor
├── Migrations/             # EF Core migrations
├── wwwroot/                # Static assets (CSS, JS)
├── Program.cs              # Entry point
└── appsettings.json        # Configuration
```

## 🔄 Recently Worked On

<!-- START_RECENTLY_WORKED_ON -->
| Feature | Date | Commit |
|---------|------|--------|
| feat: refactor YouTube player feature to strict store-driven state management | 2026-02-06 | [4da5354](https://github.com/goldbarth/DevToolbox/commit/4da53541ba51775c0d04a728bc6d1bab8679dd6c) |
| feat: add OnPlayerStateChanged method for handling YouTube player state changes | 2026-02-06 | [5511eee](https://github.com/goldbarth/DevToolbox/commit/5511eee15c2db1e7974ff611da61e2050caff35b) |
| feat: add PlayerState model for YouTubePlayer feature | 2026-02-06 | [31af266](https://github.com/goldbarth/DevToolbox/commit/31af2668cd3ee8f2aa35fdc92293986700fe9fae) |
| feat: implement YouTube Player with playlist and video management | 2026-02-04 | [d31b79d](https://github.com/goldbarth/DevToolbox/commit/d31b79df00b28e1a6e04b567ce96384eeba90a09) |
| feat: add drawers for creating playlists and adding videos | 2026-02-04 | [5767720](https://github.com/goldbarth/DevToolbox/commit/5767720f8ee5db6889d5c726484a45b21523427a) |
<!-- END_RECENTLY_WORKED_ON -->

## 📜 License

This project is licensed under the [MIT License](LICENSE).

## 📧 Contact

[![Portfolio](https://img.shields.io/badge/GitHub%20Pages-121013?logo=github&logoColor=white)](https://goldbarth.github.io/Portfolio/#/)
[![LinkedIn](https://custom-icon-badges.demolab.com/badge/LinkedIn-0A66C2?logo=linkedin-white&logoColor=fff)](https://www.linkedin.com/in/felix-wahl-6763791b9/)
[<kbd>E-Mail</kbd>](mailto:felix.wahl@live.de)

---

*Built with ❤️ and .NET*