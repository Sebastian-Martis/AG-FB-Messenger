// ===========================================================================
// AG Desktop Messenger - Aplikacja do czatowania przez Messenger
// ===========================================================================

const { app, BrowserWindow, shell, Tray, Menu, nativeImage, globalShortcut, Notification, session, dialog } = require('electron');
const path = require('path');
const UsageTracker = require('./usage-tracker');

// Obsługa instalatora Squirrel (Windows) - WAŻNE dla poprawnej instalacji/deinstalacji
if (require('electron-squirrel-startup')) {
    app.quit();
}

// ===========================================================================
// Konfiguracja
// ===========================================================================

const CONFIG = {
    // Rozmiary okna
    DEFAULT_WIDTH: 1200,
    DEFAULT_HEIGHT: 800,
    MIN_WIDTH: 400,
    MIN_HEIGHT: 300,

    // URL Messengera
    MESSENGER_URL: 'https://www.messenger.com/',

    // User-Agent (symulacja prawdziwego Chrome)
    USER_AGENT: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',

    // Domeny dozwolone do otwierania w aplikacji
    ALLOWED_DOMAINS: ['messenger.com', 'facebook.com', 'fbcdn.net', 'google.com', 'facebook.net'],

    // Ścieżka do ikony (jeśli istnieje)
    ICON_PATH: path.join(__dirname, 'assets', 'icon.png')
};

// ===========================================================================
// Przechowywanie stanu okna (pozycja, rozmiar)
// ===========================================================================

let Store;
let store;

// Dynamiczny import dla electron-store (ESM module)
async function initStore() {
    try {
        Store = (await import('electron-store')).default;
        store = new Store({
            defaults: {
                windowBounds: {
                    width: CONFIG.DEFAULT_WIDTH,
                    height: CONFIG.DEFAULT_HEIGHT,
                    x: undefined,
                    y: undefined
                },
                isMaximized: false
            }
        });
    } catch (e) {
        console.log('electron-store nie jest dostępny, używam domyślnych ustawień');
        store = null;
    }
}

// ===========================================================================
// Zmienne globalne
// ===========================================================================

let mainWindow = null;
let tray = null;
let isQuitting = false;
let usageTracker = null;

// ===========================================================================
// Tworzenie ikony dla zasobnika systemowego (Tray)
// ===========================================================================

function createTrayIcon() {
    // Jeśli mamy własną ikonę, użyj jej
    try {
        const fs = require('fs');
        if (fs.existsSync(CONFIG.ICON_PATH)) {
            return nativeImage.createFromPath(CONFIG.ICON_PATH);
        }
    } catch (e) {
        // Ignoruj błędy
    }

    // Domyślna ikona (mały niebieski kwadrat)
    return nativeImage.createFromDataURL(
        'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAAdgAAAHYBTnsmCAAAABl0RVh0U29mdHdhcmUAd3d3Lmlua3NjYXBlLm9yZ5vuPBoAAABUSURBVDiNY/j//z8DDDAyMjKgA0ZGRob/6HxGED7AwIAugCIAkoRRgCEwaoC/v/9/BgYGhv///zMwMDBg9eLAgQP/mQICAv6DFKMbABIPUgxSPGoDAAD0bCUlWq+eAAAAAElFTkSuQmCC'
    );
}

// ===========================================================================
// Tworzenie menu zasobnika systemowego
// ===========================================================================

function createTray() {
    try {
        const icon = createTrayIcon();
        tray = new Tray(icon);

        const contextMenu = Menu.buildFromTemplate([
            {
                label: 'Pokaż Messenger',
                click: () => {
                    if (mainWindow) {
                        mainWindow.show();
                        mainWindow.focus();
                    }
                }
            },
            {
                label: 'Odśwież',
                click: () => {
                    if (mainWindow) {
                        mainWindow.webContents.reload();
                    }
                }
            },
            { type: 'separator' },
            {
                label: 'Wyczyść dane i zaloguj ponownie',
                click: async () => {
                    if (mainWindow) {
                        await session.defaultSession.clearStorageData();
                        mainWindow.webContents.reload();
                    }
                }
            },
            { type: 'separator' },
            {
                label: 'Zamknij',
                click: () => {
                    isQuitting = true;
                    app.quit();
                }
            }
        ]);

        tray.setToolTip('AG Messenger');
        tray.setContextMenu(contextMenu);

        // Kliknięcie na ikonę w zasobniku pokazuje okno
        tray.on('click', () => {
            if (mainWindow) {
                if (mainWindow.isVisible()) {
                    mainWindow.focus();
                } else {
                    mainWindow.show();
                }
            }
        });
    } catch (e) {
        console.log('Nie udało się utworzyć ikony w zasobniku:', e.message);
    }
}

// ===========================================================================
// Główna funkcja tworzenia okna
// ===========================================================================

function createWindow() {
    // Pobierz zapisany stan okna
    let windowBounds = {
        width: CONFIG.DEFAULT_WIDTH,
        height: CONFIG.DEFAULT_HEIGHT,
        x: undefined,
        y: undefined
    };
    let isMaximized = false;

    if (store) {
        windowBounds = store.get('windowBounds', windowBounds);
        isMaximized = store.get('isMaximized', false);
    }

    // Tworzenie głównego okna
    mainWindow = new BrowserWindow({
        width: windowBounds.width,
        height: windowBounds.height,
        x: windowBounds.x,
        y: windowBounds.y,
        minWidth: CONFIG.MIN_WIDTH,
        minHeight: CONFIG.MIN_HEIGHT,
        title: 'FB-Messenger-JaRoD', // Zmiana tytułu
        icon: path.join(__dirname, 'assets', 'icon.png'), // Nowa ikona
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            notifications: true,
            partition: 'persist:messenger'
        },
        show: false, // Ukryte na start
        backgroundColor: '#ffffff' // Białe tło dla splash screena
    });

    // Utwórz menu aplikacji z działającymi skrótami
    const menuTemplate = [
        {
            label: 'JaRoD-CENTER',
            submenu: [
                {
                    label: 'O firmie',
                    click: () => {
                        dialog.showMessageBox(mainWindow, {
                            type: 'info',
                            title: 'O programie',
                            message: 'FB-Messenger-JaRoD\n\nWersja: 1.2.0\nCreated by JaRoD-CENTER',
                            icon: path.join(__dirname, 'assets', 'icon.png')
                        });
                    }
                },
                {
                    label: '📊 Statystyki użycia',
                    click: () => {
                        if (usageTracker) {
                            dialog.showMessageBox(mainWindow, {
                                type: 'info',
                                title: 'Statystyki użycia',
                                message: usageTracker.getFormattedStats()
                            });
                        }
                    }
                },
                { type: 'separator' },
                {
                    label: 'Zamknij',
                    accelerator: 'Alt+F4',
                    click: () => { isQuitting = true; app.quit(); }
                }
            ]
        },
        {
            label: 'Widok',
            submenu: [
                {
                    label: 'Odśwież / Wróć do Messenger',
                    accelerator: 'CmdOrCtrl+R',
                    click: () => { if (mainWindow) mainWindow.loadURL(CONFIG.MESSENGER_URL); }
                },
                {
                    label: 'Odśwież (F5)',
                    accelerator: 'F5',
                    click: () => { if (mainWindow) mainWindow.loadURL(CONFIG.MESSENGER_URL); }
                },
                { type: 'separator' },
                {
                    label: 'Narzędzia deweloperskie',
                    accelerator: 'F12',
                    click: () => { if (mainWindow) mainWindow.webContents.toggleDevTools(); }
                }
            ]
        }
    ];

    const menu = Menu.buildFromTemplate(menuTemplate);
    Menu.setApplicationMenu(menu);

    // Jeśli było zmaksymalizowane, zmaksymalizuj
    // if (isMaximized) {
    //     mainWindow.maximize();
    // }

    // Pokaż okno gdy jest gotowe
    // mainWindow.once('ready-to-show', () => {
    //     mainWindow.show();
    // });

    // Załaduj Messenger
    // mainWindow.webContents.setUserAgent(CONFIG.USER_AGENT);
    // mainWindow.loadURL(CONFIG.MESSENGER_URL);

    // ===============================================
    // LOGIKA SPLASH SCREEN
    // ===============================================

    // 1. Najpierw załaduj splash screen
    mainWindow.loadFile(path.join(__dirname, 'splash.html'));

    // Pokaż okno gdy splash jest gotowy
    mainWindow.once('ready-to-show', () => {
        mainWindow.show();

        // Jeśli było zmaksymalizowane, zmaksymalizuj (ale dopiero po pokazaniu)
        if (isMaximized) {
            mainWindow.maximize();
        }
    });

    // 2. Po 3 sekundach załaduj Messenger
    setTimeout(() => {
        // Ustaw User-Agent
        mainWindow.webContents.setUserAgent(CONFIG.USER_AGENT);
        // Załaduj właściwą stronę
        mainWindow.loadURL(CONFIG.MESSENGER_URL);
    }, 6000); // 6 sekund na przeczytanie instrukcji

    // ===========================================================================
    // Obsługa zapisywania stanu okna
    // ===========================================================================

    function saveWindowState() {
        if (!store || !mainWindow) return;

        const isMax = mainWindow.isMaximized();
        store.set('isMaximized', isMax);

        // Zapisz wymiary tylko jeśli okno nie jest zmaksymalizowane
        if (!isMax) {
            const bounds = mainWindow.getBounds();
            store.set('windowBounds', bounds);
        }
    }

    mainWindow.on('resize', saveWindowState);
    mainWindow.on('move', saveWindowState);

    // ===========================================================================
    // Obsługa zamykania okna
    // ===========================================================================

    mainWindow.on('close', (event) => {
        // Jeśli nie wychodzimy z aplikacji, tylko zminimalizuj do tray
        if (!isQuitting && tray) {
            event.preventDefault();
            mainWindow.hide();

            // Pokaż powiadomienie o zminimalizowaniu (tylko raz)
            if (!app.wasMinimizedNotificationShown) {
                new Notification({
                    title: 'AG Messenger',
                    body: 'Aplikacja działa w tle. Kliknij ikonę w zasobniku aby otworzyć.'
                }).show();
                app.wasMinimizedNotificationShown = true;
            }
        }
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });

    // ===========================================================================
    // Obsługa linków zewnętrznych
    // ===========================================================================

    mainWindow.webContents.setWindowOpenHandler(({ url }) => {
        // Sprawdź czy URL jest z dozwolonej domeny
        const isAllowed = CONFIG.ALLOWED_DOMAINS.some(domain => url.includes(domain));

        if (!isAllowed) {
            // Otwórz w domyślnej przeglądarce
            shell.openExternal(url);
            return { action: 'deny' };
        }

        // Dozwolone domeny otwórz w nowym oknie aplikacji
        return { action: 'allow' };
    });

    // Obsługa nawigacji - blokuj wyjście z Messengera
    mainWindow.webContents.on('will-navigate', (event, url) => {
        const isAllowed = CONFIG.ALLOWED_DOMAINS.some(domain => url.includes(domain));

        if (!isAllowed) {
            event.preventDefault();
            shell.openExternal(url);
        }
    });

    // ===========================================================================
    // Obsługa błędów ładowania
    // ===========================================================================

    mainWindow.webContents.on('did-fail-load', (event, errorCode, errorDescription) => {
        console.log(`Błąd ładowania: ${errorCode} - ${errorDescription}`);

        // Wyświetl stronę błędu
        mainWindow.loadFile(path.join(__dirname, 'error.html')).catch(() => {
            // Jeśli nie ma pliku error.html, wyświetl prosty komunikat
            mainWindow.loadURL('data:text/html,<html><body style="background:#1a1a1a;color:#fff;font-family:sans-serif;display:flex;justify-content:center;align-items:center;height:100vh;margin:0;"><div style="text-align:center"><h1>Brak połączenia</h1><p>Nie można połączyć się z Messengerem.</p><p>Sprawdź połączenie internetowe i naciśnij Ctrl+R aby spróbować ponownie.</p></div></body></html>');
        });
    });

    // ===========================================================================
    // Menu kontekstowe (prawy przycisk myszy)
    // ===========================================================================

    mainWindow.webContents.on('context-menu', (event, params) => {
        const menu = Menu.buildFromTemplate([
            { label: 'Cofnij', role: 'undo', enabled: params.editFlags.canUndo },
            { label: 'Ponów', role: 'redo', enabled: params.editFlags.canRedo },
            { type: 'separator' },
            { label: 'Wytnij', role: 'cut', enabled: params.editFlags.canCut },
            { label: 'Kopiuj', role: 'copy', enabled: params.editFlags.canCopy },
            { label: 'Wklej', role: 'paste', enabled: params.editFlags.canPaste },
            { label: 'Zaznacz wszystko', role: 'selectAll' },
            { type: 'separator' },
            {
                label: 'Odśwież',
                accelerator: 'CmdOrCtrl+R',
                click: () => mainWindow.webContents.reload()
            },
            {
                label: 'Narzędzia deweloperskie',
                accelerator: 'F12',
                click: () => mainWindow.webContents.toggleDevTools()
            }
        ]);

        menu.popup();
    });
}

// ===========================================================================
// Rejestracja skrótów klawiaturowych
// ===========================================================================

function registerShortcuts() {
    // Ctrl+Shift+M - szybkie przełączanie widoczności
    globalShortcut.register('CommandOrControl+Shift+M', () => {
        if (mainWindow) {
            if (mainWindow.isVisible()) {
                mainWindow.hide();
            } else {
                mainWindow.show();
                mainWindow.focus();
            }
        }
    });
}

// ===========================================================================
// Inicjalizacja aplikacji
// ===========================================================================

app.whenReady().then(async () => {
    // 1. Zapewnij, że działa tylko jedna instancja aplikacji
    const gotTheLock = app.requestSingleInstanceLock();
    if (!gotTheLock) {
        app.quit();
        return;
    }

    app.on('second-instance', () => {
        // Jeśli ktoś próbuje uruchomić drugą instancję, przywróć główną
        if (mainWindow) {
            if (mainWindow.isMinimized()) mainWindow.restore();
            mainWindow.show();
            mainWindow.focus();
        }
    });

    // 2. Skonfiguruj uprawnienia sesji (dla trwałego logowania)
    const partition = 'persist:messenger';
    const ses = session.fromPartition(partition);

    ses.setPermissionRequestHandler((webContents, permission, callback) => {
        // Zezwalaj na wszystko dla naszych domen
        const url = webContents.getURL();
        // Zawsze zezwalaj na powiadomienia i dżwięki
        if (permission === 'notifications' || permission === 'media') {
            callback(true);
            return;
        }

        callback(true); // Domyślnie zezwalaj na storage itp.
    });

    // Wymuś akceptację ciasteczek (ważne dla 2FA/Logowania)
    // Nie czyścimy ciasteczek przy starcie!

    // Zainicjuj store
    await initStore();

    // Zainicjuj śledzenie użycia
    usageTracker = new UsageTracker();
    usageTracker.trackLaunch();

    // Utwórz okno
    createWindow();

    // Utwórz ikonę w zasobniku
    createTray();

    // Zarejestruj skróty klawiaturowe
    registerShortcuts();

    // macOS: Utwórz okno po kliknięciu ikony w docku
    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow();
        } else if (mainWindow) {
            mainWindow.show();
        }
    });
});

// ===========================================================================
// Zamykanie aplikacji
// ===========================================================================

app.on('before-quit', () => {
    // Zapisz statystyki sesji przed zamknięciem
    if (usageTracker) {
        usageTracker.trackSessionEnd();
    }
    isQuitting = true;
});

app.on('window-all-closed', () => {
    // Na Windows/Linux zamknij całą aplikację
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('will-quit', () => {
    // Wyrejestruj wszystkie skróty klawiaturowe
    globalShortcut.unregisterAll();
});

// ===========================================================================
// Obsługa błędów
// ===========================================================================

process.on('uncaughtException', (error) => {
    console.error('Nieobsłużony wyjątek:', error);
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('Nieobsłużona obietnica:', promise, 'powód:', reason);
});
