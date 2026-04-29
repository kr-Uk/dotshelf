-- 사용자 앱 v1.1 — 공간 주인이 받을 수 있는 포스트잇 스타일 화이트리스트.
-- expand-only 마이그레이션 (새 테이블 두 개, 기존 컬럼 무관).

CREATE TABLE space_guestbook_postit_whitelists (
    space_id    BIGINT      PRIMARY KEY,
    allow_all   BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE space_guestbook_postit_whitelist_assets (
    space_id   BIGINT NOT NULL,
    asset_id   BIGINT NOT NULL,
    PRIMARY KEY (space_id, asset_id),
    CONSTRAINT fk_whitelist_assets_space FOREIGN KEY (space_id)
        REFERENCES space_guestbook_postit_whitelists(space_id)
        ON DELETE CASCADE
);

CREATE INDEX idx_whitelist_assets_asset_id
    ON space_guestbook_postit_whitelist_assets(asset_id);
