// ===========================================================================
// Usage Tracker - Moduł monitorowania użycia aplikacji
// ===========================================================================

const fs = require('fs');
const fsPromises = require('fs').promises;
const path = require('path');
const { app } = require('electron');

class UsageTracker {
    constructor() {
        this.dataPath = path.join(app.getPath('userData'), 'usage-stats.json');
        this.sessionStart = Date.now();
        this.data = this.loadSync(); // Sync tylko przy starcie
        this.saveQueue = Promise.resolve(); // Kolejka zapisu
    }

    // Załaduj dane synchronicznie (tylko przy starcie)
    loadSync() {
        try {
            if (fs.existsSync(this.dataPath)) {
                const raw = fs.readFileSync(this.dataPath, 'utf8');
                return JSON.parse(raw);
            }
        } catch (e) {
            console.log('Nie można załadować statystyk użycia:', e.message);
        }

        // Domyślne dane
        return {
            firstLaunch: new Date().toISOString(),
            launchCount: 0,
            totalSessionMinutes: 0,
            lastLaunch: null,
            loginEvents: []
        };
    }

    // Zapisz dane asynchronicznie (nie blokuje głównego wątku)
    async save() {
        // Używamy kolejki aby uniknąć równoczesnych zapisów
        this.saveQueue = this.saveQueue.then(async () => {
            try {
                await fsPromises.writeFile(
                    this.dataPath,
                    JSON.stringify(this.data, null, 2),
                    'utf8'
                );
            } catch (e) {
                console.log('Nie można zapisać statystyk użycia:', e.message);
            }
        });
        return this.saveQueue;
    }

    // Zarejestruj uruchomienie aplikacji
    trackLaunch() {
        this.data.launchCount++;
        this.data.lastLaunch = new Date().toISOString();
        this.sessionStart = Date.now();
        this.save();
    }

    // Zarejestruj zakończenie sesji
    trackSessionEnd() {
        const sessionMinutes = Math.round((Date.now() - this.sessionStart) / 60000);
        this.data.totalSessionMinutes += sessionMinutes;
        this.save();
    }

    // Zarejestruj wydarzenie logowania
    trackLogin(method) {
        this.data.loginEvents.push({
            date: new Date().toISOString(),
            method: method // 'code' lub 'phone_approval'
        });
        // Zachowaj tylko ostatnie 10 wydarzeń
        if (this.data.loginEvents.length > 10) {
            this.data.loginEvents = this.data.loginEvents.slice(-10);
        }
        this.save();
    }

    // Pobierz podsumowanie statystyk
    getSummary() {
        const hours = Math.floor(this.data.totalSessionMinutes / 60);
        const minutes = this.data.totalSessionMinutes % 60;

        return {
            firstLaunch: this.data.firstLaunch ? new Date(this.data.firstLaunch).toLocaleDateString('pl-PL') : 'Nieznana',
            lastLaunch: this.data.lastLaunch ? new Date(this.data.lastLaunch).toLocaleDateString('pl-PL') : 'Brak',
            launchCount: this.data.launchCount,
            totalTime: `${hours}h ${minutes}min`,
            loginCount: this.data.loginEvents.length,
            appVersion: app.getVersion() // Dynamicznie z package.json
        };
    }

    // Formatuj do wyświetlenia w dialogu
    getFormattedStats() {
        const s = this.getSummary();
        return `📊 Statystyki użycia J-Connect Enterprise

📅 Pierwsze uruchomienie: ${s.firstLaunch}
📅 Ostatnie uruchomienie: ${s.lastLaunch}

🚀 Liczba uruchomień: ${s.launchCount}
⏱️ Łączny czas użycia: ${s.totalTime}
🔐 Zarejestrowane logowania: ${s.loginCount}

📦 Wersja aplikacji: ${s.appVersion}`;
    }
}

module.exports = UsageTracker;

