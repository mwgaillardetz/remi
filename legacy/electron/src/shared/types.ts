export interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface OllamaModel {
  name: string;
  modified_at: string;
  size: number;
}

export type KoalaState = 'idle' | 'talking' | 'listening' | 'thinking';
