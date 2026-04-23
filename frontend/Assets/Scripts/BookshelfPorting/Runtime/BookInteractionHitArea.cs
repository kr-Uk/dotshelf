using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [RequireComponent(typeof(BoxCollider))]
    public class BookInteractionHitArea : MonoBehaviour
    {
        [SerializeField] private BookEntity owner = null;

        private BoxCollider hitCollider;

        public BookEntity Owner => owner;

        public void Configure(BookEntity targetOwner, Vector3 size, Vector3 center)
        {
            owner = targetOwner;

            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider>();
            }

            hitCollider.isTrigger = false;
            hitCollider.size = size;
            hitCollider.center = center;
        }

        public void SetActive(bool isActive)
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider>();
            }

            hitCollider.enabled = isActive;
        }

        private void Awake()
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider>();
            }
        }
    }
}
