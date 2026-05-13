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
--
-- 주의: PostgreSQL 전용 함수 (lo_get, convert_from, jsonb_typeof) 사용.
--       이 프로젝트는 PostgreSQL only 라 OK. 테스트는 application-test.yml 의 flyway.enabled=false +
--       ddl-auto=create-drop 정책으로 V27 SQL 자체가 실행되지 않으므로 H2 호환 무관.

ALTER TABLE assets ADD COLUMN theme VARCHAR(30);
ALTER TABLE assets ADD COLUMN placement VARCHAR(10);

-- LOB fetch 를 한 row 당 1회만 하기 위해 CTE 로 parsed jsonb 를 캐싱.
-- placement 값은 BE/DB 가 새로 강제할 도메인 ('floor'|'wall') 안에 들어오는 값만 통과시킨다 —
-- legacy DB 에 손으로 입력된 비표준 값이 있다면 NULL 로 떨어뜨려 CHECK 제약 추가가 실패하지 않게 한다.
WITH parsed AS (
    SELECT id,
           (convert_from(lo_get(compatible_targets::oid), 'UTF8'))::jsonb AS doc
    FROM assets
    WHERE compatible_targets IS NOT NULL
)
UPDATE assets a
SET
    theme = parsed.doc -> 'themes' ->> 0,
    placement = CASE
        WHEN parsed.doc -> 'placement' ->> 0 IN ('floor', 'wall')
        THEN parsed.doc -> 'placement' ->> 0
        ELSE NULL
    END
FROM parsed
WHERE a.id = parsed.id
  AND jsonb_typeof(parsed.doc) = 'object';

-- 백필 결과 로그 — 운영자가 어드민에서 NULL row 를 재설정해야 하는 갯수를 즉시 파악할 수 있게.
-- backfilled: 객체 형태 LOB 에서 theme/placement 둘 중 하나라도 추출된 row 수
-- legacy_skipped: compatible_targets 는 있는데 (배열 / 비표준 schema) backfill 결과 둘 다 NULL 인 row 수
DO $$
DECLARE
    backfilled INT;
    legacy_skipped INT;
BEGIN
    SELECT COUNT(*) INTO backfilled
    FROM assets
    WHERE theme IS NOT NULL OR placement IS NOT NULL;

    SELECT COUNT(*) INTO legacy_skipped
    FROM assets
    WHERE compatible_targets IS NOT NULL
      AND theme IS NULL
      AND placement IS NULL;

    RAISE NOTICE 'V27 backfill: backfilled=% legacy_skipped=% (legacy row 는 admin-web edit 폼에서 재설정 필요)',
        backfilled, legacy_skipped;
END $$;

-- 검색용 인덱스. theme/placement 둘 다 자주 필터링되므로 단일 컬럼 인덱스 두 개.
-- 복합 인덱스는 사용 빈도 / 카디널리티 데이터 확보 후 가서 추가.
CREATE INDEX IF NOT EXISTS idx_assets_theme ON assets(theme);
CREATE INDEX IF NOT EXISTS idx_assets_placement ON assets(placement);

-- placement 값 도메인 가드. floor/wall 외 값이 들어오면 BE 가 잘못 보낸 거.
-- theme 은 FE THEME_OPTIONS 가 늘어날 여지 있어 enum 제약 없이 둔다.
ALTER TABLE assets
    ADD CONSTRAINT ck_assets_placement_values
    CHECK (placement IS NULL OR placement IN ('floor', 'wall'));
