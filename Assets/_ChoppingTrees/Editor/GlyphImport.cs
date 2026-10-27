using UnityEditor;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Controller button glyphs and controller icons (Resources/Glyphs) import as crisp UI sprites:
    /// no compression (they are small), mipmaps for clean downscaling, transparent edges kept.
    /// </summary>
    public class GlyphImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Glyphs/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.filterMode = UnityEngine.FilterMode.Trilinear;
        }
    }
}
