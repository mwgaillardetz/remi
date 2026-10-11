// Optional baseline for the historical renderer. No model, microphone, or user profile is opened.
const { app, BrowserWindow, ipcMain } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const workspace = path.resolve(__dirname, '../..');
const output = path.join(workspace, 'artifacts/electron-baseline');
fs.mkdirSync(output, { recursive: true });
app.setPath('userData', path.join(output, 'isolated-profile'));
const ready = path.join(output, 'ready.json');
if (fs.existsSync(ready)) fs.unlinkSync(ready);
app.whenReady().then(async () => {
  ipcMain.handle('window:getPosition', () => [100, 100]);
  ipcMain.handle('window:resize', () => {});
  for (const name of ['ollama:models', 'ollama:setHost']) ipcMain.handle(name, () => []);
  ipcMain.handle('ollama:setModel', () => {});
  const window = new BrowserWindow({ width: 400, height: 500, frame: false, transparent: true,
    alwaysOnTop: true, hasShadow: false, webPreferences: { contextIsolation: true,
      nodeIntegration: false, preload: path.join(workspace, 'dist/main/preload.js') } });
  window.webContents.session.setPermissionRequestHandler((_, __, callback) => callback(false));
  window.webContents.session.setPermissionCheckHandler(() => false);
  await window.loadFile(path.join(workspace, 'dist/index.html'));
  await new Promise(resolve => setTimeout(resolve, 2500));
  const canvas = await window.webContents.executeJavaScript("!!document.querySelector('canvas')");
  if (!canvas) throw new Error('Historical Three.js renderer did not mount.');
  fs.writeFileSync(ready, JSON.stringify({ pids: app.getAppMetrics().map(metric => metric.pid), electron: process.versions.electron }));
}).catch(error => { fs.writeFileSync(path.join(output, 'error.txt'), error.stack); app.exit(1); });
app.on('window-all-closed', () => app.quit());
setTimeout(() => app.exit(), 45000);
