using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Seabright
{
    /// <summary>
    /// Targeted standalone audio acceptance. Live construction and save/load paths are
    /// exercised; full city growth remains the separate CityRuntimePlaytest scenario.
    /// </summary>
    public sealed class CityAudioPlaytest : MonoBehaviour
    {
        CityGame game;
        CityAudio sound;
        AudioOutputProbe probe;
        Report report;
        string seed, artifactDirectory;
        int seedRoads, seedZones;
        float started, captureStarted;
        readonly float[] output = new float[2048];
        readonly List<Check> checks = new List<Check>();
        readonly List<Measurement> measurements = new List<Measurement>();
        readonly List<string> errors = new List<string>();
        int warnings;

        void Awake() { Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) warnings++;
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 32)
                errors.Add(type + ": " + message + "\n" + stack);
        }

        IEnumerator Start()
        {
            game = CityGame.Instance;
            started = Time.realtimeSinceStartup;
            artifactDirectory = game != null ? game.ArtifactDirectory : Path.Combine(Application.persistentDataPath, "AudioTest");
            report = new Report {
                startedUtc = DateTime.UtcNow.ToString("O"), applicationVersion = Application.version,
                buildGuid = Application.buildGUID, unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(), operatingSystem = SystemInfo.operatingSystem,
                sampleRate = AudioSettings.outputSampleRate, isEditor = Application.isEditor,
                scope = "Audio-only standalone acceptance: real listener output, 12-second listener PCM recording, live world pointer handler, save/load, volume and ambience behavior. No repeated city-growth benchmark. Direct cue smoke tests establish playback only, not new milestone/growth gameplay.",
                inputScope = "Calls the same CityGame.ProcessPointer used by live mouse down/held/up. The sound panel opens via its public UI method; this does not synthesize operating-system clicks or slider drags.",
                measurementScope = "AudioListener.GetOutputData recent 2048-sample channel-0 windows after warmup; RMS is a sampled output estimate. The WAV is chronological listener OnAudioFilterRead PCM, written without normalization. No microphone or external loopback device.",
                savePath = game != null ? game.SavePath : ""
            };
            var routines = new Stack<IEnumerator>(); routines.Push(Run());
            while (routines.Count > 0)
            {
                bool more = false; object instruction = null; Exception failure = null;
                try { IEnumerator current = routines.Peek(); more = current.MoveNext(); if (more) instruction = current.Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { Add("audio harness completed without exception", false, failure.ToString()); break; }
                if (!more) { routines.Pop(); continue; }
                if (instruction is IEnumerator nested && !(instruction is CustomYieldInstruction)) { routines.Push(nested); continue; }
                yield return instruction;
            }
            try
            {
                if (game != null && seed != null)
                {
                    game.Speed = 0; game.Sim.LoadJson(seed); game.RoadsBuilt = seedRoads; game.ZonesPainted = seedZones;
                    game.HUD.CloseModal(); game.Help = game.Budget = game.Photo = false;
                    game.SetTool(TileKind.Empty); game.Selected = game.Hover = new Vector2Int(-1, -1);
                    if (game.Night) game.ToggleNight(); game.Rig.ResetView();
                    sound.ResetCitySnapshot(); sound.RestoreDefaults();
                    Add("test leaves seeded city intact and uses an isolated save", game.Sim.SaveJson() == seed &&
                        Path.GetFullPath(game.SavePath).StartsWith(Path.GetFullPath(artifactDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                        "Restored the full initial city JSON; the test save stays inside Artifacts and audio preference persistence is disabled by -audioTest.");
                }
            }
            catch (Exception ex) { Add("restore audio test state", false, ex.ToString()); }
            yield return null;
            report.runtimeErrors = errors.ToArray(); report.warningCount = warnings;
            Add("no runtime errors, exceptions or assertions", errors.Count == 0, errors.Count + " errors; " + warnings + " warnings.");
            report.checks = checks.ToArray(); report.measurements = measurements.ToArray();
            report.passed = checks.Count > 0 && checks.TrueForAll(c => c.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O"); report.durationSeconds = Time.realtimeSinceStartup - started;
            try { Directory.CreateDirectory(artifactDirectory); File.WriteAllText(Path.Combine(artifactDirectory, "audio-playtest.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception ex) { report.passed = false; Debug.LogError("SEABRIGHT_AUDIO_REPORT_FAILED " + ex); }
            Debug.Log((report.passed ? "SEABRIGHT_AUDIO_COMPLETE " : "SEABRIGHT_AUDIO_FAILED ") + artifactDirectory);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-quitAfterAudioTest") >= 0) Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Run()
        {
            if (game == null || game.Audio == null || game.Cam == null || game.HUD == null)
                throw new InvalidOperationException("City/audio initialization was incomplete.");
            Directory.CreateDirectory(artifactDirectory);
            sound = game.Audio; seed = game.Sim.SaveJson(); seedRoads = game.RoadsBuilt; seedZones = game.ZonesPainted;
            game.Speed = 0; game.Help = game.Budget = game.Photo = false; game.HUD.CloseModal();
            game.Selected = new Vector2Int(-1, -1); game.SetTool(TileKind.Empty);
            if (game.Night) game.ToggleNight();
            sound.RestoreDefaults(); sound.ResetCitySnapshot();
            probe = game.Cam.gameObject.AddComponent<AudioOutputProbe>(); probe.Begin(12); captureStarted = Time.realtimeSinceStartup;
            // GetOutputData allocates its recent-sample history on the first call.
            AudioListener.GetOutputData(output, 0);
            yield return new WaitForSecondsRealtime(2);
            Add("all soundtrack, ambience and effects assets load", sound.Ready && sound.LoadedClipCount == 15 && sound.LoopsPlaying,
                sound.LoadedClipCount + " clips; all five looping sources playing=" + sound.LoopsPlaying + ".");
            Add("default sound controls are audible and unmuted", !sound.Muted && Approximately(sound.Master, .85f) &&
                Approximately(sound.Music, .48f) && Approximately(sound.Ambience, .8f) && Approximately(sound.Effects, .8f),
                "Master=" + sound.Master + "; music=" + sound.Music + "; ambience=" + sound.Ambience + "; effects=" + sound.Effects + ".");
            yield return Measure("default soundtrack and coastal ambience", .7f, m => Add("actual listener mix is audible without clipping", m.rms > .01f && m.peak < .99f, Describe(m)));

            yield return FrameAt(17, 24, 180);
            int count = sound.PlayedCount(CityAudio.Cue.Road);
            yield return Road(15, 24, 18, 24);
            Add("road construction emits its effect through live pointer input", game.Sim.Get(18, 24).Kind == TileKind.Road && sound.PlayedCount(CityAudio.Cue.Road) == count + 1,
                "Extended the starter road by three purchased cells through down/held/up input.");
            yield return new WaitForSecondsRealtime(.45f);
            count = sound.PlayedCount(CityAudio.Cue.Road); float funds = game.Sim.Money;
            yield return Road(15, 24, 18, 24);
            Add("a repeated road stroke does not emit a false build effect", game.Sim.Money == funds && sound.PlayedCount(CityAudio.Cue.Road) == count,
                "Same existing road, unchanged treasury and unchanged Road cue count.");

            count = sound.PlayedCount(CityAudio.Cue.Zone);
            yield return Paint(TileKind.Residential, 16, 23);
            Add("zoning emits its effect through live pointer input", game.Sim.Get(16, 23).Kind == TileKind.Residential && sound.PlayedCount(CityAudio.Cue.Zone) == count + 1,
                "Residential designation placed beside the new road.");
            yield return new WaitForSecondsRealtime(.45f);
            count = sound.PlayedCount(CityAudio.Cue.Zone); funds = game.Sim.Money;
            yield return Paint(TileKind.Residential, 16, 23);
            Add("repainting the same zone is silent", game.Sim.Money == funds && sound.PlayedCount(CityAudio.Cue.Zone) == count,
                "Unchanged zone designation, treasury and Zone cue count.");
            count = sound.PlayedCount(CityAudio.Cue.Facility);
            yield return Paint(TileKind.Park, 17, 25);
            Add("facility placement emits its effect", game.Sim.Get(17, 25).Kind == TileKind.Park && sound.PlayedCount(CityAudio.Cue.Facility) == count + 1,
                "Purchased a park using live world pointer input.");
            yield return new WaitForSecondsRealtime(.45f);
            game.SetTool(TileKind.Empty); game.Demolish = true;
            count = sound.PlayedCount(CityAudio.Cue.Bulldoze);
            Click(VisiblePoint(17, 25)); yield return null;
            Add("demolition emits its effect", game.Sim.Get(17, 25).Kind == TileKind.Empty && sound.PlayedCount(CityAudio.Cue.Bulldoze) == count + 1,
                "The placed park was demolished through the same live pointer path.");
            yield return new WaitForSecondsRealtime(.45f);
            count = sound.PlayedCount(CityAudio.Cue.Bulldoze);
            Click(VisiblePoint(17, 25));
            Add("bulldozing empty land is silent", sound.PlayedCount(CityAudio.Cue.Bulldoze) == count, "No extra demolition cue for an empty site.");
            game.SetTool(TileKind.Empty);
            count = sound.PlayedCount(CityAudio.Cue.Denied); game.SetTool(TileKind.Office);
            Add("locked facilities provide an audible denial", game.Tool != TileKind.Office && sound.PlayedCount(CityAudio.Cue.Denied) == count + 1,
                "Selecting locked offices triggers the live denial path.");
            count = sound.PlayedCount(CityAudio.Cue.Save); bool saved = game.Save();
            Add("save confirmation emits its effect", saved && sound.PlayedCount(CityAudio.Cue.Save) == count + 1, "Saved only to " + game.SavePath);
            yield return new WaitForSecondsRealtime(.4f);
            count = sound.PlayedCount(CityAudio.Cue.Load); int milestoneCount = sound.PlayedCount(CityAudio.Cue.Milestone);
            string savedCity = game.Sim.SaveJson(); game.Load(); yield return null;
            Add("load confirmation emits its effect without a milestone replay", game.Sim.SaveJson() == savedCity &&
                sound.PlayedCount(CityAudio.Cue.Load) == count + 1 && sound.PlayedCount(CityAudio.Cue.Milestone) == milestoneCount,
                "Loaded the same paused city; no extra milestone chime.");

            yield return FrameAt(17, 24, 180);
            game.SetTool(TileKind.Residential); Vector3 blockedPoint = VisiblePoint(18, 23);
            string before = game.Sim.SaveJson(); game.HUD.OpenSoundSettings();
            Click(blockedPoint); yield return new WaitForEndOfFrame();
            Add("sound settings block construction underneath the panel", game.HUD.ModalOpen && before == game.Sim.SaveJson() && game.Hover.x < 0,
                "A valid empty residential site stayed unchanged while the sound modal was open.");
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(artifactDirectory, "14-sound-settings.png"), screenshot.EncodeToPNG()); Destroy(screenshot);
            report.settingsScreenshot = "14-sound-settings.png";
            game.HUD.CloseModal(); game.SetTool(TileKind.Empty);
            while (Time.realtimeSinceStartup - captureStarted < 12.15f) yield return null;
            report.preview = probe.SaveWav(Path.Combine(artifactDirectory, "audio-preview.wav"));
            Add("listener captured a chronological unclipped gameplay preview", report.preview.durationSeconds >= 11.5f && report.preview.rms > .0001f &&
                report.preview.clippedSamples == 0 && report.preview.nonFiniteSamples == 0,
                report.preview.durationSeconds.ToString("F2") + "s; " + report.preview.channels + " channels; " + report.preview.sampleRate + "Hz; RMS=" + report.preview.rms.ToString("F6") + "; peak=" + report.preview.peak.ToString("F6") + ".");

            sound.SetMuted(true); yield return new WaitForSecondsRealtime(.35f);
            yield return Measure("muted master output", .35f, m => Add("mute silences actual listener output", sound.Muted && m.peak < .00001f, Describe(m)));
            sound.SetMuted(false); yield return new WaitForSecondsRealtime(.25f);
            yield return Measure("unmuted master output", .35f, m => Add("unmute restores actual audio output", !sound.Muted && Audible(m), Describe(m)));
            sound.SetVolumes(.85f, 0, 0, 0); StopOneShots(); yield return new WaitForSecondsRealtime(1.1f);
            yield return Measure("all three buses at zero", .35f, m => Add("music ambience and effect levels reach silence independently of mute", !sound.Muted && m.peak < .00003f, Describe(m)));
            sound.SetVolumes(.85f, .48f, 0, 0); yield return new WaitForSecondsRealtime(.7f);
            yield return Measure("music only", .4f, m => Add("music bus produces audible background soundtrack", Audible(m), Describe(m)));
            sound.SetVolumes(.85f, 0, .8f, 0); yield return new WaitForSecondsRealtime(.7f);
            yield return Measure("paused daytime ambience only", .5f, m => Add("natural ambience continues while the city is paused", game.Speed == 0 && sound.LoopsPlaying && Audible(m), Describe(m)));
            float dayBird = sound.LoopVolume(3), dayNight = sound.LoopVolume(4);
            game.ToggleNight(); yield return new WaitForSecondsRealtime(1.5f);
            Add("night crossfades birds into nocturnal ambience", sound.LoopVolume(3) < dayBird - .01f && sound.LoopVolume(4) > dayNight + .01f,
                "Bird gain " + dayBird.ToString("F3") + " to " + sound.LoopVolume(3).ToString("F3") + "; night gain " + dayNight.ToString("F3") + " to " + sound.LoopVolume(4).ToString("F3") + ".");
            AudioSource[] sources = sound.GetComponentsInChildren<AudioSource>();
            var originalPitches = new float[sources.Length];
            for (int i = 0; i < sources.Length; i++) originalPitches[i] = sources[i].pitch;
            game.Speed = 3; yield return null;
            bool pitchesUnchanged = true; int loops = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                pitchesUnchanged &= Approximately(sources[i].pitch, originalPitches[i]);
                if (sources[i].loop) { loops++; pitchesUnchanged &= Approximately(sources[i].pitch, 1); }
            }
            Add("3x simulation speed does not pitch-shift music or ambience", pitchesUnchanged && loops == 5,
                loops + " loops retain pitch 1; every source keeps its previous pitch. Per-effect artistic pitch variation is independent of simulation speed.");
            game.Speed = 0; game.ToggleNight();

            sound.SetVolumes(.85f, 0, 0, .8f); StopOneShots(); yield return new WaitForSecondsRealtime(1.1f);
            foreach (CityAudio.Cue cue in Enum.GetValues(typeof(CityAudio.Cue)))
            {
                // Stop previous one-shot tails to make each measurement attributable
                // to this clip. Loop sources stay alive at zero bus volume.
                StopOneShots(); yield return new WaitForSecondsRealtime(.12f); bool dispatched = sound.Play(cue);
                yield return Measure("isolated effect " + cue, .45f, m => Add("effect playback reaches listener: " + cue, dispatched && Audible(m),
                    "Direct sound-dispatch smoke test; " + Describe(m)));
            }
            yield return new WaitForSecondsRealtime(.4f);
            count = sound.PlayedCount(CityAudio.Cue.Zone);
            bool first = sound.Play(CityAudio.Cue.Zone), second = sound.Play(CityAudio.Cue.Zone);
            Add("rapid repeated effects are throttled", first && !second && sound.PlayedCount(CityAudio.Cue.Zone) == count + 1,
                "Two zone cue requests in one frame produce one voice.");
            sources = sound.GetComponentsInChildren<AudioSource>();
            Add("audio source allocation remains bounded", sources.Length == 13, sources.Length + " sources after gameplay and all cues; expected five loops plus an eight-voice pool.");
        }

        IEnumerator Measure(string name, float seconds, Action<Measurement> finished)
        {
            var measurement = new Measurement { name = name };
            double power = 0; long samples = 0; float begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < seconds)
            {
                yield return null;
                AudioListener.GetOutputData(output, 0);
                foreach (float value in output) { power += value * (double)value; measurement.peak = Mathf.Max(measurement.peak, Mathf.Abs(value)); }
                samples += output.Length; measurement.windows++;
            }
            measurement.durationSeconds = Time.realtimeSinceStartup - begin;
            measurement.rms = samples > 0 ? (float)Math.Sqrt(power / samples) : 0;
            measurements.Add(measurement); finished(measurement);
        }
        void StopOneShots() { foreach (AudioSource source in sound.GetComponentsInChildren<AudioSource>()) if (!source.loop) source.Stop(); }
        static bool Audible(Measurement value) { return value.rms > .003f && value.peak > .006f; }
        static bool Approximately(float a, float b) { return Mathf.Abs(a - b) < .002f; }
        static string Describe(Measurement value) { return "RMS=" + value.rms.ToString("F6") + "; peak=" + value.peak.ToString("F6") + "; " + value.windows + " output windows over " + value.durationSeconds.ToString("F2") + "s."; }
        void Add(string name, bool passed, string detail) { checks.Add(new Check { name = name, passed = passed, detail = detail }); }
        IEnumerator FrameAt(int x, int z, float distance)
        {
            game.Rig.Focus = CitySimulation.World(x, z); game.Rig.Distance = distance; game.Rig.Yaw = -28; game.Rig.Pitch = 48;
            yield return new WaitForSecondsRealtime(.65f);
        }
        Vector3 VisiblePoint(int x, int z)
        {
            Vector3 point = game.Cam.WorldToScreenPoint(CitySimulation.World(x, z) + Vector3.up * .05f);
            if (point.z <= 0 || point.x < 0 || point.y < 0 || point.x >= Screen.width || point.y >= Screen.height || game.HUD.PointerOverUI(point))
                throw new InvalidOperationException("Audio construction fixture is outside unobscured world: " + x + "," + z);
            return point;
        }
        void Click(Vector3 point) { game.ProcessPointer(point, true, true, false); game.ProcessPointer(point, false, false, true); }
        IEnumerator Road(int x0, int z0, int x1, int z1)
        {
            game.SetTool(TileKind.Road);
            Vector3 a = VisiblePoint(x0, z0), b = VisiblePoint(x1, z1);
            game.ProcessPointer(a, true, true, false); yield return null;
            game.ProcessPointer(b, false, true, false); yield return null;
            game.ProcessPointer(b, false, false, true); yield return null;
        }
        IEnumerator Paint(TileKind kind, int x, int z) { game.SetTool(kind); Click(VisiblePoint(x, z)); yield return null; }

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Measurement { public string name; public int windows; public float rms, peak, durationSeconds; }
        [Serializable] sealed class Report
        {
            public bool passed, isEditor;
            public string startedUtc, completedUtc, applicationVersion, buildGuid, unityVersion, platform, operatingSystem,
                scope, inputScope, measurementScope, savePath, settingsScreenshot;
            public int sampleRate, warningCount;
            public float durationSeconds;
            public AudioOutputProbe.Capture preview;
            public Check[] checks;
            public Measurement[] measurements;
            public string[] runtimeErrors;
        }
    }
}
