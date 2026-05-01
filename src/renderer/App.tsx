import React, { useState, useEffect } from 'react';
import KoalaCharacter from './components/KoalaCharacter';
import ChatOverlay from './components/ChatOverlay';

interface Message {
  role: 'user' | 'assistant';
  content: string;
}

function App() {
  const [chatOpen, setChatOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isTyping, setIsTyping] = useState(false);
  const [koalaState, setKoalaState] = useState<'idle' | 'talking' | 'listening' | 'thinking'>('idle');
  const saved = JSON.parse(localStorage.getItem('remi-settings') ?? '{}');
  const [ttsEnabled, setTtsEnabled] = useState<boolean>(saved.ttsEnabled ?? true);
  const [ttsRate, setTtsRate] = useState<number>(saved.ttsRate ?? 1.5);
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [selectedVoice, setSelectedVoice] = useState<string>(saved.selectedVoice ?? '');
  const [micDevices, setMicDevices] = useState<MediaDeviceInfo[]>([]);
  const [selectedMic, setSelectedMic] = useState<string>(saved.selectedMic ?? '');
  const [models, setModels] = useState<string[]>([]);
  const [model, setModel] = useState<string>(saved.model ?? '');

  useEffect(() => {
    localStorage.setItem('remi-settings', JSON.stringify({ ttsEnabled, ttsRate, selectedVoice, selectedMic, model }));
  }, [ttsEnabled, ttsRate, selectedVoice, selectedMic, model]);

  useEffect(() => {
    const loadVoices = () => {
      const v = window.speechSynthesis.getVoices();
      if (v.length === 0) return;
      setVoices(v);
      if (!saved.selectedVoice) {
        const aussie = v.find(x => x.lang === 'en-AU');
        setSelectedVoice(aussie?.name ?? v[0]?.name ?? '');
      }
    };
    loadVoices();
    window.speechSynthesis.addEventListener('voiceschanged', loadVoices);
    return () => window.speechSynthesis.removeEventListener('voiceschanged', loadVoices);
  }, []);

  useEffect(() => {
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(stream => {
        stream.getTracks().forEach(t => t.stop());
        return navigator.mediaDevices.enumerateDevices();
      })
      .then(devices => {
        const mics = devices.filter(d => d.kind === 'audioinput');
        setMicDevices(mics);
        if (mics.length > 0) setSelectedMic(mics[0].deviceId);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    window.electron.ollamaModels().then(list => {
      setModels(list);
      if (list.length > 0) setModel(list[0]);
    }).catch(() => {});
  }, []);

  const handleModelChange = (m: string) => {
    setModel(m);
    window.electron.setModel(m);
  };

  const speak = (text: string) => {
    if (!ttsEnabled) return;
    window.speechSynthesis.cancel();
    const clean = text
      .replace(/[\p{Emoji_Presentation}\p{Extended_Pictographic}]/gu, '')
      .replace(/[*_~`#>|\\]/g, '')
      .replace(/\s+/g, ' ')
      .trim();
    if (!clean) return;
    const utt = new SpeechSynthesisUtterance(clean);
    utt.rate = ttsRate;
    const voice = window.speechSynthesis.getVoices().find(v => v.name === selectedVoice);
    if (voice) { utt.voice = voice; utt.lang = voice.lang; }
    window.speechSynthesis.speak(utt);
  };

  const sendMessage = async (content: string) => {
    const userMessage: Message = { role: 'user', content };
    setMessages((prev) => [...prev, userMessage]);
    setKoalaState('thinking');
    setIsTyping(true);

    try {
      const response = await window.electron.ollamaChat(
        content,
        messages.map((m) => ({ role: m.role, content: m.content }))
      );
      const assistantMessage: Message = { role: 'assistant', content: response };
      setMessages((prev) => [...prev, assistantMessage]);
      setKoalaState('talking');
      speak(response);
    } catch (error: any) {
      const errMsg = error?.message ?? String(error);
      setMessages((prev) => [...prev, { role: 'assistant', content: `[Error] ${errMsg}` }]);
    } finally {
      setIsTyping(false);
      setKoalaState('idle');
    }
  };

  const handleVoiceInput = async (transcript: string) => {
    if (!transcript.trim() || transcript.startsWith('[Error]')) {
      if (transcript.startsWith('[Error]')) {
        setMessages(prev => [...prev, { role: 'assistant', content: transcript }]);
      }
      return;
    }
    sendMessage(transcript);
  };

  const WIN_W = 400;
  const remiH = chatOpen ? 440 : 500;
  const chatH = 360;

  return (
    <div style={{ width: WIN_W + 'px', height: (chatOpen ? remiH + chatH : remiH) + 'px', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <div style={{ width: WIN_W + 'px', height: remiH + 'px', flexShrink: 0 }}>
        <KoalaCharacter
          state={koalaState}
          width={WIN_W}
          height={remiH}
          onChatClick={() => {
            const next = !chatOpen;
            setChatOpen(next);
            window.electron.resizeWindow(next ? remiH + chatH : 500);
          }}
          chatOpen={chatOpen}
        />
      </div>

      {chatOpen && (
        <div style={{ width: WIN_W + 'px', height: chatH + 'px', flexShrink: 0, background: '#1a1a2e', borderRadius: '12px 12px 0 0' }}>
          <ChatOverlay
            messages={messages}
            isTyping={isTyping}
            onSendMessage={sendMessage}
            onVoiceInput={handleVoiceInput}
            onClose={() => { setChatOpen(false); window.electron.resizeWindow(500); }}
            ttsEnabled={ttsEnabled}
            onTtsToggle={setTtsEnabled}
            ttsRate={ttsRate}
            onTtsRateChange={setTtsRate}
            voices={voices}
            selectedVoice={selectedVoice}
            onVoiceChange={setSelectedVoice}
            micDevices={micDevices}
            selectedMic={selectedMic}
            onMicChange={setSelectedMic}
            models={models}
            model={model}
            onModelChange={handleModelChange}
          />
        </div>
      )}
    </div>
  );
}

export default App;
