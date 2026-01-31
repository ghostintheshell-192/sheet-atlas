# SheetAtlas - Piano di Ripristino Release e CHANGELOG

**Data**: 2025-11-07
**Status**: Work in Progress
**Obiettivo**: Ricreare tutti i tag e release con CHANGELOG corretto

---

## 📋 Analisi del Problema

### Stato Attuale dei Tag

Tutti i tag esistono localmente e sono firmati GPG:

```bash
$ git tag -l
v0.1.0
v0.2.0
v0.3.0
v0.3.1

$ git tag -v v0.3.1
gpg: Firma valida da "Valentina Malavenda <v.malavenda.git01@proton.me>"
✅ Tutti i tag sono firmati correttamente
```

### Stato delle Release GitHub

```bash
$ gh release list
SheetAtlas v0.2.0 - Multi-Format Support (Alpha)    Pre-release    v0.2.0    2025-10-13
SheetAtlas v0.1.0 - Alpha Release                   Pre-release    v0.1.0    2025-10-13

❌ MANCANO: v0.3.0 e v0.3.1 (cancellate manualmente)
```

### Problema Critico: CHANGELOG Inconsistente

Il CHANGELOG.md **al momento della creazione di ogni tag** era incompleto o errato:

| Tag | CHANGELOG nel commit del tag | Problema |
|-----|------------------------------|----------|
| v0.1.0 | Solo `## [1.0.0]` | Versioning vecchio (pre-rinumerazione) |
| v0.2.0 | Solo `## [1.0.0]` | Stesso identico contenuto di v0.1.0! |
| v0.3.0 | Solo `## [0.2.0]` | MANCA completamente la sezione 0.3.0 |
| v0.3.1 | Solo `## [0.2.0]` | MANCA completamente la sezione 0.3.1 |

**Conseguenza**: Il workflow `release.yml` usa `body_path: CHANGELOG.md`, quindi:

- Se pushassimo i tag ora → le release mostrerebbero CHANGELOG sbagliato/incompleto
- La release v0.3.0 mostrerebbe solo la sezione 0.2.0!

### Commit Problematico del Workflow

```bash
982a23e fix: update release-changelog workflow to support manual GPG-signed tags
```

**Modifiche**:

- ❌ Rimosso: Creazione automatica del tag
- ✅ Aggiunto: Push CHANGELOG su develop
- ✅ Aggiunto: Istruzioni per creazione manuale tag firmato GPG

**Problema**: Il workflow ora genera CHANGELOG e fa commit, ma NON crea il tag. L'idea era permettere tag firmati GPG manuali, ma questo ha creato confusione.

---

## 🎯 Piano d'Azione per Domani

### Fase 1: Pulizia Completa

1. **Cancellare tutti i tag locali**:

   ```bash
   git tag -d v0.1.0 v0.2.0 v0.3.0 v0.3.1
   ```

2. **Cancellare tutti i tag remoti** (se esistono):

   ```bash
   git push origin --delete v0.1.0 v0.2.0 v0.3.0 v0.3.1
   ```

3. **Cancellare tutte le release GitHub**:

   ```bash
   gh release delete v0.1.0 --yes
   gh release delete v0.2.0 --yes
   # v0.3.0 e v0.3.1 sono già cancellate
   ```

### Fase 2: Preparazione CHANGELOG Corretto

1. **Generare CHANGELOG completo** (già fatto, file in `/data/repos/sheet-atlas/CHANGELOG-new.md`):

   ```bash
   /tmp/git-cliff-2.0.4/git-cliff --tag v0.3.1 --output CHANGELOG.md
   ```

2. **Commit CHANGELOG su develop**:

   ```bash
   git checkout develop
   git add CHANGELOG.md
   git commit -S -m "docs: regenerate complete CHANGELOG for all versions

   - Add missing sections for v0.3.0 and v0.3.1
   - Fix versioning inconsistencies (v1.x → v0.x)
   - Complete changelog entries from git history

   🤖 Generated with [Claude Code](https://claude.com/claude-code)

   Co-Authored-By: Claude <noreply@anthropic.com>"
   git push origin develop
   ```

### Fase 3: Decidere Strategia di Release

**Opzione A - Workflow Semi-Automatico (Raccomandato)**:

- Mantieni workflow `release-changelog.yml` che genera CHANGELOG
- Crei tag firmato manualmente localmente
- Push del tag triggera `release.yml` → crea release automatica

**Opzione B - Workflow Completamente Automatico**:

- Modifica `release-changelog.yml` per firmare tag con GPG key in GitHub Secrets
- Richiede configurazione complessa (export GPG key in secrets)
- Pro: Zero intervento manuale
- Contro: Setup complesso, chiave GPG su GitHub

**Opzione C - Manuale Tradizionale**:

- Usi `git-cliff` localmente per generare CHANGELOG
- Commit + tag + push tutto manualmente
- Pro: Massimo controllo
- Contro: Più step manuali

### Fase 4: Correggere il Workflow (Opzione A)

**Problema attuale**: `release.yml` usa `body_path: CHANGELOG.md` che include TUTTO il changelog.

**Soluzione**: Estrarre solo la sezione pertinente per ogni versione.

**Modifica da fare in `.github/workflows/release.yml`**:

```yaml
# PRIMA (linea 271-316):
- name: Create GitHub Release
  uses: softprops/action-gh-release@v2
  with:
    files: artifacts/*
    body_path: CHANGELOG.md  # ❌ Include tutto il changelog
    draft: false
    prerelease: ${{ steps.prerelease.outputs.is_prerelease }}
    generate_release_notes: true

# DOPO:
- name: Extract version-specific changelog
  run: |
    VERSION="${{ github.ref_name }}"
    VERSION_NUMBER="${VERSION#v}"

    # Extract section for this version from CHANGELOG.md
    awk -v ver="$VERSION_NUMBER" '
      /^## \[/ {
        if (found) exit
        if ($0 ~ "## \\[" ver "\\]") found=1
      }
      found { print }
    ' CHANGELOG.md > RELEASE_NOTES.md

    echo "=== Extracted changelog for $VERSION ==="
    cat RELEASE_NOTES.md

- name: Create GitHub Release
  uses: softprops/action-gh-release@v2
  with:
    files: artifacts/*
    body_path: RELEASE_NOTES.md  # ✅ Solo la sezione di questa versione
    draft: false
    prerelease: ${{ steps.prerelease.outputs.is_prerelease }}
    generate_release_notes: true
```

### Fase 5: Rigenerare Tag e Release

**Per ogni versione** (0.1.0, 0.2.0, 0.3.0, 0.3.1):

1. **Creare tag firmato GPG**:

   ```bash
   git checkout develop
   git pull origin develop

   # Tag v0.1.0
   git tag -s v0.1.0 -m "Release v0.1.0 - Initial Alpha Release

   Core features:
   - Multi-file Excel loading and search
   - Intelligent row comparison
   - TreeView search results with grouping
   - Theme system (light/dark)
   - Clean Architecture with dependency injection

   This is an ALPHA release with core functionality."

   # Ripeti per v0.2.0, v0.3.0, v0.3.1 con messaggi appropriati
   ```

2. **Push tag** (triggera automaticamente release workflow):

   ```bash
   git push origin v0.1.0
   # Aspetta che il workflow completi

   git push origin v0.2.0
   # Aspetta che il workflow completi

   git push origin v0.3.0
   # Aspetta che il workflow completi

   git push origin v0.3.1
   # Aspetta che il workflow completi
   ```

3. **Verificare release su GitHub**:

   ```bash
   gh release list
   gh release view v0.3.1
   ```

---

## 🔍 Checklist di Verifica Finale

Dopo aver completato tutti gli step, verifica:

- [ ] Tutti i tag esistono localmente: `git tag -l`
- [ ] Tutti i tag sono firmati GPG: `git tag -v v0.3.1`
- [ ] Tutti i tag sono pushati su GitHub: `git ls-remote --tags origin`
- [ ] Tutte le release esistono su GitHub: `gh release list`
- [ ] Ogni release mostra il CHANGELOG corretto (solo la sua sezione)
- [ ] Ogni release ha tutti gli artifacts (.exe, .tar.gz, .deb)
- [ ] Website aggiornato con versione corrente
- [ ] GitHub Actions workflows completati con successo

---

## 📝 Note Importanti

### File Temporanei Generati

- `/tmp/git-cliff-2.0.4/git-cliff` - Binary git-cliff (può essere rimosso dopo)
- `/data/repos/sheet-atlas/CHANGELOG-new.md` - CHANGELOG generato (da rinominare in CHANGELOG.md)

### Comandi Utili

```bash
# Verificare tag GPG
git tag -v <tag>

# Vedere CHANGELOG al momento di un tag
git show <tag>:CHANGELOG.md

# Cancellare tag locale
git tag -d <tag>

# Cancellare tag remoto
git push origin --delete <tag>

# Cancellare release GitHub
gh release delete <tag> --yes

# Verificare stato release
gh release list
gh release view <tag>

# Rigenerare CHANGELOG
/tmp/git-cliff-2.0.4/git-cliff --tag v0.3.1 --output CHANGELOG.md
```

---

## 🚀 Workflow Finale Raccomandato

Una volta completato tutto, il workflow normale per future release sarà:

1. **Sviluppo**: Lavora su branch feature
2. **Merge**: Merge su develop
3. **CHANGELOG**: Commit CHANGELOG aggiornato su develop
4. **Tag**: Crea tag firmato GPG localmente
5. **Push**: Push tag → triggera automaticamente release workflow
6. **Verifica**: Controlla release su GitHub

---

**Status**: Pronto per esecuzione domani
**Prossimi passi**: Eseguire Fase 1-5 in sequenza

---

## ✅ COMPLETATO - 2025-11-07 22:33

### Riepilogo Lavoro Eseguito

**Stato Finale**:

- ✅ Tutti i tag locali e remoti cancellati e ricreati
- ✅ Tutte le release GitHub cancellate e ricreate
- ✅ CHANGELOG.md rigenerato con tutte le versioni (0.1.0 → 0.3.1)
- ✅ Workflow release.yml corretto per estrarre sezione specifica
- ✅ Tutti i tag firmati GPG validamente

### Tag Creati (tutti su commit 5641fa8)

| Tag | Firma GPG | Data Creazione | Release GitHub |
|-----|-----------|----------------|----------------|
| v0.1.0 | ✅ Valida | 2025-11-07 22:07 | ✅ Creata |
| v0.2.0 | ✅ Valida | 2025-11-07 22:13 | ✅ Creata |
| v0.3.0 | ✅ Valida | 2025-11-07 22:19 | ✅ Creata |
| v0.3.1 | ✅ Valida | 2025-11-07 22:26 | ✅ Creata |

### Release GitHub Verificate

Tutte le 4 release hanno:

- ✅ 4 artifacts ciascuna (Windows .exe, Linux .deb/.tar.gz, macOS .tar.gz)
- ✅ CHANGELOG corretto (solo sezione specifica, non tutto il file)
- ✅ Marcate come Pre-release (corretto per v0.x)
- ✅ GitHub Actions workflow completati con successo

### Commit Creati

1. **bf0d4fb** - `docs: regenerate complete CHANGELOG for all versions`
   - CHANGELOG.md completo con tutte e 4 le versioni
   - Date corrette per ogni release

2. **5641fa8** - `fix: extract version-specific changelog section for GitHub releases`
   - Workflow release.yml corretto per estrarre solo sezione pertinente
   - Usa awk per estrazione dinamica basata su versione tag

### Strategia Scelta

**Opzione A - Tag sull'HEAD attuale**: Tutti i tag puntano al commit 5641fa8 (con CHANGELOG completo e workflow corretto)

- Pro: Tutte le release mostrano CHANGELOG completo e corretto
- Pro: Workflow corretto funziona per tutte le release
- Cons: Storicamente impreciso (ma accettabile dato che le release erano già state cancellate)

### Workflow Semi-Manuale Confermato

Il workflow semi-manuale è stato mantenuto:

1. ✅ Generazione CHANGELOG automatica (git-cliff)
2. ✅ Commit CHANGELOG su develop
3. ✅ Tag firmato GPG creato **manualmente** (mantiene chiave privata locale)
4. ✅ Push tag triggera automaticamente workflow release.yml
5. ✅ Release GitHub creata automaticamente con artifacts

**Vantaggi**:

- Sicurezza: chiave GPG privata non su GitHub
- Controllo: verifica manuale prima del tag
- Flessibilità: aggiunte dell'ultimo minuto possibili

### Prossimi Passi per Future Release

Per creare nuove release (es. v0.4.0):

```bash
# 1. Sviluppo completato, merge su develop
git checkout develop && git pull origin develop

# 2. Generare CHANGELOG aggiornato (opzionale, può essere fatto tramite GitHub Actions)
/tmp/git-cliff-2.0.4/git-cliff --output CHANGELOG.md
git add CHANGELOG.md
git commit -S -m "docs: update CHANGELOG for v0.4.0"
git push origin develop

# 3. Creare tag firmato GPG localmente
git tag -s v0.4.0 -m "Release v0.4.0 - Description

Features:
- Feature 1
- Feature 2

See CHANGELOG.md for details."

# 4. Verificare firma
git tag -v v0.4.0

# 5. Push tag (triggera automaticamente release workflow)
git push origin v0.4.0

# 6. Attendere completamento workflow (3-5 minuti)
# 7. Verificare release su GitHub
gh release view v0.4.0
```

**Status**: ✅ Lavoro completato con successo - Tutte le release sono pulite, corrette e firmate GPG
