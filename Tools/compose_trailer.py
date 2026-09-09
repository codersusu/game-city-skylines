#!/usr/bin/env python3
"""Compose Seabright's 54 s trailer from captured gameplay and its own audio assets.

Requires Python with Pillow and NumPy, and FFmpeg with libx264. Run --titles-only
to review title/caption art before gameplay.mp4 exists. No internet or API calls.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import re
import shutil
import subprocess
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
WIDTH, HEIGHT, FPS, DURATION, RATE = 1920, 1080, 30, 54.0, 48000
MIDNIGHT = (7, 30, 37)
IVORY = (242, 238, 224)
MINT = (166, 216, 203)
GOLD = (204, 177, 117)
CHAPTERS = [
    (0.0, 7.0, "SMALL BEGINNINGS", "Lay the foundations of your coastal city."),
    (7.0, 24.0, "ROOM TO GROW", "Homes. Workplaces. A city coming together."),
    (24.0, 32.0, "A SKYLINE OF YOUR OWN", "Make space for a modern city."),
    (32.0, 39.0, "MAKE ROOM FOR SOMETHING BIG", "Landmarks. Neighborhoods. Life on every street."),
    (39.0, 46.0, "A CITY THAT LIVES", "From the first morning to the lights of night."),
]


def run(command: list[str], *, capture: bool = False) -> str:
    result = subprocess.run(command, check=True, text=True,
                            stdout=subprocess.PIPE if capture else None,
                            stderr=subprocess.PIPE if capture else None)
    return (result.stdout or "") + (result.stderr or "")


def font_path(preferred: str | None) -> Path:
    candidates = [preferred, "/System/Library/Fonts/Avenir Next.ttc",
                  "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]
    for name in candidates:
        if name and Path(name).is_file():
            return Path(name)
    raise FileNotFoundError("Pass --font with a TrueType or OpenType font path")


def font(path: Path, size: int, weight: str = "regular") -> ImageFont.FreeTypeFont:
    index = {"regular": 7, "medium": 5, "demi": 2, "bold": 0}[weight]
    return ImageFont.truetype(str(path), size, index=index if path.name == "Avenir Next.ttc" else 0)


def tracked(draw: ImageDraw.ImageDraw, text: str, y: float, face: ImageFont.FreeTypeFont,
            color: tuple[int, ...], spacing: float, center: float = WIDTH / 2,
            left: float | None = None) -> None:
    advances = [draw.textlength(letter, font=face) for letter in text]
    width = sum(advances) + spacing * max(0, len(text) - 1)
    x = center - width / 2 if left is None else left
    for letter, advance in zip(text, advances):
        draw.text((round(x), round(y)), letter, font=face, fill=color, anchor="lt")
        x += advance + spacing


def make_title(path: Path, emblem: Path, face_path: Path, ending: bool) -> None:
    canvas = Image.new("RGB", (WIDTH, HEIGHT), MIDNIGHT)
    mark = Image.open(emblem).convert("RGBA")
    # Layout-only resampling; the original generated brand PNG is never modified.
    mark.thumbnail((354, 354), Image.Resampling.LANCZOS)
    canvas.paste(mark, ((WIDTH - mark.width) // 2, 168), mark)
    draw = ImageDraw.Draw(canvas)
    tracked(draw, "SEABRIGHT", 577, font(face_path, 120, "demi"), IVORY, 18)
    draw.line((WIDTH // 2 - 44, 742, WIDTH // 2 + 44, 742), fill=GOLD, width=2)
    tracked(draw, "PLAYABLE DEMO" if ending else "A CITY IN BALANCE", 789,
            font(face_path, 25, "medium"), MINT, 6)
    canvas.save(path)


def make_caption(path: Path, face_path: Path, title: str, detail: str) -> None:
    # A local, feathered scrim gives readable captions without hiding the city.
    canvas = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    alpha = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    yy = np.arange(HEIGHT, dtype=np.float32)[:, None]
    xx = np.arange(WIDTH, dtype=np.float32)[None, :]
    vertical = np.clip((yy - 744) / 142, 0, 1) * np.clip((1050 - yy) / 100, 0, 1)
    horizontal = np.clip((1410 - xx) / 560, 0, 1)
    alpha[:] = (vertical * horizontal * 166).astype(np.uint8)
    scrim = Image.new("RGBA", (WIDTH, HEIGHT), (*MIDNIGHT, 0))
    scrim.putalpha(Image.fromarray(alpha))
    canvas.alpha_composite(scrim)
    draw = ImageDraw.Draw(canvas)
    draw.line((86, 851, 140, 851), fill=(*GOLD, 255), width=3)
    tracked(draw, title, 881, font(face_path, 37, "demi"), (*IVORY, 255), 2.6, left=86)
    draw.text((87, 940), detail, font=font(face_path, 23), fill=(*MINT, 255), anchor="lt")
    canvas.save(path)


def prepare_art(folder: Path, emblem: Path, face_path: Path) -> dict:
    folder.mkdir(parents=True, exist_ok=True)
    intro, outro = folder / "title-intro.png", folder / "title-outro.png"
    make_title(intro, emblem, face_path, False)
    make_title(outro, emblem, face_path, True)
    captions = []
    for i, (_, _, title, detail) in enumerate(CHAPTERS):
        path = folder / f"caption-{i + 1:02d}.png"
        make_caption(path, face_path, title, detail)
        captions.append(path)
    label = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(label)
    draw.rounded_rectangle((83, 61, 388, 108), radius=4, fill=(*MIDNIGHT, 185))
    tracked(draw, "GAMEPLAY TIMELAPSE", 75, font(face_path, 17, "demi"), (*IVORY, 255), 1.4, left=99)
    timelapse = folder / "timelapse-label.png"
    label.save(timelapse)
    return {"intro": intro, "outro": outro, "captions": captions, "timelapse": timelapse}


def read_wav(path: Path) -> np.ndarray:
    with wave.open(str(path), "rb") as source:
        channels, width, rate, frames = (source.getnchannels(), source.getsampwidth(),
                                         source.getframerate(), source.getnframes())
        if width != 2:
            raise ValueError(f"{path}: expected 16-bit PCM WAV; found {8 * width}-bit")
        samples = np.frombuffer(source.readframes(frames), dtype="<i2").astype(np.float32)
    samples = samples.reshape(-1, channels) / 32768.0
    if channels == 1:
        samples = np.repeat(samples, 2, axis=1)
    elif channels != 2:
        raise ValueError(f"{path}: expected mono/stereo")
    if rate != RATE:
        old = np.arange(len(samples), dtype=np.float64) / rate
        new = np.arange(round(len(samples) * RATE / rate), dtype=np.float64) / RATE
        samples = np.column_stack([np.interp(new, old, samples[:, c]) for c in range(2)]).astype(np.float32)
    return samples


def write_wav(path: Path, samples: np.ndarray) -> None:
    with wave.open(str(path), "wb") as target:
        target.setnchannels(2)
        target.setsampwidth(2)
        target.setframerate(RATE)
        target.writeframes(np.round(np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())


def loop_audio(samples: np.ndarray, count: int, overlap_seconds: float = 1.0) -> np.ndarray:
    """Crossfade successive loops, retaining each original loop's leading section."""
    overlap = min(round(overlap_seconds * RATE), len(samples) // 4)
    stride = len(samples) - overlap
    result = np.zeros((count + len(samples), 2), dtype=np.float32)
    fade = np.linspace(0, 1, overlap, endpoint=False, dtype=np.float32)[:, None]
    for offset in range(0, count, stride):
        part = samples.copy()
        if offset:
            part[:overlap] *= fade
        part[-overlap:] *= 1 - fade
        result[offset:offset + len(part)] += part
    return result[:count]


def load_events(path: Path) -> tuple[list[dict], str]:
    if not path.exists():
        return [], "Capture event file unavailable; music and ambience only, no guessed action sounds."
    raw = json.loads(path.read_text())
    raw = raw.get("events", []) if isinstance(raw, dict) else raw
    aliases = {"Road": "road_build", "Zone": "zone_paint", "Facility": "facility_build",
               "Milestone": "milestone", "Grow": "grow", "Ui": "ui_click",
               "Bulldoze": "bulldoze", "Save": "save", "Load": "load"}
    events = []
    for row in raw:
        if not isinstance(row, dict):
            continue
        name = row.get("cue", row.get("sound", ""))
        name = aliases.get(name, name)
        try:
            at = float(row.get("time", row.get("timeSeconds", -99)))
        except (ValueError, TypeError):
            continue
        if 0 <= at < 46 and re.fullmatch(r"[a-z_]+", str(name)):
            events.append({"time": at + 3.0, "cue": name, "volume": float(row.get("volume", 1.0))})
    return sorted(events, key=lambda event: event["time"]), "Captured gameplay event times offset by the 3-second title."


def make_audio(audio_dir: Path, events_path: Path, output: Path, ffmpeg: str) -> dict:
    count = round(DURATION * RATE)
    time = np.arange(count, dtype=np.float32) / RATE
    events, event_note = load_events(events_path)
    music = loop_audio(read_wav(audio_dir / "music_coastal.wav"), count, 2.0) * 0.8
    duck = np.ones(count, dtype=np.float32)
    sounds = np.zeros((count, 2), dtype=np.float32)
    applied, skipped = [], []
    last_cue: dict[str, float] = {}
    for event in events:
        name, at = event["cue"], event["time"]
        path = audio_dir / (name + ".wav")
        # Dense accelerated growth events should not become a wall of chimes.
        cooldown = 1.8 if name in ("grow", "milestone") else 0.17
        if not path.is_file() or at - last_cue.get(name, -100) < cooldown:
            skipped.append(event)
            continue
        last_cue[name] = at
        sample = read_wav(path)
        start, end = round(at * RATE), min(count, round(at * RATE) + len(sample))
        volume = min(1.1, max(0.0, event["volume"])) * (0.77 if name == "milestone" else 0.83)
        sounds[start:end] += sample[:end - start] * volume
        # Gentle music ducking is driven by actual action cues, with no narration.
        attack, release = 0.06, 0.6
        local = np.maximum(0, np.minimum((time - (at - attack)) / attack,
                                         (at + len(sample) / RATE + release - time) / release))
        duck = np.minimum(duck, 1 - 0.24 * np.minimum(1, local))
        applied.append(event)
    music *= duck[:, None]
    game_gate = np.clip((time - 2.7) / 1.0, 0, 1) * np.clip((50.1 - time) / 1.2, 0, 1)
    night = np.clip((time - 41.8) / 1.8, 0, 1)
    town_growth = 0.25 + 0.75 * np.clip((time - 9) / 19, 0, 1)
    ambience = loop_audio(read_wav(audio_dir / "ambience_coast.wav"), count) * 0.16
    ambience += loop_audio(read_wav(audio_dir / "ambience_town.wav"), count) * (0.38 * town_growth * (1 - 0.5 * night))[:, None]
    ambience += loop_audio(read_wav(audio_dir / "ambience_birds.wav"), count) * (0.54 * (1 - night))[:, None]
    ambience += loop_audio(read_wav(audio_dir / "ambience_night.wav"), count) * (0.66 * night)[:, None]
    mix = music + ambience * game_gate[:, None] + sounds
    envelope = np.clip(time / 0.75, 0, 1) * np.clip((DURATION - time) / 2.1, 0, 1)
    mix *= envelope[:, None]
    peak = float(np.max(np.abs(mix)))
    if peak > 0.89:
        mix *= 0.89 / peak
    raw_path = output.with_name("trailer-mix-premaster.wav")
    write_wav(raw_path, mix)
    run([ffmpeg, "-hide_banner", "-loglevel", "warning", "-y", "-i", str(raw_path),
         "-af", "loudnorm=I=-17:TP=-1.5:LRA=9", "-ar", str(RATE), "-c:a", "pcm_s16le", str(output)])
    master = read_wav(output)
    level = run([ffmpeg, "-hide_banner", "-i", str(output), "-af",
                 "loudnorm=I=-17:TP=-1.5:LRA=9:print_format=json", "-f", "null", "-"], capture=True)
    matches = re.findall(r"\{\s*\"input_i\"[\s\S]*?\}", level)
    loudness = json.loads(matches[-1]) if matches else {}
    return {"source": "Seabright's original in-game WAV assets, mixed for the trailer; no external stock audio",
            "sample_rate": RATE, "channels": 2, "duration_seconds": len(master) / RATE,
            "rms_dbfs": round(20 * math.log10(max(float(np.sqrt(np.mean(master * master))), 1e-12)), 2),
            "sample_peak_dbfs": round(20 * math.log10(max(float(np.max(np.abs(master))), 1e-12)), 2),
            "clipped_samples": int(np.count_nonzero(np.abs(master) >= 0.99997)),
            "integrated_lufs": loudness.get("input_i"), "true_peak_dbtp": loudness.get("input_tp"),
            "events_note": event_note, "events_applied": applied, "events_skipped": skipped,
            "music_ducking": "24% reduction during cues, 60 ms attack / 600 ms release"}


def video_options() -> list[str]:
    return ["-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
            "-r", str(FPS), "-video_track_timescale", "15360", "-an"]


def make_population_counter(ffmpeg: str, events_path: Path, face_path: Path, folder: Path) -> Path | None:
    if not events_path.exists():
        return None
    payload = json.loads(events_path.read_text())
    samples = payload.get("samples", []) if isinstance(payload, dict) else []
    samples = sorted((s for s in samples if "time" in s and "population" in s), key=lambda s: float(s["time"]))
    if not samples:
        return None
    target = folder / "population-counter.mov"
    command = [ffmpeg, "-hide_banner", "-loglevel", "warning", "-y", "-f", "rawvideo", "-pix_fmt", "rgba",
               "-s", "360x102", "-r", "4", "-i", "pipe:0", "-an", "-c:v", "qtrle", "-pix_fmt", "argb", str(target)]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    try:
        for frame in range(17 * 4):
            at = 7 + frame / 4
            previous = [sample for sample in samples if float(sample["time"]) <= at]
            current = previous[-1] if previous else samples[0]
            card = Image.new("RGBA", (360, 102), (0, 0, 0, 0))
            draw = ImageDraw.Draw(card)
            draw.rounded_rectangle((0, 0, 359, 101), radius=5, fill=(*MIDNIGHT, 201))
            draw.text((25, 18), "CITY POPULATION", font=font(face_path, 15, "demi"), fill=(*MINT, 255), anchor="lt")
            draw.text((25, 48), f"{int(current['population']):,}", font=font(face_path, 36, "demi"), fill=(*IVORY, 255), anchor="lt")
            if "day" in current:
                draw.text((334, 64), f"DAY {int(float(current['day']))}", font=font(face_path, 17, "medium"), fill=(*GOLD, 255), anchor="rt")
            process.stdin.write(card.tobytes())
    finally:
        process.stdin.close()
    if process.wait() != 0:
        raise RuntimeError("FFmpeg failed encoding population counter")
    return target


def media_duration(ffmpeg: str, path: Path) -> float:
    inspection = subprocess.run([ffmpeg, "-hide_banner", "-i", str(path)], text=True, capture_output=True)
    match = re.search(r"Duration: (\d+):(\d+):(\d+\.\d+)", inspection.stderr)
    if not match:
        raise RuntimeError(f"Could not inspect media duration: {path}\n{inspection.stderr[-2000:]}")
    return int(match[1]) * 3600 + int(match[2]) * 60 + float(match[3])


def make_video(ffmpeg: str, source: Path, art: dict, audio: Path, output: Path,
               counter: Path | None = None) -> None:
    work = output.parent
    intro, middle, outro = [work / name for name in ("intro-render.mp4", "gameplay-captioned.mp4", "outro-render.mp4")]
    for card, target, seconds, fade_in, fade_out in [(art["intro"], intro, 3, 0.35, 0.45),
                                                   (art["outro"], outro, 5, 0.5, 1.0)]:
        run([ffmpeg, "-hide_banner", "-loglevel", "warning", "-y", "-loop", "1", "-framerate", str(FPS),
             "-i", str(card), "-vf", f"fade=t=in:st=0:d={fade_in}:color=0x071e25,fade=t=out:st={seconds - fade_out}:d={fade_out}:color=0x071e25",
             "-t", str(seconds), *video_options(), str(target)])
    command = [ffmpeg, "-hide_banner", "-loglevel", "warning", "-y", "-i", str(source)]
    for asset in art["captions"] + [art["timelapse"]]:
        command += ["-loop", "1", "-framerate", str(FPS), "-i", str(asset)]
    if counter:
        command += ["-i", str(counter)]
    filters = [f"[0:v]trim=duration=46,setpts=PTS-STARTPTS,fps={FPS},scale={WIDTH}:{HEIGHT}:force_original_aspect_ratio=decrease,pad={WIDTH}:{HEIGHT}:(ow-iw)/2:(oh-ih)/2:color=0x071e25,setsar=1[base]"]
    previous = "base"
    for i, (start, end, _, _) in enumerate(CHAPTERS):
        # The growth label remains throughout, while the feature caption clears.
        begin = start + 0.65
        finish = min(end - 0.45, begin + 5.5)
        filters.append(f"[{i + 1}:v]format=rgba,fade=t=in:st={begin}:d=0.45:alpha=1,fade=t=out:st={finish - 0.45}:d=0.45:alpha=1[c{i}]")
        filters.append(f"[{previous}][c{i}]overlay=0:0:enable='between(t,{begin},{finish})':eof_action=pass[v{i}]")
        previous = f"v{i}"
    filters += ["[6:v]format=rgba,fade=t=in:st=7:d=0.35:alpha=1,fade=t=out:st=23.65:d=0.35:alpha=1[tl]",
                f"[{previous}][tl]overlay=0:0:enable='between(t,7,24)':eof_action=pass[labels]"]
    previous = "labels"
    if counter:
        filters += ["[7:v]format=rgba,fade=t=in:st=0:d=0.35:alpha=1,fade=t=out:st=16.65:d=0.35:alpha=1,setpts=PTS-STARTPTS+7/TB[count]",
                    "[labels][count]overlay=x=1477:y=61:enable='between(t,7,24)':eof_action=pass[counted]"]
        previous = "counted"
    filters += [f"[{previous}]fade=t=in:st=0:d=0.45:color=0x071e25,fade=t=out:st=45.55:d=0.45:color=0x071e25[final]"]
    command += ["-filter_complex", ";".join(filters), "-map", "[final]", "-t", "46", *video_options(), str(middle)]
    run(command)
    concat = work / "trailer-concat.txt"
    # Filenames are constants relative to this manifest, avoiding path escaping.
    concat.write_text("file 'intro-render.mp4'\nfile 'gameplay-captioned.mp4'\nfile 'outro-render.mp4'\n")
    run([ffmpeg, "-hide_banner", "-loglevel", "warning", "-y", "-f", "concat", "-safe", "1", "-i", str(concat),
         "-i", str(audio), "-map", "0:v:0", "-map", "1:a:0", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
         "-ar", str(RATE), "-t", str(DURATION), "-movflags", "+faststart", "-metadata", "title=Seabright — A City in Balance",
         "-metadata", "comment=Actual Seabright demo gameplay; growth sequence accelerated. Audio uses the game's original music, ambience and effects.", str(output)])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    parser.add_argument("--gameplay", type=Path, default=ROOT / "Artifacts/Trailer/gameplay.mp4")
    parser.add_argument("--events", type=Path, default=ROOT / "Artifacts/Trailer/capture-events.json")
    parser.add_argument("--output", type=Path, default=ROOT / "Artifacts/Trailer/Seabright-trailer.mp4")
    parser.add_argument("--font")
    parser.add_argument("--titles-only", action="store_true")
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    face_path = font_path(args.font)
    art = prepare_art(ROOT / "Media/Trailer", ROOT / "Media/Brand/Seabright-emblem.png", face_path)
    poster = args.output.with_name("Seabright-trailer-poster.png")
    shutil.copy2(art["intro"], poster)
    if args.titles_only:
        print(json.dumps({"title": str(art["intro"]), "poster": str(poster), "font": str(face_path)}, indent=2))
        return
    if not args.gameplay.is_file():
        raise FileNotFoundError(f"Capture gameplay first: {args.gameplay}")
    source_duration = media_duration(args.ffmpeg, args.gameplay)
    if not 45.9 <= source_duration <= 46.1:
        raise ValueError(f"Expected 46 seconds of gameplay, received {source_duration:.2f}")
    audio_path = args.output.with_name("trailer-audio.wav")
    audio_report = make_audio(ROOT / "Assets/Resources/Audio", args.events, audio_path, args.ffmpeg)
    counter = make_population_counter(args.ffmpeg, args.events, face_path, ROOT / "Media/Trailer")
    make_video(args.ffmpeg, args.gameplay, art, audio_path, args.output, counter)
    actual_duration = media_duration(args.ffmpeg, args.output)
    if abs(actual_duration - DURATION) > 0.06:
        raise ValueError(f"Trailer duration mismatch: {actual_duration}")
    report = {"output": str(args.output), "poster": str(poster), "duration_seconds": actual_duration,
              "resolution": [WIDTH, HEIGHT], "fps": FPS, "encoding": "H.264 CRF 18 / yuv420p, AAC 192 kbps / 48 kHz stereo, faststart",
              "font": str(face_path), "emblem": "Media/Brand/Seabright-emblem.png",
              "source_gameplay": str(args.gameplay), "gameplay_duration_seconds": source_duration,
              "population_counter": "Recorded simulation samples, shown without interpolating population" if counter else "Unavailable",
              "title_seconds": 3, "closing_seconds": 5,
              "chapters": [{"trailer_start": s + 3, "trailer_end": e + 3, "title": title} for s, e, title, _ in CHAPTERS],
              "audio": audio_report}
    args.output.with_name("trailer-metadata.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
