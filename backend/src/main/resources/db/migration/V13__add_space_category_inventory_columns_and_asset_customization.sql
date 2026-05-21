-- v1.0 customization 시스템 — 카테고리별 개별 적용 + 커스터마이징 허용/불허 테마 지원.
-- 기능명세서 §1.1, API 명세서 "260502 갱신" 참고.

-- spaces 테이블에 카테고리별 inventory id 6개 추가.
-- currentThemeInventoryId(기존)는 전체 테마 적용 추적용으로 그대로 두고,
-- 아래 6개는 카테고리별 개별 항목 추적. 사용자가 카테고리별 PATCH 호출 시 갱신.
ALTER TABLE spaces
    ADD COLUMN IF NOT EXISTS current_wallpaper_inventory_id bigint NULL,
    ADD COLUMN IF NOT EXISTS current_floor_inventory_id bigint NULL,
    ADD COLUMN IF NOT EXISTS current_desk_inventory_id bigint NULL,
    ADD COLUMN IF NOT EXISTS current_dashboard_inventory_id bigint NULL,
    ADD COLUMN IF NOT EXISTS current_interior_light_inventory_id bigint NULL,
    ADD COLUMN IF NOT EXISTS current_exterior_light_inventory_id bigint NULL;

-- 책장은 별도 테이블(bookshelves.skin_inventory_id)에 이미 있음 — 추가 컬럼 불필요.

-- assets에 커스터마이징 허용 플래그.
-- false = 전체 테마 적용 후 일부 카테고리 변경 시 자동 해제(themeReset 응답).
-- 기본값 true (기존 자산은 모두 customization 허용).
ALTER TABLE assets
    ADD COLUMN IF NOT EXISTS allows_customization boolean NOT NULL DEFAULT TRUE;
