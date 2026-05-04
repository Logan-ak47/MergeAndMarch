using UnityEditor;
using UnityEngine;

namespace MergeAndMarch.Editor
{
    public class MergeAndMarchSpritePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_MergeAndMarch/Sprites/"))
            {
                return;
            }

            if (assetImporter is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = GetPixelsPerUnit(assetPath);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
        }

        private static float GetPixelsPerUnit(string path)
        {
            if (path.StartsWith("Assets/_MergeAndMarch/Sprites/Troops/"))
            {
                return 1100f;
            }

            if (path.StartsWith("Assets/_MergeAndMarch/Sprites/Enemies/"))
            {
                return 1200f;
            }

            return 100f;
        }
    }
}
