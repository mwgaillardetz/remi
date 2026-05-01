import { app, BrowserWindow, ipcMain, Tray, globalShortcut, shell } from 'electron';
import path from 'path';
import { OllamaService } from './ollama';
import { createTray } from './tray';
import { VoiceService } from './voice';

// Required for Web Speech API to work in Electron
app.commandLine.appendSwitch('enable-speech-input');
app.commandLine.appendSwitch('enable-features', 'WebSpeechAPI');

let mainWindow: BrowserWindow | null = null;
let tray: Tray | null = null;
let ollama: OllamaService | null = null;
let voice: VoiceService | null = null;

const isDev = process.env.NODE_ENV !== 'production';

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 400,
    height: 500,
    show: false,
    frame: false,
    transparent: true,
    hasShadow: false,
    alwaysOnTop: true,
    skipTaskbar: false,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  mainWindow.setAlwaysOnTop(true, 'screen-saver');
  mainWindow.setVisibleOnAllWorkspaces(true, { visibleOnFullScreen: true });

  mainWindow.webContents.session.setPermissionRequestHandler((webContents, permission, callback) => {
    if (permission === 'media') return callback(true);
    callback(false);
  });

  mainWindow.webContents.session.setPermissionCheckHandler((webContents, permission) => {
    if (permission === 'media') return true;
    return false;
  });

  if (isDev) {
    mainWindow.loadURL('http://localhost:3369');
  } else {
    mainWindow.loadFile(path.join(__dirname, '../index.html'));
  }

  mainWindow.webContents.once('did-finish-load', () => {
    if (!mainWindow) return;
    const { screen } = require('electron');
    const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize;
    const winW = 400, winH = 500;
    const targetX = Math.floor(Math.random() * (sw - winW));
    const targetY = sh - winH - 20;

    mainWindow.setPosition(targetX, -winH);
    mainWindow.show();

    let currentY = -winH;
    const interval = setInterval(() => {
      if (!mainWindow) return clearInterval(interval);
      const remaining = targetY - currentY;
      if (Math.abs(remaining) < 2) {
        mainWindow.setPosition(targetX, targetY);
        return clearInterval(interval);
      }
      currentY += remaining * 0.15;
      mainWindow.setPosition(targetX, Math.round(currentY));
    }, 16);
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function registerShortcuts() {
  globalShortcut.register('CommandOrControl+Shift+B', () => {
    if (mainWindow) {
      if (mainWindow.isVisible()) {
        mainWindow.hide();
      } else {
        mainWindow.show();
        mainWindow.setAlwaysOnTop(true, 'screen-saver');
      }
    }
  });
}

async function initializeServices() {
  ollama = new OllamaService();
  voice = new VoiceService();

  ipcMain.handle('window:resize', (_: any, height: number) => {
    if (mainWindow) mainWindow.setSize(400, height, true);
  });

  ipcMain.on('shell:open', (_, url: string) => shell.openExternal(url));

  ipcMain.on('window:startMove', () => {    mainWindow?.moveTop();
  });

  ipcMain.on('window:move', (_: any, dx: number, dy: number) => {
    if (!mainWindow) return;
    const [x, y] = mainWindow.getPosition();
    mainWindow.setPosition(x + dx, y + dy);
  });

  ipcMain.handle('ollama:chat', async (_, message: string, history: any[]) => {
    if (!ollama) throw new Error('Ollama not initialized');
    return ollama.chat(message, history);
  });

  ipcMain.handle('ollama:models', async () => {
    if (!ollama) throw new Error('Ollama not initialized');
    return ollama.listModels();
  });

  ipcMain.handle('ollama:setModel', async (_, model: string) => {
    if (!ollama) throw new Error('Ollama not initialized');
    ollama.setModel(model);
  });

  ipcMain.handle('voice:start-recording', async () => {
    if (!voice) throw new Error('Voice not initialized');
    return voice.startRecording();
  });

  ipcMain.handle('voice:stop-recording', async () => {
    if (!voice) throw new Error('Voice not initialized');
    return voice.stopRecording();
  });

  ipcMain.handle('voice:synthesize', async (_, text: string) => {
    if (!voice) throw new Error('Voice not initialized');
    return voice.synthesize(text);
  });

  ipcMain.handle('voice:transcribe', async (_, audioBase64: string) => {
    const fs = require('fs');
    const os = require('os');
    const path = require('path');
    const { spawnSync } = require('child_process');

    const ts = Date.now();
    const tmpWebm = path.join(os.tmpdir(), `remi-${ts}.webm`);
    const tmpWav = path.join(os.tmpdir(), `remi-${ts}.wav`);
    fs.writeFileSync(tmpWebm, Buffer.from(audioBase64, 'base64'));

    // Convert to wav with ffmpeg
    const ffmpeg = spawnSync('ffmpeg', ['-y', '-i', tmpWebm, '-ar', '16000', '-ac', '1', tmpWav], { timeout: 10000 });
    try { fs.unlinkSync(tmpWebm); } catch {}

    if (ffmpeg.status !== 0 || !fs.existsSync(tmpWav)) {
      return '⚠️ ffmpeg not found — install ffmpeg and add it to PATH.';
    }

    // Try whisper first (much better accuracy)
    const whisper = spawnSync('python', ['-m', 'whisper', tmpWav, '--model', 'base', '--output_format', 'txt', '--output_dir', os.tmpdir(), '--language', 'en'], { timeout: 60000 });
    const txtFile = tmpWav.replace('.wav', '.txt');

    if (whisper.status === 0 && fs.existsSync(txtFile)) {
      const transcript = fs.readFileSync(txtFile, 'utf8').trim();
      try { fs.unlinkSync(tmpWav); fs.unlinkSync(txtFile); } catch {}
      return transcript;
    }

    // Fallback: Windows SAPI
    try { fs.unlinkSync(tmpWav); } catch {}
    const stderr = whisper.stderr?.toString() || '';
    const stdout = whisper.stdout?.toString() || '';
    return `⚠️ Whisper failed (status ${whisper.status}): ${stderr || stdout || 'no output'}`;
  });
}

app.whenReady().then(async () => {
  await initializeServices();
  createWindow();
  tray = createTray(mainWindow);
  registerShortcuts();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  globalShortcut.unregisterAll();
  if (process.platform !== 'darwin') app.quit();
});

app.on('will-quit', () => {
  globalShortcut.unregisterAll();
});
