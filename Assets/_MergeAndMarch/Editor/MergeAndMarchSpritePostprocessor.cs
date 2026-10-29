using UnityEditor;
using UnityEngine;

namespace MergeAndMarch.Editor
{
    public class MergeAndMarchSpritePostprocessor : AssetPostprocessor
    {
        private const string SpriteRoot = "Assets/_MergeAndMarch/Sprites/";
        private const string BeanRoot = "Assets/_MergeAndMarch/Sprites/Beans/";

        // Art bible Section 8: one texel density for every world sprite.
        private const float BeanPixelsPerUnit = 256f;

        // Feet sit on an 8 px margin of a 256 canvas (16 px of the 512 boss canvas).
        private static readonly Vector2 FeetPivot = new(0.5f, 8f / 256f);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot))
            {
                return;
            }

            if (assetImporter is not TextureImporter importer)
            {
                return;
            }

            if (assetPath.StartsWith(BeanRoot))
            {
                ConfigureBeanSprite(importer);
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

        /// <summary>
        /// Import rules for the bean art set (design/art/art-bible.md Section 8):
        /// PPU 256, feet pivot for unit layers, Read/Write off, no mipmaps,
        /// ASTC 4x4 on Android.
        /// </summary>
        private void ConfigureBeanSprite(TextureImporter importer)
        {
            bool isUnitLayer = assetPath.Contains("/Units/") || assetPath.Contains("/Enemies/") || assetPath.Contains("/Faces/");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = BeanPixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = isUnitLayer ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = isUnitLayer ? FeetPivot : new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_4x4;
            android.maxTextureSize = assetPath.Contains("_boss_") ? 512 : 256;
            importer.SetPlatformTextureSettings(android);
        }

        private static float GetPixelsPerUnit(string path)
        {
            // Legacy AI chibi sprites, archived under Sprites/_archived/ (still the fallback art).
            if (path.Contains("/Sprites/_archived/Troops/") || path.StartsWith("Assets/_MergeAndMarch/Sprites/Troops/"))
            {
                return 1100f;
            }

            if (path.Contains("/Sprites/_archived/Enemies/") || path.StartsWith("Assets/_MergeAndMarch/Sprites/Enemies/"))
            {
                return 1200f;
            }

            return 100f;
        }
    }
}
