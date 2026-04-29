using System.Collections.Generic;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public static class SpriteBackgroundCleaner
    {
        private static readonly Dictionary<Sprite, Sprite> CleanedSpriteCache = new();

        public static Sprite GetCleanedSprite(Sprite source)
        {
            if (source == null)
            {
                return null;
            }

            if (CleanedSpriteCache.TryGetValue(source, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Sprite cleanedSprite = TryCreateCleanedSprite(source);
            CleanedSpriteCache[source] = cleanedSprite != null ? cleanedSprite : source;
            return CleanedSpriteCache[source];
        }

        private static Sprite TryCreateCleanedSprite(Sprite source)
        {
            Texture2D sourceTexture = source.texture;
            if (sourceTexture == null)
            {
                return null;
            }

            try
            {
                Rect sourceRect = source.textureRect;
                int sourceX = Mathf.RoundToInt(sourceRect.x);
                int sourceY = Mathf.RoundToInt(sourceRect.y);
                int width = Mathf.RoundToInt(sourceRect.width);
                int height = Mathf.RoundToInt(sourceRect.height);
                Color32[] pixels = sourceTexture.GetPixels32();
                Color32[] workingPixels = new Color32[width * height];

                for (int y = 0; y < height; y++)
                {
                    int sourceRow = (sourceY + y) * sourceTexture.width;
                    int targetRow = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        workingPixels[targetRow + x] = pixels[sourceRow + sourceX + x];
                    }
                }

                ClearConnectedBackground(workingPixels, width, height);
                if (!TryGetOpaqueBounds(workingPixels, width, height, out RectInt bounds))
                {
                    return source;
                }

                Texture2D cleanedTexture = new(bounds.width, bounds.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"{source.name}_Cleaned"
                };

                Color32[] croppedPixels = new Color32[bounds.width * bounds.height];
                for (int y = 0; y < bounds.height; y++)
                {
                    int sourceRow = (bounds.y + y) * width;
                    int targetRow = y * bounds.width;
                    for (int x = 0; x < bounds.width; x++)
                    {
                        croppedPixels[targetRow + x] = workingPixels[sourceRow + bounds.x + x];
                    }
                }

                cleanedTexture.SetPixels32(croppedPixels);
                cleanedTexture.Apply(false, false);

                Vector2 pivotPixels = source.pivot - new Vector2(bounds.x, bounds.y);
                Vector2 normalizedPivot = new(
                    Mathf.Clamp01(pivotPixels.x / bounds.width),
                    Mathf.Clamp01(pivotPixels.y / bounds.height));

                return Sprite.Create(
                    cleanedTexture,
                    new Rect(0f, 0f, bounds.width, bounds.height),
                    normalizedPivot,
                    source.pixelsPerUnit,
                    0,
                    SpriteMeshType.Tight);
            }
            catch
            {
                return source;
            }
        }

        private static void ClearConnectedBackground(Color32[] pixels, int width, int height)
        {
            bool[] visited = new bool[pixels.Length];
            int[] queue = new int[pixels.Length];
            int head = 0;
            int tail = 0;

            void Enqueue(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                int index = (y * width) + x;
                if (visited[index])
                {
                    return;
                }

                visited[index] = true;
                if (!IsBackground(pixels[index]))
                {
                    return;
                }

                queue[tail++] = index;
            }

            for (int x = 0; x < width; x++)
            {
                Enqueue(x, 0);
                Enqueue(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                Enqueue(0, y);
                Enqueue(width - 1, y);
            }

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;
                Color32 color = pixels[index];
                color.a = 0;
                pixels[index] = color;

                Enqueue(x + 1, y);
                Enqueue(x - 1, y);
                Enqueue(x, y + 1);
                Enqueue(x, y - 1);
            }
        }

        private static bool TryGetOpaqueBounds(Color32[] pixels, int width, int height, out RectInt bounds)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 10)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static bool IsBackground(Color32 color)
        {
            int max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            int min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return color.a < 250 || (max - min <= 14 && min >= 220);
        }
    }
}
