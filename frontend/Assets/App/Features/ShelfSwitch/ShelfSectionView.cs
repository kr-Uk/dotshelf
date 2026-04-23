using UnityEngine;

namespace App.Features.ShelfSwitch
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShelfSectionView : MonoBehaviour
    {
        [SerializeField] private int row;
        [SerializeField] private int col;

        private BoxCollider sectionCollider;

        public int Row => row;
        public int Col => col;
        public Transform BookRoot => transform;

        private void Awake()
        {
            sectionCollider = GetComponent<BoxCollider>();
            sectionCollider.isTrigger = true;
        }

        public void Configure(int sectionRow, int sectionCol)
        {
            row = sectionRow;
            col = sectionCol;

            if (sectionCollider == null)
            {
                sectionCollider = GetComponent<BoxCollider>();
            }

            sectionCollider.isTrigger = true;
        }

        public Bounds GetLocalBounds()
        {
            if (sectionCollider == null)
            {
                sectionCollider = GetComponent<BoxCollider>();
            }

            return new Bounds(sectionCollider.center, sectionCollider.size);
        }

        private void OnDrawGizmos()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(collider.center, collider.size);
        }
    }
}

