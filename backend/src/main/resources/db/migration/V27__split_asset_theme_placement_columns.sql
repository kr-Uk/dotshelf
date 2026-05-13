-- 일반 Asset 의 호환성 메타(테마/배치) 를 compatible_targets JSON LOB 에서 빼서
-- 평범한 컬럼으로 분리한다. 검색/필터링 시 인덱스 사용 가능하게 + LOB OID 인다이렉션 제거.
--
-- compatible_targets 컬럼 자체는 살려둔다 — ThemeAsset 의 자식 자산 ID 매핑
-- ({"wallpaper":11,"floor":12,...}) 라는 전혀 다른 schema 가 같은 컬럼을 쓰고 있어 못 지움.
-- 일반 Asset 은 신규 등록부터 compatible_targets 안 채우고 theme/placement 컬럼만 채운다.
--
-- 백필 정책:
--   - compatible_targets 가 객체 형태 ({themes:[...],placement:[...]}): 첫 원소 추출해 컬럼에 set
--   - 배열 형태 (legacy seed: ["BOOKSHELF"], ["DESK","FLOOR"] 등): theme/placement 둘 다 NULL
--     (legacy 데이터는 다른 의미 체계라 깔끔하게 추출 불가. 운영자가 edit 폼에서 재설정하면 됨.)
--   - NULL / ThemeAsset 자식 매핑: theme/placement NULL

ALTER TABLE assets ADD COLUMN theme VARCHAR(30);
ALTER TABLE assets ADD COLUMN placement VARCHAR(10);

UPDATE assets
SET
    theme = (
        convert_from(lo_get(compatible_targets::oid), 'UTF8')::jsonb -> 'themes' ->> 0
    ),
    placement = (
        convert_from(lo_get(compatible_targets::oid), 'UTF8')::jsonb -> 'placement' ->> 0
    )
WHERE compatible_targets IS NOT NULL
  AND jsonb_typeof(convert_from(lo_get(compatible_targets::oid), 'UTF8')::jsonb) = 'object';

-- 검색용 인덱스. theme/placement 둘 다 자주 필터링되므로 단일 컬럼 인덱스 두 개.
-- 복합 인덱스는 사용 빈도 / 카디널리티 데이터 확보 후 가서 추가.
CREATE INDEX IF NOT EXISTS idx_assets_theme ON assets(theme);
CREATE INDEX IF NOT EXISTS idx_assets_placement ON assets(placement);

-- placement 값 도메인 가드. floor/wall 외 값이 들어오면 BE 가 잘못 보낸 거.
-- theme 은 FE THEME_OPTIONS 가 늘어날 여지 있어 enum 제약 없이 둔다.
ALTER TABLE assets
    ADD CONSTRAINT ck_assets_placement_values
    CHECK (placement IS NULL OR placement IN ('floor', 'wall'));
