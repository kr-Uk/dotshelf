using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Config
{
    [CreateAssetMenu(fileName = "MainSceneCompositionConfig", menuName = "App/Config/Main Scene Composition")]
    public sealed class MainSceneCompositionConfig : ScriptableObject
    {
        [SerializeField] private GameObject runtimeSystemsPrefab;
        [SerializeField] private GameObject roomBackgroundPrefab;
        [SerializeField] private GameObject bookshelfRigPrefab;
        [SerializeField] private GameObject notebookStationPrefab;
        [SerializeField] private GameObject cameraAnchorsPrefab;
        [SerializeField] private GameObject dashboardScreenPanelPrefab;

        [Header("Exterior (창밖 환경)")]
        [Tooltip("외부 옵션 카탈로그. Tier 1 = default 1개 포함. Tier 2 에서 BE catalog 와 동기화. " +
                 "비워두면 ExteriorApplier 의 autoApplyOnAwake fallback 사용.")]
        [SerializeField] private ExteriorCatalogAsset exteriorCatalog;

        public GameObject RuntimeSystemsPrefab => runtimeSystemsPrefab;
        public GameObject RoomBackgroundPrefab => roomBackgroundPrefab;
        public GameObject BookshelfRigPrefab => bookshelfRigPrefab;
        public GameObject NotebookStationPrefab => notebookStationPrefab;
        public GameObject CameraAnchorsPrefab => cameraAnchorsPrefab;
        public GameObject DashboardScreenPanelPrefab => dashboardScreenPanelPrefab;
        public ExteriorCatalogAsset ExteriorCatalog => exteriorCatalog;

        public void Configure(
            GameObject runtimeSystems,
            GameObject roomBackground,
            GameObject bookshelfRig,
            GameObject notebookStation,
            GameObject cameraAnchors)
        {
            runtimeSystemsPrefab = runtimeSystems;
            roomBackgroundPrefab = roomBackground;
            bookshelfRigPrefab = bookshelfRig;
            notebookStationPrefab = notebookStation;
            cameraAnchorsPrefab = cameraAnchors;
        }
    }
}
