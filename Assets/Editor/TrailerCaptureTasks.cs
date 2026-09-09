using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Seabright.Editor
{
    /// <summary>Reproducible cinematic capture. Purchases and growth use the unmodified game simulation.</summary>
    [InitializeOnLoad]
    public static class TrailerCaptureTasks
    {
        const string Active = "Seabright.TrailerCapture.Active";
        static TrailerCaptureTasks() { EditorApplication.update += StartRunner; }
        [MenuItem("Seabright/Capture Trailer")]
        public static void Capture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before capture.");
            Directory.CreateDirectory("Artifacts/Trailer");
            EditorSceneManager.OpenScene("Assets/Scenes/Seabright.unity");
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }
        static void StartRunner()
        {
            var game = CityGame.Instance;
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || !game || game.Sim == null || !game.Audio || !game.Audio.Ready) return;
            SessionState.SetBool(Active, false);
            game.gameObject.AddComponent<TrailerCaptureRunner>();
        }
    }

    public sealed class TrailerCaptureRunner : MonoBehaviour
    {
        const int Width = 1920, Height = 1080, Fps = 30, Frames = 46 * Fps;
        const string DirectoryPath = "Artifacts/Trailer";
        CityGame game;
        Process encoder;
        RenderTexture target;
        Texture2D pixels;
        readonly List<ScheduledBuild> scheduled = new List<ScheduledBuild>();
        readonly List<CaptureEvent> events = new List<CaptureEvent>();
        readonly List<Sample> samples = new List<Sample>();
        float time, lowestTreasury;
        bool officePlanned, towersPlanned, preview;
        int previousMilestone, nextBuild;
        string encoderLog = "";

        IEnumerator Start()
        {
            game = CityGame.Instance;
            IEnumerator work = Capture();
            Exception error = null;
            while (true) {
                bool more = false; object step = null;
                try { more = work.MoveNext(); if (more) step = work.Current; }
                catch (Exception ex) { error = ex; }
                if (error != null || !more) break;
                yield return step;
            }
            try { Finish(error); } catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        IEnumerator Capture()
        {
            preview = Array.IndexOf(Environment.GetCommandLineArgs(), "-trailerPreview") >= 0;
            game.enabled = false; game.Rig.enabled = false; game.HUD.enabled = false;
            game.Audio.enabled = false; game.Speed = 0; game.Photo = true;
            game.Help = game.Budget = false; game.HUD.CloseModal(); AudioListener.volume = 0;
            game.Cam.enabled = false; game.Cam.aspect = Width / (float)Height;
            game.Selected = game.Hover = new Vector2Int(-1, -1); game.SetTool(TileKind.Empty); game.SetOverlay(0);
            if (game.Night) game.ToggleNight();
            game.Sim.SeedStarterTown(); game.View.Refresh(); lowestTreasury = game.Sim.Money;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; Time.captureFramerate = Fps;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            target.Create(); pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            if (!preview) StartEncoder();
            PlanStarter();
            yield return null;
            for (int frame = 0; frame < Frames; frame++) {
                time = frame / (float)Fps;
                // This is an accelerated playthrough, never a loaded skyline or injected treasury.
                while (nextBuild < scheduled.Count && scheduled[nextBuild].Time <= time) { Execute(scheduled[nextBuild]); nextBuild++; }
                if (time >= 7 && time < 24) game.Sim.Tick(2.7f / Fps);
                else if (time < 7 || time >= 33) game.Sim.Tick(.12f / Fps);
                PlanUnlockedNeighborhoods();
                int milestone = game.Sim.PeakPopulation >= 600 ? 600 : game.Sim.PeakPopulation >= 350 ? 350 : game.Sim.PeakPopulation >= 150 ? 150 : 0;
                if (milestone > previousMilestone) { previousMilestone = milestone; Event("milestone", "Unlocked at " + milestone + " residents"); }
                lowestTreasury = Mathf.Min(lowestTreasury, game.Sim.Money);
                if (frame == 24 * Fps && !game.Sim.IsUnlocked(TileKind.Stadium)) throw new InvalidOperationException("Growth sequence did not earn the stadium unlock by the skyline shot.");
                if (frame == 33 * Fps) Execute(new ScheduledBuild { Kind = TileKind.Stadium, X = 22, Z = 32 });
                if (frame == 39 * Fps) { game.ToggleNight(); Event("ui_click", "Night view"); }
                game.View.Refresh();
                CameraPath(time);
                // Allow the previous architecture batch to be destroyed before rendering the replacement.
                yield return null;
                game.View.UpdateTraffic(1f / Fps);
                bool checkpoint = frame % Fps == 0;
                if (!preview || checkpoint) RenderFrame(frame, checkpoint);
                if (checkpoint) {
                    samples.Add(new Sample { time = time, population = game.Sim.Population, day = game.Sim.Day, treasury = game.Sim.Money, cars = game.View.TrafficCount, pedestrians = game.View.PedestrianCount });
                    if (frame % (5 * Fps) == 0) Debug.Log("SEABRIGHT_TRAILER_FRAME " + frame + "/" + Frames + " population=" + game.Sim.Population + " day=" + game.Sim.Day);
                }
            }
            File.WriteAllText(DirectoryPath + "/trailer-city-save.json", game.Sim.SaveJson());
        }

        void PlanStarter()
        {
            // The opening shows actual affordable road segments and the three distinct zone types.
            for (int x = 16; x <= 20; x++) Add(1.5f + (x - 16) * .30f, TileKind.Road, x, 24);
            Add(3.3f, TileKind.Residential, 16, 23); Add(4.0f, TileKind.Commercial, 17, 23); Add(4.7f, TileKind.Industrial, 10, 26);
            Road(7.0f, 15, 18, 15, 30); Road(7.4f, 15, 18, 26, 18); Road(7.8f, 20, 24, 26, 24);
            Road(8.2f, 15, 30, 26, 30); Road(8.6f, 26, 18, 26, 30);
            Add(8.9f, TileKind.Power, 22, 25); Add(9.1f, TileKind.Water, 22, 23);
            Add(9.3f, TileKind.Park, 20, 22); Add(9.5f, TileKind.Park, 20, 27);
            int n = 0; for (int x = 16; x <= 21; x++) for (int z = 19; z <= 20; z++) Add(9.7f + n++ * .055f, TileKind.Residential, x, z);
            scheduled.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        void PlanUnlockedNeighborhoods()
        {
            if (!officePlanned && game.Sim.IsUnlocked(TileKind.Office) && time > 10.4f) {
                officePlanned = true;
                Add(time + .1f, TileKind.Office, 16, 23); Add(time + .2f, TileKind.Office, 17, 23);
                int n = 0;
                for (int x = 9; x <= 14; x++) if (game.Sim.Get(x, 26).Kind == TileKind.Empty) Add(time + .35f + n++ * .045f, TileKind.Industrial, x, 26);
                for (int x = 16; x <= 21; x++) for (int z = 28; z <= 29; z++) Add(time + .7f + n++ * .045f, TileKind.Residential, x, z);
            }
            if (!towersPlanned && game.Sim.IsUnlocked(TileKind.HighResidential)) {
                towersPlanned = true;
                for (int x = 22; x <= 25; x++) Add(time + .1f + (x - 22) * .2f, TileKind.HighResidential, x, 19);
                Add(time + .95f, TileKind.Office, 18, 23);
            }
        }
        void Add(float at, TileKind kind, int x, int z) { scheduled.Add(new ScheduledBuild { Time = at, Kind = kind, X = x, Z = z }); }
        void Road(float at, int x, int z, int toX, int toZ) { scheduled.Add(new ScheduledBuild { Time = at, Kind = TileKind.Road, X = x, Z = z, ToX = toX, ToZ = toZ, Drag = true }); }
        void Execute(ScheduledBuild build)
        {
            string reason;
            bool ok = build.Drag ? game.Sim.BuildRoad(build.X, build.Z, build.ToX, build.ToZ, out reason) : game.Sim.Build(build.X, build.Z, build.Kind, out reason);
            if (!ok) throw new InvalidOperationException("Trailer purchase failed: " + build.Kind + " " + build.X + "," + build.Z + ": " + reason);
            string cue = build.Kind == TileKind.Road ? "road_build" : CitySimulation.IsZone(build.Kind) ? "zone_paint" : "facility_build";
            Event(cue, build.Kind + " at " + build.X + "," + build.Z);
            game.View.ShowHover(build.X, build.Z, CityHUD.Teal, time < 7 || build.Kind == TileKind.Stadium, build.Kind == TileKind.Stadium ? 3 : 1);
        }
        void Event(string cue, string detail) { events.Add(new CaptureEvent { time = time, cue = cue, detail = detail, population = game.Sim.Population, day = game.Sim.Day }); }

        static float Ease(float value) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01(value)); }
        void CameraPath(float t)
        {
            Vector3 focus; float distance, yaw, pitch;
            if (t < 7) {
                float a = Ease(t / 7); focus = Vector3.Lerp(CitySimulation.World(12, 24), CitySimulation.World(15, 24), a);
                distance = Mathf.Lerp(175, 162, a); yaw = Mathf.Lerp(-32, -25, a); pitch = 48;
            } else if (t < 24) {
                float a = Ease((t - 7) / 4); focus = Vector3.Lerp(CitySimulation.World(15, 24), CitySimulation.World(20, 24) + Vector3.up * 12, a);
                distance = Mathf.Lerp(162, 285, a); yaw = -25; pitch = 48;
                game.View.ShowHover(-1, -1, Color.clear, false);
            } else if (t < 32) {
                float a = Ease((t - 24) / 8); focus = CitySimulation.World(21, 22) + Vector3.up * 28;
                distance = Mathf.Lerp(225, 200, a); yaw = Mathf.Lerp(-42, -14, a); pitch = Mathf.Lerp(38, 34, a);
            } else if (t < 36.5f) {
                float a = Ease((t - 32) / 4.5f); focus = CitySimulation.World(22, 31) + Vector3.up * 2;
                distance = Mathf.Lerp(112, 98, a); yaw = Mathf.Lerp(-38, -18, a); pitch = 47;
                if (t > 33.7f) game.View.ShowHover(-1, -1, Color.clear, false);
            } else if (t < 39) {
                float a = Ease((t - 36.5f) / 2.5f); focus = Vector3.Lerp(CitySimulation.World(20, 24), CitySimulation.World(19, 24), a) + Vector3.up * 4;
                distance = 93; yaw = -70; pitch = 40;
            } else {
                float a = Ease((t - 39) / 7); focus = CitySimulation.World(21, 24) + Vector3.up * 20;
                distance = Mathf.Lerp(285, 265, a); yaw = Mathf.Lerp(-32, -18, a); pitch = 42;
            }
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            game.Cam.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            game.Rig.Focus = focus; game.Rig.Distance = distance; game.Rig.Yaw = yaw; game.Rig.Pitch = pitch;
        }

        void StartEncoder()
        {
            string path = Environment.GetEnvironmentVariable("SEABRIGHT_FFMPEG");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Set SEABRIGHT_FFMPEG to a local FFmpeg binary.");
            var start = new ProcessStartInfo(path, "-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -movflags +faststart " + DirectoryPath + "/gameplay.mp4") {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = Path.GetFullPath(".")
            };
            encoder = Process.Start(start);
            encoder.ErrorDataReceived += (sender, args) => { if (args.Data != null) { lock (events) encoderLog += args.Data + "\n"; } };
            encoder.BeginErrorReadLine();
        }
        void RenderFrame(int frame, bool checkpoint)
        {
            var previousTarget = game.Cam.targetTexture; var previousActive = RenderTexture.active;
            try {
                game.Cam.targetTexture = target; game.Cam.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false);
                if (!preview) { byte[] raw = pixels.GetRawTextureData(); encoder.StandardInput.BaseStream.Write(raw, 0, raw.Length); }
                if (checkpoint) File.WriteAllBytes(DirectoryPath + "/frame-" + (frame / Fps).ToString("D2") + ".png", pixels.EncodeToPNG());
            } finally { game.Cam.targetTexture = previousTarget; RenderTexture.active = previousActive; }
        }
        void Finish(Exception error)
        {
            if (encoder != null) {
                encoder.StandardInput.Close();
                if (!encoder.WaitForExit(30000)) { encoder.Kill(); if (error == null) error = new TimeoutException("Encoder did not finish."); }
                else if (encoder.ExitCode != 0 && error == null) error = new InvalidOperationException("FFmpeg failed: " + encoderLog);
                File.WriteAllText(DirectoryPath + "/capture-encoder.log", encoderLog); encoder.Dispose();
            }
            File.WriteAllText(DirectoryPath + "/capture-events.json", JsonUtility.ToJson(new EventLog { events = events.ToArray(), samples = samples.ToArray() }, true));
            File.WriteAllText(DirectoryPath + "/capture-report.json", JsonUtility.ToJson(new Report {
                recordedAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, width = Width, height = Height, fps = Fps, duration = 46,
                preview = preview, passed = error == null, population = game.Sim.Population, day = game.Sim.Day, treasury = game.Sim.Money, lowestTreasury = lowestTreasury,
                method = "Unity Play mode camera rendering, fixed 30fps traffic/camera motion; original CitySimulation.Build/BuildRoad/Tick. Starts with 24 residents and $30000. Growth waiting compressed; no population, funds, levels, unlocks or skyline injected. Interface hidden. Audio mixed later from original game assets with timestamped construction events. No user save or preferences written.",
                error = error == null ? "" : error.ToString()
            }, true));
            Time.captureFramerate = 0;
            if (target) { target.Release(); Destroy(target); } if (pixels) Destroy(pixels);
            if (error != null) Debug.LogException(error); else Debug.Log("SEABRIGHT_TRAILER_CAPTURE_COMPLETE " + DirectoryPath);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1); else EditorApplication.isPlaying = false;
        }
        sealed class ScheduledBuild { public float Time; public TileKind Kind; public int X, Z, ToX, ToZ; public bool Drag; }
        [Serializable] sealed class CaptureEvent { public float time; public string cue, detail; public int population, day; }
        [Serializable] sealed class Sample { public float time, treasury; public int population, day, cars, pedestrians; }
        [Serializable] sealed class EventLog { public CaptureEvent[] events; public Sample[] samples; }
        [Serializable] sealed class Report { public string recordedAtUtc, unityVersion, method, error; public int width, height, fps, population, day; public float duration, treasury, lowestTreasury; public bool preview, passed; }
    }
}
