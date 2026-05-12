import React, { useState, useRef, useEffect } from 'react';

interface Message {
  role: 'user' | 'assistant';
  content: string;
}

interface ChatOverlayProps {
  messages: Message[];
  isTyping: boolean;
  onSendMessage: (content: string) => void;
  onVoiceInput: (transcript: string) => void;
  onClose: () => void;
  ttsEnabled: boolean;
  onTtsToggle: (enabled: boolean) => void;
  ttsRate: number;
  onTtsRateChange: (rate: number) => void;
  voices: SpeechSynthesisVoice[];
  selectedVoice: string;
  onVoiceChange: (name: string) => void;
  micDevices: MediaDeviceInfo[];
  selectedMic: string;
  onMicChange: (deviceId: string) => void;
  models: string[];
  model: string;
  onModelChange: (model: string) => void;
  scale: number;
  onScaleChange: (scale: number) => void;
  ollamaHost: string;
  onOllamaHostChange: (host: string) => void;
  furColor: 'grey' | 'amber' | 'black';
  onFurColorChange: (color: 'grey' | 'amber' | 'black') => void;
  onQuickAction: (prompt: string, isSinging?: boolean) => void;
  showShirt: boolean;
  onShowShirtChange: (v: boolean) => void;
  shirtStyle: 'hawaiian' | 'plain' | 'tuxedo';
  onShirtStyleChange: (v: 'hawaiian' | 'plain' | 'tuxedo') => void;
  showSunglasses: boolean;
  onShowSunglassesChange: (v: boolean) => void;
}

const ChatOverlay: React.FC<ChatOverlayProps> = ({
  messages, isTyping, onSendMessage, onVoiceInput, onClose,
  ttsEnabled, onTtsToggle, ttsRate, onTtsRateChange,
  voices, selectedVoice, onVoiceChange,
  micDevices, selectedMic, onMicChange,
  models, model, onModelChange,
  scale, onScaleChange,
  ollamaHost, onOllamaHostChange,
  furColor, onFurColorChange, onQuickAction,
  showShirt, onShowShirtChange, shirtStyle, onShirtStyleChange,
  showSunglasses, onShowSunglassesChange,
}) => {
  const [inputValue, setInputValue] = useState('');
  const [isRecording, setIsRecording] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isTyping]);

  const handleSubmit = (e?: React.FormEvent) => {
    e?.preventDefault();
    if (inputValue.trim()) {
      onSendMessage(inputValue.trim());
      setInputValue('');
    }
  };

  const toggleRecording = () => {
    if (isRecording) {
      mediaRecorderRef.current?.stop();
      setIsRecording(false);
      return;
    }

    const constraints: MediaStreamConstraints = {
      audio: selectedMic ? { deviceId: { exact: selectedMic } } : true,
    };

    navigator.mediaDevices.getUserMedia(constraints).then(stream => {
      audioChunksRef.current = [];
      const mr = new MediaRecorder(stream);
      mediaRecorderRef.current = mr;

      mr.ondataavailable = e => { if (e.data.size > 0) audioChunksRef.current.push(e.data); };

      mr.onstop = async () => {
        stream.getTracks().forEach(t => t.stop());
        try {
          const blob = new Blob(audioChunksRef.current, { type: 'audio/webm' });
          const arrayBuffer = await blob.arrayBuffer();
          // Send raw bytes to main process for decoding + transcription
          const base64 = (() => {
            const bytes = new Uint8Array(arrayBuffer);
            let binary = '';
            for (let i = 0; i < bytes.length; i += 8192) {
              binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
            }
            return btoa(binary);
          })();
          const transcript = await window.electron.transcribe(base64);
          if (transcript?.trim()) onVoiceInput(transcript.trim());
        } catch (err: any) {
          onVoiceInput(`[Error] Transcription failed: ${err?.message ?? String(err)}`);
        }
      };

      mr.start();
      setIsRecording(true);
    }).catch(err => {
      onVoiceInput(`[Error] Mic access failed: ${err?.message ?? err}`);
    });
  };

  const selectStyle: React.CSSProperties = { flex: 1, minWidth: 0, background: 'rgba(255,255,255,0.1)', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: '6px', padding: '3px 6px', fontSize: '11px' };
  const labelStyle: React.CSSProperties = { display: 'flex', alignItems: 'center', gap: '8px' };
  const spanStyle: React.CSSProperties = { color: '#fff', fontSize: '12px', flexShrink: 0, width: '70px' };

  return (
    <div className="chat-overlay">
      <div className="chat-header">
        <h3>Chat with Rémi</h3>
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <button className="chat-close" onClick={() => setShowSettings(s => !s)} title="Settings">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 15.5A3.5 3.5 0 0 1 8.5 12 3.5 3.5 0 0 1 12 8.5a3.5 3.5 0 0 1 3.5 3.5 3.5 3.5 0 0 1-3.5 3.5m7.43-2.92c.04-.34.07-.69.07-1.08s-.03-.74-.07-1.08l2.32-1.82c.21-.16.27-.46.13-.7l-2.2-3.81c-.13-.24-.42-.32-.66-.24l-2.74 1.1c-.57-.44-1.18-.8-1.86-1.07L14.5 2.42C14.46 2.18 14.24 2 14 2h-4c-.24 0-.46.18-.49.42l-.41 2.96c-.68.27-1.29.63-1.86 1.07L4.5 5.37c-.24-.09-.53 0-.66.24L1.64 9.42c-.14.24-.08.54.13.7l2.32 1.82C4.03 12.26 4 12.61 4 13s.03.74.07 1.08L1.77 15.9c-.21.16-.27.46-.13.7l2.2 3.81c.13.24.42.32.66.24l2.74-1.1c.57.44 1.18.8 1.86 1.07l.41 2.96c.03.24.25.42.49.42h4c.24 0 .46-.18.49-.42l.41-2.96c.68-.27 1.29-.63 1.86-1.07l2.74 1.1c.24.09.53 0 .66-.24l2.2-3.81c.14-.24.08-.54-.13-.7l-2.32-1.82z"/></svg>
          </button>
          <button className="chat-close" onClick={onClose}>&times;</button>
        </div>
      </div>

      {showSettings && (
        <div style={{ padding: '10px 16px', borderBottom: '1px solid rgba(255,255,255,0.1)', display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={labelStyle}>
            <span style={spanStyle}>Host</span>
            <input
              type="text"
              placeholder="http://127.0.0.1:11434 (optional)"
              defaultValue={ollamaHost}
              onBlur={e => { if (e.target.value !== ollamaHost) onOllamaHostChange(e.target.value); }}
              onKeyDown={e => { if (e.key === 'Enter') { e.currentTarget.blur(); } }}
              style={{ ...selectStyle, fontFamily: 'monospace' }}
            />
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Model</span>
            <select value={model} onChange={e => onModelChange(e.target.value)} style={selectStyle}>
              {models.map(m => <option key={m} value={m} style={{ background: '#1a1a2e' }}>{m}</option>)}
            </select>
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Size</span>
            <input type="range" min="50" max="150" step="5" value={scale} onChange={e => onScaleChange(parseInt(e.target.value))} style={{ flex: 1, minWidth: 0 }} />
            <span style={{ color: 'rgba(255,255,255,0.5)', fontSize: '11px', flexShrink: 0, width: '36px' }}>{scale}%</span>
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Color</span>
            <div style={{ display: 'flex', gap: '6px' }}>
              {([['grey', '#8a8a9a', 'Koala'], ['amber', '#9a6a2a', 'Bear'], ['black', '#2a2a2a', 'Black']] as const).map(([val, hex, label]) => (
                <button key={val} onClick={() => onFurColorChange(val)} title={label} style={{ width: 22, height: 22, borderRadius: '50%', background: hex, border: furColor === val ? '2px solid #fff' : '2px solid transparent', cursor: 'pointer', padding: 0 }} />
              ))}
            </div>
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Shirt</span>
            <input type="checkbox" checked={showShirt} onChange={e => onShowShirtChange(e.target.checked)} style={{ cursor: 'pointer', flexShrink: 0 }} />
            {showShirt && (
              <select value={shirtStyle} onChange={e => onShirtStyleChange(e.target.value as any)} style={{ ...selectStyle, marginLeft: 4 }}>
                <option value="hawaiian" style={{ background: '#1a1a2e' }}>🌺 Hawaiian</option>
                <option value="plain" style={{ background: '#1a1a2e' }}>👕 Plain</option>
                <option value="tuxedo" style={{ background: '#1a1a2e' }}>🤵 Tuxedo</option>
              </select>
            )}
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Sunglasses</span>
            <input type="checkbox" checked={showSunglasses} onChange={e => onShowSunglassesChange(e.target.checked)} style={{ cursor: 'pointer', flexShrink: 0 }} />
          </label>
          <label style={labelStyle}>
            <span style={spanStyle}>Voice</span>
            <input type="checkbox" checked={ttsEnabled} onChange={e => onTtsToggle(e.target.checked)} style={{ cursor: 'pointer', flexShrink: 0 }} />
            <span style={{ color: 'rgba(255,255,255,0.5)', fontSize: '11px' }}>{ttsEnabled ? 'on' : 'off'}</span>
          </label>
          {ttsEnabled && (
            <>
              <label style={labelStyle}>
                <span style={spanStyle}>Accent</span>
                <select value={selectedVoice} onChange={e => onVoiceChange(e.target.value)} style={selectStyle}>
                  {voices.map(v => <option key={v.name} value={v.name} style={{ background: '#1a1a2e' }}>{v.name} ({v.lang})</option>)}
                </select>
              </label>
              {!voices.some(v => v.lang === 'en-AU') && (
                <div style={{ fontSize: '10px', color: 'rgba(255,200,100,0.8)', paddingLeft: '78px', lineHeight: 1.4 }}>
                  No Australian voice installed.{' '}
                  <span
                    onClick={() => window.electron.openSettings('ms-settings:speech')}
                    style={{ textDecoration: 'underline', cursor: 'pointer' }}
                  >
                    Add voices
                  </span>
                </div>
              )}
              <label style={labelStyle}>
                <span style={spanStyle}>Speed</span>
                <input type="range" min="0.5" max="2" step="0.1" value={ttsRate} onChange={e => onTtsRateChange(parseFloat(e.target.value))} style={{ flex: 1, minWidth: 0 }} />
                <span style={{ color: 'rgba(255,255,255,0.5)', fontSize: '11px', flexShrink: 0, width: '28px' }}>{ttsRate.toFixed(1)}x</span>
              </label>
            </>
          )}
          {micDevices.length > 0 && (
            <label style={labelStyle}>
              <span style={spanStyle}>Mic</span>
              <select value={selectedMic} onChange={e => onMicChange(e.target.value)} style={selectStyle}>
                {micDevices.map(d => <option key={d.deviceId} value={d.deviceId} style={{ background: '#1a1a2e' }}>{d.label || 'Microphone'}</option>)}
              </select>
            </label>
          )}
        </div>
      )}

      <div className="chat-messages">
        {messages.length === 0 && (
          <div style={{ color: 'rgba(255,255,255,0.5)', textAlign: 'center', padding: '20px' }}>
            Say hi to Rémi!
          </div>
        )}
        {messages.map((msg, idx) => (
          <div key={idx} className={`message ${msg.role === 'user' ? 'user' : 'koala'}`}>
            {msg.content}
          </div>
        ))}
        {isTyping && (
          <div className="typing-indicator">
            <span></span><span></span><span></span>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      <div style={{ display: 'flex', gap: '6px', padding: '6px 12px 0', justifyContent: 'center' }}>
        {([
          ['😄 Joke', 'Tell me a short joke!', false],
          ['🎵 Song', 'Sing me a few lines of "In My Merry Oldsmobile"!', true],
          ['💡 Fun fact', 'Give me a surprising fun fact!', false],
        ] as const).map(([label, prompt, isSinging]) => (
          <button key={label} type="button" onClick={() => onQuickAction(prompt, isSinging)}
            style={{ flex: 1, padding: '4px 0', fontSize: '11px', background: 'rgba(255,255,255,0.08)', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: '8px', cursor: 'pointer' }}>
            {label}
          </button>
        ))}
      </div>

      <form className="chat-input-area" onSubmit={handleSubmit}>
        <button type="button" className={`chat-voice-btn ${isRecording ? 'recording' : ''}`} onClick={toggleRecording} title={isRecording ? 'Stop recording' : 'Start recording'}>
          {isRecording
            ? <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>
            : <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M12 14a3 3 0 0 0 3-3V5a3 3 0 0 0-6 0v6a3 3 0 0 0 3 3zm5-3a5 5 0 0 1-10 0H5a7 7 0 0 0 6 6.93V20H9v2h6v-2h-2v-2.07A7 7 0 0 0 19 11h-2z"/></svg>
          }
        </button>
        <input
          type="text"
          className="chat-input"
          placeholder="Type a message..."
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
        />
        <button type="submit" className="chat-send-btn" disabled={!inputValue.trim()}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M2 21l21-9L2 3v7l15 2-15 2z"/></svg>
        </button>
      </form>
    </div>
  );
};

export default ChatOverlay;
