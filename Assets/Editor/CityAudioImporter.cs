using UnityEditor;
using UnityEngine;

namespace Seabright.Editor
{
    // Lossless decoded loops preserve the designed seam; the complete stereo sound bank
    // is ~27 MB in memory and remains lossless in the player.
    public sealed class CityAudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
