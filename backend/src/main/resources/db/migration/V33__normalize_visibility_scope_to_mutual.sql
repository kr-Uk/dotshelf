-- FOLLOWERS / FOLLOWERS_ONLY 레거시 visibility 값을 정식 MUTUAL(맞팔)로 통일.
-- 관계 공개 tier 를 "맞팔" 의미로 일원화하면서 컬럼 저장값도 정식 이름으로 마이그레이션한다.
-- FE 는 한동안 구 값(FOLLOWERS_ONLY)을 계속 전송하므로, 엔티티 write 정규화
-- (VisibilityScope.normalize)가 신규 row 를 MUTUAL 로 수렴시킨다 — 본 파일은 기존 row 정리.
--
-- 대상: MUTUAL 읽기 enforcement 가 적용되는 테이블 — spaces / reading_records /
-- guestbooks / guestbook_replies / feed_events. (bookshelves 는 PUBLIC/PRIVATE
-- binary 라 정규화·마이그레이션 대상에서 제외 — MR 233 리뷰 반영.)

UPDATE spaces SET visibility_scope = 'MUTUAL'
    WHERE visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');

UPDATE spaces SET guestbook_visibility_scope = 'MUTUAL'
    WHERE guestbook_visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');

UPDATE reading_records SET visibility_scope = 'MUTUAL'
    WHERE visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');

UPDATE guestbooks SET visibility_scope = 'MUTUAL'
    WHERE visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');

UPDATE guestbook_replies SET visibility_scope = 'MUTUAL'
    WHERE visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');

UPDATE feed_events SET visibility_scope = 'MUTUAL'
    WHERE visibility_scope IN ('FOLLOWERS', 'FOLLOWERS_ONLY');
