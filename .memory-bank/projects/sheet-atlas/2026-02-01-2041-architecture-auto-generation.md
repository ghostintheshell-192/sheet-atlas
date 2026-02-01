## 2026-02-01 - Sistema di documentazione architettura auto-generata

**Done**:
- Creato `.development/ARCHITECTURE.md` — mappa del progetto con descrizioni classi
- Creato `.development/scripts/generate-architecture.sh` — estrae `/// <summary>` dai file C#
- Creato hook globale `/data/repos/.git-hooks/pre-commit.d/04-generate-architecture`
  - Si attiva su qualsiasi progetto che ha lo script (portabile)
  - Rigenera solo quando ci sono file sorgente (.cs, .py, .ts, .rs) modificati
  - Aggiunge automaticamente il file aggiornato al commit
- Aggiornato `CLAUDE.md` per puntare a `.development/` come documentazione operativa
- Eliminato `class-descriptions.yaml` (non più necessario, le descrizioni vengono dal codice)
- Key Decisions ora elenca tutti gli ADR dinamicamente
- Risolto bug di raggruppamento (file nella stessa cartella ora appaiono insieme)
- Commit `630273a` su branch `docs/documentation-structure-cleanup`

**Next**:
- 57 file senza `/// <summary>` — aggiungere documentazione nel tempo
- 8 file con summary troppo lungo (>200 char) — accorciare dove appropriato
- Merge del branch `docs/documentation-structure-cleanup` quando pronta
- Considerare: Claude potrebbe proporre summary mancanti durante code review

**Notes**:
- Discussione su GitHub Issues vs tech-debt locale:
  - Per SheetAtlas (open source, sviluppatrice sola) il sistema tech-debt locale funziona bene
  - GitHub Issues utili se arrivano contributi/bug report esterni
  - Non serve duplicazione: issue esterne → GitHub, issue interne → tech-debt
- Il file ARCHITECTURE.md è pensato per Claude (niente diagrammi Mermaid, solo testo)
- Il file docs/project/ARCHITECTURE.md rimane per umani (con diagrammi)
- Lo script è portabile: funziona su qualsiasi progetto con la stessa struttura
- SheetAtlas attualmente open source, possibile modello commerciale dalla v1.0.0
