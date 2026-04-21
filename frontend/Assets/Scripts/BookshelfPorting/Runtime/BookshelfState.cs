using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public class BookshelfState : MonoBehaviour
    {
        [SerializeField] private float bookPadding = 0.01f;
        [SerializeField] private float shelfFrontInset = 0.01f;
        [SerializeField] private float animationDuration = 0.18f;

        private readonly List<ShelfSection> sections = new List<ShelfSection>();

        public IReadOnlyList<ShelfSection> Sections => sections;
        public bool IsViewingBook { get; set; }
        public BookEntity ActiveViewedBook { get; set; }
        public BookEntity ActiveDraggedBook { get; set; }
        public float BookPadding => bookPadding;
        public float ShelfFrontInset => shelfFrontInset;

        public void ResetSections(List<ShelfSection> newSections)
        {
            sections.Clear();
            sections.AddRange(newSections);
        }

        public void AddBookToSection(BookEntity book, ShelfSection section)
        {
            if (book.CurrentSection != null)
            {
                book.CurrentSection.books.Remove(book);
            }

            if (!section.books.Contains(book))
            {
                section.books.Add(book);
            }

            book.SetSection(section);
        }

        public void RemoveBook(BookEntity book)
        {
            if (book.CurrentSection == null)
            {
                return;
            }

            book.CurrentSection.books.Remove(book);
            book.SetSection(null);
        }

        public bool TryPlaceBook(BookEntity book, ShelfSection target, MonoBehaviour runner, bool animate)
        {
            if (!TryInsertIntoSection(book, target))
            {
                return false;
            }

            LayoutSection(target, runner, animate);
            return true;
        }

        public void LayoutAll(MonoBehaviour runner, bool animate)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                LayoutSection(sections[i], runner, animate);
            }
        }

        public void LayoutSection(ShelfSection section, MonoBehaviour runner, bool animate)
        {
            if (section == null)
            {
                return;
            }

            RemoveMissingBooks(section);
            var x = section.leftBound + bookPadding;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                var footprint = book.GetFootprint();
                var targetPosition = new Vector3(
                    x + footprint * 0.5f,
                    section.shelfTopY + book.height * 0.5f,
                    section.frontZ - shelfFrontInset);
                var targetRotation = BookFootprintUtility.GetVisualRotation(book.orientation);

                var targetScale = book.BaseLocalScale;

                if (animate && runner != null && book.gameObject.activeInHierarchy)
                {
                    runner.StartCoroutine(AnimateBookTo(book.transform, targetPosition, targetRotation, targetScale));
                }
                else
                {
                    book.transform.localPosition = targetPosition;
                    book.transform.localRotation = targetRotation;
                    book.transform.localScale = targetScale;
                }

                x += footprint + bookPadding;
            }
        }

        public bool ChangeBookOrientation(BookEntity book, BookOrientation nextOrientation, MonoBehaviour runner)
        {
            var source = book.CurrentSection;
            var previous = book.orientation;

            if (source == null)
            {
                book.SetOrientation(nextOrientation);
                return true;
            }

            book.orientation = nextOrientation;
            if (CanSectionContainAll(source))
            {
                LayoutSection(source, runner, true);
                return true;
            }

            var displaced = FindLargestBookExcept(source, book);
            if (displaced != null && TryMoveToNearestAvailableSection(displaced, source))
            {
                LayoutSection(source, runner, true);
                if (displaced.CurrentSection != null)
                {
                    LayoutSection(displaced.CurrentSection, runner, true);
                }
                return true;
            }

            book.orientation = previous;
            LayoutSection(source, runner, true);
            return false;
        }

        public ShelfSection FindNearestAvailableSection(Vector3 localPosition, BookEntity book, ShelfSection exclude = null)
        {
            ShelfSection best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section == exclude || !section.CanFit(book, bookPadding))
                {
                    continue;
                }

                var distance = Vector3.Distance(localPosition, section.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = section;
                }
            }

            return best;
        }

        public ShelfSection GetSection(int row, int column)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section.row == row && section.column == column)
                {
                    return section;
                }
            }

            return null;
        }

        public bool TryMoveToNearestAvailableSection(BookEntity book, ShelfSection avoid)
        {
            var destination = FindNearestAvailableSection(book.transform.localPosition, book, avoid);
            if (destination == null)
            {
                return false;
            }

            RemoveBook(book);
            AddBookToSection(book, destination);
            return true;
        }

        private bool TryInsertIntoSection(BookEntity book, ShelfSection target)
        {
            if (book == null || target == null)
            {
                return false;
            }

            RemoveMissingBooks(target);
            if (!target.books.Contains(book))
            {
                target.books.Add(book);
            }

            book.SetSection(target);

            if (CanSectionContainAll(target))
            {
                return true;
            }

            target.books.Remove(book);
            book.SetSection(null);
            return false;
        }

        private bool CanSectionContainAll(ShelfSection section)
        {
            if (section == null)
            {
                return false;
            }

            RemoveMissingBooks(section);
            var occupied = bookPadding;
            for (var i = 0; i < section.books.Count; i++)
            {
                occupied += section.books[i].GetFootprint() + bookPadding;
            }

            return occupied <= section.Width;
        }

        private static BookEntity FindLargestBookExcept(ShelfSection section, BookEntity except)
        {
            if (section == null)
            {
                return null;
            }

            RemoveMissingBooks(section);
            BookEntity candidate = null;
            var largest = float.MinValue;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == except)
                {
                    continue;
                }

                var footprint = book.GetFootprint();
                if (footprint > largest)
                {
                    largest = footprint;
                    candidate = book;
                }
            }

            return candidate;
        }

        private static void RemoveMissingBooks(ShelfSection section)
        {
            if (section == null)
            {
                return;
            }

            for (var i = section.books.Count - 1; i >= 0; i--)
            {
                if (section.books[i] == null)
                {
                    section.books.RemoveAt(i);
                }
            }
        }

        private IEnumerator AnimateBookTo(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var elapsed = 0f;
            var startPos = target.localPosition;
            var startRot = target.localRotation;
            var startScale = target.localScale;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);
                target.localPosition = Vector3.Lerp(startPos, localPosition, t);
                target.localRotation = Quaternion.Slerp(startRot, localRotation, t);
                target.localScale = Vector3.Lerp(startScale, localScale, t);
                yield return null;
            }

            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
        }
    }
}
