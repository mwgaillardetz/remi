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
  resizeWindow: (width, height) => electron.ipcRenderer.invoke("window:resize", width, height),
  startMove: () => electron.ipcRenderer.send("window:startMove"),
  moveWindow: (dx, dy) => electron.ipcRenderer.send("window:move", dx, dy),
  setWindowPosition: (x, y) => electron.ipcRenderer.send("window:setPosition", x, y),
  getWindowPosition: () => electron.ipcRenderer.invoke("window:getPosition"),
  setIgnoreMouse: (ignore) => electron.ipcRenderer.send("window:setIgnoreMouse", ignore),
  setOllamaHost: (host) => electron.ipcRenderer.invoke("ollama:setHost", host),
  openSettings: (url) => electron.ipcRenderer.send("shell:open", url)
});
