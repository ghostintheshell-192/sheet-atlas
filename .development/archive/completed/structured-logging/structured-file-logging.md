# Structured File Logging - Specification

> Sistema di logging strutturato per errori Excel, con un file JSON dedicato per ogni file Excel caricato.

---

## ✅ Stato Implementazione

**Backend: COMPLETATO** (18/10/2025)

- ✅ DTOs e modelli JSON
- ✅ FilePathHelper (sanitization, MD5 hashing)
- ✅ FileLogService (read/write JSON)
- ✅ ExcelErrorJsonConverter (serializzazione bidirezionale)
- ✅ Integrazione in LoadedFilesManager
- ✅ Test funzionali (JSON generati correttamente)

**Commit:** `edb642a` - `feat: Add structured JSON file logging system`

**Frontend: COMPLETATO** (18/10/2025)

- ⏳ File Details tab - Error History UI
- ⏳ Timeline errori con visualizzazione JSON
- ⏳ Statistiche e filtering

**Commit:** `f7ec73f` - `feat: feat(ui): Tabular error log view with full history tracking`

---

## Filosofia

- **File-Centric**: Ogni file Excel ha la sua cronologia indipendente
- **Strutturato**: JSON schema ben definito per facile parsing e query
- **Estensibile**: Schema progettato per supportare future features (validation, cleanup, versioning)
- **Performance**: File piccoli e isolati per lettura/scrittura veloce

---

## Directory Layout

```text
%LocalApplicationData%/SheetAtlas/
├── Logs/
│   ├── app-2025-10-18.log                ← Log applicazione generale (esistente)
│   └── Files/                             ← Nuovo: log strutturati per file Excel
│       ├── report-2024-xlsx-a3f912/
│       │   ├── 20251018_142315.json
│       │   ├── 20251018_153420.json
│       │   └── 20251019_091045.json
│       ├── data-sales-xlsx-b4e3a1/
│       │   ├── 20251018_144512.json
│       │   └── 20251019_083021.json
│       └── budget-q4-xlsx-9c72f5/
│           └── 20251018_160145.json
```

### Naming Conventions

**Folder per file Excel:**

- Pattern: `{filename-sanitized}-{hash-6char}/`
- Hash: Primi 6 caratteri MD5 del path completo del file
- Sanitization rules per filename:
  - Rimuovi estensione `.xlsx`
  - Converti a lowercase
  - Sostituisci spazi con `-`
  - Rimuovi caratteri speciali (eccetto `-` e `_`)
- Esempio: `"Report Q4 2024.xlsx"` con path `C:\Docs\Report Q4 2024.xlsx`
  - Sanitized: `report-q4-2024-xlsx`
  - MD5(path): `a3f912bc...`
  - Folder: `report-q4-2024-xlsx-a3f912/`
- Garantisce unicità anche con file stesso nome in path diversi

**File JSON log:**

- Pattern: `{timestamp}.json`
- Formato timestamp: `yyyyMMdd_HHmmss`
- Sorting cronologico automatico (alfabetico = cronologico)
- Esempio: `20251018_142315.json` = 2025-10-18 alle 14:23:15

---

## JSON Schema

### Schema Completo

```json
{
  "schemaVersion": "1.0",
  "file": {
    "name": "report-2024.xlsx",
    "originalPath": "C:\\Users\\User\\Documents\\report-2024.xlsx",
    "sizeBytes": 1048576,
    "hash": "md5:abc123def456...",
    "lastModified": "2025-10-17T10:30:00Z"
  },
  "loadAttempt": {
    "timestamp": "2025-10-18T14:23:15Z",
    "status": "Failed",
    "durationMs": 2345,
    "appVersion": "0.3.0"
  },
  "errors": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "timestamp": "2025-10-18T14:23:15.123Z",
      "severity": "Critical",
      "code": "FILE_001",
      "message": "File formato corrotto",
      "context": "LoadFile",
      "location": {
        "sheet": "Sheet1",
        "cell": "B2",
        "cellReference": "Sheet1!B2"
      },
      "exception": {
        "type": "System.IO.InvalidDataException",
        "message": "The file format is invalid",
        "stackTrace": "at SheetAtlas.Core.Services.ExcelReaderService.LoadFileAsync(...)\n   at ..."
      },
      "isRecoverable": false
    }
  ],
  "summary": {
    "totalErrors": 2,
    "bySeverity": {
      "Critical": 1,
      "Error": 0,
      "Warning": 1,
      "Info": 0
    },
    "byContext": {
      "LoadFile": 1,
      "ParseSheet": 1
    }
  },
  "extensions": {
    "validation": null,
    "cleanup": null,
    "metadata": null
  }
}
```

### Field Descriptions

#### **Root Level**

- `schemaVersion` (string): Versione schema JSON per retrocompatibilità
  - Formato: "Major.Minor" (es. "1.0", "1.1", "2.0")
  - Incrementa Minor per aggiunte backward-compatible
  - Incrementa Major per breaking changes

#### **file (object)**

Informazioni sul file Excel caricato:

- `name` (string): Nome originale del file (con estensione)
- `originalPath` (string): Path assoluto completo
- `sizeBytes` (number): Dimensione file in bytes
- `hash` (string): Hash MD5 del contenuto file con prefix `"md5:"`
  - Usato per rilevare se file è stato modificato
- `lastModified` (string): Timestamp ultima modifica file (ISO 8601)

#### **loadAttempt (object)**

Informazioni sul tentativo di caricamento:

- `timestamp` (string): Quando è stato fatto il tentativo (ISO 8601)
- `status` (string): Risultato del caricamento
  - Valori: `"Success"`, `"PartialSuccess"`, `"Failed"`
- `durationMs` (number): Durata operazione in millisecondi
- `appVersion` (string): Versione SheetAtlas che ha fatto il caricamento

#### **errors (array of objects)**

Lista errori incontrati durante il caricamento:

- `id` (string): GUID unico dell'errore
- `timestamp` (string): Quando è stato rilevato l'errore (ISO 8601 con ms)
- `severity` (string): Livello gravità
  - Valori: `"Critical"`, `"Error"`, `"Warning"`, `"Info"`
- `code` (string): Codice errore standard (es. `"FILE_001"`, `"SHEET_002"`)
- `message` (string): Messaggio user-friendly
- `context` (string): Contesto operazione (es. `"LoadFile"`, `"ParseSheet"`)
- `location` (object, nullable): Posizione errore nel file Excel
  - `sheet` (string, nullable): Nome foglio
  - `cell` (string, nullable): Coordinata cella (es. `"B2"`)
  - `cellReference` (string, nullable): Riferimento completo (es. `"Sheet1!B2"`)
- `exception` (object, nullable): Dettagli eccezione tecnica
  - `type` (string): Tipo eccezione .NET
  - `message` (string): Messaggio eccezione
  - `stackTrace` (string): Stack trace completo
- `isRecoverable` (boolean): Se errore è recuperabile da utente

#### **summary (object)**

Aggregazioni pre-calcolate per performance UI:

- `totalErrors` (number): Conteggio totale errori
- `bySeverity` (object): Conteggio per severity level
  - Keys: `"Critical"`, `"Error"`, `"Warning"`, `"Info"`
  - Values: number
- `byContext` (object): Conteggio per contesto operazione
  - Keys: nomi contesti (es. `"LoadFile"`, `"ParseSheet"`)
  - Values: number

#### **extensions (object)**

Placeholder per future features:

- `validation` (object, nullable): Risultati validazione file (future)
- `cleanup` (object, nullable): Azioni cleanup applicate (future)
- `metadata` (object, nullable): Metadati custom (future)

---

## Caratteristiche Tecniche

### Write Strategy

- **Quando**: Ogni volta che un file Excel viene caricato (Success/PartialSuccess/Failed)
- **Come**: Scrittura asincrona, non blocca UI
- **Atomicità**: Write temporaneo + rename per evitare corruzioni

### Read Strategy

- **On-Demand**: Carica JSON solo quando necessario (es. utente apre File Details)
- **Caching**: Mantieni in memoria ultimo log letto per file corrente
- **Parsing**: Deserializza solo campi necessari (es. skip `stackTrace` se non serve)

### Cleanup Policy

- **Retention**: Configurabile via `appsettings.json` (default: 30 giorni)
- **Trigger**: Background task periodico (es. ogni 24h)
- **Criterio**: Elimina file JSON più vecchi di retention period

### Configuration

Settings in `appsettings.json`:

```json
{
  "Logging": {
    "FileLogging": {
      "RetentionDays": 30,
      "Enabled": true
    }
  }
}
```

- `RetentionDays`: Giorni prima di eliminare log vecchi (0 = disabilita cleanup)
- `Enabled`: Abilita/disabilita file logging completamente

### Performance Considerations

- File JSON tipico: < 100KB (anche con 50+ errori)
- Scrittura: < 20ms (asincrona)
- Lettura singolo file: < 50ms
- Lettura cronologia (10 file): < 200ms

---

## Use Cases

### Immediate (Backend)

1. **Persistenza automatica**: Salva log strutturato per ogni caricamento file
2. **Cronologia errori**: Recupera storico errori per file specifico
3. **Query statistiche**: Calcola trend errori (miglioramento/peggioramento)

### Future (Frontend + Features)

1. **File Details UI**: Mostra cronologia errori in tab dedicato
2. **Validation**: Aggiungi risultati validazione in `extensions.validation`
3. **Cleanup**: Traccia azioni cleanup in `extensions.cleanup`
4. **Versioning**: Confronta file Excel nel tempo usando hash

---

## Migration & Compatibility

### Schema Evolution

Se schema cambia (es. nuovi campi):

- Incrementa `schemaVersion`
- Backend legge vecchi JSON e applica default per campi mancanti
- Opzionale: migration script per aggiornare vecchi file

### Backward Compatibility

- Campi obbligatori: Non rimuovere mai
- Nuovi campi: Sempre nullable o con default
- Breaking changes: Incrementa Major version e fornisci migration tool

---

## Implementazione Backend - Dettagli

### Componenti Implementati

**DTOs** (`SheetAtlas.Core/Application/DTOs/`)

- `FileLogEntry.cs` - Root object per JSON
- `FileInfoDto.cs` - Metadata file Excel
- `LoadAttemptInfo.cs` - Info tentativo caricamento
- `ErrorSummary.cs` - Aggregazioni per UI

**Services** (`SheetAtlas.Core/Application/Services/`)

- `IFileLogService.cs` + `FileLogService.cs` - Read/write JSON con rotazione
- `ExcelErrorJsonConverter.cs` - Custom converter per serializzazione Exception

**Helpers** (`SheetAtlas.Core/Shared/Helpers/`)

- `FilePathHelper.cs` - Sanitization, MD5 hash (path + file content), folder naming

**Integration** (`SheetAtlas.UI.Avalonia/`)

- `App.axaml.cs` - DI registration
- `LoadedFilesManager.cs` - Auto-save dopo ogni caricamento file

### Decisioni Implementative

1. **Error Codes:** Semplici categorie (FILE, SHEET, CELL, UNKNOWN) senza numeri progressivi
2. **isRecoverable:** Basato su tipo Exception (FileNotFound, UnauthorizedAccess, IOException)
3. **MD5.HashData():** Usato metodo moderno .NET 8 per performance
4. **Folder naming:** `{sanitized-name}-{hash-6char}/` con hash del path per unicità
5. **Serializzazione bidirezionale:** ExcelErrorJsonConverter gestisce sia Write che Read

### Test Effettuati

- ✅ File Success: JSON generato con errors=[]
- ✅ File con errori: JSON con exception, isRecoverable, error codes
- ✅ Deserializzazione: Read() ricostruisce ExcelError correttamente
- ✅ Build senza warning

---

**Versione:** 1.1
**Data:** 2025-10-18
**Ultimo aggiornamento:** 2025-10-18 (Backend completato)
