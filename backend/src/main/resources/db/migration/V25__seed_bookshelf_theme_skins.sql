-- Seed the Unity bookshelf theme skins used by the runtime design switcher.
-- Bookshelf placement remains a Unity-side layout concern; the backend stores
-- the selected skin through bookshelves.skin_inventory_id.

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
    'BOOKSHELF',
    'Bookshelf',
    COALESCE(MAX(display_order), 0) + 1,
    true,
    null
FROM public.asset_categories
ON CONFLICT (code) DO NOTHING;

WITH bookshelf_category AS (
    SELECT id
    FROM public.asset_categories
    WHERE code = 'BOOKSHELF'
    LIMIT 1
),
theme_skins AS (
    SELECT
        'BOOKSHELF_PASTEL_CABIN_SKIN'::varchar(80) AS code,
        'Pastel Cabin Bookshelf Skin'::varchar(100) AS name,
        'bookshelf/pastel_cabin_skin'::varchar(150) AS unity_resource_key,
        'pastel-cabin'::varchar(80) AS collection_code,
        null::varchar(500) AS preview_image_url,
        now() - interval '1 day' AS released_at
    UNION ALL
    SELECT
        'BOOKSHELF_UNDERWATER_SHELL_SKIN'::varchar(80),
        'Underwater Shell Bookshelf Skin'::varchar(100),
        'bookshelf/underwater_shell_skin'::varchar(150),
        'underwater-shell'::varchar(80),
        null::varchar(500),
        now() - interval '1 day'
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
    is_active
)
SELECT
    now(),
    now(),
    bookshelf_category.id,
    theme_skins.code,
    theme_skins.name,
    'NORMAL',
    'BOOKSHELF',
    'BOOKSHELF_SKIN',
    theme_skins.collection_code,
    theme_skins.unity_resource_key,
    null,
    null,
    null,
    null,
    1,
    theme_skins.preview_image_url,
    0,
    'DotShelf',
    'DotShelf Studio',
    null,
    theme_skins.released_at,
    null,
    false,
    true
FROM theme_skins
CROSS JOIN bookshelf_category
ON CONFLICT (code) DO UPDATE SET
    updated_at = now(),
    category_id = excluded.category_id,
    name = excluded.name,
    tier = excluded.tier,
    asset_scope = excluded.asset_scope,
    asset_type = excluded.asset_type,
    collection_code = excluded.collection_code,
    unity_resource_key = excluded.unity_resource_key,
    preview_image_url = excluded.preview_image_url,
    price_pages = excluded.price_pages,
    creator = excluded.creator,
    supplier = excluded.supplier,
    released_at = excluded.released_at,
    is_active = true;

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
        'BOOKSHELF_PASTEL_CABIN_SKIN',
        'BOOKSHELF_UNDERWATER_SHELL_SKIN'
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
