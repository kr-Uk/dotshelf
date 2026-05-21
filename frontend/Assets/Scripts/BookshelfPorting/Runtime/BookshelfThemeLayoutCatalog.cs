using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [CreateAssetMenu(menuName = "Dotshelf/Bookshelf Theme Layout Catalog", fileName = "BookshelfThemeLayoutCatalog")]
    public sealed class BookshelfThemeLayoutCatalog : ScriptableObject
    {
        [SerializeField] private BookshelfThemeLayout[] layouts = new BookshelfThemeLayout[0];

        public float MaxFocusWidth
        {
            get
            {
                var max = 0f;
                if (layouts == null)
                {
                    return max;
                }

                for (var i = 0; i < layouts.Length; i++)
                {
                    var layout = layouts[i];
                    if (layout != null)
                    {
                        max = Mathf.Max(max, Mathf.Abs(layout.FocusSize.x));
                    }
                }

                return max;
            }
        }

        public bool TryGetLayout(BookshelfThemeStyle style, out BookshelfThemeLayout layout)
        {
            if (layouts != null)
            {
                for (var i = 0; i < layouts.Length; i++)
                {
                    var candidate = layouts[i];
                    if (candidate != null && candidate.Style == style)
                    {
                        layout = candidate;
                        return true;
                    }
                }
            }

            layout = null;
            return false;
        }

#if UNITY_EDITOR
        public void SetLayouts(IReadOnlyList<BookshelfThemeLayout> nextLayouts)
        {
            if (nextLayouts == null)
            {
                layouts = new BookshelfThemeLayout[0];
                return;
            }

            var result = new List<BookshelfThemeLayout>();
            for (var i = 0; i < nextLayouts.Count; i++)
            {
                if (nextLayouts[i] != null)
                {
                    result.Add(nextLayouts[i]);
                }
            }

            layouts = result.ToArray();
        }
#endif
    }
}
