import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('electron', {
  // Ollama
  ollamaChat: (message: string, history: any[]) =>
    ipcRenderer.invoke('ollama:chat', message, history),
  ollamaModels: () => ipcRenderer.invoke('ollama:models'),
  setModel: (model: string) => ipcRenderer.invoke('ollama:setModel', model),

  // Voice
  startRecording: () => ipcRenderer.invoke('voice:start-recording'),
  stopRecording: () => ipcRenderer.invoke('voice:stop-recording'),
  synthesizeSpeech: (text: string) => ipcRenderer.invoke('voice:synthesize', text),
  transcribe: (audioBase64: string) => ipcRenderer.invoke('voice:transcribe', audioBase64),

  // Window
  resizeWindow: (height: number) => ipcRenderer.invoke('window:resize', height),
  startMove: () => ipcRenderer.send('window:startMove'),
  moveWindow: (dx: number, dy: number) => ipcRenderer.send('window:move', dx, dy),
  openSettings: (url: string) => ipcRenderer.send('shell:open', url),
});
