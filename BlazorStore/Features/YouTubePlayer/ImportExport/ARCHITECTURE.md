# YouTube Player — Import / Export Architecture

## Schema v1 (`ExportEnvelopeV1`)

```jsonc
{
  "schemaVersion": 1,                        // required, int — always 1 for this version
  "exportedAtUtc": "2026-02-12T14:30:00Z",   // required, ISO 8601 UTC
  "selectedPlaylistId": "guid | null",        // optional — last-selected playlist at export time
  "playlists": [                              // required, may be empty []
    {
      "id": "guid",                           // required
      "name": "string",                       // required, non-empty
      "description": "string",                // required, may be ""
      "createdAtUtc": "ISO 8601",             // required
      "updatedAtUtc": "ISO 8601",             // required
      "videos": [                             // required, may be empty []
        {
          "id": "guid",                       // required
          "youTubeId": "string (11 chars)",   // required, [A-Za-z0-9_-]{11}
          "title": "string",                  // required, non-empty
          "thumbnailUrl": "string | null",     // optional
          "duration": "string | null",         // optional, TimeSpan format "mm:ss" or "hh:mm:ss"
          "position": 0,                      // required, int >= 0
          "addedAtUtc": "ISO 8601"            // required
        }
      ]
    }
  ]
}
```

### Invariants

| Field               | Constraint                                  |
|---------------------|---------------------------------------------|
| `schemaVersion`     | Must equal `1`                              |
| `exportedAtUtc`     | Valid UTC datetime                           |
| `playlists`         | Non-null array (empty is allowed)           |
| `playlist.id`       | Non-empty GUID                              |
| `playlist.name`     | Non-empty, non-whitespace string            |
| `video.id`          | Non-empty GUID                              |
| `video.youTubeId`   | Exactly 11 characters, `[A-Za-z0-9_-]`     |
| `video.title`       | Non-empty, non-whitespace string            |
| `video.position`    | `>= 0`, unique within its playlist          |
| `selectedPlaylistId`| If present, must reference an existing playlist ID in the file |

---

## Import Policy

### `ImportMode.ReplaceAll` (default, only mode in v1)

1. Delete **all** existing playlists and their videos from the database.
2. Insert every playlist and video from the import file.
3. Optionally restore `selectedPlaylistId` in the UI state.

This is an atomic operation — if any step fails, the entire import is rolled back.

### `IdStrategy.TrustIncoming` (default, only strategy in v1)

- The GUIDs in the file are used as-is for persistence.
- After the mode-specific cleanup (step 1 of ReplaceAll), a collision check runs:
  if any imported ID already exists in the DB, the import aborts with `ImportError.IdCollision`.
- Rationale: Trusting incoming IDs preserves referential identity across
  export → edit → re-import cycles.

### `ImportOptions`

```csharp
record ImportOptions(
    ImportMode Mode = ImportMode.ReplaceAll,
    IdStrategy IdStrategy = IdStrategy.TrustIncoming
);
```

---

## Error Types

### Import Errors (`ImportError`)

| Variant              | When                                                         |
|----------------------|--------------------------------------------------------------|
| `ParseError`         | JSON is malformed or cannot be deserialized into the DTO     |
| `UnsupportedSchema`  | `schemaVersion` > `ExportEnvelopeV1.CurrentSchemaVersion`    |
| `ValidationError`    | A required field is missing, empty, or violates a constraint |
| `IdCollision`        | An imported GUID already exists after mode-specific cleanup  |
| `PersistenceFailed`  | Validation passed, but DB write/transaction failed           |

### Export Errors (`ExportError`)

| Variant               | When                                                       |
|-----------------------|------------------------------------------------------------|
| `SerializationFailed` | State → JSON conversion threw an exception                 |
| `InteropFailed`       | JS interop for triggering the browser file download failed |

All error types are **sealed records** inheriting from a private-constructor abstract base,
following the existing discriminated-union pattern used by `PlayerState`, `PlaylistsState`,
and `PlaybackDecision` in this codebase.
