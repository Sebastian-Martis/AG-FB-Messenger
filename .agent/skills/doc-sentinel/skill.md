---
name: doc-sentinel
description: Strażnik spójności README, CHANGELOG i ARCHITECTURE.
---

# DOC SENTINEL SKILL

## OBOWIĄZKOWE PLIKI
1. **README.md:** Opis, Stack, Quickstart (komendy dev/test), struktura projektu.
2. **CHANGELOG.md:** Styl "Keep a Changelog". Sekcje: [Added], [Changed], [Fixed]. Zawsze aktualizuj sekcję `## [Unreleased]`.
3. **ARCHITECTURE.md:** Kluczowe decyzje, schematy bazy danych (Mermaid ERD) i diagramy przepływu danych.
4. **VERSIONING.md:** Zasady SemVer 2.0.0.

## ZASADY AKTUALIZACJI
1. Każda nowa logika = wpis w CHANGELOG.
2. Każda zmiana architektury = aktualizacja ARCHITECTURE.md (w tym diagramy Mermaid).
3. Dokumentacja musi być w synchronizacji z aktualnym stanem plików w każdym commicie.