-- Register Unity-built-in spooky room assets in the backend catalog.
-- These rows intentionally do not require asset_url. The Unity client can
-- resolve unity_resource_key to a prefab shipped in the build and only falls
-- back to S3 when a URL is provided.
-- Because the current Unity catalog exposes these built-in prefabs without a
-- download step, treat them as default/free assets and grant them to active
-- users so server ownership and client visibility stay consistent.

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
local_assets AS (
    SELECT *
    FROM (VALUES
        ('SPOOKY_BED', 'Spooky Bed', 'Assets/Arts/Models/spooky_bed.glb', 'floor'),
        ('SPOOKY_CHARACTER', 'Spooky Character', 'Assets/Arts/Models/spooky_character.glb', 'floor'),
        ('SPOOKY_DESK', 'Spooky Desk', 'Assets/Arts/Models/spooky_desk.glb', 'floor'),
        ('SPOOKY_LAMP', 'Spooky Lamp', 'Assets/Arts/Models/spooky_lamp.glb', 'floor'),
        ('SPOOKY_PUMPKIN', 'Spooky Pumpkin', 'Assets/Arts/Models/spooky_pumpkin.glb', 'floor'),
        ('SPOOKY_REPEATER', 'Spooky Repeater', 'Assets/Arts/Models/spooky_repeater.glb', 'floor'),
        ('SPOOKY_SKULL', 'Spooky Skull', 'Assets/Arts/Models/spooky_skull.glb', 'floor'),
        ('SPOOKY_WINDOW', 'Spooky Window', 'Assets/Arts/Models/spooky_window.glb', 'wall')
    ) AS item(code, name, unity_resource_key, placement)
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
    local_assets.code,
    local_assets.name,
    'NORMAL',
    'SPACE',
    'ASSET',
    'spooky',
    local_assets.unity_resource_key,
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
    'SPOOKY',
    local_assets.placement
FROM local_assets
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
    price_pages = excluded.price_pages,
    creator = excluded.creator,
    supplier = excluded.supplier,
    is_active = true,
    allows_customization = excluded.allows_customization,
    theme = excluded.theme,
    placement = excluded.placement;

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
    assets.id,
    'DEFAULT',
    now()
FROM public.users users
CROSS JOIN public.assets assets
WHERE assets.code IN (
        'SPOOKY_BED',
        'SPOOKY_CHARACTER',
        'SPOOKY_DESK',
        'SPOOKY_LAMP',
        'SPOOKY_PUMPKIN',
        'SPOOKY_REPEATER',
        'SPOOKY_SKULL',
        'SPOOKY_WINDOW'
    )
  AND users.deleted_at IS NULL
  AND UPPER(users.status) = 'ACTIVE'
  AND NOT EXISTS (
        SELECT 1
        FROM public.user_asset_inventories inventory
        WHERE inventory.user_id = users.id
          AND inventory.asset_id = assets.id
    )
ON CONFLICT (user_id, asset_id) DO NOTHING;
