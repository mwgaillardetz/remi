import React, { useState, useEffect, useRef } from 'react';
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
  const [koalaState, setKoalaState] = useState<'idle' | 'talking' | 'listening' | 'thinking' | 'excited' | 'singing'>('idle');
  const saved = JSON.parse(localStorage.getItem('remi-settings') ?? '{}');
  const [ttsEnabled, setTtsEnabled] = useState<boolean>(saved.ttsEnabled ?? true);
  const [ttsRate, setTtsRate] = useState<number>(saved.ttsRate ?? 1.5);
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [selectedVoice, setSelectedVoice] = useState<string>(saved.selectedVoice ?? '');
  const [micDevices, setMicDevices] = useState<MediaDeviceInfo[]>([]);
  const [selectedMic, setSelectedMic] = useState<string>(saved.selectedMic ?? '');
  const [models, setModels] = useState<string[]>([]);
  const [model, setModel] = useState<string>(saved.model ?? '');
  const [scale, setScale] = useState<number>(Math.max(50, saved.scale ?? 100));
  const [ollamaHost, setOllamaHost] = useState<string>(saved.ollamaHost ?? '');
  const [furColor, setFurColor] = useState<'grey' | 'amber' | 'black'>(saved.furColor ?? 'grey');
  const [showShirt, setShowShirt] = useState<boolean>(saved.showShirt ?? false);
  const [shirtStyle, setShirtStyle] = useState<'hawaiian' | 'plain' | 'tuxedo'>(saved.shirtStyle ?? 'hawaiian');
  const [showSunglasses, setShowSunglasses] = useState<boolean>(saved.showSunglasses ?? false);

  useEffect(() => {
    localStorage.setItem('remi-settings', JSON.stringify({ ttsEnabled, ttsRate, selectedVoice, selectedMic, model, scale, ollamaHost, furColor, showShirt, shirtStyle, showSunglasses }));
  }, [ttsEnabled, ttsRate, selectedVoice, selectedMic, model, scale, ollamaHost, furColor, showShirt, shirtStyle, showSunglasses]);

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
    const host = saved.ollamaHost;
    window.electron.ollamaModels().then(list => {
      setModels(list);
      if (list.length > 0) setModel(saved.model ?? list[0]);
    }).catch(() => {});
    if (host) {
      window.electron.setOllamaHost(host).then(list => {
        setModels(list);
        if (list.length > 0) setModel(saved.model ?? list[0]);
      }).catch(() => {});
    }
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

  const sendMessage = async (content: string, excited = false, singing = false) => {
    const userMessage: Message = { role: 'user', content };
    setMessages((prev) => [...prev, userMessage]);
    setKoalaState(singing ? 'singing' : excited ? 'excited' : 'thinking');
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

  const draggingRef = useRef(false);
  const ignoringRef = useRef(true);
  const winPosRef = useRef<[number, number]>([0, 0]);

  // Keep a fresh copy of window position so drag start is instant (no async gap)
  useEffect(() => {
    const refresh = () => {
      window.electron.getWindowPosition().then(pos => { winPosRef.current = pos; });
    };
    refresh();
    const id = setInterval(refresh, 500);
    return () => clearInterval(id);
  }, []);

  // Use pointermove (forwarded even when ignoring) to toggle click-through based on cursor position.
  // This is more reliable than mouseenter/mouseleave which can miss events when ignore is active.
  useEffect(() => {
    const onPointerMove = (e: PointerEvent) => {
      if (draggingRef.current) return;
      // e.clientX/Y are in CSS pixels relative to the window
      const remiBottom = Math.round((chatOpen ? 440 : 500) * (scale / 100));
      const totalBottom = chatOpen ? remiBottom + 360 : remiBottom;
      const overInteractive = e.clientX >= 0 && e.clientX <= 400 && e.clientY >= 0 && e.clientY <= totalBottom;
      if (overInteractive && ignoringRef.current) {
        ignoringRef.current = false;
        window.electron.setIgnoreMouse(false);
      } else if (!overInteractive && !ignoringRef.current) {
        ignoringRef.current = true;
        window.electron.setIgnoreMouse(true);
      }
    };
    window.addEventListener('pointermove', onPointerMove);
    return () => window.removeEventListener('pointermove', onPointerMove);
  }, [chatOpen, scale]);

  const handleRemiMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    let moved = false;
    const [winX0, winY0] = winPosRef.current;
    const mouseX0 = e.screenX, mouseY0 = e.screenY;
    draggingRef.current = true;
    window.electron.startMove();
    const onMove = (me: MouseEvent) => {
      const dx = me.screenX - mouseX0, dy = me.screenY - mouseY0;
      if (Math.abs(dx) > 2 || Math.abs(dy) > 2) moved = true;
      const newX = winX0 + dx, newY = winY0 + dy;
      winPosRef.current = [newX, newY];
      window.electron.setWindowPosition(newX, newY);
    };
    const onUp = () => {
      draggingRef.current = false;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
      if (!moved) {
        const next = !chatOpen;
        setChatOpen(next);
        const ns = scale / 100;
        const nextRemiH = Math.round((next ? 440 : 500) * ns);
        window.electron.resizeWindow(BASE_W, next ? nextRemiH + 360 : nextRemiH);
      }
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  const BASE_W = 400;
  const BASE_REMI_H = chatOpen ? 440 : 500;
  const s = scale / 100;
  const remiH = Math.round(BASE_REMI_H * s);
  const chatH = 360;
  const totalH = chatOpen ? remiH + chatH : remiH;

  return (
    <div style={{ width: BASE_W + 'px', height: totalH + 'px', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      {/* Rémi is rendered at full 400×BASE_REMI_H then CSS-scaled down to remiH */}
      <div style={{ width: BASE_W + 'px', height: remiH + 'px', flexShrink: 0, overflow: 'hidden', position: 'relative' }}>
        <div style={{
          width: BASE_W + 'px',
          height: BASE_REMI_H + 'px',
          transformOrigin: 'top left',
          transform: `scale(${s})`,
        }}>
          <KoalaCharacter
            state={koalaState}
            width={BASE_W}
            height={BASE_REMI_H}
            furColor={furColor}
            showShirt={showShirt}
            shirtStyle={shirtStyle}
            showSunglasses={showSunglasses}
          />
        </div>
        {/* Drag overlay in real (scaled) coordinates */}
        <div
          onMouseDown={handleRemiMouseDown}
          style={{ position: 'absolute', inset: 0, cursor: 'grab', zIndex: 10 }}
          title="Click to chat · Drag to move"
        />
        {!chatOpen && (
          <div style={{ position: 'absolute', bottom: '8px', left: '50%', transform: 'translateX(-50%)', color: 'rgba(255,255,255,0.7)', fontSize: '12px', pointerEvents: 'none', textAlign: 'center', zIndex: 11 }}>
            Chat with Rémi
          </div>
        )}
      </div>

      {chatOpen && (
        <div style={{ width: BASE_W + 'px', height: chatH + 'px', flexShrink: 0, background: '#1a1a2e', borderRadius: '12px 12px 0 0' }}>
          <ChatOverlay
            messages={messages}
            isTyping={isTyping}
            onSendMessage={sendMessage}
            onVoiceInput={handleVoiceInput}
            onClose={() => { setChatOpen(false); window.electron.resizeWindow(BASE_W, remiH); }}
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
            scale={scale}
            onScaleChange={(newScale) => {
              setScale(newScale);
              const ns = newScale / 100;
              const newRemiH = Math.round((chatOpen ? 440 : 500) * ns);
              window.electron.resizeWindow(BASE_W, chatOpen ? newRemiH + chatH : newRemiH);
            }}
            ollamaHost={ollamaHost}
            onOllamaHostChange={(host) => {
              setOllamaHost(host);
              window.electron.setOllamaHost(host).then(list => {
                setModels(list);
                if (list.length > 0) setModel(list[0]);
              }).catch(() => {});
            }}
            furColor={furColor}
            onFurColorChange={setFurColor}
            onQuickAction={(prompt, isSinging) => sendMessage(prompt, !isSinging, isSinging)}
            showShirt={showShirt}
            onShowShirtChange={setShowShirt}
            shirtStyle={shirtStyle}
            onShirtStyleChange={setShirtStyle}
            showSunglasses={showSunglasses}
            onShowSunglassesChange={setShowSunglasses}
          />
        </div>
      )}
    </div>
  );
}

export default App;
