BEGIN;

-- DotShelf mock data seed for a freshly migrated development/staging database.
-- flyway_schema_history is intentionally excluded; this script only inserts app data.

INSERT INTO public.users (
    id, created_at, updated_at, email, nickname, profile_image_url, introduction,
    role, status, is_onboarded, last_login_at, deleted_at
) VALUES
    (1001, now(), now(), 'minji.mock@dotshelf.dev', 'minji.reader', 'https://cdn.dotshelf.dev/mock/profiles/minji.png', '에세이와 기록을 좋아하는 독자입니다.', 'USER', 'ACTIVE', true, now() - interval '1 hour', null),
    (1002, now(), now(), 'junho.mock@dotshelf.dev', 'junho.shelf', 'https://cdn.dotshelf.dev/mock/profiles/junho.png', 'SF와 개발서를 함께 읽습니다.', 'USER', 'ACTIVE', true, now() - interval '2 hours', null),
    (1003, now(), now(), 'seoyeon.mock@dotshelf.dev', 'seoyeon.space', 'https://cdn.dotshelf.dev/mock/profiles/seoyeon.png', '다른 사람의 서재 구경을 좋아합니다.', 'USER', 'ACTIVE', true, now() - interval '3 hours', null),
    (1004, now(), now(), 'admin.mock@dotshelf.dev', 'dotshelf.admin', 'https://cdn.dotshelf.dev/mock/profiles/admin.png', '관리자 화면 검증용 계정입니다.', 'ADMIN', 'ACTIVE', true, now() - interval '30 minutes', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_social_accounts (
    id, created_at, updated_at, user_id, provider, provider_user_id, email_at_provider, linked_at
) VALUES
    (1001, now(), now(), 1001, 'GOOGLE', 'mock-google-minji-1001', 'minji.mock@dotshelf.dev', now() - interval '10 days'),
    (1002, now(), now(), 1002, 'KAKAO', 'mock-kakao-junho-1002', 'junho.mock@dotshelf.dev', now() - interval '9 days'),
    (1003, now(), now(), 1003, 'APPLE', 'mock-apple-seoyeon-1003', 'seoyeon.mock@dotshelf.dev', now() - interval '8 days'),
    (1004, now(), now(), 1004, 'GOOGLE', 'mock-google-admin-1004', 'admin.mock@dotshelf.dev', now() - interval '7 days')
ON CONFLICT DO NOTHING;

INSERT INTO public.auth_refresh_tokens (
    id, created_at, updated_at, user_id, token_hash, device_id, user_agent, ip_address,
    issued_at, expires_at, revoked_at, replaced_by_token_id, reuse_detected_at
) VALUES
    (1001, now(), now(), 1001, 'mock-refresh-hash-minji-1001', 'mock-web-minji', 'DotShelf Mock Browser', '10.0.0.11', now() - interval '1 hour', now() + interval '29 days', null, null, null),
    (1002, now(), now(), 1002, 'mock-refresh-hash-junho-1002', 'mock-unity-junho', 'DotShelf Unity Mock', '10.0.0.12', now() - interval '2 hours', now() + interval '28 days', null, null, null),
    (1003, now(), now(), 1003, 'mock-refresh-hash-seoyeon-1003', 'mock-ios-seoyeon', 'DotShelf iOS Mock', '10.0.0.13', now() - interval '3 hours', now() + interval '27 days', null, null, null)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_notification_settings (
    id, created_at, updated_at, user_id, notify_new_guestbook, notify_guestbook_reply,
    notify_reading_record_reaction, notify_new_follower, notify_book_added_by_others
) VALUES
    (1001, now(), now(), 1001, true, true, true, true, true),
    (1002, now(), now(), 1002, true, true, false, true, true),
    (1003, now(), now(), 1003, true, false, true, true, false),
    (1004, now(), now(), 1004, true, true, true, true, true)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_feed_settings (
    id, created_at, updated_at, user_id, show_book_added, show_reading_record,
    show_space_visit, show_guestbook, show_reading_record_comment
) VALUES
    (1001, now(), now(), 1001, true, true, true, true, true),
    (1002, now(), now(), 1002, true, true, true, false, true),
    (1003, now(), now(), 1003, true, false, true, true, true),
    (1004, now(), now(), 1004, true, true, true, true, true)
ON CONFLICT DO NOTHING;

INSERT INTO public.page_wallets (
    id, created_at, updated_at, user_id, balance_pages
) VALUES
    (1001, now(), now(), 1001, 5200),
    (1002, now(), now(), 1002, 3400),
    (1003, now(), now(), 1003, 7800),
    (1004, now(), now(), 1004, 99999)
ON CONFLICT DO NOTHING;

INSERT INTO public.follows (
    id, created_at, updated_at, follower_user_id, following_user_id
) VALUES
    (1001, now(), now(), 1001, 1002),
    (1002, now(), now(), 1001, 1003),
    (1003, now(), now(), 1002, 1001),
    (1004, now(), now(), 1002, 1003),
    (1005, now(), now(), 1003, 1001),
    (1006, now(), now(), 1003, 1002)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_blocks (
    id, created_at, updated_at, blocker_user_id, blocked_user_id
) VALUES
    (1001, now(), now(), 1001, 1004),
    (1002, now(), now(), 1002, 1004),
    (1003, now(), now(), 1003, 1004)
ON CONFLICT DO NOTHING;

INSERT INTO public.asset_categories (
    id, created_at, updated_at, code, name, display_order, is_active, parent_category_id
) VALUES
    (1001, now(), now(), 'BOOKSHELF', '책장', 1, true, null),
    (1002, now(), now(), 'INTERIOR', '인테리어 오브젝트', 2, true, null),
    (1003, now(), now(), 'THEME', '공간 테마', 3, true, null)
ON CONFLICT DO NOTHING;

INSERT INTO public.assets (
    id, created_at, updated_at, category_id, code, name, tier, asset_scope, asset_type,
    unity_resource_key, asset_url, preview_image_url, price_pages, creator, supplier,
    compatible_targets, released_at, valid_until, is_default, is_active
) VALUES
    (1001, now(), now(), 1001, 'BOOKSHELF_WALNUT_BODY', '월넛 기본 책장', 'NORMAL', 'BOOKSHELF', 'BOOKSHELF_BODY', 'bookshelf/walnut_body', null, 'https://cdn.dotshelf.dev/mock/assets/bookshelf_walnut.png', 0, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["BOOKSHELF"]', 'UTF8')), now() - interval '20 days', null, true, true),
    (1002, now(), now(), 1001, 'BOOKSHELF_IVORY_SKIN', '아이보리 책장 스킨', 'NORMAL', 'BOOKSHELF', 'BOOKSHELF_SKIN', 'bookshelf/ivory_skin', null, 'https://cdn.dotshelf.dev/mock/assets/bookshelf_ivory.png', 700, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["BOOKSHELF"]', 'UTF8')), now() - interval '18 days', null, false, true),
    (1003, now(), now(), 1002, 'OBJECT_SKY_NOTEBOOK', '스카이 노트북', 'RARE', 'SPACE', 'INTERIOR_OBJECT', 'objects/sky_notebook', null, 'https://cdn.dotshelf.dev/mock/assets/sky_notebook.png', 1200, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["DESK","FLOOR"]', 'UTF8')), now() - interval '16 days', null, false, true),
    (1004, now(), now(), 1002, 'POST_IT_MINT', '민트 방명록 포스트잇', 'NORMAL', 'GUESTBOOK', 'POST_IT', 'guestbook/post_it_mint', null, 'https://cdn.dotshelf.dev/mock/assets/post_it_mint.png', 300, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["GUESTBOOK"]', 'UTF8')), now() - interval '14 days', null, false, true),
    (1005, now(), now(), 1003, 'THEME_CALM_STUDY', '차분한 서재 테마', 'RARE', 'SPACE', 'THEME', 'themes/calm_study', null, 'https://cdn.dotshelf.dev/mock/assets/theme_calm_study.png', 1500, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["SPACE"]', 'UTF8')), now() - interval '12 days', null, false, true),
    (1006, now(), now(), 1002, 'OBJECT_SMALL_PLANT', '작은 화분', 'NORMAL', 'SPACE', 'INTERIOR_OBJECT', 'objects/small_plant', null, 'https://cdn.dotshelf.dev/mock/assets/small_plant.png', 450, 'DotShelf', 'DotShelf Studio', lo_from_bytea(0, convert_to('["DESK","SHELF"]', 'UTF8')), now() - interval '10 days', null, false, true)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_asset_inventories (
    id, created_at, updated_at, user_id, asset_id, grant_type, acquired_at
) VALUES
    (1001, now(), now(), 1001, 1001, 'DEFAULT', now() - interval '9 days'),
    (1002, now(), now(), 1001, 1002, 'PURCHASED', now() - interval '8 days'),
    (1003, now(), now(), 1001, 1005, 'PURCHASED', now() - interval '7 days'),
    (1004, now(), now(), 1002, 1001, 'DEFAULT', now() - interval '9 days'),
    (1005, now(), now(), 1002, 1003, 'PURCHASED', now() - interval '6 days'),
    (1006, now(), now(), 1002, 1005, 'PURCHASED', now() - interval '5 days'),
    (1007, now(), now(), 1003, 1001, 'DEFAULT', now() - interval '9 days'),
    (1008, now(), now(), 1003, 1004, 'EVENT', now() - interval '4 days'),
    (1009, now(), now(), 1003, 1006, 'PURCHASED', now() - interval '3 days'),
    (1010, now(), now(), 1001, 1006, 'PURCHASED', now() - interval '2 days'),
    (1011, now(), now(), 1001, 1004, 'EVENT', now() - interval '2 days'),
    (1012, now(), now(), 1002, 1004, 'EVENT', now() - interval '2 days')
ON CONFLICT DO NOTHING;

INSERT INTO public.asset_purchases (
    id, created_at, updated_at, user_id, asset_id, inventory_id, price_pages,
    purchase_status, purchased_at, page_transaction_id, payment_order_id
) VALUES
    (1001, now(), now(), 1001, 1002, 1002, 700, 'COMPLETED', now() - interval '8 days', null, null),
    (1002, now(), now(), 1002, 1003, 1005, 1200, 'COMPLETED', now() - interval '6 days', null, null),
    (1003, now(), now(), 1003, 1006, 1009, 450, 'COMPLETED', now() - interval '3 days', null, null),
    (1004, now(), now(), 1001, 1006, 1010, 450, 'COMPLETED', now() - interval '2 days', null, null)
ON CONFLICT DO NOTHING;

INSERT INTO public.asset_wishlists (
    id, created_at, updated_at, user_id, asset_id
) VALUES
    (1001, now(), now(), 1001, 1003),
    (1002, now(), now(), 1002, 1002),
    (1003, now(), now(), 1003, 1005)
ON CONFLICT DO NOTHING;

INSERT INTO public.space_templates (
    id, created_at, updated_at, code, name, preview_image_url, layout_payload, display_order, is_active
) VALUES
    (1001, now(), now(), 'CALM_READER_ROOM', '차분한 독서방', 'https://cdn.dotshelf.dev/mock/templates/calm_reader.png', lo_from_bytea(0, convert_to('{"theme":"calm","lights":"warm","layout":"reader"}', 'UTF8')), 1, true),
    (1002, now(), now(), 'SF_BLUE_ROOM', '푸른 SF 서재', 'https://cdn.dotshelf.dev/mock/templates/sf_blue.png', lo_from_bytea(0, convert_to('{"theme":"blue","lights":"neon","layout":"sf"}', 'UTF8')), 2, true),
    (1003, now(), now(), 'ESSAY_WINDOW_ROOM', '창가 에세이 공간', 'https://cdn.dotshelf.dev/mock/templates/essay_window.png', lo_from_bytea(0, convert_to('{"theme":"window","lights":"daylight","layout":"essay"}', 'UTF8')), 3, true)
ON CONFLICT DO NOTHING;

INSERT INTO public.spaces (
    id, created_at, updated_at, owner_user_id, template_id, space_name, visibility_scope,
    guestbook_visibility_scope, visit_count, like_count, layout_version, current_theme_inventory_id, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1001, '민지의 저녁 서재', 'PUBLIC', 'PUBLIC', 3, 2, 1, 1003, null),
    (1002, now(), now(), 1002, 1002, '준호의 SF 책장', 'PUBLIC', 'PUBLIC', 2, 2, 1, 1006, null),
    (1003, now(), now(), 1003, 1003, '서연의 창가 공간', 'PUBLIC', 'PUBLIC', 2, 2, 1, null, null),
    (1004, now(), now(), 1004, 1001, '관리자 검수 공간', 'PRIVATE', 'PRIVATE', 0, 0, 1, null, null)
ON CONFLICT DO NOTHING;

INSERT INTO public.space_layout_snapshots (
    id, created_at, updated_at, space_id, layout_version, layout_payload, saved_by_user_id, saved_at
) VALUES
    (1001, now(), now(), 1001, 1, lo_from_bytea(0, convert_to('{"bookshelf":1001,"theme":"calm","objects":[1003,1006]}', 'UTF8')), 1001, now() - interval '2 days'),
    (1002, now(), now(), 1002, 1, lo_from_bytea(0, convert_to('{"bookshelf":1002,"theme":"blue","objects":[1003]}', 'UTF8')), 1002, now() - interval '2 days'),
    (1003, now(), now(), 1003, 1, lo_from_bytea(0, convert_to('{"bookshelf":1003,"theme":"window","objects":[1006]}', 'UTF8')), 1003, now() - interval '2 days')
ON CONFLICT DO NOTHING;

INSERT INTO public.books (
    id, created_at, updated_at, isbn13, isbn10, title, title_search, author, publisher,
    published_date, description, cover_image_url, source, source_url, page_count, language,
    view_count, like_count, bookshelf_add_count, rating_sum, rating_count
) VALUES
    (1001, now(), now(), '9788936434120', '8936434129', '모순', '모순', '양귀자', '쓰다', date '2013-04-01', lo_from_bytea(0, convert_to('삶의 모순과 선택을 담담하게 바라보는 장편소설입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/mosun.jpg', 'LOCAL', null, 308, 'ko', 120, 5, 3, 13.8, 3),
    (1002, now(), now(), '9788934972464', '8934972468', '불편한 편의점', '불편한편의점', '김호연', '나무옆의자', date '2021-04-20', lo_from_bytea(0, convert_to('편의점을 중심으로 사람들이 서로를 회복시키는 이야기입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/store.jpg', 'LOCAL', null, 268, 'ko', 96, 4, 3, 13.2, 3),
    (1003, now(), now(), '9788936434267', '8936434269', '아몬드', '아몬드', '손원평', '창비', date '2017-03-31', lo_from_bytea(0, convert_to('감정을 느끼기 어려운 소년이 관계를 배워가는 성장담입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/almond.jpg', 'LOCAL', null, 264, 'ko', 88, 4, 3, 12.9, 3),
    (1004, now(), now(), '9788954682152', '8954682155', '작별인사', '작별인사', '김영하', '복복서가', date '2022-05-02', lo_from_bytea(0, convert_to('존재와 기억의 경계를 묻는 SF 장편입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/goodbye.jpg', 'LOCAL', null, 308, 'ko', 74, 3, 0, 0.0, 0),
    (1005, now(), now(), '9791165341909', '1165341906', '달러구트 꿈 백화점', '달러구트꿈백화점', '이미예', '팩토리나인', date '2020-07-08', lo_from_bytea(0, convert_to('꿈을 사고파는 백화점에서 벌어지는 따뜻한 판타지입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/dream.jpg', 'KAKAO', 'https://search.daum.net/mock/dream', 300, 'ko', 64, 2, 0, 0.0, 0),
    (1006, now(), now(), '9788965964434', '8965964432', '클린 코드', '클린코드', 'Robert C. Martin', '인사이트', date '2013-12-24', lo_from_bytea(0, convert_to('읽기 좋은 소프트웨어 코드를 만드는 원칙을 정리한 개발서입니다.', 'UTF8')), 'https://cdn.dotshelf.dev/mock/books/clean-code.jpg', 'LOCAL', null, 584, 'ko', 150, 6, 0, 0.0, 0)
ON CONFLICT DO NOTHING;

INSERT INTO public.book_reviews (
    id, created_at, updated_at, book_id, user_id, rating, content, like_count, status, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1001, 4.8, '문장이 조용하게 오래 남았어요.', 2, 'ACTIVE', null),
    (1002, now(), now(), 1001, 1002, 4.5, '인물의 선택을 따라가며 생각할 거리가 많았습니다.', 1, 'ACTIVE', null),
    (1003, now(), now(), 1001, 1003, 4.5, '다 읽고 나서 제목을 다시 보게 됩니다.', 2, 'ACTIVE', null),
    (1004, now(), now(), 1002, 1001, 4.4, '따뜻하고 편하게 읽히는 책이에요.', 1, 'ACTIVE', null),
    (1005, now(), now(), 1002, 1002, 4.3, '캐릭터가 선명해서 금방 몰입했습니다.', 1, 'ACTIVE', null),
    (1006, now(), now(), 1002, 1003, 4.5, '가볍지만 여운이 있어 추천합니다.', 0, 'ACTIVE', null),
    (1007, now(), now(), 1003, 1001, 4.2, '감정의 의미를 다시 생각하게 됩니다.', 1, 'ACTIVE', null),
    (1008, now(), now(), 1003, 1002, 4.4, '짧지만 밀도 있는 성장 서사입니다.', 0, 'ACTIVE', null),
    (1009, now(), now(), 1003, 1003, 4.3, '청소년 소설이지만 어른에게도 좋습니다.', 1, 'ACTIVE', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.user_books (
    id, created_at, updated_at, user_id, book_id, selected_cover_product_id, reading_status,
    storage_status, owner_rating, is_liked, added_at, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1001, null, 'READING', 'ON_SHELF', 4.8, true, now() - interval '8 days', null),
    (1002, now(), now(), 1001, 1002, null, 'READ', 'ON_SHELF', 4.4, true, now() - interval '7 days', null),
    (1003, now(), now(), 1001, 1003, null, 'UNREAD', 'ON_SHELF', null, false, now() - interval '6 days', null),
    (1004, now(), now(), 1002, 1001, null, 'READ', 'ON_SHELF', 4.5, true, now() - interval '8 days', null),
    (1005, now(), now(), 1002, 1002, null, 'READING', 'ON_SHELF', 4.3, false, now() - interval '7 days', null),
    (1006, now(), now(), 1002, 1006, null, 'READING', 'ON_SHELF', null, true, now() - interval '5 days', null),
    (1007, now(), now(), 1003, 1001, null, 'READ', 'ON_SHELF', 4.5, true, now() - interval '8 days', null),
    (1008, now(), now(), 1003, 1002, null, 'UNREAD', 'ON_SHELF', null, false, now() - interval '6 days', null),
    (1009, now(), now(), 1003, 1003, null, 'READING', 'ON_SHELF', 4.3, true, now() - interval '4 days', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.bookshelves (
    id, created_at, updated_at, space_id, body_inventory_id, skin_inventory_id, name,
    description, visibility_scope, display_order, status, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1001, 1002, '저녁 독서 책장', '읽는 중인 책과 리뷰할 책을 모아둔 책장입니다.', 'PUBLIC', 1, 'ACTIVE', null),
    (1002, now(), now(), 1002, 1004, null, 'SF와 개발서', '상상력과 개발 원칙을 함께 꽂아둔 책장입니다.', 'PUBLIC', 1, 'ACTIVE', null),
    (1003, now(), now(), 1003, 1007, null, '창가의 문장들', '에세이와 소설을 천천히 정리하는 책장입니다.', 'PUBLIC', 1, 'ACTIVE', null),
    (1004, now(), now(), 1004, null, null, '검수용 책장', '관리자 검수용 샘플 책장입니다.', 'PRIVATE', 1, 'ACTIVE', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.bookshelf_sections (
    id, created_at, updated_at, bookshelf_id, row_no, column_no, max_item_count, alignment
) VALUES
    (1001, now(), now(), 1001, 1, 1, 12, 'LEFT'),
    (1002, now(), now(), 1001, 1, 2, 12, 'CENTER'),
    (1003, now(), now(), 1002, 1, 1, 12, 'LEFT'),
    (1004, now(), now(), 1003, 1, 1, 12, 'RIGHT'),
    (1005, now(), now(), 1004, 1, 1, 12, 'LEFT')
ON CONFLICT DO NOTHING;

INSERT INTO public.bookshelf_items (
    id, created_at, updated_at, bookshelf_id, bookshelf_section_id, item_type, user_book_id,
    asset_inventory_id, slot_no, facing_direction, display_style, rotation_deg, status
) VALUES
    (1001, now(), now(), 1001, 1001, 'BOOK', 1001, null, 1, 'FRONT', 'SPINE', 0.00, 'ACTIVE'),
    (1002, now(), now(), 1001, 1001, 'BOOK', 1002, null, 2, 'FRONT', 'FRONT', 0.00, 'ACTIVE'),
    (1003, now(), now(), 1001, 1002, 'ASSET', null, 1010, 1, 'FRONT', 'OBJECT', 0.00, 'ACTIVE'),
    (1004, now(), now(), 1002, 1003, 'BOOK', 1004, null, 1, 'FRONT', 'SPINE', 0.00, 'ACTIVE'),
    (1005, now(), now(), 1002, 1003, 'BOOK', 1006, null, 2, 'FRONT', 'SPINE', 0.00, 'ACTIVE'),
    (1006, now(), now(), 1002, 1003, 'ASSET', null, 1005, 3, 'FRONT', 'OBJECT', 0.00, 'ACTIVE'),
    (1007, now(), now(), 1003, 1004, 'BOOK', 1007, null, 1, 'FRONT', 'FRONT', 0.00, 'ACTIVE'),
    (1008, now(), now(), 1003, 1004, 'BOOK', 1009, null, 2, 'FRONT', 'SPINE', 0.00, 'ACTIVE'),
    (1009, now(), now(), 1003, 1004, 'ASSET', null, 1009, 3, 'FRONT', 'OBJECT', 0.00, 'ACTIVE')
ON CONFLICT DO NOTHING;

INSERT INTO public.reading_records (
    id, created_at, updated_at, user_book_id, title, content, visibility_scope,
    like_count, comment_count, status, deleted_at
) VALUES
    (1001, now(), now(), 1001, '모순 1장 기록', lo_from_bytea(0, convert_to('선택의 기준이 흔들리는 장면을 기록했습니다.', 'UTF8')), 'PUBLIC', 2, 2, 'ACTIVE', null),
    (1002, now(), now(), 1002, '편의점에서 발견한 위로', lo_from_bytea(0, convert_to('인물들이 서로에게 건네는 작은 친절이 좋았습니다.', 'UTF8')), 'PUBLIC', 1, 2, 'ACTIVE', null),
    (1003, now(), now(), 1004, '모순을 다시 읽으며', lo_from_bytea(0, convert_to('같은 책을 다른 관점에서 다시 읽은 메모입니다.', 'UTF8')), 'FOLLOWERS', 1, 1, 'ACTIVE', null),
    (1004, now(), now(), 1006, '클린 코드 체크리스트', lo_from_bytea(0, convert_to('함수와 이름 짓기 원칙을 프로젝트에 적용해 보고 싶습니다.', 'UTF8')), 'PUBLIC', 0, 1, 'ACTIVE', null),
    (1005, now(), now(), 1007, '문장 수집 노트', lo_from_bytea(0, convert_to('좋았던 문장을 짧게 옮겨 적었습니다.', 'UTF8')), 'PUBLIC', 1, 0, 'ACTIVE', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.reading_record_comments (
    id, created_at, updated_at, reading_record_id, user_id, parent_comment_id,
    depth, content, like_count, is_deleted
) VALUES
    (1001, now(), now(), 1001, 1002, null, 0, '저도 이 장면이 오래 남았어요.', 1, false),
    (1002, now(), now(), 1001, 1001, 1001, 1, '같이 읽은 사람의 관점이 궁금했어요.', 0, false),
    (1003, now(), now(), 1002, 1003, null, 0, '따뜻한 기록이네요.', 1, false),
    (1004, now(), now(), 1002, 1002, 1003, 1, '다음 장도 기록해볼게요.', 0, false),
    (1005, now(), now(), 1003, 1001, null, 0, '다시 읽으면 다르게 보이는 책 같아요.', 0, false),
    (1006, now(), now(), 1004, 1001, null, 0, '체크리스트 공유 기대합니다.', 0, false)
ON CONFLICT DO NOTHING;

INSERT INTO public.reading_record_likes (
    id, created_at, updated_at, reading_record_id, user_id
) VALUES
    (1001, now(), now(), 1001, 1002),
    (1002, now(), now(), 1001, 1003),
    (1003, now(), now(), 1002, 1001),
    (1004, now(), now(), 1003, 1001),
    (1005, now(), now(), 1005, 1002)
ON CONFLICT DO NOTHING;

INSERT INTO public.reading_record_comment_likes (
    id, created_at, updated_at, comment_id, user_id
) VALUES
    (1001, now(), now(), 1001, 1001),
    (1002, now(), now(), 1003, 1002),
    (1003, now(), now(), 1005, 1003)
ON CONFLICT DO NOTHING;

INSERT INTO public.book_promotions (
    id, created_at, updated_at, book_id, promotion_type, title, description, image_url,
    landing_url, sponsor_name, display_order, starts_at, ends_at, is_active
) VALUES
    (1001, now(), now(), 1001, 'FEATURED', '오늘의 문장 추천', '모순을 읽고 남긴 기록을 둘러보세요.', 'https://cdn.dotshelf.dev/mock/promotions/mosun.png', 'https://dotshelf.dev/books/1001', 'DotShelf', 1, now() - interval '1 day', now() + interval '20 days', true),
    (1002, now(), now(), 1002, 'SEASONAL', '따뜻한 소설 큐레이션', '불편한 편의점으로 시작하는 회복의 독서.', 'https://cdn.dotshelf.dev/mock/promotions/store.png', 'https://dotshelf.dev/books/1002', 'DotShelf', 2, now() - interval '1 day', now() + interval '20 days', true),
    (1003, now(), now(), 1006, 'SPONSORED', '개발자 책장 추천', '클린 코드를 책장에 꽂아보세요.', 'https://cdn.dotshelf.dev/mock/promotions/clean-code.png', 'https://dotshelf.dev/books/1006', 'DotShelf', 3, now() - interval '1 day', now() + interval '20 days', true)
ON CONFLICT DO NOTHING;

INSERT INTO public.space_likes (
    id, created_at, updated_at, space_id, user_id
) VALUES
    (1001, now(), now(), 1001, 1002),
    (1002, now(), now(), 1001, 1003),
    (1003, now(), now(), 1002, 1001),
    (1004, now(), now(), 1002, 1003),
    (1005, now(), now(), 1003, 1001),
    (1006, now(), now(), 1003, 1002)
ON CONFLICT DO NOTHING;

INSERT INTO public.space_visits (
    id, created_at, updated_at, space_id, visitor_user_id, visited_date, visited_at
) VALUES
    (1001, now(), now(), 1001, 1002, current_date, now() - interval '5 hours'),
    (1002, now(), now(), 1001, 1003, current_date - 1, now() - interval '1 day'),
    (1003, now(), now(), 1001, 1004, current_date - 2, now() - interval '2 days'),
    (1004, now(), now(), 1002, 1001, current_date, now() - interval '4 hours'),
    (1005, now(), now(), 1002, 1003, current_date - 1, now() - interval '1 day'),
    (1006, now(), now(), 1003, 1001, current_date, now() - interval '3 hours'),
    (1007, now(), now(), 1003, 1002, current_date - 1, now() - interval '1 day')
ON CONFLICT DO NOTHING;

INSERT INTO public.guestbooks (
    id, created_at, updated_at, space_id, writer_user_id, post_it_inventory_id, content,
    visibility_scope, reply_count, status, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1002, 1012, '문장 좋았어요!', 'PUBLIC', 1, 'ACTIVE', null),
    (1002, now(), now(), 1001, 1003, 1008, '책장 분위기 최고', 'PUBLIC', 1, 'ACTIVE', null),
    (1003, now(), now(), 1002, 1001, 1011, 'SF 책 추천해요', 'PUBLIC', 1, 'ACTIVE', null),
    (1004, now(), now(), 1002, 1003, 1008, '노트북 예뻐요', 'PUBLIC', 1, 'ACTIVE', null),
    (1005, now(), now(), 1003, 1001, 1011, '창가 느낌 좋아요', 'PUBLIC', 1, 'ACTIVE', null),
    (1006, now(), now(), 1003, 1002, 1012, '다음 기록 기대!', 'PUBLIC', 1, 'ACTIVE', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.guestbook_replies (
    id, created_at, updated_at, guestbook_id, writer_user_id, content, visibility_scope, deleted_at
) VALUES
    (1001, now(), now(), 1001, 1001, '들러줘서 고마워요.', 'PUBLIC', null),
    (1002, now(), now(), 1002, 1001, '다음엔 책 추천도 남겨주세요.', 'PUBLIC', null),
    (1003, now(), now(), 1003, 1002, '추천 목록에 추가해둘게요.', 'PUBLIC', null),
    (1004, now(), now(), 1004, 1002, '노트북 스킨도 마음에 들어요.', 'PUBLIC', null),
    (1005, now(), now(), 1005, 1003, '오후 빛이 제일 좋아요.', 'PUBLIC', null),
    (1006, now(), now(), 1006, 1003, '새 기록 곧 올릴게요.', 'PUBLIC', null)
ON CONFLICT DO NOTHING;

INSERT INTO public.guest_migration_imports (
    id, created_at, updated_at, user_id, migration_status, payload, guest_session_id,
    imported_space_id, failure_reason, requested_at, completed_at
) VALUES
    (1001, now(), now(), 1001, 'COMPLETED', lo_from_bytea(0, convert_to('{"source":"guest","books":2,"notes":1}', 'UTF8')), 'guest-session-minji-001', 1001, null, now() - interval '7 days', now() - interval '7 days' + interval '3 minutes'),
    (1002, now(), now(), 1002, 'PENDING', lo_from_bytea(0, convert_to('{"source":"guest","books":1,"notes":0}', 'UTF8')), 'guest-session-junho-001', null, null, now() - interval '1 hour', null),
    (1003, now(), now(), 1003, 'FAILED', lo_from_bytea(0, convert_to('{"source":"guest","books":0,"notes":2}', 'UTF8')), 'guest-session-seoyeon-001', null, 'mock invalid legacy payload', now() - interval '3 days', now() - interval '3 days' + interval '1 minute')
ON CONFLICT DO NOTHING;

INSERT INTO public.notifications (
    id, created_at, updated_at, receiver_user_id, sender_user_id, type, target_type,
    target_id, message, is_read, read_at
) VALUES
    (1001, now(), now(), 1001, 1002, 'NEW_FOLLOWER', 'USER', 1002, 'junho.shelf님이 팔로우했어요.', false, null),
    (1002, now(), now(), 1002, 1001, 'GUESTBOOK_REPLY', 'GUESTBOOK', 1003, '민지님이 방명록에 답글을 남겼어요.', true, now() - interval '2 hours'),
    (1003, now(), now(), 1003, 1001, 'SPACE_LIKE', 'SPACE', 1003, 'minji.reader님이 공간을 좋아했어요.', false, null),
    (1004, now(), now(), 1001, 1003, 'READING_RECORD_REACTION', 'READING_RECORD', 1001, 'seoyeon.space님이 독서 기록에 반응했어요.', false, null),
    (1005, now(), now(), 1002, 1003, 'BOOK_ADDED', 'BOOK', 1003, 'seoyeon.space님이 같은 책을 책장에 추가했어요.', false, null),
    (1006, now(), now(), 1004, null, 'SYSTEM', 'REPORT', 1001, '검수할 신고 샘플이 생성되었습니다.', false, null)
ON CONFLICT DO NOTHING;

INSERT INTO public.reports (
    id, created_at, updated_at, reporter_user_id, reported_user_id, target_type, target_id,
    reason_type, reason_detail, status, is_blinded_to_reporter, is_globally_blinded,
    reviewed_by_user_id, reviewed_at
) VALUES
    (1001, now(), now(), 1001, 1002, 'GUESTBOOK', 1003, 'SPAM', '반복 홍보성 문구로 신고된 샘플입니다.', 'PENDING', true, false, null, null),
    (1002, now(), now(), 1002, 1003, 'READING_RECORD_COMMENT', 1003, 'ABUSE', '공격적인 댓글 신고 샘플입니다.', 'RESOLVED', true, false, 1004, now() - interval '1 day'),
    (1003, now(), now(), 1003, 1001, 'SPACE', 1001, 'INAPPROPRIATE', '공간 공개 범위 검토용 신고 샘플입니다.', 'REJECTED', false, false, 1004, now() - interval '2 days')
ON CONFLICT DO NOTHING;

INSERT INTO public.feed_events (
    id, created_at, updated_at, actor_user_id, space_id, book_id, reading_record_id,
    event_type, target_type, target_id, visibility_scope, occurred_at
) VALUES
    (1001, now(), now(), 1001, 1001, 1001, null, 'BOOK_ADDED', 'USER_BOOK', 1001, 'PUBLIC', now() - interval '8 days'),
    (1002, now(), now(), 1001, 1001, 1001, 1001, 'READING_RECORD_CREATED', 'READING_RECORD', 1001, 'PUBLIC', now() - interval '2 days'),
    (1003, now(), now(), 1002, 1002, 1006, null, 'BOOK_ADDED', 'USER_BOOK', 1006, 'PUBLIC', now() - interval '5 days'),
    (1004, now(), now(), 1003, 1003, null, null, 'SPACE_VISITED', 'SPACE', 1001, 'PUBLIC', now() - interval '1 day'),
    (1005, now(), now(), 1002, 1001, null, null, 'GUESTBOOK_CREATED', 'GUESTBOOK', 1001, 'PUBLIC', now() - interval '1 day'),
    (1006, now(), now(), 1003, 1002, null, null, 'SPACE_LIKED', 'SPACE', 1002, 'PUBLIC', now() - interval '3 hours')
ON CONFLICT DO NOTHING;

-- Refresh aggregate counters from the relation rows above.
UPDATE public.books b
SET
    rating_count = COALESCE(r.rating_count, 0),
    rating_sum = COALESCE(r.rating_sum, 0),
    bookshelf_add_count = COALESCE(ub.add_count, 0)
FROM (
    SELECT book_id, count(*)::integer AS rating_count, COALESCE(sum(rating), 0)::numeric(10,1) AS rating_sum
    FROM public.book_reviews
    WHERE status = 'ACTIVE'
    GROUP BY book_id
) r
FULL JOIN (
    SELECT book_id, count(*)::bigint AS add_count
    FROM public.user_books
    WHERE storage_status <> 'DELETED'
    GROUP BY book_id
) ub ON ub.book_id = r.book_id
WHERE b.id = COALESCE(r.book_id, ub.book_id);

UPDATE public.reading_records rr
SET
    like_count = COALESCE(l.like_count, 0),
    comment_count = COALESCE(c.comment_count, 0)
FROM (
    SELECT reading_record_id, count(*)::integer AS like_count
    FROM public.reading_record_likes
    GROUP BY reading_record_id
) l
FULL JOIN (
    SELECT reading_record_id, count(*)::integer AS comment_count
    FROM public.reading_record_comments
    WHERE is_deleted = false
    GROUP BY reading_record_id
) c ON c.reading_record_id = l.reading_record_id
WHERE rr.id = COALESCE(l.reading_record_id, c.reading_record_id);

UPDATE public.reading_record_comments c
SET like_count = COALESCE(l.like_count, 0)
FROM (
    SELECT comment_id, count(*)::integer AS like_count
    FROM public.reading_record_comment_likes
    GROUP BY comment_id
) l
WHERE c.id = l.comment_id;

UPDATE public.guestbooks g
SET reply_count = COALESCE(r.reply_count, 0)
FROM (
    SELECT guestbook_id, count(*)::integer AS reply_count
    FROM public.guestbook_replies
    WHERE deleted_at IS NULL
    GROUP BY guestbook_id
) r
WHERE g.id = r.guestbook_id;

UPDATE public.spaces s
SET
    like_count = COALESCE(l.like_count, 0),
    visit_count = COALESCE(v.visit_count, 0)
FROM (
    SELECT space_id, count(*)::bigint AS like_count
    FROM public.space_likes
    GROUP BY space_id
) l
FULL JOIN (
    SELECT space_id, count(*)::bigint AS visit_count
    FROM public.space_visits
    GROUP BY space_id
) v ON v.space_id = l.space_id
WHERE s.id = COALESCE(l.space_id, v.space_id);

DO $$
DECLARE
    r record;
    seq_name text;
    max_id bigint;
BEGIN
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
          AND table_name <> 'flyway_schema_history'
    LOOP
        seq_name := pg_get_serial_sequence(format('%I.%I', r.table_schema, r.table_name), 'id');
        IF seq_name IS NOT NULL THEN
            EXECUTE format('SELECT COALESCE(MAX(id), 0) FROM %I.%I', r.table_schema, r.table_name)
                INTO max_id;
            IF max_id > 0 THEN
                EXECUTE format('SELECT setval(%L, %s, true)', seq_name, max_id);
            END IF;
        END IF;
    END LOOP;
END $$;

COMMIT;
