---
name: architect-core
description: Globalne standardy architektury i bezpieczeństwa dla projektów Martis.
---

# ARCHITECT CORE SKILL

## ZASADY OPERACYJNE (SAFETY & ENV)
1. **Wykrywanie Stosu:** Przed rozpoczęciem pracy zidentyfikuj stack technologiczny i wersje (np. Node, Flutter, Python).
2. **Autonomia:** Działaj w obrębie katalogu projektu. Polecenia `rm` stosuj tylko do buildów i śmieci (np. .dart_tool, node_modules).
3. **Obsługa Błędów (RCA):** Jeśli komenda zawiedzie, podaj:
   - Krótką diagnozę przyczyny (Root Cause).
   - Logi błędu.
   - Gotową komendę naprawczą.

## CLEAN ARCHITECTURE
1. **Warstwowość:** Wymuszaj podział: `domain` (logika), `infrastructure` (dane/API), `presentation` (UI/Kontrolery).
2. **Modularność:** Grupuj kod domenowo (np. `/auth`, `/users`). Każdy moduł musi mieć spójną strukturę wewnętrzną.
3. **Identyfikatory:** Używaj UUID/ULID dla encji zamiast auto-increment ID.
4. **Boilerplate:** Usuwaj domyślne kody demonstracyjne frameworków (np. Counter App) natychmiast po init.