-- Backfill books.like_count from existing user_books likes before runtime like/unlike starts
-- maintaining the aggregate counter.
UPDATE public.books b
SET like_count = COALESCE(l.like_count, 0)
FROM (
    SELECT book_id, COUNT(*)::bigint AS like_count
    FROM public.user_books
    WHERE is_liked = TRUE
      AND deleted_at IS NULL
      AND UPPER(storage_status) <> 'DELETED'
    GROUP BY book_id
) l
WHERE b.id = l.book_id;

UPDATE public.books b
SET like_count = 0
WHERE NOT EXISTS (
    SELECT 1
    FROM public.user_books ub
    WHERE ub.book_id = b.id
      AND ub.is_liked = TRUE
      AND ub.deleted_at IS NULL
      AND UPPER(ub.storage_status) <> 'DELETED'
);
