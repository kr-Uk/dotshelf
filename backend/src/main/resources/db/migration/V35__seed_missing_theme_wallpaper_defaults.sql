-- Add missing wallpaper fixed defaults without changing the already-applied V34 checksum.

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
fixed_wallpapers AS (
    SELECT *
    FROM (VALUES
        ('FANTASY_WALLPAPER', 'Fantasy Wallpaper', 'WALLPAPER', 'FANTASY', 'Fantasy', 'Assets/App/Composition/Configs/RoomThemes/RoomTheme_Fantasy.asset#wallpaper'),
        ('SPACE_WALLPAPER', 'Space Wallpaper', 'WALLPAPER', 'SPACE', 'Space', 'Assets/App/Composition/Configs/RoomThemes/RoomTheme_Space.asset#wallpaper'),
        ('SPOOKY_WALLPAPER', 'Spooky Wallpaper', 'WALLPAPER', 'SPOOKY', 'Spooky', 'Assets/App/Composition/Configs/RoomThemes/RoomTheme_Spooky.asset#wallpaper'),
        ('VINTAGE_WALLPAPER', 'Vintage Wallpaper', 'WALLPAPER', 'VINTAGE', 'Vintage', 'Assets/App/Composition/Configs/RoomThemes/RoomTheme_Vintage.asset#wallpaper')
    ) AS item(code, name, asset_type, collection_code, theme, unity_resource_key)
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
    fixed_wallpapers.code,
    fixed_wallpapers.name,
    'NORMAL',
    'SPACE',
    fixed_wallpapers.asset_type,
    fixed_wallpapers.collection_code,
    fixed_wallpapers.unity_resource_key,
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
    fixed_wallpapers.theme,
    null
FROM fixed_wallpapers
CROSS JOIN interior_category
ON CONFLICT (code) DO UPDATE SET
    updated_at = now(),
    category_id = excluded.category_id,
    name = excluded.name,
    tier = excluded.tier,
    asset_scope = excluded.asset_scope,
    asset_type = excluded.asset_type,
    collection_code = excluded.collection_code,
    unity_resource_key = COALESCE(assets.unity_resource_key, excluded.unity_resource_key),
    asset_url = COALESCE(assets.asset_url, excluded.asset_url),
    content_type = COALESCE(assets.content_type, excluded.content_type),
    file_size = COALESCE(assets.file_size, excluded.file_size),
    file_hash = COALESCE(assets.file_hash, excluded.file_hash),
    version = excluded.version,
    preview_image_url = COALESCE(assets.preview_image_url, excluded.preview_image_url),
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
        current_wallpaper_asset.id AS current_wallpaper_asset_id,
        wallpaper_asset.id AS wallpaper_asset_id
    FROM theme_map
    JOIN public.assets theme
      ON theme.code = theme_map.theme_code
    LEFT JOIN public.assets current_wallpaper_asset
      ON current_wallpaper_asset.id = pg_temp.safe_asset_compatible_target_id(
            pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid),
            'wallpaper'
         )
     AND current_wallpaper_asset.asset_type = 'WALLPAPER'
     AND current_wallpaper_asset.is_active = true
    LEFT JOIN public.assets wallpaper_asset
      ON wallpaper_asset.code = theme_map.collection_code || '_WALLPAPER'
     AND wallpaper_asset.asset_type = 'WALLPAPER'
     AND wallpaper_asset.is_active = true
),
theme_defaults AS (
    SELECT
        theme_asset_id,
        jsonb_strip_nulls(
            current_targets
            || jsonb_build_object('wallpaper', COALESCE(current_wallpaper_asset_id, wallpaper_asset_id))
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
        pg_temp.safe_asset_compatible_target_id(
            pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid),
            'wallpaper'
        ) AS wallpaper_asset_id
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
    SELECT DISTINCT theme_defaults.wallpaper_asset_id AS asset_id
    FROM theme_defaults
    JOIN public.assets wallpaper_asset
      ON wallpaper_asset.id = theme_defaults.wallpaper_asset_id
     AND wallpaper_asset.asset_type = 'WALLPAPER'
     AND wallpaper_asset.is_active = true
    WHERE theme_defaults.wallpaper_asset_id IS NOT NULL
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
        pg_temp.safe_asset_compatible_target_id(
            pg_temp.safe_asset_compatible_targets_jsonb(theme.compatible_targets::oid),
            'wallpaper'
        ) AS wallpaper_asset_id
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
        wallpaper_inventory.id AS wallpaper_inventory_id
    FROM public.spaces space
    JOIN public.user_asset_inventories theme_inventory
      ON theme_inventory.id = space.current_theme_inventory_id
     AND theme_inventory.user_id = space.owner_user_id
    JOIN theme_defaults
      ON theme_defaults.theme_asset_id = theme_inventory.asset_id
    JOIN public.assets wallpaper_asset
      ON wallpaper_asset.id = theme_defaults.wallpaper_asset_id
     AND wallpaper_asset.asset_type = 'WALLPAPER'
     AND wallpaper_asset.is_active = true
    JOIN public.user_asset_inventories wallpaper_inventory
      ON wallpaper_inventory.user_id = space.owner_user_id
     AND wallpaper_inventory.asset_id = wallpaper_asset.id
    WHERE space.deleted_at IS NULL
      AND space.current_theme_inventory_id IS NOT NULL
)
UPDATE public.spaces space
SET
    current_wallpaper_inventory_id = COALESCE(space.current_wallpaper_inventory_id, space_defaults.wallpaper_inventory_id),
    updated_at = now()
FROM space_defaults
WHERE space.id = space_defaults.space_id;

DROP FUNCTION pg_temp.safe_asset_compatible_target_id(jsonb, text);
DROP FUNCTION pg_temp.safe_asset_compatible_targets_jsonb(oid);
