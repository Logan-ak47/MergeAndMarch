using System.Collections.Generic;
using System.IO;
using System.Text;
using MergeAndMarch.Data;
using UnityEditor;
using UnityEngine;

namespace MergeAndMarch.Editor
{
    /// <summary>
    /// Wires the generated bean sprites (art-source/beans/generate_beans.py) into the game data:
    /// creates the JuiceConfig and BeanArtSet assets in Resources, assigns per-unit sprites to every
    /// TroopData and EnemyData, and applies the data values set by design/art/art-bible.md
    /// (tier scales x1.18 / x1.38, slot 22% and lane 12% opacity, Section 4 troop colours).
    /// Safe to run repeatedly.
    /// </summary>
    public static class BeanArtInstaller
    {
        private const string BeanRoot = "Assets/_MergeAndMarch/Sprites/Beans";
        private const string ResourceFolder = "Assets/_MergeAndMarch/Resources/MergeAndMarch";
        private const string JuiceConfigPath = ResourceFolder + "/JuiceConfig.asset";
        private const string BeanArtSetPath = ResourceFolder + "/BeanArtSet.asset";

        // Art bible Section 4 primary palette.
        private static readonly Dictionary<TroopType, string> TroopColors = new()
        {
            { TroopType.Knight, "#2f6bff" },
            { TroopType.Archer, "#18c964" },
            { TroopType.Mage, "#a347ff" },
            { TroopType.Healer, "#ff6fd1" },
            { TroopType.Bomber, "#ff7a00" }
        };

        // Bean bodies already carry the shape differences (narrow Rusher, wide Tank, 2x Boss canvas),
        // so the old per-type size scales are normalised. The spawner multiplies the Boss by 1.45 on
        // wave 16, so 0.69 lands the 2-unit boss canvas at the bible's 2x size.
        private static readonly Dictionary<EnemyType, float> EnemySizeScales = new()
        {
            { EnemyType.Grunt, 1f },
            { EnemyType.Rusher, 1f },
            { EnemyType.Tank, 1f },
            { EnemyType.Flyer, 0.95f },
            { EnemyType.Boss, 0.69f }
        };

        [MenuItem("Merge And March/Install Bean Art")]
        public static void InstallFromMenu()
        {
            string report = Install();
            Debug.Log(report);
        }

        /// <summary>Runs the installer and returns a human-readable report of what changed.</summary>
        public static string Install()
        {
            StringBuilder report = new("Bean art install:\n");
            EnsureFolder(ResourceFolder);

            JuiceConfig juice = LoadOrCreate<JuiceConfig>(JuiceConfigPath, report);
            BeanArtSet art = LoadOrCreate<BeanArtSet>(BeanArtSetPath, report);
            FillArtSet(art, report);
            EditorUtility.SetDirty(juice);
            EditorUtility.SetDirty(art);

            foreach (string guid in AssetDatabase.FindAssets("t:TroopData"))
            {
                TroopData troop = AssetDatabase.LoadAssetAtPath<TroopData>(AssetDatabase.GUIDToAssetPath(guid));
                string key = troop.troopType.ToString().ToLowerInvariant();
                troop.beanBody = Sprite($"Units/char_{key}_body.png", report);
                troop.beanProp = Sprite($"Units/char_{key}_prop.png", report);
                troop.beanAccent = Sprite($"Units/char_{key}_t3accent.png", report);
                troop.beanFlash = Sprite($"Units/char_{key}_flash.png", report);
                if (TroopColors.TryGetValue(troop.troopType, out string hex) && ColorUtility.TryParseHtmlString(hex, out Color color))
                {
                    troop.troopColor = color;
                }

                EditorUtility.SetDirty(troop);
                report.AppendLine($"  troop {troop.name}: bean sprites {(troop.HasBeanArt ? "assigned" : "MISSING")}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData"))
            {
                EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                string key = enemy.enemyType.ToString().ToLowerInvariant();
                enemy.beanBody = Sprite($"Enemies/char_enemy_{key}_body.png", report);
                enemy.beanFlash = Sprite($"Enemies/char_enemy_{key}_flash.png", report);
                enemy.beanWings = enemy.enemyType == EnemyType.Flyer ? Sprite("Enemies/char_enemy_flyer_wings.png", report) : null;
                if (EnemySizeScales.TryGetValue(enemy.enemyType, out float sizeScale))
                {
                    enemy.sizeScale = sizeScale;
                }

                EditorUtility.SetDirty(enemy);
                report.AppendLine($"  enemy {enemy.name}: bean sprites {(enemy.HasBeanArt ? "assigned" : "MISSING")}, sizeScale {enemy.sizeScale}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:GameConfig"))
            {
                GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid));
                config.tierTwoScale = 1.18f;
                config.tierThreeScale = 1.38f;
                config.slotTint = WithAlpha(config.slotTint, 0.22f);
                config.laneGuideTint = WithAlpha(config.laneGuideTint, 0.12f);
                EditorUtility.SetDirty(config);
                report.AppendLine($"  config {config.name}: tier scales 1.18 / 1.38, slot 22%, lanes 12%");
            }

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        private static void FillArtSet(BeanArtSet art, StringBuilder report)
        {
            art.allyIdle = Sprite("Faces/char_face_ally_idle.png", report);
            art.allyBlink = Sprite("Faces/char_face_ally_blink.png", report);
            art.allyAttack = Sprite("Faces/char_face_ally_attack.png", report);
            art.allyHurt = Sprite("Faces/char_face_ally_hurt.png", report);
            art.allyDizzy = Sprite("Faces/char_face_ally_dizzy.png", report);
            art.allyHappy = Sprite("Faces/char_face_ally_happy.png", report);
            art.enemyIdle = Sprite("Faces/char_face_enemy_idle.png", report);
            art.enemyBlink = Sprite("Faces/char_face_enemy_blink.png", report);
            art.enemyHurt = Sprite("Faces/char_face_enemy_hurt.png", report);
            art.enemyDizzy = Sprite("Faces/char_face_enemy_dizzy.png", report);
            art.tierBadges = new[]
            {
                Sprite("UI/ui_badge_t1.png", report),
                Sprite("UI/ui_badge_t2.png", report),
                Sprite("UI/ui_badge_t3.png", report)
            };
            art.fxCircle = Sprite("FX/vfx_circle.png", report);
            art.fxStar = Sprite("FX/vfx_star.png", report);
            art.fxSparkle = Sprite("FX/vfx_sparkle.png", report);
            art.fxConfetti = Sprite("FX/vfx_confetti.png", report);
            art.fxChunk = Sprite("FX/vfx_chunk.png", report);
            art.fxRing = Sprite("FX/vfx_ring.png", report);
            art.fxPuff = Sprite("FX/vfx_puff.png", report);
            art.fxRays = Sprite("FX/vfx_rays.png", report);
            art.fxGlow = Sprite("FX/vfx_glow.png", report);
            art.fxShadow = Sprite("FX/vfx_shadow.png", report);
            art.fxCoin = Sprite("FX/vfx_coin.png", report);
        }

        private static Sprite Sprite(string relativePath, StringBuilder report)
        {
            string path = $"{BeanRoot}/{relativePath}";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                report.AppendLine($"  MISSING sprite: {path}");
            }

            return sprite;
        }

        private static T LoadOrCreate<T>(string path, StringBuilder report) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            report.AppendLine($"  created {path}");
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
