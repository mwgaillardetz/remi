"use strict";
const electron = require("electron");
electron.contextBridge.exposeInMainWorld("electron", {
  // Ollama
  ollamaChat: (message, history) => electron.ipcRenderer.invoke("ollama:chat", message, history),
  ollamaModels: () => electron.ipcRenderer.invoke("ollama:models"),
  setModel: (model) => electron.ipcRenderer.invoke("ollama:setModel", model),
  // Voice
  startRecording: () => electron.ipcRenderer.invoke("voice:start-recording"),
  stopRecording: () => electron.ipcRenderer.invoke("voice:stop-recording"),
  synthesizeSpeech: (text) => electron.ipcRenderer.invoke("voice:synthesize", text),
  transcribe: (audioBase64) => electron.ipcRenderer.invoke("voice:transcribe", audioBase64),
  // Window
  resizeWindow: (height) => electron.ipcRenderer.invoke("window:resize", height),
  startMove: () => electron.ipcRenderer.send("window:startMove"),
  moveWindow: (dx, dy) => electron.ipcRenderer.send("window:move", dx, dy),
  openSettings: (url) => electron.ipcRenderer.send("shell:open", url)
});
