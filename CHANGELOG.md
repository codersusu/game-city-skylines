# Changelog

Versions describe delivered development stages. They were created locally before the first repository commit; these entries do not imply earlier Git tags or published releases.

## Gameplay trailer — 2026-09-09

- Created an original transparent sea-and-skyline emblem and clean, enlarged opening/closing title cards.
- Produced a 54-second 1080p/30 fps gameplay trailer covering construction, earned growth, towers, a stadium, street activity and night lighting.
- Captured a separate affordable 24-to-793-resident playthrough through the existing simulation, with an explicit timelapse label and recorded population/day counters.
- Mixed the game's own music, ambience and timestamped effects into a stereo trailer soundtrack.
- Added repeatable Unity capture and FFmpeg composition tools, with capture/edit evidence. Normal gameplay remains version 0.2.1.

## Documentation and repository publication — 2026-09-09

- Consolidated the overview and owner prompts in README, with separate game design, implementation/status, changelog and review documents.
- Added five fresh Unity camera captures and retained a standalone sound-panel capture for visual review.
- Recorded development duration, input/cached/output token usage and an explicitly estimated API-price equivalent.
- Added an editor review-capture command using the existing earned-city fixture, avoiding a repeated growth run.
- Kept source assets, licenses, generated measurements and selected test evidence in Git; excluded builds, caches, raw logs, credentials and personal backups.
- Gameplay and player audio remain version 0.2.1.

## 0.2.1 — Sound and music — 2026-09-09

- Replaced the barely audible ambience with original background music and separate coast, town, daytime birds and nighttime layers.
- Added feedback for construction, zoning, facilities, demolition, denied actions, UI, development, milestones and save/load.
- Added camera/context-sensitive ambience, milestone music ducking, rate limits and a bounded eight-voice effect pool.
- Added Sound & Music controls, independent category volumes, persistent preferences, mute shortcut M and a test effect.
- Added targeted listener-output validation and a chronological audio preview; 36/36 checks passed, with no runtime errors, warnings or clipping in the recorded preview.

## 0.2.0 — Grow a city from a small settlement — 2026-09-09

- Changed normal play from a prebuilt showcase city to a 24-resident starter with $30,000 and room to expand.
- Added permanent population milestones, development grants, office districts, residential towers and a functional 3 × 3 coastal stadium.
- Made homes, shops and industry visibly different both as construction sites and developed buildings.
- Added richer scanned surface materials, smooth tree canopies, architectural details, revised water and night lighting.
- Fixed stripped instancing variants so cars and walkers actually render, then verified both visible pixels and movement.
- Replaced displaced/blank toolbar drawings with cached antialiased icon masks; added lock cues and a clear pause banner.
- Fixed roof-based picking/demolition and cancellation of unfinished road strokes when loading a city.
- Added save version 2, migration from version 1 and validation of landmark reservations.
- Passed 15 simulation checks and a 33-check standalone growth scenario, reaching 609 residents and an affordable operating stadium without injected resources.

## 0.1.0 — Initial playable prototype — 2026-09-08 to 2026-09-09

- Created the Unity runtime bootstrap, coastal map, camera, procedural presentation and management interface.
- Implemented connected roads, zoning, growth, basic utilities/services, tax and upkeep, pollution/land value, overlays and local persistence.
- Reused Kenney CC0 models and Liberation Sans; assembled an initial populated showcase district.
- Added initial simulation checks and native macOS build tooling.
- Human feedback exposed major shortcomings in the growth experience, visual distinction, traffic visibility, icon rendering and audio. The following revisions addressed those issues within the demo's scope.
