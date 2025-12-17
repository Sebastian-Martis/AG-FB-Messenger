const { app, shell, dialog, Menu } = require('electron');
const path = require('path');
const { autoUpdater } = require('electron-updater');

function createMenu(mainWindow, usageTracker) {
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
                            message: 'FB-Messenger-JaRoD\n\nWersja: ' + app.getVersion() + '\nCreated by JaRoD-CENTER',
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
                {
                    label: '💬 Wyślij opinię / Zgłoś błąd',
                    click: () => {
                        shell.openExternal('mailto:biuro@jarod-center.com?subject=FB-Messenger-JaRoD%20Feedback');
                    }
                },
                { type: 'separator' },
                {
                    label: 'Zamknij',
                    accelerator: 'Alt+F4',
                    click: () => {
                        // Wymuś zamknięcie (obejście minimalizacji)
                        app.emit('before-quit');
                        app.quit();
                    }
                }
            ]
        },
        {
            label: 'Widok',
            submenu: [
                {
                    label: 'Odśwież / Wróć do Messenger',
                    accelerator: 'CmdOrCtrl+R',
                    click: () => { if (mainWindow) mainWindow.loadURL('https://www.messenger.com/'); }
                },
                {
                    label: 'Odśwież (F5)',
                    accelerator: 'F5',
                    click: () => { if (mainWindow) mainWindow.loadURL('https://www.messenger.com/'); }
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
}

module.exports = { createMenu };
