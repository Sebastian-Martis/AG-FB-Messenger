---
name: gitops-expert
description: Zarządzanie historią zmian i repozytorium w standardzie Martis.
---

# GITOPS EXPERT SKILL

## GIT DISCIPLINE
1. **Format Commita:** ZAWSZE stosuj: `[TYPE]: Short description in English`.
   - TYPY: Feature, Fix, Refactor, Docs, Test, Chore, Build, CI.
2. **Atomiczne Commity:** Jeden commit = jedna logiczna zmiana. Nie mieszaj refaktoryzacji z nową funkcją.
3. **Ochrona Gałęzi:** Zakaz `force-push` na `main` lub `master`.
4. **Repo Init:** Jeśli brak Gita, wykonaj `git init` i stwórz profesjonalny `.gitignore` dla danego stacka.

## PROCEDURA KOŃCOWA
1. Przed zakończeniem zadania sprawdź `git status`.
2. Upewnij się, że wszystkie nowe pliki są śledzone (git add).
3. Wygeneruj komunikat commita zgodny ze standardem.