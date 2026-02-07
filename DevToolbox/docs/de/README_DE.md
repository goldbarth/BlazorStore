# 🧰 DevToolbox

> Blazor-Server Portfolio-Projekt mit Fokus auf **saubere Architektur**, **store-getriebenes State-Management** und **kontrollierte JavaScript-Interop**.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat-square&logo=csharp)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite)

---

## 💡 Motivation

Ich wollte kein weiteres Todo-App-Portfolio. Stattdessen habe ich mir die Frage gestellt: *Wie würde ich ein reales Feature mit Persistenz, asynchronen Workflows und externer API-Anbindung in Blazor sauber umsetzen?*

DevToolbox ist meine Antwort darauf — ein Projekt, in dem ich bewusst architektonische Entscheidungen treffe, dokumentiere und an konkreten Features demonstriere. Der Fokus liegt nicht auf Feature-Menge, sondern auf **Tiefe und Qualität** der Umsetzung.

## 🔭 Scope of this Project

DevToolbox ist ein **Portfolio-Showcase**, kein Produktivtool. Das Projekt wächst organisch — neue Features entstehen nur, wenn sie architektonisch interessant sind und echte Komplexität mitbringen.

### 🎵 YouTube Playlist Manager (Architektur-Demonstrator)

Das Haupt-Feature kombiniert bewusst mehrere Herausforderungen in einem Feature:

- **Playlist-Verwaltung** — Erstellen und Organisieren eigener Playlists
- **Integrierter Player** — YouTube IFrame API via kontrollierter JS-Interop
- **Drag & Drop** — SortableJS mit bewusstem Lifecycle-Handling in Blazor
- **Persistenz** — Lokale Speicherung über SQLite (EF Core)
- **Explizites State-Management** — Store-getriebener Datenfluss über Actions, Reducer und Effects

> Weitere Tools folgen, wenn sie architektonisch etwas Neues einbringen.

## 🧠 Architecture — TL;DR

Das Projekt verwendet eine **strikte Store-Architektur** mit unidirektionalem Datenfluss:

```
UI → Action → Reducer → State → UI
                ↓
            Effects (DB, JS-Interop)
```

| Prinzip | Umsetzung |
|---------|-----------|
| **Single Source of Truth** | Gesamter Feature-State lebt im Store |
| **Pure Reducer** | Keine DB-Zugriffe, kein JS, keine Async-Logik |
| **Side-Effect Isolation** | DB und JS-Interop ausschließlich in Effects |
| **UI ist dispatch-only** | Komponenten lesen State und dispatchen Actions — keine direkte Manipulation |

Diese Architektur ist bewusst strenger als für ein Projekt dieser Größe nötig. Ziel ist es zu zeigen, dass ich die Prinzipien nicht nur kenne, sondern auch konsequent umsetze.

## 🎯 Why this matters

Dieses Projekt zeigt, wie ich an Software-Entwicklung herangehe:

- **Architektur-Entscheidungen bewusst treffen** statt Copy-Paste aus Tutorials
- **State-Management ernst nehmen** — auch wenn Blazor kein Redux „braucht"
- **JS-Interop kontrolliert einsetzen** — kein Hidden State zwischen C# und JavaScript
- **Code dokumentieren** — nicht nur was, sondern warum

> Kurz: Nicht das Feature ist der Showcase — die Art der Umsetzung ist es.

## 📚 Documentation

- [Architektur](ARCHITECTURE_DE.md)
- [Architecture Decision Records](ADR_DE.md)

## 🗺️ Roadmap

> Kein Zeitplan, keine Versprechen — nur die Richtung, in die das Projekt wächst.

**Aktueller Fokus**
- Store-Architektur stabilisieren und härten
- Architektur-Entscheidungen dokumentieren (ADRs)

**Als Nächstes**
- Fehlerbehandlungsstrategie — einheitliches Pattern für Store-Fehler und UI-Feedback
- Undo/Redo für Queue-Actions — zeigt Zeitreise-Fähigkeit der Store-Architektur
- Shuffle/Repeat-Modi — Erweiterung der bestehenden Queue-Logik um Playback-Strategien
- UI polieren — Responsiveness, Edge Cases, Micro-Interactions

**Auf dem Radar**
- Playback-Persistenz — Player-State (Position, aktiver Track) über Sessions hinweg wiederherstellen
- Cross-Feature-Kommunikation — Event-Bus oder Shared State zwischen zukünftigen Feature-Modulen

## 🛠️ Tech Stack

| Technologie | Version | Verwendung |
|-------------|---------|------------|
| **.NET** | 10.0 | Backend & Frontend Framework |
| **C#** | 14.0 | Programmiersprache |
| **Blazor Server** | — | Interactive Web UI |
| **Entity Framework Core** | 10.0.2 | ORM für Datenbankzugriff |
| **SQLite** | 10.0.2 | Lokale Datenbank |
| **MudBlazor** | 8.15.0 | UI-Komponenten-Bibliothek |
| **ASP.NET Core MVC** | — | Routing & Navigation |

## 📁 Projektstruktur

```
DevToolbox/
├── Components/             # Wiederverwendbare Blazor-Komponenten
│   ├── Layout/             # NavMenu, MainLayout
│   ├── Pages/              # Home, Error, NotFound
│   └── App.razor           # Root-Komponente
├── Data/                   # Datenzugriffsschicht
│   ├── ApplicationDbContext.cs
│   └── EntityMapping/      # Fluent API Konfigurationen
├── Features/               # Feature-Module (je Feature in sich geschlossen)
│   └── YouTubePlayer/
│       ├── Components/     # Feature-spezifische UI-Komponenten
│       ├── Models/         # Domain Models
│       ├── State/          # State Slices + Actions
│       ├── Store/          # Store + Reducer + Effects
│       └── YouTubePlayer.razor
├── Migrations/             # EF Core Migrationen
├── wwwroot/                # Statische Assets (CSS, JS)
├── Program.cs              # Einstiegspunkt
└── appsettings.json        # Konfiguration
```

## 🔄 Recently Worked On

<!-- START_RECENTLY_WORKED_ON -->
| Feature | Datum | Commit |
|---------|-------|--------|
| feat: refactor YouTube player feature to strict store-driven state management | 2026-02-06 | [4da5354](https://github.com/goldbarth/DevToolbox/commit/4da53541ba51775c0d04a728bc6d1bab8679dd6c) |
| feat: add OnPlayerStateChanged method for handling YouTube player state changes | 2026-02-06 | [5511eee](https://github.com/goldbarth/DevToolbox/commit/5511eee15c2db1e7974ff611da61e2050caff35b) |
| feat: add PlayerState model for YouTubePlayer feature | 2026-02-06 | [31af266](https://github.com/goldbarth/DevToolbox/commit/31af2668cd3ee8f2aa35fdc92293986700fe9fae) |
| feat: implement YouTube Player with playlist and video management | 2026-02-04 | [d31b79d](https://github.com/goldbarth/DevToolbox/commit/d31b79df00b28e1a6e04b567ce96384eeba90a09) |
| feat: add drawers for creating playlists and adding videos | 2026-02-04 | [5767720](https://github.com/goldbarth/DevToolbox/commit/5767720f8ee5db6889d5c726484a45b21523427a) |
<!-- END_RECENTLY_WORKED_ON -->

## 📜 Lizenz

Dieses Projekt ist unter der [MIT-Lizenz](LICENSE) lizenziert.

## 📧 Kontakt

- [![Portfolio](https://img.shields.io/badge/GitHub%20Pages-121013?logo=github&logoColor=white)](https://goldbarth.github.io/Portfolio/#/)
- [![LinkedIn](https://custom-icon-badges.demolab.com/badge/LinkedIn-0A66C2?logo=linkedin-white&logoColor=fff)](https://www.linkedin.com/in/felix-wahl-6763791b9/)
- [<kbd>E-Mail</kbd>](mailto:felix.wahl@live.de)

---

*Entwickelt mit ❤️ und .NET*