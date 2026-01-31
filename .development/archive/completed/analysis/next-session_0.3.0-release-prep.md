# Next Session: Release 0.3.0-alpha Preparation

**Data prevista**: 2025-10-23
**Obiettivo**: Preparare e rilasciare versione 0.3.0-alpha

---

## ✅ Completato Oggi (2025-10-22)

### Issue Risolte

1. **Issue #4**: Sequential file loading → Parallel loading (2.5-5x più veloce)
2. **Issue #2**: Memory leak da PropertyChanged inline → Handler cleanup
3. **Issue #5**: Fire-and-forget inconsistency + retry flicker → Consistenza + UX smooth

### Risultati

- 193/193 test passano
- Branch develop aggiornato e pulito
- Performance significativamente migliorata
- UX retry senza flicker

---

## 🎯 TODO per Prossima Sessione [TUTTO FATTO]

### 1. SEO e Sito Web

**Problema**:

- Sito single-page ha SEO terribile
- 0 statistiche su Google Search Console dopo 20 giorni
- Probabilmente in 20esima pagina dei risultati

**Fatto**:

- [x] Creare pagina `/download.html` separata
  - Screenshots della GUI (NUOVI! aspetto aggiornato)
  - Features dettagliate
  - Istruzioni download/installazione
  - Link ai file download per ogni piattaforma
- [x] Generare `sitemap.xml`
  - Include index.html
  - Include download.html
- [x] Verificare/migliorare SEO meta tags
  - Meta description
  - Keywords
  - Open Graph tags (social sharing)
  - Structured data (Schema.org)
- [x] Testare su Google Search Console

### 2. Screenshots Nuovi

**Fatto**:

- [x] Screenshot principale (home/confronto file)
- [x] Screenshot file details
- [x] Screenshot search/filtri
- [x] (Opzionale) Screenshot confronto risultati
- [x] Aggiornare sia sito che repository

### 3. Release 0.3.0-alpha

**Checklist Pre-Release**:

- [x] Verificare CHANGELOG.md aggiornato
- [x] Verificare version number in project files
- [x] SEO e sito completati
- [x] Screenshots aggiornati
- [x] Tag git: `git tag v0.3.0-alpha`
- [x] Push tag: `git push origin v0.3.0-alpha`
- [x] GitHub Actions farà il resto automaticamente

---

## 📊 Issue Rimanenti (per 0.4.0)

- **issue_1**: Inconsistent reader patterns (MEDIUM)
- **issue_3**: Converter creates brushes (MEDIUM)

Bassa priorità, possono aspettare prossima release.

---

## 📝 Note Tecniche

### File da Modificare per SEO

```
docs/website/
├── index.html (già esiste)
├── download.html (DA CREARE)
├── sitemap.xml (DA CREARE)
└── images/ (aggiornare screenshots)
```

### Workflow Release Automatico

- Crea tag → GitHub Actions compila → Crea release → Aggiorna website → Deploy

### Comandi Utili

```bash
# Aggiornare progetti list
cd /data/repos/rules && python3 update_projects.py

# Vedere ultimo commit
git log --oneline -1

# Creare tag release
git tag v0.3.0-alpha
git push origin v0.3.0-alpha
```

---

## 💤 Riposa Bene

Domani riprendiamo freschi con:

1. SEO (30-45 min)
2. Screenshots (15-20 min)
3. Release (10 min)

**Tempo stimato totale**: ~1 ora

Buona notte! 🌙
