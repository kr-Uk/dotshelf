using App.Domain;
using UnityEngine;

namespace App.Features.ShelfSwitch
{
    public sealed class ShelfView : MonoBehaviour
    {
        [SerializeField] private ShelfSectionView[] sections;

        public Shelf BoundShelf { get; private set; }
        public ShelfSectionView[] Sections => sections;

        public void Bind(Shelf shelf)
        {
            BoundShelf = shelf;
        }

        public ShelfSectionView GetSection(int row, int col)
        {
            if (sections == null)
            {
                return null;
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section != null && section.Row == row && section.Col == col)
                {
                    return section;
                }
            }

            return null;
        }

        public void CacheSectionsFromChildren()
        {
            sections = GetComponentsInChildren<ShelfSectionView>(true);
        }
    }
}

