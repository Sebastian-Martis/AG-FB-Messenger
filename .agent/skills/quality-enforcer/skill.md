---
name: quality-enforcer
description: Wymuszanie testów i automatyzacja CI/CD.
---

# QUALITY ENFORCER SKILL

## DEFINITION OF DONE (DoD)
Zadanie jest ukończone TYLKO gdy:
1. Kod buduje się bez ostrzeżeń.
2. Nowa logika biznesowa posiada testy jednostkowe.
3. Linter i Formatter zostały uruchomione i nie zwracają błędów.
4. Dokumentacja została zaktualizowana.

## AUTOMATYZACJA
1. **CI/CD:** Jeśli projekt jest na GitHubie, zaproponuj/stwórz `.github/workflows/ci.yml`.
2. **Mockowanie:** W testach wymagaj wstrzykiwania zależności (DI) i używania Mocków.
3. **Artefakty:** Nazywaj buildy: `app-<nazwa>-v<wersja>+<build>`.