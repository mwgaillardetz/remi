export class VoiceService {
  async startRecording(): Promise<void> {
    return Promise.resolve();
  }

  async stopRecording(): Promise<string | null> {
    return null;
  }

  async synthesize(text: string): Promise<void> {
    return Promise.resolve();
  }
}
