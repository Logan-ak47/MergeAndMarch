using MergeAndMarch.Gameplay;
using UnityEngine;

namespace MergeAndMarch.Core
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class GridCameraFramer : MonoBehaviour
    {
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private float horizontalPadding = 0.55f;
        [SerializeField] private float verticalPadding = 1.0f;
        [SerializeField] private Vector2 framingOffset = new(0f, 0f);
        [SerializeField] private bool updateContinuouslyInEditor = true;
        [SerializeField] private Vector2 gridViewportAnchor = new(0.5f, 0.24f);

        private Camera targetCamera;

        private void Awake()
        {
            CacheCamera();
            RefreshFrame();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying || updateContinuouslyInEditor)
            {
                RefreshFrame();
            }
        }

        private void OnValidate()
        {
            CacheCamera();
            gridViewportAnchor.x = Mathf.Clamp(gridViewportAnchor.x, 0.1f, 0.9f);
            gridViewportAnchor.y = Mathf.Clamp(gridViewportAnchor.y, 0.1f, 0.9f);
            RefreshFrame();
        }

        public void RefreshFrame()
        {
            if (battleGrid == null)
            {
                return;
            }

            CacheCamera();
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            Bounds gridBounds = battleGrid.GetGridWorldBounds();
            float aspect = Mathf.Max(targetCamera.aspect, 0.01f);
            float halfHeight = Mathf.Max((gridBounds.size.y * 0.5f) + verticalPadding, ((gridBounds.size.x * 0.5f) + horizontalPadding) / aspect);
            float halfWidth = halfHeight * aspect;

            targetCamera.orthographicSize = halfHeight;

            float viewportOffsetX = (gridViewportAnchor.x - 0.5f) * 2f * halfWidth;
            float viewportOffsetY = (gridViewportAnchor.y - 0.5f) * 2f * halfHeight;

            Vector3 position = targetCamera.transform.position;
            position.x = gridBounds.center.x - viewportOffsetX + framingOffset.x;
            position.y = gridBounds.center.y - viewportOffsetY + framingOffset.y;
            targetCamera.transform.position = position;
        }

        public void SetBattleGrid(BattleGrid grid)
        {
            battleGrid = grid;
            RefreshFrame();
        }

        private void CacheCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
        }
    }
}
