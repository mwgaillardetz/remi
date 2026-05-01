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
}

const ChatOverlay: React.FC<ChatOverlayProps> = ({
  messages, isTyping, onSendMessage, onVoiceInput, onClose,
  ttsEnabled, onTtsToggle, ttsRate, onTtsRateChange,
  voices, selectedVoice, onVoiceChange,
  micDevices, selectedMic, onMicChange,
  models, model, onModelChange,
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
          onVoiceInput(`⚠️ Transcription failed: ${err?.message ?? String(err)}`);
        }
      };

      mr.start();
      setIsRecording(true);
    }).catch(err => {
      onVoiceInput(`⚠️ Mic access failed: ${err?.message ?? err}`);
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
          <button className="chat-close" onClick={() => setShowSettings(s => !s)} title="Settings">⚙️</button>
          <button className="chat-close" onClick={onClose}>&times;</button>
        </div>
      </div>

      {showSettings && (
        <div style={{ padding: '10px 16px', borderBottom: '1px solid rgba(255,255,255,0.1)', display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={labelStyle}>
            <span style={spanStyle}>Model</span>
            <select value={model} onChange={e => onModelChange(e.target.value)} style={selectStyle}>
              {models.map(m => <option key={m} value={m} style={{ background: '#1a1a2e' }}>{m}</option>)}
            </select>
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

      <form className="chat-input-area" onSubmit={handleSubmit}>
        <button type="button" className={`chat-voice-btn ${isRecording ? 'recording' : ''}`} onClick={toggleRecording} title={isRecording ? 'Stop recording' : 'Start recording'}>
          {isRecording ? '🎤' : '🎙️'}
        </button>
        <input
          type="text"
          className="chat-input"
          placeholder="Type a message..."
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
        />
        <button type="submit" className="chat-send-btn" disabled={!inputValue.trim()}>➤</button>
      </form>
    </div>
  );
};

export default ChatOverlay;
