using System;
using System.Collections.Generic;
using App.Domain;
using UnityEngine;

namespace App.Features.ShelfSwitch
{
    public sealed class ShelfManager : MonoBehaviour
    {
        [SerializeField] private ShelfView shelfPrefab;
        [SerializeField] private Transform shelfRoot;

        private readonly List<ShelfView> shelves = new List<ShelfView>();

        public ShelfView ActiveShelf { get; private set; }
        public int ActiveIndex { get; private set; } = -1;
        public IReadOnlyList<ShelfView> Shelves => shelves;
        public bool CanUserSwitch { get; set; } = true;
        public bool CanInternalSwitch { get; set; } = true;

        public event Action<ShelfView> OnActiveShelfChanged;

        public ShelfView CreateShelf()
        {
            var orderIndex = shelves.Count;
            var domainShelf = new Shelf($"shelf_{orderIndex + 1:000}", orderIndex);
            var view = shelfPrefab != null
                ? Instantiate(shelfPrefab, shelfRoot != null ? shelfRoot : transform)
                : new GameObject(domainShelf.ShelfId).AddComponent<ShelfView>();

            if (shelfRoot != null && view.transform.parent != shelfRoot)
            {
                view.transform.SetParent(shelfRoot, false);
            }

            view.name = domainShelf.ShelfId;
            view.CacheSectionsFromChildren();
            view.Bind(domainShelf);
            shelves.Add(view);

            if (ActiveShelf == null)
            {
                SwitchToIndex(0);
            }

            return view;
        }

        public void DeleteShelf(string shelfId)
        {
            if (shelves.Count <= 1)
            {
                return;
            }

            var index = shelves.FindIndex(shelf => shelf != null && shelf.BoundShelf != null && shelf.BoundShelf.ShelfId == shelfId);
            if (index < 0)
            {
                return;
            }

            var target = shelves[index];
            shelves.RemoveAt(index);
            if (target != null)
            {
                Destroy(target.gameObject);
            }

            for (var i = 0; i < shelves.Count; i++)
            {
                if (shelves[i]?.BoundShelf != null)
                {
                    shelves[i].BoundShelf.OrderIndex = i;
                }
            }

            SwitchToIndex(Mathf.Clamp(ActiveIndex, 0, shelves.Count - 1));
        }

        public void SwitchToNext()
        {
            if (!CanInternalSwitch || shelves.Count == 0)
            {
                return;
            }

            SwitchToIndex((ActiveIndex + 1) % shelves.Count);
        }

        public void SwitchToPrev()
        {
            if (!CanInternalSwitch || shelves.Count == 0)
            {
                return;
            }

            var next = ActiveIndex - 1;
            if (next < 0)
            {
                next = shelves.Count - 1;
            }

            SwitchToIndex(next);
        }

        public void SwitchToIndex(int index)
        {
            if (index < 0 || index >= shelves.Count || ActiveIndex == index)
            {
                return;
            }

            ActiveIndex = index;
            ActiveShelf = shelves[index];
            OnActiveShelfChanged?.Invoke(ActiveShelf);
        }
    }
}

