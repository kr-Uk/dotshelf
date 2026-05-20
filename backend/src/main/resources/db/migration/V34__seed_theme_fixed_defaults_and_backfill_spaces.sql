-- Add server-side fixed category defaults for Unity built-in themes and
-- backfill existing spaces so /v1/spaces/me is authoritative after RESET.

INSERT INTO public.asset_categories (
    created_at,
    updated_at,
    code,
    name,
    display_order,
    is_active,
    parent_category_id
) SELECT
    now(),
    now(),
    'INTERIOR',
    'Interior',
    COALESCE(MAX(display_order), 0) + 1,
    true,
    null
FROM public.asset_categories
ON CONFLICT (code) DO UPDATE SET
    updated_at = now(),
    name = excluded.name,
    is_active = true;

WITH interior_category AS (
    SELECT id
    FROM public.asset_categories
    WHERE code = 'INTERIOR'
    LIMIT 1
),
fixed_assets AS (
    SELECT *
    FROM (VALUES
        ('DEEPSEA_WALLPAPER', 'DeepSea Wallpaper', 'WALLPAPER', 'DEEPSEA', 'DeepSea'),
        ('DEEPSEA_FLOOR', 'DeepSea Floor', 'FLOOR', 'DEEPSEA', 'DeepSea'),
        ('DEEPSEA_DASHBOARD_SKIN', 'DeepSea Dashboard Skin', 'DASHBOARD_SKIN', 'DEEPSEA', 'DeepSea'),
        ('DEEPSEA_INTERIOR_LIGHT', 'DeepSea Interior Light', 'INTERIOR_LIGHT', 'DEEPSEA', 'DeepSea'),
        ('DEEPSEA_EXTERIOR_LIGHT', 'DeepSea Exterior Light', 'EXTERIOR_LIGHT', 'DEEPSEA', 'DeepSea'),
        ('FANTASY_FLOOR', 'Fantasy Floor', 'FLOOR', 'FANTASY', 'Fantasy'),
        ('FANTASY_DASHBOARD_SKIN', 'Fantasy Dashboard Skin', 'DASHBOARD_SKIN', 'FANTASY', 'Fantasy'),
        ('FANTASY_INTERIOR_LIGHT', 'Fantasy Interior Light', 'INTERIOR_LIGHT', 'FANTASY', 'Fantasy'),
        ('FANTASY_EXTERIOR_LIGHT', 'Fantasy Exterior Light', 'EXTERIOR_LIGHT', 'FANTASY', 'Fantasy'),
        ('SPACE_FLOOR', 'Space Floor', 'FLOOR', 'SPACE', 'Space'),
        ('SPACE_DASHBOARD_SKIN', 'Space Dashboard Skin', 'DASHBOARD_SKIN', 'SPACE', 'Space'),
        ('SPACE_INTERIOR_LIGHT', 'Space Interior Light', 'INTERIOR_LIGHT', 'SPACE', 'Space'),
        ('SPACE_EXTERIOR_LIGHT', 'Space Exterior Light', 'EXTERIOR_LIGHT', 'SPACE', 'Space'),
        ('SPOOKY_FLOOR', 'Spooky Floor', 'FLOOR', 'SPOOKY', 'Spooky'),
        ('SPOOKY_DASHBOARD_SKIN', 'Spooky Dashboard Skin', 'DASHBOARD_SKIN', 'SPOOKY', 'Spooky'),
        ('SPOOKY_INTERIOR_LIGHT', 'Spooky Interior Light', 'INTERIOR_LIGHT', 'SPOOKY', 'Spooky'),
        ('SPOOKY_EXTERIOR_LIGHT', 'Spooky Exterior Light', 'EXTERIOR_LIGHT', 'SPOOKY', 'Spooky'),
        ('VINTAGE_FLOOR', 'Vintage Floor', 'FLOOR', 'VINTAGE', 'Vintage'),
        ('VINTAGE_DASHBOARD_SKIN', 'Vintage Dashboard Skin', 'DASHBOARD_SKIN', 'VINTAGE', 'Vintage'),
        ('VINTAGE_INTERIOR_LIGHT', 'Vintage Interior Light', 'INTERIOR_LIGHT', 'VINTAGE', 'Vintage'),
        ('VINTAGE_EXTERIOR_LIGHT', 'Vintage Exterior Light', 'EXTERIOR_LIGHT', 'VINTAGE', 'Vintage')
    ) AS item(code, name, asset_type, collection_code, theme)
)
INSERT INTO public.assets (
    created_at,
    updated_at,
    category_id,
    code,
    name,
    tier,
    asset_scope,
    asset_type,
    collection_code,
    unity_resource_key,
    asset_url,
    content_type,
    file_size,
    file_hash,
    version,
    preview_image_url,
    price_pages,
    creator,
    supplier,
    compatible_targets,
    released_at,
    valid_until,
    is_default,
    is_active,
    allows_customization,
    theme,
    placement
)
SELECT
    now(),
    now(),
    interior_category.id,
    fixed_assets.code,
    fixed_assets.name,
    'NORMAL',
    'SPACE',
    fixed_assets.asset_type,
    fixed_assets.collection_code,
    null,
    null,
    null,
    null,
    null,
    1,
    null,
    0,
    'DotShelf',
    'DotShelf Studio',
    null,
    now(),
    null,
    true,
    true,
    true,
    fixed_assets.theme,
    null
FROM fixed_assets
CROSS JOIN interior_category
ON CONFLICT (code) DO UPDATE SET
    updated_at = now(),
    category_id = excluded.category_id,
    name = excluded.name,
    tier = excluded.tier,
    asset_scope = excluded.asset_scope,
    asset_type = excluded.asset_type,
    collection_code = excluded.collection_code,
    unity_resource_key = excluded.unity_resource_key,
    asset_url = excluded.asset_url,
    content_type = excluded.content_type,
    file_size = excluded.file_size,
    file_hash = excluded.file_hash,
    version = excluded.version,
    preview_image_url = excluded.preview_image_url,
    price_pages = excluded.price_pages,
    creator = excluded.creator,
    supplier = excluded.supplier,
    released_at = excluded.released_at,
    valid_until = excluded.valid_until,
    is_default = true,
    is_active = true,
    allows_customization = excluded.allows_customization,
    theme = excluded.theme,
    placement = excluded.placement;

CREATE OR REPLACE FUNCTION pg_temp.safe_asset_compatible_targets_jsonb(value oid)
RETURNS jsonb
LANGUAGE plpgsql
AS $$
BEGIN
    IF value IS NULL THEN
        RETURN '{}'::jsonb;
    END IF;
    RETURN convert_from(lo_get(value), 'UTF8')::jsonb;
EXCEPTION WHEN OTHERS THEN
    RETURN '{}'::jsonb;
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.safe_asset_compatible_target_id(value jsonb, target_key text)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    raw_value text;
BEGIN
    IF value IS NULL THEN
        RETURN null;
    END IF;

    raw_value := value ->> target_key;
    IF raw_value IS NULL OR raw_value !~ '^[0-9]+$' THEN
        RETURN null;
    END IF;

    RETURN raw_value::bigint;
END;
$$;

WITH theme_map AS (
    SELECT *
    FROM (VALUES
        ('THEME_DEEPSEA', 'DEEPSEA'),
        ('THEME_FANTASY', 'FANTASY'),
        ('THEME_SPACE', 'SPACE'),
        ('THEME_SPOOKY', 'SPOOKY'),
        ('THEME_VINTAGE', 'VINTAGE')
    ) AS item(theme_code, collection_code)
),
theme_targets AS (
    SELECT
        theme.id AS theme_asset_id,
        pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid) AS current_targets,
        wallpaper_asset.id AS wallpaper_asset_id,
        floor_asset.id AS floor_asset_id,
        dashboard_asset.id AS dashboard_asset_id,
        interior_light_asset.id AS interior_light_asset_id,
        exterior_light_asset.id AS exterior_light_asset_id
    FROM theme_map
    JOIN public.assets theme
      ON theme.code = theme_map.theme_code
    LEFT JOIN public.assets wallpaper_asset
      ON wallpaper_asset.code = theme_map.collection_code || '_WALLPAPER'
    LEFT JOIN public.assets floor_asset
      ON floor_asset.code = theme_map.collection_code || '_FLOOR'
    LEFT JOIN public.assets dashboard_asset
      ON dashboard_asset.code = theme_map.collection_code || '_DASHBOARD_SKIN'
    LEFT JOIN public.assets interior_light_asset
      ON interior_light_asset.code = theme_map.collection_code || '_INTERIOR_LIGHT'
    LEFT JOIN public.assets exterior_light_asset
      ON exterior_light_asset.code = theme_map.collection_code || '_EXTERIOR_LIGHT'
),
theme_defaults AS (
    SELECT
        theme_asset_id,
        jsonb_strip_nulls(
            current_targets
            || jsonb_build_object(
                'wallpaper', COALESCE(pg_temp.safe_asset_compatible_target_id(current_targets, 'wallpaper'), wallpaper_asset_id),
                'floor', COALESCE(pg_temp.safe_asset_compatible_target_id(current_targets, 'floor'), floor_asset_id),
                'dashboard', COALESCE(pg_temp.safe_asset_compatible_target_id(current_targets, 'dashboard'), dashboard_asset_id),
                'interiorLight', COALESCE(pg_temp.safe_asset_compatible_target_id(current_targets, 'interiorLight'), interior_light_asset_id),
                'exteriorLight', COALESCE(pg_temp.safe_asset_compatible_target_id(current_targets, 'exteriorLight'), exterior_light_asset_id)
            )
        ) AS compatible_targets
    FROM theme_targets
)
UPDATE public.assets theme
SET
    compatible_targets = lo_from_bytea(0, convert_to(theme_defaults.compatible_targets::text, 'UTF8')),
    updated_at = now()
FROM theme_defaults
WHERE theme.id = theme_defaults.theme_asset_id;

WITH theme_defaults AS (
    SELECT
        theme.id AS theme_asset_id,
        pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid) AS compatible_targets
    FROM public.assets theme
    WHERE theme.code IN (
        'THEME_DEEPSEA',
        'THEME_FANTASY',
        'THEME_SPACE',
        'THEME_SPOOKY',
        'THEME_VINTAGE'
    )
),
default_asset_ids AS (
    SELECT DISTINCT (value #>> '{}')::bigint AS asset_id
    FROM theme_defaults
    CROSS JOIN LATERAL jsonb_each(theme_defaults.compatible_targets) AS entries(key, value)
    WHERE jsonb_typeof(value) = 'number'
)
INSERT INTO public.user_asset_inventories (
    created_at,
    updated_at,
    user_id,
    asset_id,
    grant_type,
    acquired_at
)
SELECT
    now(),
    now(),
    users.id,
    default_asset_ids.asset_id,
    'DEFAULT',
    now()
FROM public.users users
CROSS JOIN default_asset_ids
JOIN public.assets assets
  ON assets.id = default_asset_ids.asset_id
WHERE users.deleted_at IS NULL
  AND UPPER(users.status) = 'ACTIVE'
  AND NOT EXISTS (
        SELECT 1
        FROM public.user_asset_inventories inventory
        WHERE inventory.user_id = users.id
          AND inventory.asset_id = default_asset_ids.asset_id
    )
ON CONFLICT (user_id, asset_id) DO NOTHING;

WITH theme_defaults AS (
    SELECT
        theme.id AS theme_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'wallpaper') AS wallpaper_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'floor') AS floor_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'desk') AS desk_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'dashboard') AS dashboard_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'bookshelfSkinDefault') AS bookshelf_skin_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'interiorLight') AS interior_light_asset_id,
        pg_temp.safe_asset_compatible_target_id(pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid), 'exteriorLight') AS exterior_light_asset_id
    FROM public.assets theme
    WHERE theme.code IN (
        'THEME_DEEPSEA',
        'THEME_FANTASY',
        'THEME_SPACE',
        'THEME_SPOOKY',
        'THEME_VINTAGE'
    )
),
space_defaults AS (
    SELECT
        space.id AS space_id,
        wallpaper_inventory.id AS wallpaper_inventory_id,
        floor_inventory.id AS floor_inventory_id,
        desk_inventory.id AS desk_inventory_id,
        dashboard_inventory.id AS dashboard_inventory_id,
        bookshelf_skin_inventory.id AS bookshelf_skin_inventory_id,
        interior_light_inventory.id AS interior_light_inventory_id,
        exterior_light_inventory.id AS exterior_light_inventory_id
    FROM public.spaces space
    JOIN public.user_asset_inventories theme_inventory
      ON theme_inventory.id = space.current_theme_inventory_id
     AND theme_inventory.user_id = space.owner_user_id
    JOIN theme_defaults
      ON theme_defaults.theme_asset_id = theme_inventory.asset_id
    LEFT JOIN public.user_asset_inventories wallpaper_inventory
      ON wallpaper_inventory.user_id = space.owner_user_id
     AND wallpaper_inventory.asset_id = theme_defaults.wallpaper_asset_id
    LEFT JOIN public.user_asset_inventories floor_inventory
      ON floor_inventory.user_id = space.owner_user_id
     AND floor_inventory.asset_id = theme_defaults.floor_asset_id
    LEFT JOIN public.user_asset_inventories desk_inventory
      ON desk_inventory.user_id = space.owner_user_id
     AND desk_inventory.asset_id = theme_defaults.desk_asset_id
    LEFT JOIN public.user_asset_inventories dashboard_inventory
      ON dashboard_inventory.user_id = space.owner_user_id
     AND dashboard_inventory.asset_id = theme_defaults.dashboard_asset_id
    LEFT JOIN public.user_asset_inventories bookshelf_skin_inventory
      ON bookshelf_skin_inventory.user_id = space.owner_user_id
     AND bookshelf_skin_inventory.asset_id = theme_defaults.bookshelf_skin_asset_id
    LEFT JOIN public.user_asset_inventories interior_light_inventory
      ON interior_light_inventory.user_id = space.owner_user_id
     AND interior_light_inventory.asset_id = theme_defaults.interior_light_asset_id
    LEFT JOIN public.user_asset_inventories exterior_light_inventory
      ON exterior_light_inventory.user_id = space.owner_user_id
     AND exterior_light_inventory.asset_id = theme_defaults.exterior_light_asset_id
    WHERE space.deleted_at IS NULL
      AND space.current_theme_inventory_id IS NOT NULL
)
UPDATE public.spaces space
SET
    current_wallpaper_inventory_id = COALESCE(space.current_wallpaper_inventory_id, space_defaults.wallpaper_inventory_id),
    current_floor_inventory_id = COALESCE(space.current_floor_inventory_id, space_defaults.floor_inventory_id),
    current_desk_inventory_id = COALESCE(space.current_desk_inventory_id, space_defaults.desk_inventory_id),
    current_dashboard_inventory_id = COALESCE(space.current_dashboard_inventory_id, space_defaults.dashboard_inventory_id),
    current_bookshelf_skin_inventory_id = COALESCE(space.current_bookshelf_skin_inventory_id, space_defaults.bookshelf_skin_inventory_id),
    current_interior_light_inventory_id = COALESCE(space.current_interior_light_inventory_id, space_defaults.interior_light_inventory_id),
    current_exterior_light_inventory_id = COALESCE(space.current_exterior_light_inventory_id, space_defaults.exterior_light_inventory_id),
    updated_at = now()
FROM space_defaults
WHERE space.id = space_defaults.space_id;

DROP FUNCTION pg_temp.safe_asset_compatible_target_id(jsonb, text);
DROP FUNCTION pg_temp.safe_asset_compatible_targets_jsonb(oid);
