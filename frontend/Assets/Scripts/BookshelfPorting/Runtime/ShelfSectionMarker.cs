using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [RequireComponent(typeof(BoxCollider))]
    public class ShelfSectionMarker : MonoBehaviour
    {
        [SerializeField] private int row;
        [SerializeField] private int column;

        private BoxCollider markerCollider;
        private MeshRenderer markerRenderer;

        public int Row => row;
        public int Column => column;

        public void Configure(int sectionRow, int sectionColumn, Material material)
        {
            row = sectionRow;
            column = sectionColumn;

            if (markerCollider == null)
            {
                markerCollider = GetComponent<BoxCollider>();
            }

            if (markerRenderer == null)
            {
                markerRenderer = GetComponent<MeshRenderer>();
            }

            markerCollider.isTrigger = true;
            markerRenderer.sharedMaterial = material;
        }

        public void SetVisible(bool isVisible)
        {
            if (markerCollider == null)
            {
                markerCollider = GetComponent<BoxCollider>();
            }

            if (markerRenderer == null)
            {
                markerRenderer = GetComponent<MeshRenderer>();
            }

            markerCollider.enabled = isVisible;
            markerRenderer.enabled = isVisible;
        }

        private void Awake()
        {
            markerCollider = GetComponent<BoxCollider>();
            markerRenderer = GetComponent<MeshRenderer>();
        }
    }
}
