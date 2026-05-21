-- Register Unity-built-in vintage room assets in the backend catalog.
-- These assets are shipped with the Unity client and are referenced by saved
-- layoutPayload as path:Assets/Arts/Models/vintage_*.glb. Keep them free/default
-- so other-space rendering can resolve manifests without requiring S3 downloads.

WITH interior_category AS (
    SELECT id
    FROM public.asset_categories
    WHERE code = 'INTERIOR'
    LIMIT 1
),
local_assets AS (
    SELECT *
    FROM (VALUES
        ('VINTAGE_DASHBOARD', 'Vintage Dashboard', 'Assets/Arts/Models/vintage_dashboard.glb', 'floor'),
        ('VINTAGE_FRAME', 'Vintage Frame', 'Assets/Arts/Models/vintage_frame.glb', 'wall'),
        ('VINTAGE_LAMP_MINT', 'Vintage Lamp Mint', 'Assets/Arts/Models/vintage_lamp_mint.glb', 'floor'),
        ('VINTAGE_RUG', 'Vintage Rug', 'Assets/Arts/Models/vintage_rug.glb', 'floor'),
        ('VINTAGE_TABLE', 'Vintage Table', 'Assets/Arts/Models/vintage_table.glb', 'floor'),
        ('VINTAGE_TABLE_MINI', 'Vintage Table Mini', 'Assets/Arts/Models/vintage_table_mini.glb', 'floor')
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
    'vintage',
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
    'VINTAGE',
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
    is_default = true,
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
        'VINTAGE_DASHBOARD',
        'VINTAGE_FRAME',
        'VINTAGE_LAMP_MINT',
        'VINTAGE_RUG',
        'VINTAGE_TABLE',
        'VINTAGE_TABLE_MINI'
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
