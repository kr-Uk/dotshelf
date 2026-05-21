-- 사용자 앱 v1.0 갱신 — 독후감 조회수 컬럼 (대시보드 정렬 "조회수순" 지원).
-- 증가 로직은 follow-up. 이번 마이그레이션은 컬럼 expand만, 정렬 키만 안전하게 0으로 시작.

ALTER TABLE reading_records
    ADD COLUMN view_count INTEGER NOT NULL DEFAULT 0;
