-- 사용자 앱 v1.0 갱신 — 화이트리스트 무결성 보강 (MR #60 #7).
-- V9에서 누락된 spaces/assets 외래키를 expand-only로 추가.
-- 공간/포스트잇 자산이 삭제되면 화이트리스트도 함께 정리되도록 ON DELETE CASCADE.

ALTER TABLE space_guestbook_postit_whitelists
    ADD CONSTRAINT fk_whitelist_space
        FOREIGN KEY (space_id) REFERENCES spaces(id)
        ON DELETE CASCADE;

ALTER TABLE space_guestbook_postit_whitelist_assets
    ADD CONSTRAINT fk_whitelist_assets_asset
        FOREIGN KEY (asset_id) REFERENCES assets(id)
        ON DELETE CASCADE;
