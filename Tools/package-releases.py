#!/usr/bin/env python3
"""Prepare the verified Mac download and the desktop browser distribution."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import zipfile

root = Path(__file__).resolve().parents[1]
release = root / 'Builds/Release'
release.mkdir(parents=True, exist_ok=True)
web = root / 'Builds/WebGL'
report = json.loads((root / 'Artifacts/web-build-result.json').read_text())
assert report['result'] == 'Succeeded' and report['errors'] == 0
subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(root / 'Builds/Seabright.app')], check=True)
mac = release / 'Seabright-macOS-AppleSilicon.zip'
shutil.copy2(root / 'Builds/Seabright-macOS.zip', mac)
with zipfile.ZipFile(mac) as archive:
    assert archive.testzip() is None
    assert any(name.endswith('Seabright.app/Contents/MacOS/Seabright') for name in archive.namelist())

files = [p for p in web.rglob('*') if p.is_file() and p.name not in ('audio-check.html', '.DS_Store')]
assert (web / 'index.html') in files and any(p.name.endswith('.wasm.unityweb') for p in files)
instructions = '''Seabright 0.2.1 — desktop browser build

Play online: https://codersusu.github.io/game-city-skylines/

To host locally, extract this ZIP, open a terminal in the extracted folder and run:
  python3 -m http.server 8080 --bind 127.0.0.1
Then visit http://127.0.0.1:8080/ in a desktop browser.
Serve over HTTP(S); double-clicking index.html as a file will not load Unity.
No custom compression headers are needed: the Unity loader decompresses its files.

Click Load game, then Enter Seabright to enable sound. Keyboard and mouse are required.
WASD: move. Q/E: orbit. Scroll: zoom. 1: roads. 2: homes. 3: shops. 4: industry.
Space: pause/run. N: day/night. M: mute. Use the game toolbar to Save and Load.
Saves live in this browser's site storage. Clearing site data removes them.
Native Mac saves and browser saves are separate. Mobile/touch play is not supported.

Code, instructions and licenses: https://github.com/codersusu/game-city-skylines
Third-party notices are also in StreamingAssets.
'''
(web / 'README.txt').write_text(instructions)
files = [p for p in web.rglob('*') if p.is_file() and p.name not in ('audio-check.html', '.DS_Store')]
web_zip = release / 'Seabright-WebGL.zip'
with zipfile.ZipFile(web_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(files):
        archive.write(path, path.relative_to(web))
with zipfile.ZipFile(web_zip) as archive:
    assert archive.testzip() is None and 'audio-check.html' not in archive.namelist()

manifest = {'version': '0.2.1', 'mac': 'Previously tested Apple Silicon build from 2026-09-09; ad-hoc signed, not notarized.',
            'web': 'Desktop browser build from 2026-09-14; local load/construction/growth/persistence/audio/mute/night/fullscreen smoke checks.', 'assets': {}}
for path in (mac, web_zip):
    manifest['assets'][path.name] = {'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
(release / 'SHA256SUMS.txt').write_text(''.join(f"{value['sha256']}  {name}\n" for name, value in manifest['assets'].items()))
(root / 'Artifacts/release-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps(manifest, indent=2))
