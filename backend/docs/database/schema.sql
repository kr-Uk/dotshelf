-- DotShelf database schema draft for user app v1.0.
-- Target database: PostgreSQL.
--
-- Design notes:
-- 1. The previous schema was MySQL-flavored. This version uses PostgreSQL syntax.
-- 2. Visibility is modeled as PUBLIC / FOLLOWERS / PRIVATE instead of booleans.
-- 3. Books and bookshelf decor share the same shelf-placement table because they
--    participate in the same section ordering and drag/drop behavior.
-- 4. Asset ownership and purchase history are separated. Inventory answers
--    "does the user own this?", purchases answer "how did the user get it?".
-- 5. Some cross-row validations still belong in application logic:
--    - bookshelf_items.bookshelf_section_id must belong to bookshelf_items.bookshelf_id.
--    - bookshelf_items.slot_no must not exceed bookshelf_sections.max_item_count.
--    - selected assets must be compatible with their application target.
--    - reports.target_type / target_id must point to an existing reportable row.
--    - Last-write-wins layout conflict policy should compare spaces.layout_version.

-- ---------------------------------------------------------------------------
-- Asset catalog
-- ---------------------------------------------------------------------------

CREATE TABLE asset_categories (
    id BIGSERIAL PRIMARY KEY,
    parent_category_id BIGINT NULL REFERENCES asset_categories(id),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(100) NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_asset_categories_code UNIQUE (code)
);

CREATE TABLE assets (
    id BIGSERIAL PRIMARY KEY,
    category_id BIGINT NOT NULL REFERENCES asset_categories(id),
    code VARCHAR(80) NOT NULL,
    name VARCHAR(100) NOT NULL,
    tier VARCHAR(30) NOT NULL DEFAULT 'NORMAL',
    asset_scope VARCHAR(30) NOT NULL,
    asset_type VARCHAR(50) NOT NULL,
    unity_resource_key VARCHAR(150) NULL,
    asset_url VARCHAR(500) NULL,
    preview_image_url VARCHAR(500) NULL,
    price_pages INT NOT NULL DEFAULT 0,
    creator VARCHAR(100) NULL,
    supplier VARCHAR(100) NULL,
    compatible_targets JSONB NULL,
    released_at TIMESTAMPTZ NULL,
    valid_until TIMESTAMPTZ NULL,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_assets_code UNIQUE (code),
    CONSTRAINT ck_assets_price_pages CHECK (price_pages >= 0)
);

CREATE TABLE asset_tags (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(50) NOT NULL,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_asset_tags_code UNIQUE (code)
);

CREATE TABLE asset_tag_mappings (
    id BIGSERIAL PRIMARY KEY,
    asset_id BIGINT NOT NULL REFERENCES assets(id),
    tag_id BIGINT NOT NULL REFERENCES asset_tags(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_asset_tag_mappings_asset_tag UNIQUE (asset_id, tag_id)
);

-- ---------------------------------------------------------------------------
-- User / auth / settings
-- ---------------------------------------------------------------------------

CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(100) NULL,
    nickname VARCHAR(50) NOT NULL,
    profile_image_url VARCHAR(500) NULL,
    introduction VARCHAR(255) NULL,
    role VARCHAR(20) NOT NULL DEFAULT 'USER',
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    is_onboarded BOOLEAN NOT NULL DEFAULT FALSE,
    last_login_at TIMESTAMPTZ NULL,
    deleted_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_users_email UNIQUE (email),
    CONSTRAINT uk_users_nickname UNIQUE (nickname)
);

CREATE TABLE user_social_accounts (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    provider VARCHAR(30) NOT NULL,
    provider_user_id VARCHAR(100) NOT NULL,
    email_at_provider VARCHAR(100) NULL,
    linked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_social_accounts_provider_user UNIQUE (provider, provider_user_id),
    CONSTRAINT uk_user_social_accounts_user_provider UNIQUE (user_id, provider)
);

CREATE TABLE auth_refresh_tokens (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    token_hash VARCHAR(255) NOT NULL,
    device_id VARCHAR(100) NULL,
    user_agent VARCHAR(500) NULL,
    ip_address VARCHAR(45) NULL,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ NULL,
    replaced_by_token_id BIGINT NULL REFERENCES auth_refresh_tokens(id),
    reuse_detected_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_auth_refresh_tokens_hash UNIQUE (token_hash)
);

CREATE TABLE device_tokens (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    platform VARCHAR(30) NOT NULL,
    token VARCHAR(500) NOT NULL,
    device_id VARCHAR(100) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_used_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_device_tokens_token UNIQUE (token)
);

CREATE TABLE user_notification_settings (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    notify_new_guestbook BOOLEAN NOT NULL DEFAULT TRUE,
    notify_guestbook_reply BOOLEAN NOT NULL DEFAULT TRUE,
    notify_reading_record_reaction BOOLEAN NOT NULL DEFAULT TRUE,
    notify_new_follower BOOLEAN NOT NULL DEFAULT TRUE,
    notify_book_added_by_others BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_notification_settings_user UNIQUE (user_id)
);

CREATE TABLE user_feed_settings (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    show_book_added BOOLEAN NOT NULL DEFAULT TRUE,
    show_reading_record BOOLEAN NOT NULL DEFAULT TRUE,
    show_space_visit BOOLEAN NOT NULL DEFAULT TRUE,
    show_guestbook BOOLEAN NOT NULL DEFAULT TRUE,
    show_reading_record_comment BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_feed_settings_user UNIQUE (user_id)
);

CREATE TABLE user_blocks (
    id BIGSERIAL PRIMARY KEY,
    blocker_user_id BIGINT NOT NULL REFERENCES users(id),
    blocked_user_id BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_blocks_blocker_blocked UNIQUE (blocker_user_id, blocked_user_id),
    CONSTRAINT ck_user_blocks_not_self CHECK (blocker_user_id <> blocked_user_id)
);

CREATE TABLE follows (
    id BIGSERIAL PRIMARY KEY,
    follower_user_id BIGINT NOT NULL REFERENCES users(id),
    following_user_id BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_follows_follower_following UNIQUE (follower_user_id, following_user_id),
    CONSTRAINT ck_follows_not_self CHECK (follower_user_id <> following_user_id)
);

-- ---------------------------------------------------------------------------
-- Wallet / payment / purchases
-- ---------------------------------------------------------------------------

CREATE TABLE page_wallets (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    balance_pages INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_page_wallets_user UNIQUE (user_id),
    CONSTRAINT ck_page_wallets_balance CHECK (balance_pages >= 0)
);

CREATE TABLE payment_methods (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    provider VARCHAR(30) NOT NULL,
    provider_payment_method_id VARCHAR(150) NOT NULL,
    display_name VARCHAR(100) NULL,
    brand VARCHAR(50) NULL,
    last4 VARCHAR(4) NULL,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_payment_methods_provider_id UNIQUE (provider, provider_payment_method_id)
);

CREATE TABLE payment_orders (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    payment_method_id BIGINT NULL REFERENCES payment_methods(id),
    platform VARCHAR(30) NOT NULL,
    product_type VARCHAR(30) NOT NULL,
    product_id BIGINT NULL,
    amount_krw INT NOT NULL DEFAULT 0,
    pages_amount INT NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    provider_order_id VARCHAR(150) NULL,
    provider_transaction_id VARCHAR(150) NULL,
    receipt_payload JSONB NULL,
    requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    approved_at TIMESTAMPTZ NULL,
    failed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_payment_orders_amount CHECK (amount_krw >= 0),
    CONSTRAINT ck_payment_orders_pages CHECK (pages_amount >= 0)
);

CREATE TABLE page_transactions (
    id BIGSERIAL PRIMARY KEY,
    wallet_id BIGINT NOT NULL REFERENCES page_wallets(id),
    user_id BIGINT NOT NULL REFERENCES users(id),
    payment_order_id BIGINT NULL REFERENCES payment_orders(id),
    transaction_type VARCHAR(30) NOT NULL,
    amount_pages INT NOT NULL,
    balance_after INT NOT NULL,
    reason VARCHAR(100) NULL,
    reference_type VARCHAR(30) NULL,
    reference_id BIGINT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_page_transactions_amount_not_zero CHECK (amount_pages <> 0),
    CONSTRAINT ck_page_transactions_balance_after CHECK (balance_after >= 0)
);

CREATE TABLE user_asset_inventories (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    asset_id BIGINT NOT NULL REFERENCES assets(id),
    grant_type VARCHAR(30) NOT NULL,
    acquired_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_asset_inventories_user_asset UNIQUE (user_id, asset_id)
);

CREATE TABLE asset_purchases (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    asset_id BIGINT NOT NULL REFERENCES assets(id),
    inventory_id BIGINT NULL REFERENCES user_asset_inventories(id),
    payment_order_id BIGINT NULL REFERENCES payment_orders(id),
    page_transaction_id BIGINT NULL REFERENCES page_transactions(id),
    price_pages INT NOT NULL,
    purchase_status VARCHAR(30) NOT NULL DEFAULT 'COMPLETED',
    purchased_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_asset_purchases_price CHECK (price_pages >= 0)
);

CREATE TABLE asset_wishlists (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    asset_id BIGINT NOT NULL REFERENCES assets(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_asset_wishlists_user_asset UNIQUE (user_id, asset_id)
);

-- ---------------------------------------------------------------------------
-- Books and licensed cover products
-- ---------------------------------------------------------------------------

CREATE TABLE books (
    id BIGSERIAL PRIMARY KEY,
    isbn13 VARCHAR(13) NULL,
    isbn10 VARCHAR(10) NULL,
    title VARCHAR(255) NOT NULL,
    author VARCHAR(255) NOT NULL,
    publisher VARCHAR(255) NULL,
    published_date DATE NULL,
    description TEXT NULL,
    cover_image_url VARCHAR(500) NULL,
    page_count INT NULL,
    language VARCHAR(20) NULL,
    view_count BIGINT NOT NULL DEFAULT 0,
    like_count BIGINT NOT NULL DEFAULT 0,
    bookshelf_add_count BIGINT NOT NULL DEFAULT 0,
    rating_sum NUMERIC(10,1) NOT NULL DEFAULT 0.0,
    rating_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_books_isbn13 UNIQUE (isbn13)
);

CREATE TABLE book_external_mappings (
    id BIGSERIAL PRIMARY KEY,
    book_id BIGINT NOT NULL REFERENCES books(id),
    source_provider VARCHAR(30) NOT NULL,
    external_book_id VARCHAR(100) NOT NULL,
    source_url VARCHAR(500) NULL,
    raw_payload JSONB NULL,
    last_synced_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_book_external_mappings_provider_external UNIQUE (source_provider, external_book_id)
);

CREATE TABLE book_cover_products (
    id BIGSERIAL PRIMARY KEY,
    book_id BIGINT NOT NULL REFERENCES books(id),
    code VARCHAR(80) NOT NULL,
    name VARCHAR(100) NOT NULL,
    asset_url VARCHAR(500) NOT NULL,
    preview_image_url VARCHAR(500) NULL,
    price_pages INT NOT NULL,
    supplier VARCHAR(100) NULL,
    license_status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
    released_at TIMESTAMPTZ NULL,
    valid_until TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_book_cover_products_code UNIQUE (code),
    CONSTRAINT ck_book_cover_products_price CHECK (price_pages >= 0)
);

CREATE TABLE user_book_cover_purchases (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    book_cover_product_id BIGINT NOT NULL REFERENCES book_cover_products(id),
    payment_order_id BIGINT NULL REFERENCES payment_orders(id),
    page_transaction_id BIGINT NULL REFERENCES page_transactions(id),
    price_pages INT NOT NULL,
    purchased_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_user_book_cover_purchases_user_product UNIQUE (user_id, book_cover_product_id),
    CONSTRAINT ck_user_book_cover_purchases_price CHECK (price_pages >= 0)
);

-- ---------------------------------------------------------------------------
-- Space / layout / decorations
-- ---------------------------------------------------------------------------

CREATE TABLE space_templates (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(50) NOT NULL,
    name VARCHAR(100) NOT NULL,
    preview_image_url VARCHAR(500) NULL,
    layout_payload JSONB NOT NULL DEFAULT '{}'::jsonb,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_space_templates_code UNIQUE (code)
);

CREATE TABLE spaces (
    id BIGSERIAL PRIMARY KEY,
    owner_user_id BIGINT NOT NULL REFERENCES users(id),
    template_id BIGINT NULL REFERENCES space_templates(id),
    space_name VARCHAR(100) NOT NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    guestbook_visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    visit_count BIGINT NOT NULL DEFAULT 0,
    like_count BIGINT NOT NULL DEFAULT 0,
    layout_version BIGINT NOT NULL DEFAULT 0,
    current_theme_inventory_id BIGINT NULL REFERENCES user_asset_inventories(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT ck_spaces_visibility CHECK (visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE')),
    CONSTRAINT ck_spaces_guestbook_visibility CHECK (guestbook_visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE'))
);

CREATE TABLE space_visits (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    visitor_user_id BIGINT NOT NULL REFERENCES users(id),
    visited_date DATE NOT NULL,
    visited_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_space_visits_space_visitor_date UNIQUE (space_id, visitor_user_id, visited_date)
);

CREATE TABLE space_likes (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    user_id BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_space_likes_space_user UNIQUE (space_id, user_id)
);

CREATE TABLE space_asset_applications (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    asset_inventory_id BIGINT NOT NULL REFERENCES user_asset_inventories(id),
    application_target VARCHAR(50) NOT NULL,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_space_asset_applications_space_target UNIQUE (space_id, application_target)
);

CREATE TABLE space_decorations (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    asset_inventory_id BIGINT NOT NULL REFERENCES user_asset_inventories(id),
    anchor_type VARCHAR(30) NOT NULL,
    pos_x NUMERIC(10,2) NOT NULL,
    pos_y NUMERIC(10,2) NOT NULL,
    pos_z NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    z_index INT NOT NULL DEFAULT 0,
    rotation_deg NUMERIC(6,2) NOT NULL DEFAULT 0.00,
    scale_x NUMERIC(6,2) NOT NULL DEFAULT 1.00,
    scale_y NUMERIC(6,2) NOT NULL DEFAULT 1.00,
    flip_x BOOLEAN NOT NULL DEFAULT FALSE,
    flip_y BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE space_layout_snapshots (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    layout_version BIGINT NOT NULL,
    layout_payload JSONB NOT NULL,
    saved_by_user_id BIGINT NOT NULL REFERENCES users(id),
    saved_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_space_layout_snapshots_space_version UNIQUE (space_id, layout_version)
);

-- ---------------------------------------------------------------------------
-- Bookshelves, user books, and unified shelf items
-- ---------------------------------------------------------------------------

CREATE TABLE bookshelves (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    body_inventory_id BIGINT NOT NULL REFERENCES user_asset_inventories(id),
    skin_inventory_id BIGINT NULL REFERENCES user_asset_inventories(id),
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    display_order INT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT uk_bookshelves_space_display_order UNIQUE (space_id, display_order),
    CONSTRAINT ck_bookshelves_visibility CHECK (visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE'))
);

CREATE TABLE bookshelf_sections (
    id BIGSERIAL PRIMARY KEY,
    bookshelf_id BIGINT NOT NULL REFERENCES bookshelves(id),
    row_no INT NOT NULL,
    column_no INT NOT NULL,
    max_item_count INT NOT NULL,
    alignment VARCHAR(20) NOT NULL DEFAULT 'LEFT',
    pos_x NUMERIC(10,2) NULL,
    pos_y NUMERIC(10,2) NULL,
    width NUMERIC(10,2) NULL,
    height NUMERIC(10,2) NULL,
    z_index INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_bookshelf_sections_bookshelf_row_column UNIQUE (bookshelf_id, row_no, column_no),
    CONSTRAINT ck_bookshelf_sections_alignment CHECK (alignment IN ('LEFT', 'CENTER', 'RIGHT')),
    CONSTRAINT ck_bookshelf_sections_max_item_count CHECK (max_item_count > 0)
);

CREATE TABLE user_books (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    book_id BIGINT NOT NULL REFERENCES books(id),
    selected_cover_product_id BIGINT NULL REFERENCES book_cover_products(id),
    reading_status VARCHAR(20) NOT NULL DEFAULT 'UNREAD',
    storage_status VARCHAR(20) NOT NULL DEFAULT 'ON_SHELF',
    owner_rating NUMERIC(2,1) NULL,
    is_liked BOOLEAN NOT NULL DEFAULT FALSE,
    added_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT uk_user_books_user_book UNIQUE (user_id, book_id),
    CONSTRAINT ck_user_books_reading_status CHECK (reading_status IN ('UNREAD', 'READING', 'READ')),
    CONSTRAINT ck_user_books_storage_status CHECK (storage_status IN ('ON_SHELF', 'ARCHIVED', 'DELETED')),
    CONSTRAINT ck_user_books_owner_rating CHECK (owner_rating IS NULL OR (owner_rating >= 0 AND owner_rating <= 5))
);

CREATE TABLE bookshelf_items (
    id BIGSERIAL PRIMARY KEY,
    bookshelf_id BIGINT NOT NULL REFERENCES bookshelves(id),
    bookshelf_section_id BIGINT NULL REFERENCES bookshelf_sections(id),
    item_type VARCHAR(20) NOT NULL,
    user_book_id BIGINT NULL REFERENCES user_books(id),
    asset_inventory_id BIGINT NULL REFERENCES user_asset_inventories(id),
    slot_no INT NULL,
    facing_direction VARCHAR(20) NOT NULL DEFAULT 'FRONT',
    display_style VARCHAR(30) NOT NULL DEFAULT 'SPINE',
    rotation_deg NUMERIC(6,2) NOT NULL DEFAULT 0.00,
    pos_x NUMERIC(10,2) NULL,
    pos_y NUMERIC(10,2) NULL,
    pos_z NUMERIC(10,2) NULL,
    scale_x NUMERIC(6,2) NOT NULL DEFAULT 1.00,
    scale_y NUMERIC(6,2) NOT NULL DEFAULT 1.00,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_bookshelf_items_type CHECK (item_type IN ('BOOK', 'DECOR')),
    CONSTRAINT ck_bookshelf_items_status CHECK (status IN ('ACTIVE', 'REMOVED')),
    CONSTRAINT ck_bookshelf_items_payload CHECK (
        (item_type = 'BOOK' AND user_book_id IS NOT NULL AND asset_inventory_id IS NULL)
        OR
        (item_type = 'DECOR' AND user_book_id IS NULL AND asset_inventory_id IS NOT NULL)
    ),
    CONSTRAINT ck_bookshelf_items_active_location CHECK (
        status <> 'ACTIVE'
        OR
        (bookshelf_section_id IS NOT NULL AND slot_no IS NOT NULL AND slot_no > 0)
    )
);

CREATE UNIQUE INDEX uk_bookshelf_items_section_slot_active
    ON bookshelf_items (bookshelf_section_id, slot_no)
    WHERE status = 'ACTIVE';

CREATE UNIQUE INDEX uk_bookshelf_items_user_book_active
    ON bookshelf_items (user_book_id)
    WHERE status = 'ACTIVE' AND item_type = 'BOOK';

-- ---------------------------------------------------------------------------
-- Reading records and comments
-- ---------------------------------------------------------------------------

CREATE TABLE reading_records (
    id BIGSERIAL PRIMARY KEY,
    user_book_id BIGINT NOT NULL REFERENCES user_books(id),
    title VARCHAR(150) NULL,
    content TEXT NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    like_count INT NOT NULL DEFAULT 0,
    comment_count INT NOT NULL DEFAULT 0,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT uk_reading_records_user_book UNIQUE (user_book_id),
    CONSTRAINT ck_reading_records_visibility CHECK (visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE'))
);

CREATE TABLE reading_record_likes (
    id BIGSERIAL PRIMARY KEY,
    reading_record_id BIGINT NOT NULL REFERENCES reading_records(id),
    user_id BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_reading_record_likes_record_user UNIQUE (reading_record_id, user_id)
);

CREATE TABLE reading_record_comments (
    id BIGSERIAL PRIMARY KEY,
    reading_record_id BIGINT NOT NULL REFERENCES reading_records(id),
    user_id BIGINT NOT NULL REFERENCES users(id),
    parent_comment_id BIGINT NULL REFERENCES reading_record_comments(id),
    depth INT NOT NULL DEFAULT 0,
    content VARCHAR(1000) NOT NULL,
    like_count INT NOT NULL DEFAULT 0,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_reading_record_comments_depth CHECK (depth IN (0, 1))
);

CREATE TABLE reading_record_comment_likes (
    id BIGSERIAL PRIMARY KEY,
    comment_id BIGINT NOT NULL REFERENCES reading_record_comments(id),
    user_id BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_reading_record_comment_likes_comment_user UNIQUE (comment_id, user_id)
);

-- ---------------------------------------------------------------------------
-- Guestbook
-- ---------------------------------------------------------------------------

CREATE TABLE guestbooks (
    id BIGSERIAL PRIMARY KEY,
    space_id BIGINT NOT NULL REFERENCES spaces(id),
    writer_user_id BIGINT NOT NULL REFERENCES users(id),
    post_it_inventory_id BIGINT NULL REFERENCES user_asset_inventories(id),
    content VARCHAR(30) NOT NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    reply_count INT NOT NULL DEFAULT 0,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT ck_guestbooks_visibility CHECK (visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE'))
);

CREATE TABLE guestbook_replies (
    id BIGSERIAL PRIMARY KEY,
    guestbook_id BIGINT NOT NULL REFERENCES guestbooks(id),
    writer_user_id BIGINT NOT NULL REFERENCES users(id),
    content VARCHAR(1000) NOT NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT ck_guestbook_replies_visibility CHECK (visibility_scope IN ('PUBLIC', 'PRIVATE'))
);

-- ---------------------------------------------------------------------------
-- Feed / notifications / reports / promotions
-- ---------------------------------------------------------------------------

CREATE TABLE feed_events (
    id BIGSERIAL PRIMARY KEY,
    actor_user_id BIGINT NOT NULL REFERENCES users(id),
    space_id BIGINT NULL REFERENCES spaces(id),
    book_id BIGINT NULL REFERENCES books(id),
    reading_record_id BIGINT NULL REFERENCES reading_records(id),
    event_type VARCHAR(50) NOT NULL,
    target_type VARCHAR(50) NOT NULL,
    target_id BIGINT NOT NULL,
    visibility_scope VARCHAR(20) NOT NULL DEFAULT 'PUBLIC',
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_feed_events_visibility CHECK (visibility_scope IN ('PUBLIC', 'FOLLOWERS', 'PRIVATE'))
);

CREATE TABLE notifications (
    id BIGSERIAL PRIMARY KEY,
    receiver_user_id BIGINT NOT NULL REFERENCES users(id),
    sender_user_id BIGINT NULL REFERENCES users(id),
    type VARCHAR(50) NOT NULL,
    target_type VARCHAR(50) NULL,
    target_id BIGINT NULL,
    message VARCHAR(255) NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE reports (
    id BIGSERIAL PRIMARY KEY,
    reporter_user_id BIGINT NOT NULL REFERENCES users(id),
    reported_user_id BIGINT NULL REFERENCES users(id),
    target_type VARCHAR(50) NOT NULL,
    target_id BIGINT NOT NULL,
    reason_type VARCHAR(50) NOT NULL,
    reason_detail VARCHAR(500) NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    is_blinded_to_reporter BOOLEAN NOT NULL DEFAULT TRUE,
    is_globally_blinded BOOLEAN NOT NULL DEFAULT FALSE,
    reviewed_by_user_id BIGINT NULL REFERENCES users(id),
    reviewed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uk_reports_reporter_target UNIQUE (reporter_user_id, target_type, target_id)
);

CREATE TABLE book_promotions (
    id BIGSERIAL PRIMARY KEY,
    book_id BIGINT NOT NULL REFERENCES books(id),
    promotion_type VARCHAR(30) NOT NULL,
    title VARCHAR(100) NOT NULL,
    description VARCHAR(500) NULL,
    image_url VARCHAR(500) NULL,
    landing_url VARCHAR(500) NULL,
    sponsor_name VARCHAR(100) NULL,
    display_order INT NOT NULL DEFAULT 0,
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- Guest migration / onboarding support
-- ---------------------------------------------------------------------------

CREATE TABLE guest_migration_imports (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    migration_status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    payload JSONB NOT NULL,
    imported_space_id BIGINT NULL REFERENCES spaces(id),
    failure_reason VARCHAR(500) NULL,
    requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- Recommended lookup indexes
-- ---------------------------------------------------------------------------

CREATE INDEX idx_user_social_accounts_user ON user_social_accounts(user_id);
CREATE INDEX idx_auth_refresh_tokens_user ON auth_refresh_tokens(user_id);
CREATE INDEX idx_user_asset_inventories_user ON user_asset_inventories(user_id);
CREATE INDEX idx_asset_purchases_user ON asset_purchases(user_id);
CREATE INDEX idx_books_title ON books(title);
CREATE INDEX idx_book_external_mappings_book ON book_external_mappings(book_id);
CREATE INDEX idx_spaces_owner ON spaces(owner_user_id);
CREATE INDEX idx_space_visits_space_date ON space_visits(space_id, visited_date);
CREATE INDEX idx_space_likes_space ON space_likes(space_id);
CREATE INDEX idx_bookshelves_space ON bookshelves(space_id);
CREATE INDEX idx_bookshelf_sections_bookshelf ON bookshelf_sections(bookshelf_id);
CREATE INDEX idx_user_books_user ON user_books(user_id);
CREATE INDEX idx_user_books_book ON user_books(book_id);
CREATE INDEX idx_bookshelf_items_bookshelf ON bookshelf_items(bookshelf_id);
CREATE INDEX idx_bookshelf_items_section ON bookshelf_items(bookshelf_section_id);
CREATE INDEX idx_reading_records_user_book ON reading_records(user_book_id);
CREATE INDEX idx_reading_record_comments_record ON reading_record_comments(reading_record_id);
CREATE INDEX idx_guestbooks_space ON guestbooks(space_id);
CREATE INDEX idx_feed_events_actor_occurred ON feed_events(actor_user_id, occurred_at DESC);
CREATE INDEX idx_feed_events_occurred ON feed_events(occurred_at DESC);
CREATE INDEX idx_notifications_receiver_created ON notifications(receiver_user_id, created_at DESC);
CREATE INDEX idx_reports_target ON reports(target_type, target_id);
