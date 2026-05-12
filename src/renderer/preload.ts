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
  resizeWindow: (width: number, height: number) => ipcRenderer.invoke('window:resize', width, height),
  startMove: () => ipcRenderer.send('window:startMove'),
  moveWindow: (dx: number, dy: number) => ipcRenderer.send('window:move', dx, dy),
  setWindowPosition: (x: number, y: number) => ipcRenderer.send('window:setPosition', x, y),
  getWindowPosition: (): Promise<[number, number]> => ipcRenderer.invoke('window:getPosition'),
  setIgnoreMouse: (ignore: boolean) => ipcRenderer.send('window:setIgnoreMouse', ignore),
  setOllamaHost: (host: string): Promise<string[]> => ipcRenderer.invoke('ollama:setHost', host),
  openSettings: (url: string) => ipcRenderer.send('shell:open', url),
});
