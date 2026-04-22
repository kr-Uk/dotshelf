using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public class BookshelfState : MonoBehaviour
    {
        private const float SectionDropAreaMargin = 0.08f;
        private const float MinimumSectionFitScale = 0.68f;

        [SerializeField] private float bookPadding = 0.01f;
        [SerializeField] private float shelfFrontInset = 0.01f;
        [SerializeField] private float animationDuration = 0.18f;

        private readonly List<ShelfSection> sections = new List<ShelfSection>();
        private readonly List<BookLayoutTarget> layoutTargets = new List<BookLayoutTarget>();

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

        public bool TryPlaceBookForDrop(
            BookEntity book,
            ShelfSection target,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            localScale = Vector3.one;

            if (!TryInsertIntoSection(book, target))
            {
                return false;
            }

            if (TryGetBookLayoutTransform(book, target, out localPosition, out localRotation, out localScale))
            {
                return true;
            }

            target.books.Remove(book);
            book.SetSection(null);
            return false;
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
            LayoutSection(section, runner, animate, null);
        }

        public void LayoutSectionExcept(ShelfSection section, MonoBehaviour runner, bool animate, BookEntity excludedBook)
        {
            LayoutSection(section, runner, animate, excludedBook);
        }

        private void LayoutSection(ShelfSection section, MonoBehaviour runner, bool animate, BookEntity excludedBook)
        {
            if (section == null)
            {
                return;
            }

            BuildSectionLayout(section, layoutTargets);
            for (var i = 0; i < layoutTargets.Count; i++)
            {
                var target = layoutTargets[i];
                var book = target.Book;

                if (book == excludedBook)
                {
                    continue;
                }

                if (animate && runner != null && book.gameObject.activeInHierarchy)
                {
                    runner.StartCoroutine(AnimateBookTo(book.transform, target.LocalPosition, target.LocalRotation, target.LocalScale));
                }
                else
                {
                    book.transform.localPosition = target.LocalPosition;
                    book.transform.localRotation = target.LocalRotation;
                    book.transform.localScale = target.LocalScale;
                }
            }
        }

        public bool TryGetBookLayoutTransform(
            BookEntity targetBook,
            ShelfSection section,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            localScale = Vector3.one;

            if (targetBook == null || section == null)
            {
                return false;
            }

            BuildSectionLayout(section, layoutTargets);
            for (var i = 0; i < layoutTargets.Count; i++)
            {
                var target = layoutTargets[i];
                if (target.Book == targetBook)
                {
                    localPosition = target.LocalPosition;
                    localRotation = target.LocalRotation;
                    localScale = target.LocalScale;
                    return true;
                }
            }

            return false;
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
                if (section == exclude || !CanSectionContainBook(section, book))
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

        public ShelfSection FindAvailableDropSection(Vector3 localPosition, BookEntity book, float margin = SectionDropAreaMargin)
        {
            ShelfSection best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (!IsInsideSectionDropBounds(localPosition, section, margin) ||
                    !CanSectionContainBook(section, book))
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

        private static bool IsInsideSectionDropBounds(Vector3 localPosition, ShelfSection section, float margin)
        {
            if (section == null)
            {
                return false;
            }

            return localPosition.x >= section.leftBound - margin &&
                   localPosition.x <= section.rightBound + margin &&
                   localPosition.y >= section.shelfTopY - margin &&
                   localPosition.y <= section.shelfTopY + section.sectionHeight + margin;
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
            return GetSectionLayoutScale(section) >= MinimumSectionFitScale;
        }

        private bool CanSectionContainBook(ShelfSection section, BookEntity book)
        {
            if (section == null || book == null)
            {
                return false;
            }

            RemoveMissingBooks(section);
            return GetSectionLayoutScale(section, book) >= MinimumSectionFitScale;
        }

        private void BuildSectionLayout(ShelfSection section, List<BookLayoutTarget> targets)
        {
            targets.Clear();

            if (section == null)
            {
                return;
            }

            RemoveMissingBooks(section);
            var sectionScale = GetSectionLayoutScale(section);
            var x = section.leftBound + bookPadding;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                var footprint = book.GetFootprint() * sectionScale;
                // X uses scaled footprints while Z aligns the visual front edge inside the section depth.
                targets.Add(new BookLayoutTarget(
                    book,
                    new Vector3(
                        x + footprint * 0.5f,
                        section.shelfTopY + book.height * sectionScale * 0.5f,
                        section.GetFrontAlignedBookCenterZ(book, sectionScale, shelfFrontInset)),
                    BookFootprintUtility.GetVisualRotation(book.orientation),
                    book.BaseLocalScale * sectionScale));

                x += footprint + bookPadding;
            }
        }

        private float GetSectionLayoutScale(ShelfSection section)
        {
            return GetSectionLayoutScale(section, null);
        }

        private float GetSectionLayoutScale(ShelfSection section, BookEntity candidateBook)
        {
            if (section == null)
            {
                return 1f;
            }

            var footprintSum = 0f;
            var bookCount = 0;
            var candidateAlreadyIncluded = false;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == null)
                {
                    continue;
                }

                if (book == candidateBook)
                {
                    candidateAlreadyIncluded = true;
                }

                footprintSum += book.GetFootprint();
                bookCount++;
            }

            if (candidateBook != null && !candidateAlreadyIncluded)
            {
                footprintSum += candidateBook.GetFootprint();
                bookCount++;
            }

            if (bookCount == 0 || footprintSum <= 0f)
            {
                return 1f;
            }

            var availableForBooks = section.Width - bookPadding * (bookCount + 1);
            if (availableForBooks <= 0f)
            {
                return 0f;
            }

            return Mathf.Min(1f, availableForBooks / footprintSum);
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

        private readonly struct BookLayoutTarget
        {
            public BookLayoutTarget(BookEntity book, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
            {
                Book = book;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            public BookEntity Book { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }
    }
}
