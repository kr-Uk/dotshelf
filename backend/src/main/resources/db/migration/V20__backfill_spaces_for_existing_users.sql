-- 기존 가입자 중 활성 Space row가 없는 사용자에게 기본 공간을 보강한다.
-- 신규 가입자는 AuthService -> SpaceService.ensureDefaultSpace에서 생성되고,
-- 이 migration은 해당 로직이 배포되기 전 가입한 사용자에 대한 backfill이다.
INSERT INTO public.spaces (
    created_at,
    updated_at,
    current_theme_inventory_id,
    deleted_at,
    guestbook_visibility_scope,
    layout_version,
    like_count,
    owner_user_id,
    space_name,
    template_id,
    visibility_scope,
    visit_count,
    thumbnail_url
)
SELECT
    NOW(),
    NOW(),
    NULL,
    NULL,
    'PUBLIC',
    0,
    0,
    u.id,
    '나의 공간',
    NULL,
    'PUBLIC',
    0,
    NULL
FROM public.users u
WHERE u.deleted_at IS NULL
  AND UPPER(u.status) <> 'DELETED'
  AND NOT EXISTS (
      SELECT 1
      FROM public.spaces s
      WHERE s.owner_user_id = u.id
        AND s.deleted_at IS NULL
  );
