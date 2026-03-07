# Menu Functions Roadmap - SheetAtlas

## 📋 Overview

Questo documento pianifica l'implementazione delle funzioni menu per SheetAtlas, organizzando le voci per priorità e dettagli implementativi.

## 🎯 UI Cleanup Completato (Branch: feature/ui-improvements)

### ✅ Modifiche Applicate

- **Rimosso**: "Compare Selected Rows" dal menu Tools (resta solo come bottone contestuale)
- **Rimosso**: "Switch Theme" dal menu View (andrà in Settings)
- **Aggiunto**: Menu temporaneo "🎨 Theme" per test durante sviluppo
- **Pulito**: Menu View ora contiene solo "Show Status Bar" e "Show Search Panel"

### Struttura Menu Attuale

```text
File
├── 📁 Load Files (Ctrl+O) ✅ FUNZIONANTE
├── Recent Files (placeholder)
└── Exit (Alt+F4) (placeholder)

View
├── Show Status Bar (placeholder)
└── Show Search Panel (placeholder)

Tools
├── Export Results (placeholder)
└── Settings (placeholder)

Help
├── About SheetAtlas (placeholder)
└── Documentation (placeholder)

🎨 Theme (TEMPORANEO)
└── Switch to [Light/Dark] Theme (Ctrl+T) ✅ FUNZIONANTE
```

## 🚀 Roadmap Implementazione

### **FASE 1: Settings Dialog (Priorità ALTA)**

#### Settings da Implementare

1. **Tema**
   - Radio buttons: System / Light / Dark
   - Sostituisce menu temporaneo "🎨 Theme"

2. **Export Options**
   - Formato default: CSV / Excel / JSON
   - Cartella default per export
   - Formato data/ora per export

3. **Performance**
   - Limite righe risultati ricerca (default: 1000)
   - Memoria cache per file grandi

4. **Locale** (futuro)
   - Language: English / Italiano
   - Formato numeri/date regionale

#### Note Tecniche

- Dialog window modale
- Salvataggio preferences in user settings file
- Binding con ThemeManager esistente

### **FASE 2: Export Results (Priorità MEDIA)**

#### Export Targets

1. **Risultati Ricerca**
   - Query utilizzata + timestamp
   - Lista match trovati con posizione file/sheet/cella
   - Formato: CSV, Excel, JSON

2. **Confronto Righe**
   - Righe confrontate + differenze evidenziate
   - Formato: Excel con highlighting

3. **File Metadata**
   - Lista file caricati con info (size, sheets, last modified)

#### Note Tecniche

- Usare DocumentFormat.OpenXml per Excel export
- System.Text.Json per JSON export
- CsvHelper library per CSV

### **FASE 3: View Menu Functions (Priorità BASSA)**

#### Show Status Bar

- Barra informazioni in fondo alla window
- Mostra: file caricati, operazione corrente, memoria utilizzata

#### Show Search Panel

- **DUBBIO**: La search è già sempre visibile.
- **Alternativa**: Advanced search panel con filtri?

#### Refresh View

- Pulisce area risultati ricerca
- Reset view allo stato iniziale

### **FASE 4: File Menu Enhancements (Priorità BASSA)**

#### Recent Files

- Lista ultimi 10 file aperti
- Salvataggio in user preferences
- Clear recent files option

#### Advanced File Operations

- Drag & drop support
- Multi-select file loading
- File watcher per reload automatico

### **FASE 5: Help Menu (Priorità BASSA)**

#### About Dialog

- Version info
- Credits
- License information

#### Documentation

- User manual integrato
- Keyboard shortcuts help
- Troubleshooting guide

## 🎨 UX Principles Applicati

### Menu Organization

- **File**: Operazioni sui file (Load, Recent, Exit)
- **View**: Controllo visualizzazione UI
- **Tools**: Utility e funzioni avanzate (Export, Settings)
- **Help**: Documentazione e info app

### Design Decisions

- ✅ **Load Files** rimane in File menu (convenzione Windows)
- ✅ **Compare Rows** solo come bottone (azione contestuale)
- ✅ **Theme Switch** va in Settings (configurazione)
- ✅ **Tab navigation** già pulita (no toggle panels needed)

## 📝 Note Implementazione

### Tecnologie

- **Dialog Windows**: Avalonia Window con ShowDialog()
- **Settings Storage**: JSON file in user AppData
- **Export Libraries**: DocumentFormat.OpenXml, CsvHelper
- **Localization**: Avalonia Localization (futuro)

### Testing

- Unit tests per export functions
- UI tests per settings dialog
- Integration tests per file operations

---

**Documento creato**: Settembre 2025
**Branch**: feature/ui-improvements
**Status**: UI cleanup completato, roadmap definita per future implementazioni
