using System;
using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [CreateAssetMenu(menuName = "Dotshelf/Bookshelf Theme Layout", fileName = "BookshelfThemeLayout")]
    public sealed class BookshelfThemeLayout : ScriptableObject
    {
        [SerializeField] private BookshelfThemeStyle style = BookshelfThemeStyle.Default;
        [SerializeField] private GameObject bookshelfPrefab = null;
        [SerializeField] private Vector3 prefabLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 prefabLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 prefabLocalScale = Vector3.one;
        [SerializeField] private bool preservePrefabLocalTransform;
        [SerializeField] private Vector3 focusLocalPosition = new Vector3(0f, 0.8f, -0.175f);
        [SerializeField] private Vector3 focusSize = new Vector3(1.7f, 1.7f, 0.18f);
        [SerializeField] private List<BookshelfSectionLayout> sections = new List<BookshelfSectionLayout>();

        public BookshelfThemeStyle Style => style;
        public GameObject BookshelfPrefab => bookshelfPrefab;
        public Vector3 FocusLocalPosition => focusLocalPosition;
        public Vector3 FocusSize => focusSize;
        public IReadOnlyList<BookshelfSectionLayout> Sections => sections;
        public bool IsUsable => sections != null && sections.Count > 0;

        public GameObject InstantiateVisual(Transform parent)
        {
            if (bookshelfPrefab == null || parent == null)
            {
                return null;
            }

            var instance = Instantiate(bookshelfPrefab, parent, false);
            instance.name = $"{name}_Visual";
            instance.transform.localPosition = prefabLocalPosition;
            instance.transform.localRotation = Quaternion.Euler(prefabLocalEulerAngles);
            instance.transform.localScale = prefabLocalScale;

            if (preservePrefabLocalTransform)
            {
                var transformLock = instance.GetComponent<BookshelfThemeLayoutTransformLock>();
                if (transformLock == null)
                {
                    transformLock = instance.AddComponent<BookshelfThemeLayoutTransformLock>();
                }

                transformLock.Configure(name, prefabLocalPosition, prefabLocalEulerAngles, prefabLocalScale);
            }

            return instance;
        }

        public void BuildSections(List<ShelfSection> target)
        {
            if (target == null)
            {
                return;
            }

            target.Clear();
            if (sections == null)
            {
                return;
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var layout = sections[i];
                if (layout != null && layout.IsValid)
                {
                    target.Add(layout.ToRuntimeSection());
                }
            }
        }

#if UNITY_EDITOR
        public void SetGeneratedData(
            BookshelfThemeStyle nextStyle,
            GameObject nextBookshelfPrefab,
            Vector3 nextPrefabLocalPosition,
            Vector3 nextPrefabLocalEulerAngles,
            Vector3 nextPrefabLocalScale,
            bool nextPreservePrefabLocalTransform,
            Vector3 nextFocusLocalPosition,
            Vector3 nextFocusSize,
            List<BookshelfSectionLayout> nextSections)
        {
            style = nextStyle;
            bookshelfPrefab = nextBookshelfPrefab;
            prefabLocalPosition = nextPrefabLocalPosition;
            prefabLocalEulerAngles = nextPrefabLocalEulerAngles;
            prefabLocalScale = nextPrefabLocalScale;
            preservePrefabLocalTransform = nextPreservePrefabLocalTransform;
            focusLocalPosition = nextFocusLocalPosition;
            focusSize = nextFocusSize;
            sections = nextSections != null
                ? new List<BookshelfSectionLayout>(nextSections)
                : new List<BookshelfSectionLayout>();
        }
#endif
    }

    [Serializable]
    public sealed class BookshelfSectionLayout
    {
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private float leftBound;
        [SerializeField] private float rightBound;
        [SerializeField] private float shelfTopY;
        [SerializeField] private float sectionHeight;
        [SerializeField] private float bookYOffset;
        [SerializeField] private float frontBoundZ;
        [SerializeField] private float backBoundZ;
        [SerializeField] private ShelfSectionAlignment alignment = ShelfSectionAlignment.Left;
        [SerializeField] private bool isAdvertisementSection;

        public int Row => row;
        public int Column => column;
        public float LeftBound => leftBound;
        public float RightBound => rightBound;
        public float ShelfTopY => shelfTopY;
        public float SectionHeight => sectionHeight;
        public float BookYOffset => bookYOffset;
        public float FrontBoundZ => frontBoundZ;
        public float BackBoundZ => backBoundZ;
        public ShelfSectionAlignment Alignment => alignment;
        public bool IsAdvertisementSection => isAdvertisementSection;

        public bool IsValid =>
            rightBound > leftBound &&
            sectionHeight > 0f &&
            backBoundZ > frontBoundZ;

        public BookshelfSectionLayout(
            int row,
            int column,
            float leftBound,
            float rightBound,
            float shelfTopY,
            float sectionHeight,
            float bookYOffset,
            float frontBoundZ,
            float backBoundZ,
            ShelfSectionAlignment alignment,
            bool isAdvertisementSection)
        {
            this.row = row;
            this.column = column;
            this.leftBound = leftBound;
            this.rightBound = rightBound;
            this.shelfTopY = shelfTopY;
            this.sectionHeight = sectionHeight;
            this.bookYOffset = bookYOffset;
            this.frontBoundZ = frontBoundZ;
            this.backBoundZ = backBoundZ;
            this.alignment = alignment;
            this.isAdvertisementSection = isAdvertisementSection;
        }

        public ShelfSection ToRuntimeSection()
        {
            var section = new ShelfSection(
                row,
                column,
                leftBound,
                rightBound,
                shelfTopY,
                sectionHeight,
                (frontBoundZ + backBoundZ) * 0.5f,
                frontBoundZ,
                backBoundZ)
            {
                alignment = alignment,
                bookYOffset = bookYOffset,
                isAdvertisementSection = isAdvertisementSection
            };

            return section;
        }
    }
}
