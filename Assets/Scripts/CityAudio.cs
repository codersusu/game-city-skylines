using UnityEngine;

namespace Seabright
{
    /// <summary>Original score, a camera-aware coastal soundscape and bounded interaction voices.</summary>
    public sealed class CityAudio : MonoBehaviour
    {
        public enum Cue { Ui, Road, Zone, Facility, Bulldoze, Denied, Milestone, Save, Load, Grow }
        const string Pref = "Seabright.Audio.";
        static readonly string[] LoopNames = { "music_coastal", "ambience_coast", "ambience_town", "ambience_birds", "ambience_night" };
        static readonly string[] CueNames = { "ui_click", "road_build", "zone_paint", "facility_build", "bulldoze", "denied", "milestone", "save", "load", "grow" };
        static readonly float[] Cooldowns = { .07f, .18f, .11f, .18f, .14f, .4f, 2.5f, .3f, .3f, 3.5f };
        static readonly float[] CueGains = { .6f, .8f, .72f, .8f, .8f, .7f, .82f, .8f, .8f, .52f };
        readonly AudioSource[] loops = new AudioSource[5], voices = new AudioSource[8];
        readonly AudioClip[] clips = new AudioClip[10];
        readonly float[] nextCue = new float[10], voiceGains = new float[8];
        readonly int[] played = new int[10];
        readonly int[] levels = new int[CitySimulation.Size * CitySimulation.Size];
        readonly TileKind[] kinds = new TileKind[CitySimulation.Size * CitySimulation.Size];
        CityGame game;
        int observedRevision = -1, observedMilestone, voiceCursor;
        float nextDensity, townDensity, duckUntil, preferencesChangedAt = -1;
        bool isolated;
        public float Master { get; private set; } = .85f;
        public float Music { get; private set; } = .48f;
        public float Ambience { get; private set; } = .8f;
        public float Effects { get; private set; } = .8f;
        public bool Muted { get; private set; }
        public bool Ready { get; private set; }
        public int LoadedClipCount { get; private set; }
        public bool LoopsPlaying { get { foreach (var s in loops) if (!s || !s.isPlaying) return false; return true; } }
        public int PlayedCount(Cue cue) => played[(int)cue];
        public float LoopVolume(int index) => index >= 0 && index < loops.Length && loops[index] ? loops[index].volume : 0;

        public void Initialize(CityGame owner)
        {
            game = owner;
            isolated = game.AudioTestEnabled || game.AutoplayEnabled || game.InputDiagnostics;
            if (!isolated) {
                Master = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref + "Master", Master));
                Music = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref + "Music", Music));
                Ambience = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref + "Ambience", Ambience));
                Effects = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref + "Effects", Effects));
                Muted = PlayerPrefs.GetInt(Pref + "Muted", 0) != 0;
            }
            double startsAt = AudioSettings.dspTime + .15;
            for (int i = 0; i < loops.Length; i++) {
                loops[i] = Source(LoopNames[i], true, i == 0 ? 80 : 150);
                loops[i].clip = Load(LoopNames[i]);
                if (loops[i].clip) loops[i].PlayScheduled(startsAt);
            }
            for (int i = 0; i < clips.Length; i++) clips[i] = Load(CueNames[i]);
            for (int i = 0; i < voices.Length; i++) voices[i] = Source("Interaction " + (i + 1), false, 40);
            AudioListener.volume = Muted ? 0 : Master;
            ResetCitySnapshot();
            Ready = LoadedClipCount == 15;
        }

        AudioSource Source(string label, bool loop, int priority)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            source.volume = 0; source.priority = priority; source.dopplerLevel = 0;
            source.ignoreListenerPause = true;
            return source;
        }
        AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip) { clip.LoadAudioData(); LoadedClipCount++; }
            else Debug.LogError("Seabright audio asset missing: " + name);
            return clip;
        }

        void Update()
        {
            if (!Ready || game.Sim == null) return;
            float now = Time.unscaledTime;
            if (now >= nextDensity) {
                nextDensity = now + .5f;
                float occupants = 0;
                foreach (var tile in game.Sim.Tiles) {
                    float distance = Vector3.Distance(game.Rig.Focus, CitySimulation.World(tile.X, tile.Z));
                    occupants += (tile.Residents + tile.Jobs) * Mathf.Clamp01(1 - distance / 100);
                }
                townDensity = Mathf.Clamp01(occupants / 160f);
            }
            float close = Mathf.Lerp(.4f, 1, Mathf.InverseLerp(650, 80, game.Rig.Distance));
            float shore = Mathf.Clamp01((game.Rig.Focus.x + 200) / 380);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f);
            Mix(0, Music * (now < duckUntil ? .55f : 1), blend);
            Mix(1, Ambience * Mathf.Lerp(.19f, .45f, shore), blend);
            Mix(2, Ambience * (.12f + townDensity * .48f) * close * (game.Night ? .55f : 1) * (game.Speed == 0 ? .25f : 1), blend);
            Mix(3, Ambience * (game.Night ? 0 : .45f) * close, blend);
            Mix(4, Ambience * (game.Night ? .5f : 0) * close, blend);
            ObserveCity();
            if (preferencesChangedAt >= 0 && now - preferencesChangedAt > .75f) FlushPreferences();
        }
        void Mix(int index, float target, float blend) { loops[index].volume = Mathf.Lerp(loops[index].volume, target, blend); }

        public bool Play(Cue cue)
        {
            int index = (int)cue;
            if (!Ready || Muted || Master <= 0 || Effects <= 0 || !clips[index] || Time.unscaledTime < nextCue[index]) return false;
            nextCue[index] = Time.unscaledTime + Cooldowns[index];
            int slot = voiceCursor++ % voices.Length;
            // Prefer an idle source; never allocate a source for each painted tile.
            for (int i = 0; i < voices.Length; i++) if (!voices[i].isPlaying) { slot = i; break; }
            var source = voices[slot]; source.Stop(); source.clip = clips[index];
            voiceGains[slot] = CueGains[index]; source.volume = Effects * voiceGains[slot];
            bool variation = cue == Cue.Zone || cue == Cue.Road || cue == Cue.Bulldoze;
            source.pitch = variation ? 1 + (played[index] % 5 - 2) * .025f : 1;
            source.Play(); played[index]++;
            if (cue == Cue.Milestone) duckUntil = Time.unscaledTime + 2.8f;
            return true;
        }

        static int Milestone(int population) => population >= 600 ? 3 : population >= 350 ? 2 : population >= 150 ? 1 : 0;
        public void ResetCitySnapshot()
        {
            if (game == null || game.Sim == null) return;
            for (int i = 0; i < levels.Length; i++) { levels[i] = game.Sim.Tiles[i].Level; kinds[i] = game.Sim.Tiles[i].Kind; }
            observedMilestone = Milestone(game.Sim.PeakPopulation); observedRevision = game.Sim.Revision;
        }
        void ObserveCity()
        {
            if (game.Sim.Revision == observedRevision) return;
            bool grew = false;
            for (int i = 0; i < levels.Length; i++) {
                var tile = game.Sim.Tiles[i];
                if (CitySimulation.IsZone(tile.Kind) && kinds[i] == tile.Kind && tile.Level > levels[i]) grew = true;
                levels[i] = tile.Level; kinds[i] = tile.Kind;
            }
            int milestone = Milestone(game.Sim.PeakPopulation);
            if (milestone > observedMilestone) Play(Cue.Milestone);
            else if (grew) Play(Cue.Grow);
            observedRevision = game.Sim.Revision; observedMilestone = milestone;
        }

        public void SetVolumes(float master, float music, float ambience, float effects)
        {
            master = Mathf.Clamp01(master); music = Mathf.Clamp01(music); ambience = Mathf.Clamp01(ambience); effects = Mathf.Clamp01(effects);
            if (Master == master && Music == music && Ambience == ambience && Effects == effects) return;
            Master = master; Music = music; Ambience = ambience; Effects = effects;
            AudioListener.volume = Muted ? 0 : Master;
            if (Music == 0 && loops[0]) loops[0].volume = 0;
            if (Ambience == 0) for (int i = 1; i < loops.Length; i++) if (loops[i]) loops[i].volume = 0;
            for (int i = 0; i < voices.Length; i++) if (voices[i]) voices[i].volume = Effects * voiceGains[i];
            preferencesChangedAt = Time.unscaledTime;
        }
        public void SetMuted(bool muted)
        {
            Muted = muted; AudioListener.volume = Muted ? 0 : Master;
            if (muted) foreach (var source in voices) if (source) source.Stop();
            preferencesChangedAt = Time.unscaledTime;
        }
        public void RestoreDefaults() { SetMuted(false); SetVolumes(.85f, .48f, .8f, .8f); }
        void FlushPreferences()
        {
            if (preferencesChangedAt < 0) return;
            preferencesChangedAt = -1;
            if (isolated) return;
            PlayerPrefs.SetFloat(Pref + "Master", Master); PlayerPrefs.SetFloat(Pref + "Music", Music);
            PlayerPrefs.SetFloat(Pref + "Ambience", Ambience); PlayerPrefs.SetFloat(Pref + "Effects", Effects);
            PlayerPrefs.SetInt(Pref + "Muted", Muted ? 1 : 0); PlayerPrefs.Save();
        }
        void OnApplicationFocus(bool focused) { if (!focused) FlushPreferences(); }
        void OnApplicationQuit() { FlushPreferences(); }
    }
}
