using App.Domain;
using System;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [RequireComponent(typeof(Collider))]
    public class BookEntity : MonoBehaviour
    {
        [Header("Layout Dimensions")]
        public float thickness = 0.03f;
        public float height = 0.24f;
        public float depth = 0.16f;
        public BookOrientation orientation = BookOrientation.Spine;

        [Header("Visuals")]
        public MeshRenderer[] tintRenderers;
        public string bookId;
        public string source;
        public string isbn13;
        public string isbn10;
        public string publishedDate;
        public string coverImageUrl;
        public string sourceUrl;
        public string userBookId;
        public string bookshelfItemId;
        public string bookshelfId;
        public string bookshelfSectionId;
        public string readingRecordId;
        public string title;
        public string author;
        public string publisher;
        [TextArea]
        public string description;
        public string memoPreview;
        public int rating;
        public float averageRating;
        public int totalLikeCount;
        public int reviewCount;
        public bool hasAggregateBookStats;
        public float ownerRating;
        public ReadingStatus readingStatus = ReadingStatus.Unread;
        public bool isLiked;
        public bool isPaidCoverOwned;
        public string coverSkinResourcePath;
        public bool isDecoration;
        public bool isRead;
        public bool isPublic = true;
        public VisibilityScope reportVisibility = VisibilityScope.Public;
        public string reportTitle;
        public string reportBody;
        public string reportUpdatedLabel;
        public bool reportLiked;
        public int reportLikeCount;
        public int reportCommentCount;
        public string sizePresetId;
        public int bookshelfIndex = -1;
        public int cellIndex = -1;
        public float offsetXInCell;

        public ShelfSection CurrentSection { get; private set; }
        public ShelfSection OriginalSection { get; private set; }
        public Vector3 BaseLocalScale { get; private set; }
        public Vector3 StoredPosition { get; private set; }
        public Quaternion StoredRotation { get; private set; }
        public Vector3 StoredScale { get; private set; }
        public Vector2 AccumulatedViewRotation { get; private set; }

        private Quaternion viewBaseRotation = Quaternion.identity;

        private void Awake()
        {
            if (BaseLocalScale == Vector3.zero)
            {
                BaseLocalScale = transform.localScale;
            }
        }

        public void Initialize(string id, float thicknessValue, float heightValue, float depthValue, Color color)
        {
            bookId = id;
            thickness = thicknessValue;
            height = heightValue;
            depth = depthValue;
            BaseLocalScale = transform.localScale;

            ApplyTint(color);
            ApplyOrientationVisualsImmediate();
        }

        public void ApplySizePreset(BookSizePreset preset)
        {
            sizePresetId = preset.Id;
            thickness = preset.Thickness;
            height = preset.Height;
            depth = preset.Depth;
            transform.localScale = new Vector3(thickness, height, depth);
            BaseLocalScale = transform.localScale;
        }

        public void SetPlacementInfo(int nextBookshelfIndex, int nextCellIndex, float nextOffsetXInCell)
        {
            bookshelfIndex = nextBookshelfIndex;
            cellIndex = nextCellIndex;
            offsetXInCell = nextOffsetXInCell;
        }

        public void ApplyMetadata(Book book, UserBook userBook, ReadingRecord readingRecord)
        {
            if (book != null)
            {
                bookId = book.BookId;
                title = book.Title;
                author = book.Author;
                publisher = book.Publisher;
                description = book.Description;
                averageRating = Mathf.Clamp((float)book.AverageRating, 0f, 5f);
                totalLikeCount = Mathf.Max(0, (int)book.LikeCount);
                reviewCount = Mathf.Max(0, book.RatingCount);
                hasAggregateBookStats = true;
            }

            if (userBook == null)
            {
                return;
            }

            userBookId = userBook.UserBookId;
            ownerRating = userBook.OwnerRating.HasValue ? (float)userBook.OwnerRating.Value : 0f;
            rating = Mathf.RoundToInt(hasAggregateBookStats ? averageRating : ownerRating);
            readingStatus = userBook.ReadingStatus;
            isLiked = userBook.IsLiked;
            isRead = readingStatus == ReadingStatus.Read;
            readingRecordId = readingRecord?.Id;
            reportVisibility = readingRecord != null ? readingRecord.VisibilityScope : VisibilityScope.Public;
            isPublic = reportVisibility == VisibilityScope.Public;
            reportTitle = readingRecord?.Title;
            reportBody = readingRecord?.Content;
            reportLikeCount = readingRecord?.LikeCount ?? 0;
            reportCommentCount = readingRecord?.CommentCount ?? 0;
            reportUpdatedLabel = readingRecord != null && readingRecord.UpdatedAt != DateTimeOffset.MinValue
                ? readingRecord.UpdatedAt.ToString("yyyy.MM.dd")
                : string.Empty;
            memoPreview = readingRecord != null && !string.IsNullOrWhiteSpace(readingRecord.Content)
                ? readingRecord.Content
                : description;
        }

        public void SetSection(ShelfSection section, bool markOriginal = false)
        {
            CurrentSection = section;
            if (markOriginal || OriginalSection == null)
            {
                OriginalSection = section;
            }
        }

        public float GetFootprint()
        {
            return BookFootprintUtility.GetFootprint(thickness, depth, orientation);
        }

        public float GetLayoutFootprint()
        {
            return BookFootprintUtility.GetLayoutFootprint(thickness, depth, orientation);
        }

        public void SetOrientation(BookOrientation nextOrientation)
        {
            orientation = nextOrientation;
            ApplyOrientationVisualsImmediate();
        }

        public void ApplyOrientationVisualsImmediate()
        {
            transform.localRotation = BookFootprintUtility.GetVisualRotation(orientation);
        }

        public void CaptureCurrentTransform()
        {
            StoredPosition = transform.position;
            StoredRotation = transform.rotation;
            StoredScale = transform.localScale;
        }

        public void AddViewRotation(Vector2 delta, float speed)
        {
            AccumulatedViewRotation += delta * speed;
            var x = Mathf.Clamp(AccumulatedViewRotation.y, -85f, 85f);
            var y = AccumulatedViewRotation.x;
            transform.rotation = Quaternion.Euler(-x, y, 0f) * viewBaseRotation;
        }

        public void BeginViewRotation(Quaternion baseRotation)
        {
            viewBaseRotation = baseRotation;
            AccumulatedViewRotation = Vector2.zero;
        }

        public void ResetViewRotation()
        {
            viewBaseRotation = Quaternion.identity;
            AccumulatedViewRotation = Vector2.zero;
        }

        private void ApplyTint(Color color)
        {
            if (tintRenderers == null)
            {
                return;
            }

            for (var i = 0; i < tintRenderers.Length; i++)
            {
                if (tintRenderers[i] == null || tintRenderers[i].sharedMaterial == null)
                {
                    continue;
                }

                var targetMaterial = Application.isPlaying
                    ? tintRenderers[i].material
                    : tintRenderers[i].sharedMaterial;
                targetMaterial.color = color;
            }
        }
    }
}
