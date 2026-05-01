import axios from 'axios';

const BASE = 'http://127.0.0.1:11434';

export class OllamaService {
  private model: string = 'llama3.2';
  private systemPrompt: string = `You are Rémi, a playful, friendly, and helpful AI assistant.
You're curious, kind, and love to chat. You speak in a warm, conversational tone.
You're knowledgeable but humble, and you enjoy making people smile.
Keep responses concise and natural, like a real conversation.`;

  async chat(message: string, history: any[] = []): Promise<string> {
    const model = await this.resolveModel();
    const messages = [
      { role: 'system', content: this.systemPrompt },
      ...history,
      { role: 'user', content: message },
    ];
    const { data } = await axios.post(`${BASE}/api/chat`, { model, messages, stream: false });
    return data.message.content;
  }

  private async resolveModel(): Promise<string> {
    const models = await this.listModels();
    if (models.length === 0) throw new Error('No models found. Run: ollama pull llama3.2');
    const exact = models.find(m => m === this.model);
    if (exact) return exact;
    const prefix = models.find(m => m.startsWith(this.model + ':'));
    if (prefix) return prefix;
    this.model = models[0];
    return this.model;
  }

  async listModels(): Promise<string[]> {
    const { data } = await axios.get(`${BASE}/api/tags`);
    return (data.models ?? []).map((m: any) => m.name);
  }

  setModel(model: string) { this.model = model; }
}
