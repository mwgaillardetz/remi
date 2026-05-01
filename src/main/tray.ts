import { Tray, Menu, BrowserWindow, app, nativeImage } from 'electron';

export function createTray(mainWindow: BrowserWindow | null): Tray {
  const tray = new Tray(nativeImage.createEmpty());

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Show Rémi',
      click: () => {
        mainWindow?.show();
        mainWindow?.setAlwaysOnTop(true, 'screen-saver');
      },
    },
    {
      label: 'Hide',
      click: () => mainWindow?.hide(),
    },
    { type: 'separator' },
    {
      label: 'Quit',
      click: () => app.quit(),
    },
  ]);

  tray.setToolTip('Rémi');
  tray.setContextMenu(contextMenu);

  tray.on('click', () => {
    if (mainWindow) {
      if (mainWindow.isVisible()) {
        mainWindow.hide();
      } else {
        mainWindow.show();
        mainWindow.setAlwaysOnTop(true, 'screen-saver');
      }
    }
  });

  return tray;
}
