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

        public GameObject RuntimeSystemsPrefab => runtimeSystemsPrefab;
        public GameObject RoomBackgroundPrefab => roomBackgroundPrefab;
        public GameObject BookshelfRigPrefab => bookshelfRigPrefab;
        public GameObject NotebookStationPrefab => notebookStationPrefab;
        public GameObject CameraAnchorsPrefab => cameraAnchorsPrefab;

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
