"""Read native 16 kHz PCM directly: no ffmpeg, server, or persistent worker."""
import argparse
import os
import wave
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--audio", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--model", choices=("tiny", "base", "small"), default="base")
    args = parser.parse_args()
    os.environ.setdefault("OMP_NUM_THREADS", "2")
    import numpy as np
    import torch
    import whisper

    torch.set_num_threads(max(1, min(4, (os.cpu_count() or 2) // 2)))
    with wave.open(str(args.audio), "rb") as recording:
        if (recording.getnchannels(), recording.getsampwidth(), recording.getframerate()) != (1, 2, 16000):
            raise ValueError("Expected mono, 16-bit, 16 kHz PCM audio")
        audio = np.frombuffer(recording.readframes(recording.getnframes()), dtype="<i2").astype(np.float32) / 32768.0
    model = whisper.load_model(args.model, device="cpu")
    result = model.transcribe(audio, language="en", fp16=False)
    args.output.write_text(result["text"].strip(), encoding="utf-8")


if __name__ == "__main__":
    main()
