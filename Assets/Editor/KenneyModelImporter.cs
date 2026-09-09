using UnityEditor;

namespace Seabright.Editor
{
    // Models are reused by the runtime mesh batching system, which requires CPU mesh access.
    public sealed class KenneyModelImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/ThirdParty/Kenney/")) return;
            var model = (ModelImporter)assetImporter;
            model.isReadable = true;
            model.importAnimation = false;
            model.importCameras = false;
            model.importLights = false;
            model.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/ThirdParty/Kenney/")) return;
            var texture = (TextureImporter)assetImporter;
            texture.mipmapEnabled = true;
            texture.filterMode = UnityEngine.FilterMode.Bilinear;
            texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
