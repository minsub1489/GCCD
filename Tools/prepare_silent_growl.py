"""Deterministic original synthetic growl reference; no external recording or voice."""
import math
import pathlib
import random
import struct
import wave

destination = pathlib.Path(__file__).resolve().parents[1] / "GCCD_Project/Assets/_Project/SilentResearch/Audio/Growl.wav"
destination.parent.mkdir(parents=True, exist_ok=True)
rate, seconds = 44100, 0.7
rng = random.Random(1489)
frames = bytearray()
for i in range(round(rate * seconds)):
    t = i / rate
    envelope = min(1, t / 0.04) * min(1, (seconds - t) / 0.15)
    phase = 2 * math.pi * (95 * t - 15 * t * t)
    sample = (math.sin(phase) + .35 * math.sin(phase * 2) + .18 * math.sin(phase * 3)) * .21
    sample += (rng.random() * 2 - 1) * .045
    sample *= envelope * (.75 + .25 * math.sin(2 * math.pi * 19 * t))
    frames.extend(struct.pack("<h", round(sample * 32767)))
with wave.open(str(destination), "wb") as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes(frames)
print(destination)
