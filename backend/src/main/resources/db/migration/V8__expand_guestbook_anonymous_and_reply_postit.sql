-- 사용자 앱 v1.1 대시보드 확장 — Guestbook 익명 여부 + GuestbookReply 포스트잇 스타일.
-- expand-only 마이그레이션 (구 버전 코드와 호환).
--   1) guestbooks.anonymous : 기본 false → 구 버전이 INSERT 해도 NULL 안 됨.
--   2) guestbook_replies.post_it_inventory_id : NULL 허용 → 구 버전 INSERT 영향 없음.

ALTER TABLE guestbooks
    ADD COLUMN anonymous BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE guestbook_replies
    ADD COLUMN post_it_inventory_id BIGINT;
