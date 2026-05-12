-- 저폴리 3D 프리뷰 번들 (preview.glb). 상세 페이지 3D 회전 미리보기용으로,
-- 본체(assetUrl)와 별개로 운영자가 올리거나 admin-web 이 wasm gltfpack 으로 자동 생성한다.
-- 본체 publish 게이트에는 포함하지 않는다(자동 생성 실패해도 본체는 공개 가능).

ALTER TABLE assets
    ADD COLUMN IF NOT EXISTS preview_bundle_url varchar(500),
    ADD COLUMN IF NOT EXISTS preview_bundle_content_type varchar(120),
    ADD COLUMN IF NOT EXISTS preview_bundle_size bigint,
    ADD COLUMN IF NOT EXISTS preview_bundle_hash varchar(160);
