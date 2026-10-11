/// <reference types="vite/client" />

interface SpeechRecognition extends EventTarget {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: (event: any) => void;
  onend: () => void;
  onerror: (event: any) => void;
}

interface SpeechRecognitionConstructor {
  new (): SpeechRecognition;
}

interface Window {
  SpeechRecognition?: SpeechRecognitionConstructor;
  webkitSpeechRecognition?: SpeechRecognitionConstructor;
  electron: {
    ollamaChat: (message: string, history: any[]) => Promise<string>;
    ollamaModels: () => Promise<string[]>;
    setModel: (model: string) => Promise<void>;
    startRecording: () => Promise<void>;
    stopRecording: () => Promise<string | null>;
    synthesizeSpeech: (text: string) => Promise<void>;
    transcribe: (audioBase64: string) => Promise<string>;
    resizeWindow: (width: number, height: number) => Promise<void>;
    startMove: () => void;
    moveWindow: (dx: number, dy: number) => void;
    setWindowPosition: (x: number, y: number) => void;
    getWindowPosition: () => Promise<[number, number]>;
    setIgnoreMouse: (ignore: boolean) => void;
    setOllamaHost: (host: string) => Promise<string[]>;
    openSettings: (url: string) => void;
  };
}
