
# 🧰 DevToolbox

> Ein modernes Fullstack Blazor Portfolio-Projekt, das verschiedene nützliche Web-Tools vereint und moderne .NET-Entwicklungstechniken demonstriert.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat-square&logo=csharp)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite)

---

## 📋 Über das Projekt

DevToolbox ist ein **Portfolio-Showcase-Projekt**, das meine Fähigkeiten in der modernen .NET-Webentwicklung demonstriert. Es handelt sich um eine Blazor Server-Anwendung, die eine Sammlung nützlicher Tools bereitstellt und dabei Best Practices für Architektur, Clean Code und UI/UX-Design anwendet.

**Ziel:** Vertiefung meiner Kenntnisse in Blazor, Entity Framework Core und modernem C#-Development, während ich gleichzeitig praktisch nutzbare Tools entwickle.

## 🔄 Recently Worked On

<!-- START_RECENTLY_WORKED_ON -->
| Feature | Commit Date | Commit |
|---------|-------------|--------|
<!-- END_RECENTLY_WORKED_ON -->

---

## ✨ Features

### 🎵 YouTube Playlist Manager
- **Playlist-Verwaltung**: Erstellen und organisieren eigener YouTube-Playlists
- **Integrierter Player**: Abspielen von Videos direkt in der Anwendung
- **Drag & Drop**: Intuitive Neuordnung von Videos per Drag-and-Drop
- **Persistenz**: Alle Daten werden lokal in einer SQLite-Datenbank gespeichert
- **Automatische Metadaten**: Titel, Thumbnails und Videodauer werden automatisch abgerufen

### 🔜 Weitere geplante Tools
*Dieses Projekt wächst organisch - neue Tools werden nach Bedarf hinzugefügt und in der README ergänzt. Eine dynamische Roadmap wird in Zukunft hinzugefügt.*

## 🛠️ Tech Stack

| Technologie | Version | Verwendung |
|-------------|---------|------------|
| **.NET** | 10.0 | Backend & Frontend Framework |
| **C#** | 14.0 | Programmiersprache |
| **Blazor Server** | - | Interactive Web UI |
| **Entity Framework Core** | 10.0.2 | ORM für Datenbankzugriff |
| **SQLite** | 10.0.2 | Lokale Datenbank |
| **MudBlazor** | 8.15.0 | UI-Komponenten-Bibliothek |
| **ASP.NET Core MVC** | - | Routing & Navigation |

### Architektur-Highlights
- **Feature-basierte Organisation**: Jedes Tool ist als eigenständiges Feature strukturiert
- **Service Layer Pattern**: Saubere Trennung von Business Logic und UI
- **Entity Mapping**: Explizite Konfiguration der Datenbankbeziehungen
- **Dependency Injection**: Verwendung des integrierten DI-Containers
- **Interactive Server Components**: Echtzeit-Updates ohne vollständige Seitenneulast

## 🚀 Installation & Setup

### Voraussetzungen
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) oder höher
- Ein beliebiger Code-Editor (empfohlen: [JetBrains Rider](https://www.jetbrains.com/rider/) oder [Visual Studio 2025](https://visualstudio.microsoft.com/))
- Optional: [Git](https://git-scm.com/) für Versionskontrolle

### Schritt-für-Schritt Anleitung

1. **Repository klonen**
   ```bash
   git clone https://github.com/DEIN-USERNAME/DevToolbox.git
   cd DevToolbox
   ```

2. **Dependencies installieren**
   ```bash
   dotnet restore
   ```

3. **Datenbank einrichten**

   Die SQLite-Datenbank wird beim ersten Start automatisch erstellt. Für manuelle Migration:
   ```bash
   cd DevToolbox
   dotnet ef database update
   ```

4. **Anwendung starten**
   ```bash
   dotnet run
   ```

5. **Im Browser öffnen**

   Navigiere zu `https://localhost:5001` oder `http://localhost:5000`

### Konfiguration

Die Datenbank-Verbindungszeichenfolge kann in `appsettings.json` angepasst werden:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=devtoolbox.db"
  }
}
```
## 📁 Projektstruktur
```
DevToolbox/
├── Components/              # Wiederverwendbare Blazor-Komponenten
│   ├── Layout/             # Layout-Komponenten (NavMenu, MainLayout)
│   ├── Pages/              # Seiten (Home, Error, NotFound)
│   └── App.razor           # Root-Komponente
├── Data/                   # Datenzugriffsschicht
│   ├── ApplicationDbContext.cs    # EF Core DbContext
│   └── EntityMapping/      # Fluent API Konfigurationen
├── Features/               # Feature-Module
│   └── YouTubePlayer/      # YouTube Player Feature
│       ├── Components/     # Feature-spezifische Komponenten
│       ├── Models/         # Domain Models
│       ├── Service/        # Business Logic
│       └── YouTubePlayer.razor
├── Migrations/             # EF Core Migrationen
├── wwwroot/                # Statische Assets (CSS, JS, Bilder)
│   ├── css/
│   └── js/
├── Program.cs              # Anwendungs-Einstiegspunkt
└── appsettings.json        # Konfiguration
```

## Design-Prinzipien
- Feature-Slices: Jedes Feature ist in sich geschlossen
- Separation of Concerns: UI, Business Logic und Datenzugriff sind getrennt
- Dependency Injection: Lose Kopplung durch DI
- Single Responsibility: Klassen haben eine klare, definierte Aufgabe

## 🎯 Verwendung
### YouTube Playlist Manager

1. Neue Playlist erstellen
- Klicke auf "New Playlist" in der Playlist-Übersicht
- Gib einen Namen ein und bestätige

2. Videos hinzufügen
- Wähle eine Playlist aus
- Klicke auf "Add Video"
- Füge eine YouTube-URL ein
- Die Metadaten werden automatisch abgerufen

3. Videos abspielen & organisieren
- Klicke auf ein Video, um es abzuspielen
- Nutze die Player-Controls für Wiedergabe-Steuerung
- Ziehe Videos per Drag & Drop, um die Reihenfolge zu ändern

## 🧪 Entwicklung
### Neue Migrations erstellen

```bash
dotnet ef migrations add MigrationName --project DevToolbox
```

### Datenbank zurücksetzen

```bash
dotnet ef database update --project DevToolbox
dotnet ef database update --project DevToolbox
```
### Hot Reload nutzen

```bash
dotnet watch run
```

## 🔧 Verwendete NuGet Packages

```html
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.2" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.2" />
<PackageReference Include="MudBlazor" Version="8.15.0" />
```

## 🤝 Mitwirken

Da dies ein persönliches Portfolio-Projekt ist, nehme ich derzeit keine Pull Requests an. Feedback und Anregungen sind jedoch immer willkommen!

## 📜 Lizenz
Dieses Projekt ist unter der MIT-Lizenz lizenziert.

## 👤 Kontakt
### 📧 Kontakt
- [![Portfolio](https://img.shields.io/badge/GitHub%20Pages-121013?logo=github&logoColor=white)](https://goldbarth.github.io/Portfolio/#/)
- [![LinkedIn](https://custom-icon-badges.demolab.com/badge/LinkedIn-0A66C2?logo=linkedin-white&logoColor=fff)](https://www.linkedin.com/in/felix-wahl-6763791b9/)
- [<kbd>E-Mail</kbd>](mailto:felix.wahl@live.de)
---

*Entwickelt mit ❤️ und .NET*

<!--
## 🎨 Zusätzliche Empfehlungen

Für ein noch professionelleres Portfolio-Projekt würde ich empfehlen:

1. **Screenshots hinzufügen**: Erstelle einen `/docs` oder `/screenshots` Ordner
2. **GitHub Actions**: CI/CD Pipeline für automatische Builds
3. **Contributing Guidelines**: `CONTRIBUTING.md` für Entwickler-Standards
4. **Code of Conduct**: `CODE_OF_CONDUCT.md`
5. **Changelog**: `CHANGELOG.md` für Versionshistorie
6. **Demo-Link**: Hoste das Projekt auf Azure/AWS und füge den Link hinzu
-->

