#!/usr/bin/env python3
"""Create Seabright's original score, ambiences and interaction sounds.

Requires Python 3 + NumPy. No network, samples, credentials, or external media.
All loops are rendered on a circular timeline, including instrument tails and
echoes. Keep this source alongside the authored WAVs so the assets are editable.
"""
from pathlib import Path
import json
import math
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/Audio"
SR = 44100
RNG = np.random.default_rng(54821)
METRICS = {}


def midi(note):
    return 440.0 * 2.0 ** ((note - 69) / 12.0)


def seconds(duration):
    return np.arange(round(duration * SR), dtype=np.float64) / SR


def envelope(t, attack=.009, release=.06):
    return (1 - np.exp(-t / attack)) * np.minimum(1, (t[-1] - t) / release).clip(0, 1)


def key(note, duration=3.1, warmth=1.0):
    """A soft struck tine, voiced from decaying partials, never a square wave."""
    t = seconds(duration)
    f = midi(note)
    body = (np.sin(2*np.pi*f*t) * np.exp(-t/1.22)
            + .28*np.sin(2*np.pi*f*2.001*t) * np.exp(-t/.65)
            + .095*np.sin(2*np.pi*f*3.997*t) * np.exp(-t/.24)
            + .025*np.sin(2*np.pi*f*6.001*t) * np.exp(-t/.09))
    # The strike has a rounded attack and very short, quiet felt transient.
    return body * envelope(t, .010, .15) * warmth


def pad(note, duration=5.6):
    t = seconds(duration)
    f = midi(note)
    env = np.minimum(t/.95, 1) * np.minimum((t[-1]-t)/2.2, 1)
    body = (.64*np.sin(2*np.pi*f*t + .16*np.sin(2*np.pi*.31*t))
            + .25*np.sin(2*np.pi*f*1.0017*t)
            + .09*np.sin(2*np.pi*f*2*t))
    return body * env.clip(0, 1) ** 1.5


def add(dst, src, at, gain=1.0, pan=0.0, circular=False):
    """Equal-power stereo placement; optionally wrap full event tails."""
    start = round(at*SR)
    stereo = src[:, None] * np.array([math.sqrt((1-pan)/2), math.sqrt((1+pan)/2)]) * gain
    if circular:
        # Event duration never exceeds the buffer; split rather than allocating
        # a second large index array or dropping the tail at a loop boundary.
        start %= len(dst)
        cut = min(len(stereo), len(dst)-start)
        dst[start:start+cut] += stereo[:cut]
        if cut < len(stereo):
            dst[:len(stereo)-cut] += stereo[cut:]
    elif start < len(dst):
        dst[start:min(start+len(stereo), len(dst))] += stereo[:len(dst)-start]


def reverb(signal, circular=False, wet=.16):
    # Modest stereo early reflections preserve a clean, close interface sound.
    result = signal.copy()
    for delay, gain, swap in ((.071, .55, True), (.113, .42, False),
                              (.181, .29, True), (.277, .18, False),
                              (.421, .10, True)):
        shift = round(delay*SR)
        source = signal[:, ::-1] if swap else signal
        if circular:
            result += np.roll(source, shift, axis=0) * gain * wet
        else:
            result[shift:] += source[:-shift] * gain * wet
    return result


def noise(duration, low, high, slope=.5):
    """Periodic spectrally shaped air; no unfiltered white-noise playback."""
    count = round(duration*SR)
    freq = np.fft.rfftfreq(count, 1/SR)
    source = RNG.normal(size=len(freq)) + 1j*RNG.normal(size=len(freq))
    # Smooth 4-pole-like band edges prevent ringing.
    band = (1-np.exp(-(freq/max(low, 1))**4)) * np.exp(-(freq/high)**4)
    band *= 1 / np.maximum(freq, low)**slope
    band[0] = 0
    signal = np.fft.irfft(source*band, n=count)
    return signal / (np.std(signal) + 1e-10)


def finish(name, signal, target_rms, peak=.72, loop=False):
    signal = signal.astype(np.float64)
    signal -= signal.mean(axis=0)
    if not loop:
        # Silence at one-shot boundaries: no clicks on event start/stop.
        n = min(round(SR*.012), len(signal)//4)
        signal[:n] *= np.linspace(0, 1, n)[:, None]
        signal[-n:] *= np.linspace(1, 0, n)[:, None]
    # Use a transparent gain first, gentle soft saturation only for outliers.
    signal *= target_rms / (np.sqrt(np.mean(signal**2)) + 1e-10)
    signal = np.tanh(signal/.9)*.9
    signal *= min(1, peak/(np.max(np.abs(signal))+1e-10))
    if loop:
        # Remove a residual sample-edge jump with a tiny cubic endpoint ramp.
        # The timeline is already circular; this only smooths quantization and
        # high-frequency last/first discrepancies over 2 ms at each edge.
        n = round(SR*.002)
        edge = (signal[0] + signal[-1])/2
        weight = np.linspace(0, 1, n)[:, None]
        weight = weight*weight*(3-2*weight)
        signal[:n] = edge + (signal[:n]-edge)*weight
        signal[-n:] = edge + (signal[-n:]-edge)*weight[::-1]
        signal -= signal.mean(axis=0)
    else:
        # Saturation/fades can leave a tiny residual DC bias. Remove it without
        # disturbing silent endpoints or turning a short thump into a click.
        weight = np.ones(len(signal))
        n = min(round(SR*.012), len(signal)//4)
        weight[:n] = np.linspace(0, 1, n)
        weight[-n:] = np.linspace(1, 0, n)
        signal -= weight[:, None] * (signal.mean(axis=0) / weight.mean())
    pcm = np.round(np.clip(signal, -1, 1)*32767).astype('<i2')
    with wave.open(str(OUT / (name+'.wav')), 'wb') as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(pcm.tobytes())
    measured = pcm.astype(float)/32768
    METRICS[name] = {
        'duration_seconds': round(len(signal)/SR, 3),
        'rms_dbfs': round(float(20*np.log10(np.sqrt(np.mean(measured**2))+1e-10)), 2),
        'peak_dbfs': round(float(20*np.log10(np.max(np.abs(measured))+1e-10)), 2),
        'dc_offset': round(float(np.max(np.abs(measured.mean(axis=0)))), 7),
        'edge_jump_dbfs': round(float(20*np.log10(np.max(np.abs(measured[0]-measured[-1]))+1e-10)), 2),
        'loop': loop,
        'clipped_samples': int(np.sum(np.abs(pcm.astype(np.int32)) >= 32767)),
    }
    print(name, json.dumps(METRICS[name]))


def score():
    length = 48.0  # 16 bars at 80 BPM, 4/4.
    dst = np.zeros((round(length*SR), 2))
    # Cmaj9 / Am9 / Fmaj9 / G6sus2. Original voicing and melody.
    progression = [(36, [60, 64, 67, 71, 74]), (33, [57, 60, 64, 67, 71]),
                   (29, [57, 60, 64, 67, 72]), (31, [55, 62, 64, 69, 74])]
    melody = [[76, 74, 71, 67], [71, 72, 76, 74], [72, 76, 79, 76], [74, 71, 69, 67]]
    for bar in range(16):
        bass, chord = progression[bar % 4]
        at = bar*3
        for j, n in enumerate(chord[:4]):
            add(dst, pad(n, 5.5), at, .055, (j-1.5)*.28, True)
        add(dst, key(bass+12, 3.6), at, .16, -.08, True)
        # An intentionally spacious broken chord leaves room for city sounds.
        sequence = [0, 2, 1, 3, 2, 4]
        for j, idx in enumerate(sequence):
            velocity = .13 if j in (0, 3) else .095
            add(dst, key(chord[idx], 2.8), at+j*.375, velocity, (j%3-1)*.31, True)
        if bar >= 4:
            note = melody[bar//4-1][bar%4]
            add(dst, key(note, 3.8), at+.75, .10, .13, True)
            if bar % 4 == 2:
                add(dst, key(note-2, 2.7), at+2.25, .058, -.19, True)
    finish('music_coastal', reverb(dst, True, .36), .105, .58, True)


def coastal():
    duration = 24.0
    t = seconds(duration)
    dst = np.zeros((len(t), 2))
    for channel in range(2):
        swells = .18 + .55*(.5+.5*np.sin(2*np.pi*t/8 + channel*.31))**2
        water = noise(duration, 130, 1950, .48)*swells
        breeze = noise(duration, 65, 650, .7)*(.24+.06*np.sin(2*np.pi*t/12+.7))
        dst[:, channel] = water + breeze
    finish('ambience_coast', dst, .10, .53, True)


def traffic():
    duration = 24.0
    t = seconds(duration)
    dst = np.zeros((len(t), 2))
    for channel in range(2):
        dst[:, channel] = noise(duration, 100, 740, .8)*.033
    for start, span, freq, pan in [(1.6, 6.2, 94, -.3), (8.1, 5.7, 123, .26), (17.2, 6.1, 81, -.1)]:
        et = seconds(span)
        shape = np.sin(np.pi*et/span)**2.8
        hz = freq*(1.07 - .13*(et/span))
        phase = 2*np.pi*np.cumsum(hz)/SR
        engine = (np.sin(phase) + .32*np.sin(phase*2) + .1*np.sin(phase*3))*.17
        tirewash = noise(span, 180, 1250, .6)*.17
        add(dst, (engine+tirewash)*shape, start, 1, pan, True)
    finish('ambience_town', dst, .078, .44, True)


def birds():
    duration = 24.0
    dst = np.zeros((round(SR*duration), 2))
    # Short warbling two/three-part song phrases, separated by quiet space.
    for start, base, pan in [(1.3, 2250, -.62), (5.4, 2680, .4), (10.9, 2020, -.26),
                             (16.2, 2410, .68), (21.1, 2170, .03)]:
        for j in range(3 if start == 10.9 else 2):
            et = seconds(.14 + .03*j)
            pitch = base + 360*np.sin(np.pi*et/et[-1]) + 105*np.sin(2*np.pi*28*et)
            phase = 2*np.pi*np.cumsum(pitch)/SR
            env = np.sin(np.pi*et/et[-1])**1.4
            chirp = (.85*np.sin(phase)+.1*np.sin(phase*2))*env
            add(dst, chirp, start+j*.21, .20*(1-j*.12), pan, True)
    finish('ambience_birds', reverb(dst, True, .12), .026, .47, True)


def night():
    duration = 24.0
    t = seconds(duration)
    dst = np.zeros((len(t), 2))
    # Three distant crickets with restrained band-limited trills; no sharp hiss.
    for phrase, frequency, pan in [(1.0, 3120, -.66), (6.7, 3540, .55), (14.6, 2910, .14), (20.2, 3250, -.35)]:
        for j in range(8):
            et = seconds(.058)
            env = np.sin(np.pi*et/et[-1])**2
            tone = np.sin(2*np.pi*frequency*et+.35*np.sin(2*np.pi*61*et))
            add(dst, tone*env, phrase+j*.13, .14, pan, True)
    for ch in range(2):
        dst[:, ch] += noise(duration, 110, 550, .75)*.014
    finish('ambience_night', dst, .031, .36, True)


def sound_effects():
    # Clean short acoustic/electronic cues with sufficient tail for natural decay.
    specifications = {
        'ui_click': (.16, [(0, 79, .23), (.013, 86, .06)]),
        'zone_paint': (.26, [(0, 72, .19), (.07, 79, .15)]),
        'facility_build': (1.05, [(0, 48, .22), (.04, 60, .20), (.14, 64, .18), (.26, 67, .15)]),
        'denied': (.42, [(0, 57, .18), (.13, 53, .18)]),
        'milestone': (2.3, [(0, 48, .17), (0, 60, .18), (.10, 64, .17), (.20, 67, .16), (.32, 72, .18), (.44, 76, .14)]),
        'save': (.65, [(0, 72, .20), (.10, 79, .16)]),
        'load': (.77, [(0, 67, .16), (.10, 72, .16), (.22, 76, .13)]),
        'grow': (.78, [(0, 64, .16), (.08, 71, .12), (.16, 76, .10)]),
    }
    for name, (duration, notes) in specifications.items():
        dst = np.zeros((round(duration*SR), 2))
        for j, (at, note, gain) in enumerate(notes):
            add(dst, key(note, max(.06, duration-at)), at, gain, (j%3-1)*.14)
        finish(name, reverb(dst, False, .16), .105 if name != 'milestone' else .125, .68)
    # Building a road sounds like a muted compacting roller and a satisfying tap.
    duration = .57
    t = seconds(duration)
    dst = np.zeros((len(t), 2))
    grit = noise(duration, 160, 1700, .35)*np.exp(-t/.09)*envelope(t, .002, .05)
    tap = np.sin(2*np.pi*(76*t+45*.023*(1-np.exp(-t/.023))))*np.exp(-t/.065)
    add(dst, grit*.08+tap*.23, 0, 1, 0)
    add(dst, key(55, .4), .085, .035, .05)
    finish('road_build', dst, .105, .63)
    duration = .79
    t = seconds(duration)
    dst = np.zeros((len(t), 2))
    rubble = noise(duration, 75, 1550, .6)*np.exp(-t/.16)*envelope(t, .006, .09)
    low = np.sin(2*np.pi*(60*t+25*.04*(1-np.exp(-t/.04))))*np.exp(-t/.105)
    add(dst, rubble*.18+low*.22, 0, 1, 0)
    finish('bulldoze', dst, .105, .63)


def write_metrics():
    """Keep generated measurements beside the WAVs; narrative docs are maintained separately."""
    (OUT/'audio_metrics.json').write_text(json.dumps(METRICS, indent=2)+'\n')


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    score()
    coastal()
    traffic()
    birds()
    night()
    sound_effects()
    write_metrics()
