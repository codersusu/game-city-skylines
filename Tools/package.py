#!/usr/bin/env python3
"""Package verified local artifacts without including credentials, caches or personal saves."""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile
import datetime
import plistlib

root = Path(__file__).resolve().parent.parent
builds = root / 'Builds'
subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(builds / 'Seabright.app')], check=True)
audio = json.loads((root / 'Artifacts/audio-playtest.json').read_text())
with (builds / 'Seabright.app/Contents/Info.plist').open('rb') as stream:
    app_info = plistlib.load(stream)
assert audio['passed'] and audio['applicationVersion'] == app_info['CFBundleShortVersionString']
runtime = json.loads((root / 'Artifacts/runtime-playtest.json').read_text())
simulation = json.loads((root / 'Artifacts/simulation-acceptance.json').read_text())
build = json.loads((root / 'Artifacts/build-result.json').read_text())
assert runtime['passed'] and simulation['passed'] and build['errors'] == 0
assert audio['completedUtc'] > build['recordedAtUtc']
subprocess.run(['/usr/bin/ditto', '-c', '-k', '--sequesterRsrc', '--keepParent', str(builds / 'Seabright.app'), str(builds / 'Seabright-macOS.zip')], check=True)
files = []
for directory in ['Assets', 'Packages', 'ProjectSettings', 'Tools', 'Documentation']:
    files.extend(p for p in (root / directory).rglob('*') if p.is_file() and p.name != '.DS_Store' and '__pycache__' not in p.parts and p.suffix not in ('.pyc', '.log'))
for name in ['README.md', 'CHANGELOG.md', 'Play Seabright.command', '.gitignore', 'Artifacts/simulation-acceptance.json', 'Artifacts/runtime-playtest.json', 'Artifacts/build-result.json', 'Artifacts/grown-city-save.json', 'Artifacts/audio-playtest.json', 'Artifacts/audio-preview.wav', 'Artifacts/development-metrics.json']:
    files.append(root / name)
for name in runtime['screenshots'] + ['04-visible-vehicles.png', '05-visible-pedestrians.png', '14-sound-settings.png']:
    files.append(root / 'Artifacts' / name)
with zipfile.ZipFile(builds / 'Seabright-Unity-project.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(set(files)):
        archive.write(path, path.relative_to(root))
record = {
    'recordedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'version': audio['applicationVersion'],
    'runtimeBuildGuid': audio['buildGuid'],
    'priorGrowthBuildGuid': runtime['buildGuid'],
    'note': 'Delivered 0.2.1 standalone passed the targeted audio runtime test, including actual listener PCM capture. The retained 0.2.0 growth/rendering report describes the prior build; economy, growth and rendering algorithms are unchanged by the audio revision. Sound UI and live action feedback were checked in the current build.',
    'simulationChecksPassed': simulation['checksPassed'],
    'priorGrowthRuntimeChecksPassed': len(runtime['checks']),
    'audioChecksPassed': len(audio['checks']),
    'localSignature': {'kind': 'ad-hoc', 'verification': 'passed', 'developerIdNotarized': False},
    'sha256': {},
    'packages': {},
}
for path in files:
    if path.suffix in ('.cs', '.shader') or str(path.relative_to(root)) in ['ProjectSettings/GraphicsSettings.asset', 'Artifacts/build-result.json', 'Artifacts/runtime-playtest.json', 'Artifacts/audio-playtest.json', 'Artifacts/audio-preview.wav', 'Artifacts/simulation-acceptance.json', 'Assets/StreamingAssets/THIRD_PARTY_NOTICES.txt']:
        record['sha256'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
for name in ['Seabright-macOS.zip', 'Seabright-Unity-project.zip']:
    path = builds / name
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
        assert any(n.endswith('THIRD_PARTY_NOTICES.txt') for n in archive.namelist())
        assert not any(Path(n).name in ('env', '.env') for n in archive.namelist())
    record['packages'][name] = {'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'integrity': 'passed', 'licenseNoticesIncluded': True}
(root / 'Artifacts/delivery-manifest.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'packages': record['packages'], 'simulation': record['simulationChecksPassed'], 'priorGrowth': record['priorGrowthRuntimeChecksPassed'], 'audio': record['audioChecksPassed']}, indent=2))
