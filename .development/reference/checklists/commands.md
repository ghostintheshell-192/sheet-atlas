# GitHub CLI - Comandi Utili

Promemoria rapido per operazioni comuni con `gh` (GitHub CLI) e `git`.

---

## 🚀 WORKFLOW COMPLETO DI RILASCIO

**Scenario**: Hai sviluppato su `develop` e sei pronta per fare il rilascio su `main`.

```bash
# 1. Verifica lo stato del branch develop
git checkout develop
git status
git log --oneline -5

# 2. Assicurati che develop sia aggiornato
git pull origin develop

# 3. Passa a main e aggiorna
git checkout main
git pull origin main

# 4. Merge develop in main (fast-forward se possibile)
git merge develop

# 5. IMPORTANTE: Aggiorna CHANGELOG.md se necessario
# (editalo manualmente, poi:)
git add CHANGELOG.md
git commit -S -m "Update CHANGELOG for vX.Y.Z release"

# 6. Push main
git push origin main

# 7. Crea tag GPG-firmato
git tag -s vX.Y.Z -m "Release vX.Y.Z - Breve descrizione

# 7a. per dire a GPG: puoi stare sicuro della mia firma

 gpg --edit-key v.malavenda.git01@proton.me
  trust
  5 (I trust ultimately)
  quit


Changelog principale:
- Feature A
- Feature B
- Bug fix C"

# 8. Push del tag (questo triggera il workflow di build automatico)
git push origin vX.Y.Z

# 9. Monitora il workflow
gh run list --limit 5
gh run watch  # segue l'ultimo run in tempo reale

# 10. Quando il workflow completa, verifica la release
gh release list
gh release view vX.Y.Z

# 11. (OPZIONALE) Aggiorna le note della release se necessario
gh release edit vX.Y.Z --notes "Testo aggiornato"

# 12. Torna a develop per continuare lo sviluppo
git checkout develop
```

---

## 📋 Operazioni su Release

### Visualizzare release esistenti

```bash
# Lista tutte le release
gh release list

# Mostra dettagli di una release specifica
gh release view v0.2.0

# Vedi la release in browser
gh release view v0.2.0 --web
```

### Creare release manualmente (senza tag)

```bash
# Release normale
gh release create v1.0.0 --title "Titolo" --notes "Descrizione"

# Release prerelease (alpha/beta)
gh release create v0.3.0 --title "Alpha 0.3.0" --notes "Note" --prerelease

# Release draft (bozza)
gh release create v1.0.0 --title "Titolo" --notes "Note" --draft
```

### Modificare release esistenti

```bash
# Cambia titolo e note
gh release edit v0.2.0 --title "Nuovo titolo" --notes "Nuove note"

# Marca come prerelease
gh release edit v0.2.0 --prerelease

# Marca come release stabile (rimuove prerelease)
gh release edit v0.2.0 --latest

# Converti draft in release pubblica
gh release edit v1.0.0 --draft=false
```

### Caricare asset a release esistente

```bash
# Upload singolo file
gh release upload v0.2.0 SheetAtlas-windows-installer.exe

# Upload multipli file
gh release upload v0.2.0 file1.exe file2.tar.gz file3.dmg

# Sovrascrivi asset esistente
gh release upload v0.2.0 installer.exe --clobber
```

### Eliminare release

```bash
# Elimina release (ma NON il tag git)
gh release delete v0.2.0 --yes

# Elimina release E il tag git
gh release delete v0.2.0 --yes --cleanup-tag
```

---

## 🏷️ Operazioni su Tag Git

### Creare tag

```bash
# Tag semplice (NON raccomandato per release)
git tag v1.0.0

# Tag annotato (raccomandato)
git tag -a v1.0.0 -m "Release 1.0.0"

# Tag firmato GPG (OBBLIGATORIO per noi)
git tag -s v1.0.0 -m "Release 1.0.0 - Descrizione"

# Tag su commit specifico (non HEAD)
git tag -s v0.1.0 25738fd -m "Release 0.1.0"
```

### Visualizzare tag

```bash
# Lista tutti i tag
git tag

# Lista tag con pattern
git tag -l "v0.*"

# Mostra dettagli tag (messaggio, firma GPG, commit)
git show v0.2.0

# Verifica firma GPG di un tag
git tag -v v0.2.0
```

### Eliminare tag

```bash
# Elimina tag locale
git tag -d v1.0.0

# Elimina tag remoto
git push origin :refs/tags/v1.0.0

# Elimina multipli tag locali
git tag -d v1.0.0 v1.1.0

# Elimina multipli tag remoti
git push origin :refs/tags/v1.0.0 :refs/tags/v1.1.0
```

### Push tag

```bash
# Push singolo tag
git push origin v1.0.0

# Push tutti i tag locali
git push origin --tags

# Push con verifica firma GPG
git push --signed origin v1.0.0
```

---

## ⚙️ Operazioni su Workflow GitHub Actions

### Visualizzare workflow

```bash
# Lista tutti i workflow del repository
gh workflow list

# Mostra dettagli di workflow specifico
gh workflow view build-release.yml

# Lista run recenti (tutti i workflow)
gh run list --limit 10

# Lista run per workflow specifico
gh run list --workflow=build-release.yml --limit 5

# Filtra per stato
gh run list --workflow=build-release.yml --status=failure
```

### Monitorare workflow run

```bash
# Segui ultimo run in tempo reale
gh run watch

# Segui run specifico (usa ID da 'gh run list')
gh run watch 12345678

# Vedi dettagli di un run
gh run view 12345678

# Vedi run in browser
gh run view 12345678 --web

# Vedi log di un run
gh run view 12345678 --log
```

### Eseguire workflow manualmente

```bash
# Esegui workflow con workflow_dispatch
gh workflow run build-release.yml

# Esegui su branch specifico
gh workflow run build-release.yml --ref develop

# Esegui con input (se workflow li richiede)
gh workflow run build-release.yml --ref main -f tag=v0.2.0
```

### Gestire workflow run

```bash
# Cancella run fallito/vecchio
gh run delete 12345678

# Cancella tutti i run di un workflow (ATTENZIONE!)
gh run list --workflow=build-release.yml --json databaseId --jq '.[].databaseId' | xargs -I {} gh run delete {}

# Re-run workflow fallito
gh run rerun 12345678

# Re-run solo job falliti
gh run rerun 12345678 --failed
```

---

## 🔍 Operazioni di Diagnostica

### Verificare configurazione Git

```bash
# Mostra configurazione GPG
git config --global --get user.signingkey
git config --global --get commit.gpgsign
git config --global --get tag.gpgsign

# Mostra remote configurati
git remote -v

# Verifica SSH/HTTPS
git config --get remote.origin.url
```

### Verificare stato repository

```bash
# Stato corrente (branch, modifiche, etc.)
git status

# Log recente
git log --oneline --graph --all -10

# Differenze non committate
git diff

# Differenze staged
git diff --cached

# Commit diversi tra branch
git log main..develop --oneline
```

### Verificare release e workflow

```bash
# Release più recente
gh release list --limit 1

# Asset di una release
gh api repos/:owner/:repo/releases/tags/v0.2.0 | jq '.assets[].name'

# Stato ultimo workflow run
gh run list --limit 1 --json status,conclusion,name

# Download artifact da workflow run
gh run download 12345678
```

---

## 🆘 Situazioni di Emergenza

### Tag/release sbagliati (come oggi!)

```bash
# 1. Elimina tag locale e remoto
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# 2. Elimina release GitHub
gh release delete v1.0.0 --yes --cleanup-tag

# 3. Ricrea tag corretto
git tag -s v0.1.0 <commit-hash> -m "Messaggio corretto"
git push origin v0.1.0
```

### Workflow bloccato o errato

```bash
# 1. Cancella run in corso
gh run list --status=in_progress
gh run cancel <run-id>

# 2. Modifica workflow file
# (edita .github/workflows/nome-workflow.yml)

# 3. Commit e push
git add .github/workflows/
git commit -S -m "Fix workflow configuration"
git push origin develop  # o main, dipende

# 4. Re-trigger manualmente se serve
gh workflow run nome-workflow.yml
```

### Release mancante di asset

```bash
# 1. Scarica artifact da workflow run
gh run list --workflow=build-release.yml --limit 1
gh run download <run-id>

# 2. Upload manualmente alla release
gh release upload v0.2.0 artifacts/**/*
# oppure specifico:
gh release upload v0.2.0 SheetAtlas-windows-installer.exe
```

---

## 💡 Tips Utili

### Alias comandi frequenti

Aggiungi questi al tuo `~/.bashrc` o `~/.zshrc`:

```bash
# Git shortcuts
alias gs='git status'
alias gl='git log --oneline --graph --all -10'
alias gp='git push origin'
alias gco='git checkout'

# GitHub CLI shortcuts
alias ghrl='gh release list'
alias ghrv='gh release view'
alias ghwl='gh workflow list'
alias ghrun='gh run list --limit 5'
```

### Verifiche pre-rilascio

```bash
# Checklist rapida prima di fare merge develop → main
git checkout develop
git status  # tutto committed?
git log main..develop --oneline  # cosa sto per rilasciare?
git diff main develop  # differenze totali
# Poi procedi con il workflow di rilascio
```

### Backup prima di operazioni distruttive

```bash
# Salva stato corrente in un tag temporaneo
git tag backup-$(date +%Y%m%d-%H%M%S)

# Crea branch di backup
git branch backup-before-release

# Poi fai le operazioni pericolose
# Se serve tornare indietro:
git checkout backup-before-release
```

---

## 📚 Documentazione Ufficiale

- **GitHub CLI**: <https://cli.github.com/manual/>
- **Git**: <https://git-scm.com/docs>
- **GitHub Actions**: <https://docs.github.com/en/actions>

---

*Ultimo aggiornamento: 2025-10-13*
