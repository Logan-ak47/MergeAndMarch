using MergeAndMarch.Core;
using MergeAndMarch.Data;
using MergeAndMarch.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MergeAndMarch.Editor
{
    public static class Phase1SceneSetupTool
    {
        private const string RootPath = "Assets/_MergeAndMarch";
        private const string ScriptsPath = RootPath + "/Scripts";
        private const string DataAssetPath = RootPath + "/ScriptableObjects";
        private const string PrefabPath = RootPath + "/Prefabs";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Merge And March/Phase 1/Create Session 1 Setup")]
        public static void CreateSession1Setup()
        {
            EnsureFolders();
            EnsureSortingLayers();

            GameConfig gameConfig = EnsureGameConfig();
            Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            TroopData knightData = EnsureTroopData(
                "Knight",
                TroopType.Knight,
                "Knight",
                new Color(0.2667f, 0.5333f, 1f),
                100f,
                15f,
                2f,
                squareSprite);

            TroopData archerData = EnsureTroopData(
                "Archer",
                TroopType.Archer,
                "Archer",
                new Color(0.2667f, 1f, 0.5333f),
                40f,
                12f,
                0.8f,
                squareSprite);

            Troop troopPrefab = EnsureTroopPrefab(squareSprite);
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera camera = ConfigureSceneCamera();
            BuildScene(gameConfig, troopPrefab, knightData, archerData, squareSprite, camera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Merge & March",
                "Portrait battle setup is ready. Open Assets/Scenes/Game.unity and press Play to test the bottom-centered grid and top-down battlefield space.",
                "Nice");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets", "_MergeAndMarch");
            CreateFolderIfMissing(RootPath, "Scripts");
            CreateFolderIfMissing(RootPath, "Editor");
            CreateFolderIfMissing(RootPath, "ScriptableObjects");
            CreateFolderIfMissing(RootPath, "Prefabs");
            CreateFolderIfMissing(RootPath, "Scenes");
            CreateFolderIfMissing(ScriptsPath, "Core");
            CreateFolderIfMissing(ScriptsPath, "Data");
            CreateFolderIfMissing(ScriptsPath, "Gameplay");
            CreateFolderIfMissing("Assets", "Scenes");
        }

        private static void CreateFolderIfMissing(string parent, string child)
        {
            string combinedPath = $"{parent}/{child}";

            if (!AssetDatabase.IsValidFolder(combinedPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static GameConfig EnsureGameConfig()
        {
            string assetPath = $"{DataAssetPath}/GameConfig.asset";
            GameConfig asset = AssetDatabase.LoadAssetAtPath<GameConfig>(assetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.columns = 4;
            asset.rows = 2;
            asset.cellSize = 1.15f;
            asset.gridOffset = new Vector2(-1.725f, -3.6f);
            asset.troopBaseScale = 0.82f;
            asset.tierTwoScale = 0.97f;
            asset.tierThreeScale = 1.12f;
            asset.slotVisualScale = 0.88f;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static TroopData EnsureTroopData(
            string assetName,
            TroopType troopType,
            string displayName,
            Color troopColor,
            float baseHp,
            float baseAttack,
            float attackInterval,
            Sprite sprite)
        {
            string assetPath = $"{DataAssetPath}/{assetName}.asset";
            TroopData asset = AssetDatabase.LoadAssetAtPath<TroopData>(assetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TroopData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.troopType = troopType;
            asset.displayName = displayName;
            asset.troopColor = troopColor;
            asset.baseHP = baseHp;
            asset.baseAttack = baseAttack;
            asset.attackInterval = attackInterval;
            asset.sprite = sprite;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Troop EnsureTroopPrefab(Sprite sprite)
        {
            string assetPath = $"{PrefabPath}/TroopPrefab.prefab";
            Troop existingPrefab = AssetDatabase.LoadAssetAtPath<Troop>(assetPath);

            if (existingPrefab != null)
            {
                return existingPrefab;
            }

            GameObject prefabRoot = new("TroopPrefab");
            SpriteRenderer renderer = prefabRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Troops";
            renderer.sortingOrder = 0;
            prefabRoot.AddComponent<BoxCollider2D>();
            prefabRoot.AddComponent<Troop>();

            Troop savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath).GetComponent<Troop>();
            Object.DestroyImmediate(prefabRoot);
            return savedPrefab;
        }

        private static Camera ConfigureSceneCamera()
        {
            Camera camera = Object.FindObjectOfType<Camera>();

            if (camera == null)
            {
                return null;
            }

            camera.orthographic = true;
            camera.backgroundColor = new Color(0.102f, 0.102f, 0.18f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = false;
            }

            return camera;
        }

        private static void BuildScene(
            GameConfig gameConfig,
            Troop troopPrefab,
            TroopData knightData,
            TroopData archerData,
            Sprite slotSprite,
            Camera camera)
        {
            GameObject managers = new("_Managers");
            RunManager runManager = managers.AddComponent<RunManager>();
            managers.AddComponent<TimeScaleManager>();

            GameObject gridObject = new("BattleGrid");
            BattleGrid battleGrid = gridObject.AddComponent<BattleGrid>();
            battleGrid.SetConfig(gameConfig);

            GameObject slotsRoot = new("SlotVisuals");
            slotsRoot.transform.SetParent(gridObject.transform, false);
            battleGrid.SetSlotVisualRoot(slotsRoot.transform);
            battleGrid.RebuildSlotVisuals(slotSprite);

            GameObject troopRoot = new("Troops");

            GameObject mergeControllerObject = new("MergeController");
            MergeController mergeController = mergeControllerObject.AddComponent<MergeController>();

            SetSerializedReference(runManager, "gameConfig", gameConfig);
            SetSerializedReference(runManager, "battleGrid", battleGrid);
            SetSerializedReference(runManager, "troopPrefab", troopPrefab);
            SetSerializedReference(runManager, "knightData", knightData);
            SetSerializedReference(runManager, "archerData", archerData);
            SetSerializedReference(runManager, "troopRoot", troopRoot.transform);
            SetSerializedBool(runManager, "spawnOnStart", false);

            SetSerializedReference(mergeController, "gameConfig", gameConfig);
            SetSerializedReference(mergeController, "battleGrid", battleGrid);
            SetSerializedReference(mergeController, "targetCamera", camera);

            if (camera != null)
            {
                GridCameraFramer framer = camera.GetComponent<GridCameraFramer>();
                if (framer == null)
                {
                    framer = camera.gameObject.AddComponent<GridCameraFramer>();
                }

                SetSerializedReference(framer, "battleGrid", battleGrid);
                framer.RefreshFrame();
            }

            runManager.SetupOpeningLineup();
            SetSerializedBool(runManager, "spawnOnStart", true);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void SetSerializedReference(Object target, string fieldName, Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);

            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedBool(Object target, string fieldName, bool value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);

            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSortingLayers()
        {
            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

            AddSortingLayerIfMissing(sortingLayers, "Background");
            AddSortingLayerIfMissing(sortingLayers, "Grid");
            AddSortingLayerIfMissing(sortingLayers, "Enemies");
            AddSortingLayerIfMissing(sortingLayers, "Troops");
            AddSortingLayerIfMissing(sortingLayers, "Effects");
            AddSortingLayerIfMissing(sortingLayers, "UI");

            tagManager.ApplyModifiedProperties();
        }

        private static void AddSortingLayerIfMissing(SerializedProperty sortingLayers, string layerName)
        {
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                SerializedProperty layer = sortingLayers.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = layer.FindPropertyRelative("name");

                if (nameProperty != null && nameProperty.stringValue == layerName)
                {
                    return;
                }
            }

            sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
            SerializedProperty newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
            SerializedProperty newNameProperty = newLayer.FindPropertyRelative("name");
            SerializedProperty uniqueIdProperty = newLayer.FindPropertyRelative("uniqueID");

            if (newNameProperty != null)
            {
                newNameProperty.stringValue = layerName;
            }

            if (uniqueIdProperty != null)
            {
                uniqueIdProperty.intValue = Mathf.Abs(layerName.GetHashCode()) + 100;
            }
        }
    }
}
