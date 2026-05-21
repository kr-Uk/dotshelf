ALTER TABLE spaces ADD COLUMN book_count            BIGINT NOT NULL DEFAULT 0;
ALTER TABLE spaces ADD COLUMN reading_record_count  BIGINT NOT NULL DEFAULT 0;
ALTER TABLE spaces ADD COLUMN bookshelf_count       BIGINT NOT NULL DEFAULT 0;

-- backfill: 기존 데이터의 카운터를 실제 값으로 채운다.
UPDATE spaces s
SET bookshelf_count = (
    SELECT COUNT(*) FROM bookshelves b
    WHERE b.space_id = s.id AND b.status = 'ACTIVE' AND b.deleted_at IS NULL
);

UPDATE spaces s
SET book_count = (
    SELECT COUNT(*) FROM user_books ub
    WHERE ub.user_id = s.owner_user_id
      AND ub.storage_status = 'ON_SHELF'
      AND ub.deleted_at IS NULL
);

UPDATE spaces s
SET reading_record_count = (
    SELECT COUNT(*) FROM reading_records rr
    JOIN user_books ub ON ub.id = rr.user_book_id
    WHERE ub.user_id = s.owner_user_id
      AND rr.deleted_at IS NULL
);
