-- DotShelf ERDCloud import schema.
-- 목적: ERDCloud SQL 가져오기용 MySQL DDL.
-- 주의: 실제 실행용 PostgreSQL 스키마는 schema.sql을 기준으로 한다.
SET NAMES utf8mb4;

CREATE TABLE `asset_categories` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 카테고리 ID',
    `parent_category_id` BIGINT NULL COMMENT '상위 에셋 카테고리 ID',
    `code` VARCHAR(50) NOT NULL COMMENT '카테고리 코드',
    `name` VARCHAR(100) NOT NULL COMMENT '카테고리명',
    `display_order` INT NOT NULL DEFAULT 0 COMMENT '노출 순서',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_asset_categories_code` (`code`),
    KEY `idx_asset_categories_parent_category_id` (`parent_category_id`),
    CONSTRAINT `fk_asset_categories_parent_category_id` FOREIGN KEY (`parent_category_id`) REFERENCES `asset_categories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='에셋 카테고리';

CREATE TABLE `assets` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 ID',
    `category_id` BIGINT NOT NULL COMMENT '에셋 카테고리 ID',
    `code` VARCHAR(80) NOT NULL COMMENT '에셋 코드',
    `name` VARCHAR(100) NOT NULL COMMENT '에셋명',
    `tier` VARCHAR(30) NOT NULL DEFAULT 'NORMAL' COMMENT '등급',
    `asset_scope` VARCHAR(30) NOT NULL COMMENT '적용 범위',
    `asset_type` VARCHAR(50) NOT NULL COMMENT '에셋 타입',
    `unity_resource_key` VARCHAR(150) NULL COMMENT 'Unity 리소스 키',
    `asset_url` VARCHAR(500) NULL COMMENT '원본 에셋 URL',
    `preview_image_url` VARCHAR(500) NULL COMMENT '미리보기 이미지 URL',
    `price_pages` INT NOT NULL DEFAULT 0 COMMENT '가격(페이지)',
    `creator` VARCHAR(100) NULL COMMENT '제작자',
    `supplier` VARCHAR(100) NULL COMMENT '공급자',
    `compatible_targets` TEXT NULL COMMENT '호환 대상 JSON',
    `released_at` DATETIME NULL COMMENT '출시 일시',
    `valid_until` DATETIME NULL COMMENT '판매 종료 일시',
    `is_default` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '기본 제공 여부',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_assets_code` (`code`),
    KEY `idx_assets_category_id` (`category_id`),
    CONSTRAINT `fk_assets_category_id` FOREIGN KEY (`category_id`) REFERENCES `asset_categories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='꾸미기/스킨/테마 에셋';

CREATE TABLE `asset_tags` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 태그 ID',
    `code` VARCHAR(50) NOT NULL COMMENT '태그 코드',
    `name` VARCHAR(100) NOT NULL COMMENT '태그명',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_asset_tags_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='에셋 태그';

CREATE TABLE `asset_tag_mappings` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 태그 매핑 ID',
    `asset_id` BIGINT NOT NULL COMMENT '에셋 ID',
    `tag_id` BIGINT NOT NULL COMMENT '태그 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_asset_tag_mappings_asset_tag` (`asset_id`, `tag_id`),
    KEY `idx_asset_tag_mappings_asset_id` (`asset_id`),
    KEY `idx_asset_tag_mappings_tag_id` (`tag_id`),
    CONSTRAINT `fk_asset_tag_mappings_asset_id` FOREIGN KEY (`asset_id`) REFERENCES `assets` (`id`),
    CONSTRAINT `fk_asset_tag_mappings_tag_id` FOREIGN KEY (`tag_id`) REFERENCES `asset_tags` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='에셋-태그 매핑';

CREATE TABLE `users` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '사용자 ID',
    `email` VARCHAR(100) NULL COMMENT '이메일',
    `nickname` VARCHAR(50) NOT NULL COMMENT '닉네임',
    `profile_image_url` VARCHAR(500) NULL COMMENT '프로필 이미지 URL',
    `introduction` VARCHAR(255) NULL COMMENT '한 줄 소개',
    `role` VARCHAR(20) NOT NULL DEFAULT 'USER' COMMENT '권한',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '계정 상태',
    `is_onboarded` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '온보딩 완료 여부',
    `last_login_at` DATETIME NULL COMMENT '마지막 로그인 일시',
    `deleted_at` DATETIME NULL COMMENT '탈퇴/삭제 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_users_email` (`email`),
    UNIQUE KEY `uk_users_nickname` (`nickname`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자';

CREATE TABLE `user_social_accounts` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '소셜 계정 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `provider` VARCHAR(30) NOT NULL COMMENT '소셜 제공자',
    `provider_user_id` VARCHAR(100) NOT NULL COMMENT '소셜 제공자 사용자 ID',
    `email_at_provider` VARCHAR(100) NULL COMMENT '소셜 계정 이메일',
    `linked_at` DATETIME NOT NULL COMMENT '연동 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_social_accounts_provider_user` (`provider`, `provider_user_id`),
    UNIQUE KEY `uk_user_social_accounts_user_provider` (`user_id`, `provider`),
    KEY `idx_user_social_accounts_user_id` (`user_id`),
    CONSTRAINT `fk_user_social_accounts_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 소셜 로그인 계정';

CREATE TABLE `auth_refresh_tokens` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '리프레시 토큰 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `token_hash` VARCHAR(255) NOT NULL COMMENT '리프레시 토큰 해시',
    `device_id` VARCHAR(100) NULL COMMENT '디바이스 ID',
    `user_agent` VARCHAR(500) NULL COMMENT 'User-Agent',
    `ip_address` VARCHAR(45) NULL COMMENT 'IP 주소',
    `issued_at` DATETIME NOT NULL COMMENT '발급 일시',
    `expires_at` DATETIME NOT NULL COMMENT '만료 일시',
    `revoked_at` DATETIME NULL COMMENT '폐기 일시',
    `replaced_by_token_id` BIGINT NULL COMMENT '회전 후 대체 토큰 ID',
    `reuse_detected_at` DATETIME NULL COMMENT '재사용 탐지 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_auth_refresh_tokens_hash` (`token_hash`),
    KEY `idx_auth_refresh_tokens_user_id` (`user_id`),
    KEY `idx_auth_refresh_tokens_replaced_by_token_id` (`replaced_by_token_id`),
    CONSTRAINT `fk_auth_refresh_tokens_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_auth_refresh_tokens_replaced_by_token_id` FOREIGN KEY (`replaced_by_token_id`) REFERENCES `auth_refresh_tokens` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='JWT 리프레시 토큰';

CREATE TABLE `device_tokens` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '디바이스 토큰 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `platform` VARCHAR(30) NOT NULL COMMENT '플랫폼',
    `token` VARCHAR(500) NOT NULL COMMENT '푸시 토큰',
    `device_id` VARCHAR(100) NULL COMMENT '디바이스 ID',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `last_used_at` DATETIME NULL COMMENT '마지막 사용 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_device_tokens_token` (`token`),
    KEY `idx_device_tokens_user_id` (`user_id`),
    CONSTRAINT `fk_device_tokens_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='디바이스 푸시 토큰';

CREATE TABLE `user_notification_settings` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '알림 설정 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `notify_new_guestbook` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '새 방명록 알림 여부',
    `notify_guestbook_reply` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '방명록 답장 알림 여부',
    `notify_reading_record_reaction` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '독서기록 반응 알림 여부',
    `notify_new_follower` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '새 팔로워 알림 여부',
    `notify_book_added_by_others` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '타인의 내 책 추가 알림 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_notification_settings_user` (`user_id`),
    KEY `idx_user_notification_settings_user_id` (`user_id`),
    CONSTRAINT `fk_user_notification_settings_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 알림 설정';

CREATE TABLE `user_feed_settings` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '피드 설정 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `show_book_added` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '책 추가 활동 노출 여부',
    `show_reading_record` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '독서기록 활동 노출 여부',
    `show_space_visit` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '공간 방문 활동 노출 여부',
    `show_guestbook` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '방명록 활동 노출 여부',
    `show_reading_record_comment` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '독서기록 댓글 활동 노출 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_feed_settings_user` (`user_id`),
    KEY `idx_user_feed_settings_user_id` (`user_id`),
    CONSTRAINT `fk_user_feed_settings_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 피드 노출 설정';

CREATE TABLE `user_blocks` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '차단 ID',
    `blocker_user_id` BIGINT NOT NULL COMMENT '차단한 사용자 ID',
    `blocked_user_id` BIGINT NOT NULL COMMENT '차단된 사용자 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_blocks_blocker_blocked` (`blocker_user_id`, `blocked_user_id`),
    KEY `idx_user_blocks_blocker_user_id` (`blocker_user_id`),
    KEY `idx_user_blocks_blocked_user_id` (`blocked_user_id`),
    CONSTRAINT `fk_user_blocks_blocker_user_id` FOREIGN KEY (`blocker_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_user_blocks_blocked_user_id` FOREIGN KEY (`blocked_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 차단';

CREATE TABLE `follows` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '팔로우 ID',
    `follower_user_id` BIGINT NOT NULL COMMENT '팔로워 사용자 ID',
    `following_user_id` BIGINT NOT NULL COMMENT '팔로잉 사용자 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_follows_follower_following` (`follower_user_id`, `following_user_id`),
    KEY `idx_follows_follower_user_id` (`follower_user_id`),
    KEY `idx_follows_following_user_id` (`following_user_id`),
    CONSTRAINT `fk_follows_follower_user_id` FOREIGN KEY (`follower_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_follows_following_user_id` FOREIGN KEY (`following_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='팔로우 관계';

CREATE TABLE `page_wallets` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '페이지 지갑 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `balance_pages` INT NOT NULL DEFAULT 0 COMMENT '보유 페이지 잔액',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_page_wallets_user` (`user_id`),
    KEY `idx_page_wallets_user_id` (`user_id`),
    CONSTRAINT `fk_page_wallets_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='페이지 재화 지갑';

CREATE TABLE `payment_methods` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '결제 수단 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `provider` VARCHAR(30) NOT NULL COMMENT '결제 제공자',
    `provider_payment_method_id` VARCHAR(150) NOT NULL COMMENT '제공자 결제수단 ID',
    `display_name` VARCHAR(100) NULL COMMENT '노출명',
    `brand` VARCHAR(50) NULL COMMENT '카드/결제 브랜드',
    `last4` VARCHAR(4) NULL COMMENT '카드 끝 4자리',
    `is_default` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '기본 결제수단 여부',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '결제수단 상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_payment_methods_provider_id` (`provider`, `provider_payment_method_id`),
    KEY `idx_payment_methods_user_id` (`user_id`),
    CONSTRAINT `fk_payment_methods_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='결제 수단';

CREATE TABLE `payment_orders` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '결제 주문 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `payment_method_id` BIGINT NULL COMMENT '결제 수단 ID',
    `platform` VARCHAR(30) NOT NULL COMMENT '결제 플랫폼',
    `product_type` VARCHAR(30) NOT NULL COMMENT '상품 타입',
    `product_id` BIGINT NULL COMMENT '상품 ID(다형 참조)',
    `amount_krw` INT NOT NULL DEFAULT 0 COMMENT '결제 금액 원화',
    `pages_amount` INT NOT NULL DEFAULT 0 COMMENT '충전/구매 페이지 수량',
    `status` VARCHAR(30) NOT NULL DEFAULT 'PENDING' COMMENT '결제 상태',
    `provider_order_id` VARCHAR(150) NULL COMMENT '제공자 주문 ID',
    `provider_transaction_id` VARCHAR(150) NULL COMMENT '제공자 거래 ID',
    `receipt_payload` TEXT NULL COMMENT '영수증 원문 JSON',
    `requested_at` DATETIME NOT NULL COMMENT '요청 일시',
    `approved_at` DATETIME NULL COMMENT '승인 일시',
    `failed_at` DATETIME NULL COMMENT '실패 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    KEY `idx_payment_orders_user_id` (`user_id`),
    KEY `idx_payment_orders_payment_method_id` (`payment_method_id`),
    CONSTRAINT `fk_payment_orders_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_payment_orders_payment_method_id` FOREIGN KEY (`payment_method_id`) REFERENCES `payment_methods` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='결제 주문';

CREATE TABLE `page_transactions` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '페이지 거래 ID',
    `wallet_id` BIGINT NOT NULL COMMENT '페이지 지갑 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `payment_order_id` BIGINT NULL COMMENT '결제 주문 ID',
    `transaction_type` VARCHAR(30) NOT NULL COMMENT '거래 타입',
    `amount_pages` INT NOT NULL COMMENT '증감 페이지 수량',
    `balance_after` INT NOT NULL COMMENT '거래 후 잔액',
    `reason` VARCHAR(100) NULL COMMENT '거래 사유',
    `reference_type` VARCHAR(30) NULL COMMENT '참조 타입',
    `reference_id` BIGINT NULL COMMENT '참조 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    KEY `idx_page_transactions_wallet_id` (`wallet_id`),
    KEY `idx_page_transactions_user_id` (`user_id`),
    KEY `idx_page_transactions_payment_order_id` (`payment_order_id`),
    CONSTRAINT `fk_page_transactions_wallet_id` FOREIGN KEY (`wallet_id`) REFERENCES `page_wallets` (`id`),
    CONSTRAINT `fk_page_transactions_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_page_transactions_payment_order_id` FOREIGN KEY (`payment_order_id`) REFERENCES `payment_orders` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='페이지 재화 거래 원장';

CREATE TABLE `user_asset_inventories` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '사용자 에셋 인벤토리 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `asset_id` BIGINT NOT NULL COMMENT '에셋 ID',
    `grant_type` VARCHAR(30) NOT NULL COMMENT '획득 유형',
    `acquired_at` DATETIME NOT NULL COMMENT '획득 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_asset_inventories_user_asset` (`user_id`, `asset_id`),
    KEY `idx_user_asset_inventories_user_id` (`user_id`),
    KEY `idx_user_asset_inventories_asset_id` (`asset_id`),
    CONSTRAINT `fk_user_asset_inventories_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_user_asset_inventories_asset_id` FOREIGN KEY (`asset_id`) REFERENCES `assets` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 보유 에셋';

CREATE TABLE `asset_purchases` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 구매 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `asset_id` BIGINT NOT NULL COMMENT '에셋 ID',
    `inventory_id` BIGINT NULL COMMENT '생성된 인벤토리 ID',
    `payment_order_id` BIGINT NULL COMMENT '결제 주문 ID',
    `page_transaction_id` BIGINT NULL COMMENT '페이지 거래 ID',
    `price_pages` INT NOT NULL COMMENT '구매 가격(페이지)',
    `purchase_status` VARCHAR(30) NOT NULL DEFAULT 'COMPLETED' COMMENT '구매 상태',
    `purchased_at` DATETIME NOT NULL COMMENT '구매 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    KEY `idx_asset_purchases_user_id` (`user_id`),
    KEY `idx_asset_purchases_asset_id` (`asset_id`),
    KEY `idx_asset_purchases_inventory_id` (`inventory_id`),
    KEY `idx_asset_purchases_payment_order_id` (`payment_order_id`),
    KEY `idx_asset_purchases_page_transaction_id` (`page_transaction_id`),
    CONSTRAINT `fk_asset_purchases_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_asset_purchases_asset_id` FOREIGN KEY (`asset_id`) REFERENCES `assets` (`id`),
    CONSTRAINT `fk_asset_purchases_inventory_id` FOREIGN KEY (`inventory_id`) REFERENCES `user_asset_inventories` (`id`),
    CONSTRAINT `fk_asset_purchases_payment_order_id` FOREIGN KEY (`payment_order_id`) REFERENCES `payment_orders` (`id`),
    CONSTRAINT `fk_asset_purchases_page_transaction_id` FOREIGN KEY (`page_transaction_id`) REFERENCES `page_transactions` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='에셋 구매 이력';

CREATE TABLE `asset_wishlists` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '에셋 찜 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `asset_id` BIGINT NOT NULL COMMENT '에셋 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_asset_wishlists_user_asset` (`user_id`, `asset_id`),
    KEY `idx_asset_wishlists_user_id` (`user_id`),
    KEY `idx_asset_wishlists_asset_id` (`asset_id`),
    CONSTRAINT `fk_asset_wishlists_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_asset_wishlists_asset_id` FOREIGN KEY (`asset_id`) REFERENCES `assets` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='에셋 찜 목록';

CREATE TABLE `books` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '도서 ID',
    `isbn13` VARCHAR(13) NULL COMMENT 'ISBN13',
    `isbn10` VARCHAR(10) NULL COMMENT 'ISBN10',
    `title` VARCHAR(255) NOT NULL COMMENT '도서명',
    `author` VARCHAR(255) NOT NULL COMMENT '저자',
    `publisher` VARCHAR(255) NULL COMMENT '출판사',
    `published_date` DATE NULL COMMENT '출간일',
    `description` TEXT NULL COMMENT '도서 소개',
    `cover_image_url` VARCHAR(500) NULL COMMENT '표지 이미지 URL',
    `page_count` INT NULL COMMENT '쪽수',
    `language` VARCHAR(20) NULL COMMENT '언어',
    `view_count` BIGINT NOT NULL DEFAULT 0 COMMENT '조회 수',
    `like_count` BIGINT NOT NULL DEFAULT 0 COMMENT '좋아요 수',
    `bookshelf_add_count` BIGINT NOT NULL DEFAULT 0 COMMENT '책장 추가 수',
    `rating_sum` DECIMAL(10,1) NOT NULL DEFAULT 0.0 COMMENT '별점 합',
    `rating_count` INT NOT NULL DEFAULT 0 COMMENT '별점 개수',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_books_isbn13` (`isbn13`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='도서 마스터';

CREATE TABLE `book_external_mappings` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '외부 도서 매핑 ID',
    `book_id` BIGINT NOT NULL COMMENT '도서 ID',
    `source_provider` VARCHAR(30) NOT NULL COMMENT '외부 API 제공자',
    `external_book_id` VARCHAR(100) NOT NULL COMMENT '외부 도서 ID',
    `source_url` VARCHAR(500) NULL COMMENT '외부 원문 URL',
    `raw_payload` TEXT NULL COMMENT '외부 API 원문 JSON',
    `last_synced_at` DATETIME NULL COMMENT '마지막 동기화 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_book_external_mappings_provider_external` (`source_provider`, `external_book_id`),
    KEY `idx_book_external_mappings_book_id` (`book_id`),
    CONSTRAINT `fk_book_external_mappings_book_id` FOREIGN KEY (`book_id`) REFERENCES `books` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='외부 도서 API 매핑';

CREATE TABLE `book_cover_products` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '도서 3D 표지 상품 ID',
    `book_id` BIGINT NOT NULL COMMENT '도서 ID',
    `code` VARCHAR(80) NOT NULL COMMENT '상품 코드',
    `name` VARCHAR(100) NOT NULL COMMENT '상품명',
    `asset_url` VARCHAR(500) NOT NULL COMMENT '3D 표지 에셋 URL',
    `preview_image_url` VARCHAR(500) NULL COMMENT '미리보기 이미지 URL',
    `price_pages` INT NOT NULL COMMENT '가격(페이지)',
    `supplier` VARCHAR(100) NULL COMMENT '공급자',
    `license_status` VARCHAR(30) NOT NULL DEFAULT 'ACTIVE' COMMENT '라이선스 상태',
    `released_at` DATETIME NULL COMMENT '출시 일시',
    `valid_until` DATETIME NULL COMMENT '판매 종료 일시',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_book_cover_products_code` (`code`),
    KEY `idx_book_cover_products_book_id` (`book_id`),
    CONSTRAINT `fk_book_cover_products_book_id` FOREIGN KEY (`book_id`) REFERENCES `books` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='도서 3D 표지 상품';

CREATE TABLE `user_book_cover_purchases` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '도서 표지 구매 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `book_cover_product_id` BIGINT NOT NULL COMMENT '도서 3D 표지 상품 ID',
    `payment_order_id` BIGINT NULL COMMENT '결제 주문 ID',
    `page_transaction_id` BIGINT NULL COMMENT '페이지 거래 ID',
    `price_pages` INT NOT NULL COMMENT '구매 가격(페이지)',
    `purchased_at` DATETIME NOT NULL COMMENT '구매 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_book_cover_purchases_user_product` (`user_id`, `book_cover_product_id`),
    KEY `idx_user_book_cover_purchases_user_id` (`user_id`),
    KEY `idx_user_book_cover_purchases_book_cover_product_id` (`book_cover_product_id`),
    KEY `idx_user_book_cover_purchases_payment_order_id` (`payment_order_id`),
    KEY `idx_user_book_cover_purchases_page_transaction_id` (`page_transaction_id`),
    CONSTRAINT `fk_user_book_cover_purchases_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_user_book_cover_purchases_book_cover_product_id` FOREIGN KEY (`book_cover_product_id`) REFERENCES `book_cover_products` (`id`),
    CONSTRAINT `fk_user_book_cover_purchases_payment_order_id` FOREIGN KEY (`payment_order_id`) REFERENCES `payment_orders` (`id`),
    CONSTRAINT `fk_user_book_cover_purchases_page_transaction_id` FOREIGN KEY (`page_transaction_id`) REFERENCES `page_transactions` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 도서 3D 표지 구매';

CREATE TABLE `space_templates` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 템플릿 ID',
    `code` VARCHAR(50) NOT NULL COMMENT '템플릿 코드',
    `name` VARCHAR(100) NOT NULL COMMENT '템플릿명',
    `preview_image_url` VARCHAR(500) NULL COMMENT '미리보기 이미지 URL',
    `layout_payload` TEXT NOT NULL COMMENT '초기 공간 레이아웃 JSON',
    `display_order` INT NOT NULL DEFAULT 0 COMMENT '노출 순서',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_space_templates_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 초기 템플릿';

CREATE TABLE `spaces` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 ID',
    `owner_user_id` BIGINT NOT NULL COMMENT '공간 주인 사용자 ID',
    `template_id` BIGINT NULL COMMENT '적용한 공간 템플릿 ID',
    `space_name` VARCHAR(100) NOT NULL COMMENT '공간 이름',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '공간 공개 범위',
    `guestbook_visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '방명록 목록 공개 범위',
    `visit_count` BIGINT NOT NULL DEFAULT 0 COMMENT '누적 방문 수',
    `like_count` BIGINT NOT NULL DEFAULT 0 COMMENT '공간 좋아요 수',
    `layout_version` BIGINT NOT NULL DEFAULT 0 COMMENT '레이아웃 버전',
    `current_theme_inventory_id` BIGINT NULL COMMENT '현재 전체 테마 인벤토리 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    KEY `idx_spaces_owner_user_id` (`owner_user_id`),
    KEY `idx_spaces_template_id` (`template_id`),
    KEY `idx_spaces_current_theme_inventory_id` (`current_theme_inventory_id`),
    CONSTRAINT `fk_spaces_owner_user_id` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_spaces_template_id` FOREIGN KEY (`template_id`) REFERENCES `space_templates` (`id`),
    CONSTRAINT `fk_spaces_current_theme_inventory_id` FOREIGN KEY (`current_theme_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자 3D 공간';

CREATE TABLE `space_visits` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 방문 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `visitor_user_id` BIGINT NOT NULL COMMENT '방문자 사용자 ID',
    `visited_date` DATE NOT NULL COMMENT '방문 날짜',
    `visited_at` DATETIME NOT NULL COMMENT '방문 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_space_visits_space_visitor_date` (`space_id`, `visitor_user_id`, `visited_date`),
    KEY `idx_space_visits_space_id` (`space_id`),
    KEY `idx_space_visits_visitor_user_id` (`visitor_user_id`),
    CONSTRAINT `fk_space_visits_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_space_visits_visitor_user_id` FOREIGN KEY (`visitor_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 방문 기록';

CREATE TABLE `space_likes` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 좋아요 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `user_id` BIGINT NOT NULL COMMENT '좋아요 사용자 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_space_likes_space_user` (`space_id`, `user_id`),
    KEY `idx_space_likes_space_id` (`space_id`),
    KEY `idx_space_likes_user_id` (`user_id`),
    CONSTRAINT `fk_space_likes_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_space_likes_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 좋아요';

CREATE TABLE `space_asset_applications` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 에셋 적용 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `asset_inventory_id` BIGINT NOT NULL COMMENT '사용자 보유 에셋 ID',
    `application_target` VARCHAR(50) NOT NULL COMMENT '적용 대상',
    `applied_at` DATETIME NOT NULL COMMENT '적용 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_space_asset_applications_space_target` (`space_id`, `application_target`),
    KEY `idx_space_asset_applications_space_id` (`space_id`),
    KEY `idx_space_asset_applications_asset_inventory_id` (`asset_inventory_id`),
    CONSTRAINT `fk_space_asset_applications_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_space_asset_applications_asset_inventory_id` FOREIGN KEY (`asset_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 필수 오브젝트/스킨 적용';

CREATE TABLE `space_decorations` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 배치 꾸미기 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `asset_inventory_id` BIGINT NOT NULL COMMENT '사용자 보유 에셋 ID',
    `anchor_type` VARCHAR(30) NOT NULL COMMENT '배치 기준',
    `pos_x` DECIMAL(10,2) NOT NULL COMMENT 'X 좌표',
    `pos_y` DECIMAL(10,2) NOT NULL COMMENT 'Y 좌표',
    `pos_z` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT 'Z 좌표',
    `z_index` INT NOT NULL DEFAULT 0 COMMENT '노출 순서',
    `rotation_deg` DECIMAL(6,2) NOT NULL DEFAULT 0.00 COMMENT '회전 각도',
    `scale_x` DECIMAL(6,2) NOT NULL DEFAULT 1.00 COMMENT 'X 스케일',
    `scale_y` DECIMAL(6,2) NOT NULL DEFAULT 1.00 COMMENT 'Y 스케일',
    `flip_x` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'X축 반전 여부',
    `flip_y` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Y축 반전 여부',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    KEY `idx_space_decorations_space_id` (`space_id`),
    KEY `idx_space_decorations_asset_inventory_id` (`asset_inventory_id`),
    CONSTRAINT `fk_space_decorations_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_space_decorations_asset_inventory_id` FOREIGN KEY (`asset_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 벽/바닥 배치 꾸미기';

CREATE TABLE `space_layout_snapshots` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '공간 레이아웃 스냅샷 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `layout_version` BIGINT NOT NULL COMMENT '레이아웃 버전',
    `layout_payload` TEXT NOT NULL COMMENT '레이아웃 JSON',
    `saved_by_user_id` BIGINT NOT NULL COMMENT '저장 사용자 ID',
    `saved_at` DATETIME NOT NULL COMMENT '저장 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_space_layout_snapshots_space_version` (`space_id`, `layout_version`),
    KEY `idx_space_layout_snapshots_space_id` (`space_id`),
    KEY `idx_space_layout_snapshots_saved_by_user_id` (`saved_by_user_id`),
    CONSTRAINT `fk_space_layout_snapshots_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_space_layout_snapshots_saved_by_user_id` FOREIGN KEY (`saved_by_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 레이아웃 자동 저장 스냅샷';

CREATE TABLE `bookshelves` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '책장 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `body_inventory_id` BIGINT NOT NULL COMMENT '책장 본체 인벤토리 ID',
    `skin_inventory_id` BIGINT NULL COMMENT '책장 스킨 인벤토리 ID',
    `name` VARCHAR(100) NOT NULL COMMENT '책장 이름',
    `description` VARCHAR(255) NULL COMMENT '책장 설명',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '책장 공개 범위',
    `display_order` INT NOT NULL COMMENT '공간 내 책장 순서',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '책장 상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_bookshelves_space_display_order` (`space_id`, `display_order`),
    KEY `idx_bookshelves_space_id` (`space_id`),
    KEY `idx_bookshelves_body_inventory_id` (`body_inventory_id`),
    KEY `idx_bookshelves_skin_inventory_id` (`skin_inventory_id`),
    CONSTRAINT `fk_bookshelves_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_bookshelves_body_inventory_id` FOREIGN KEY (`body_inventory_id`) REFERENCES `user_asset_inventories` (`id`),
    CONSTRAINT `fk_bookshelves_skin_inventory_id` FOREIGN KEY (`skin_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='책장';

CREATE TABLE `bookshelf_sections` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '책장 섹션 ID',
    `bookshelf_id` BIGINT NOT NULL COMMENT '책장 ID',
    `row_no` INT NOT NULL COMMENT '행 번호',
    `column_no` INT NOT NULL COMMENT '열 번호',
    `max_item_count` INT NOT NULL COMMENT '최대 배치 개수',
    `alignment` VARCHAR(20) NOT NULL DEFAULT 'LEFT' COMMENT '섹션 정렬',
    `pos_x` DECIMAL(10,2) NULL COMMENT 'X 좌표',
    `pos_y` DECIMAL(10,2) NULL COMMENT 'Y 좌표',
    `width` DECIMAL(10,2) NULL COMMENT '너비',
    `height` DECIMAL(10,2) NULL COMMENT '높이',
    `z_index` INT NOT NULL DEFAULT 0 COMMENT '노출 순서',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_bookshelf_sections_bookshelf_row_column` (`bookshelf_id`, `row_no`, `column_no`),
    KEY `idx_bookshelf_sections_bookshelf_id` (`bookshelf_id`),
    CONSTRAINT `fk_bookshelf_sections_bookshelf_id` FOREIGN KEY (`bookshelf_id`) REFERENCES `bookshelves` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='책장 4x4 섹션';

CREATE TABLE `user_books` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '사용자 도서 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `book_id` BIGINT NOT NULL COMMENT '도서 ID',
    `selected_cover_product_id` BIGINT NULL COMMENT '선택한 3D 표지 상품 ID',
    `reading_status` VARCHAR(20) NOT NULL DEFAULT 'UNREAD' COMMENT '독서 상태',
    `storage_status` VARCHAR(20) NOT NULL DEFAULT 'ON_SHELF' COMMENT '보관 상태',
    `owner_rating` DECIMAL(2,1) NULL COMMENT '공간 주인 별점',
    `is_liked` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '공간 주인 책 좋아요 여부',
    `added_at` DATETIME NOT NULL COMMENT '추가 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_books_user_book` (`user_id`, `book_id`),
    KEY `idx_user_books_user_id` (`user_id`),
    KEY `idx_user_books_book_id` (`book_id`),
    KEY `idx_user_books_selected_cover_product_id` (`selected_cover_product_id`),
    CONSTRAINT `fk_user_books_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_user_books_book_id` FOREIGN KEY (`book_id`) REFERENCES `books` (`id`),
    CONSTRAINT `fk_user_books_selected_cover_product_id` FOREIGN KEY (`selected_cover_product_id`) REFERENCES `book_cover_products` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='사용자가 내 서재에 추가한 책';

CREATE TABLE `bookshelf_items` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '책장 배치 아이템 ID',
    `bookshelf_id` BIGINT NOT NULL COMMENT '책장 ID',
    `bookshelf_section_id` BIGINT NULL COMMENT '책장 섹션 ID',
    `item_type` VARCHAR(20) NOT NULL COMMENT '아이템 타입(책/꾸미기)',
    `user_book_id` BIGINT NULL COMMENT '사용자 도서 ID',
    `asset_inventory_id` BIGINT NULL COMMENT '사용자 보유 에셋 ID',
    `slot_no` INT NULL COMMENT '섹션 내 슬롯 번호',
    `facing_direction` VARCHAR(20) NOT NULL DEFAULT 'FRONT' COMMENT '바라보는 방향',
    `display_style` VARCHAR(30) NOT NULL DEFAULT 'SPINE' COMMENT '배치 스타일',
    `rotation_deg` DECIMAL(6,2) NOT NULL DEFAULT 0.00 COMMENT '회전 각도',
    `pos_x` DECIMAL(10,2) NULL COMMENT 'X 좌표',
    `pos_y` DECIMAL(10,2) NULL COMMENT 'Y 좌표',
    `pos_z` DECIMAL(10,2) NULL COMMENT 'Z 좌표',
    `scale_x` DECIMAL(6,2) NOT NULL DEFAULT 1.00 COMMENT 'X 스케일',
    `scale_y` DECIMAL(6,2) NOT NULL DEFAULT 1.00 COMMENT 'Y 스케일',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '배치 상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_bookshelf_items_section_slot_active` (`bookshelf_section_id`, `slot_no`),
    UNIQUE KEY `uk_bookshelf_items_user_book_active` (`user_book_id`),
    KEY `idx_bookshelf_items_bookshelf_id` (`bookshelf_id`),
    KEY `idx_bookshelf_items_bookshelf_section_id` (`bookshelf_section_id`),
    KEY `idx_bookshelf_items_user_book_id` (`user_book_id`),
    KEY `idx_bookshelf_items_asset_inventory_id` (`asset_inventory_id`),
    CONSTRAINT `fk_bookshelf_items_bookshelf_id` FOREIGN KEY (`bookshelf_id`) REFERENCES `bookshelves` (`id`),
    CONSTRAINT `fk_bookshelf_items_bookshelf_section_id` FOREIGN KEY (`bookshelf_section_id`) REFERENCES `bookshelf_sections` (`id`),
    CONSTRAINT `fk_bookshelf_items_user_book_id` FOREIGN KEY (`user_book_id`) REFERENCES `user_books` (`id`),
    CONSTRAINT `fk_bookshelf_items_asset_inventory_id` FOREIGN KEY (`asset_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='책장 내 책/꾸미기 통합 배치';

CREATE TABLE `reading_records` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '독서기록 ID',
    `user_book_id` BIGINT NOT NULL COMMENT '사용자 도서 ID',
    `title` VARCHAR(150) NULL COMMENT '독서기록 제목',
    `content` TEXT NULL COMMENT '독서기록 본문',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '공개 범위',
    `like_count` INT NOT NULL DEFAULT 0 COMMENT '좋아요 수',
    `comment_count` INT NOT NULL DEFAULT 0 COMMENT '댓글 수',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_reading_records_user_book` (`user_book_id`),
    KEY `idx_reading_records_user_book_id` (`user_book_id`),
    CONSTRAINT `fk_reading_records_user_book_id` FOREIGN KEY (`user_book_id`) REFERENCES `user_books` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='독서기록';

CREATE TABLE `reading_record_likes` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '독서기록 좋아요 ID',
    `reading_record_id` BIGINT NOT NULL COMMENT '독서기록 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_reading_record_likes_record_user` (`reading_record_id`, `user_id`),
    KEY `idx_reading_record_likes_reading_record_id` (`reading_record_id`),
    KEY `idx_reading_record_likes_user_id` (`user_id`),
    CONSTRAINT `fk_reading_record_likes_reading_record_id` FOREIGN KEY (`reading_record_id`) REFERENCES `reading_records` (`id`),
    CONSTRAINT `fk_reading_record_likes_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='독서기록 좋아요';

CREATE TABLE `reading_record_comments` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '독서기록 댓글 ID',
    `reading_record_id` BIGINT NOT NULL COMMENT '독서기록 ID',
    `user_id` BIGINT NOT NULL COMMENT '작성자 사용자 ID',
    `parent_comment_id` BIGINT NULL COMMENT '부모 댓글 ID',
    `depth` INT NOT NULL DEFAULT 0 COMMENT '댓글 깊이',
    `content` VARCHAR(1000) NOT NULL COMMENT '댓글 내용',
    `like_count` INT NOT NULL DEFAULT 0 COMMENT '좋아요 수',
    `is_deleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '삭제 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    KEY `idx_reading_record_comments_reading_record_id` (`reading_record_id`),
    KEY `idx_reading_record_comments_user_id` (`user_id`),
    KEY `idx_reading_record_comments_parent_comment_id` (`parent_comment_id`),
    CONSTRAINT `fk_reading_record_comments_reading_record_id` FOREIGN KEY (`reading_record_id`) REFERENCES `reading_records` (`id`),
    CONSTRAINT `fk_reading_record_comments_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_reading_record_comments_parent_comment_id` FOREIGN KEY (`parent_comment_id`) REFERENCES `reading_record_comments` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='독서기록 댓글/대댓글';

CREATE TABLE `reading_record_comment_likes` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '독서기록 댓글 좋아요 ID',
    `comment_id` BIGINT NOT NULL COMMENT '댓글 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_reading_record_comment_likes_comment_user` (`comment_id`, `user_id`),
    KEY `idx_reading_record_comment_likes_comment_id` (`comment_id`),
    KEY `idx_reading_record_comment_likes_user_id` (`user_id`),
    CONSTRAINT `fk_reading_record_comment_likes_comment_id` FOREIGN KEY (`comment_id`) REFERENCES `reading_record_comments` (`id`),
    CONSTRAINT `fk_reading_record_comment_likes_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='독서기록 댓글 좋아요';

CREATE TABLE `guestbooks` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '방명록 ID',
    `space_id` BIGINT NOT NULL COMMENT '공간 ID',
    `writer_user_id` BIGINT NOT NULL COMMENT '작성자 사용자 ID',
    `post_it_inventory_id` BIGINT NULL COMMENT '포스트잇 스타일 인벤토리 ID',
    `content` VARCHAR(30) NOT NULL COMMENT '방명록 내용',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '공개 범위',
    `reply_count` INT NOT NULL DEFAULT 0 COMMENT '답장 수',
    `status` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT '상태',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    KEY `idx_guestbooks_space_id` (`space_id`),
    KEY `idx_guestbooks_writer_user_id` (`writer_user_id`),
    KEY `idx_guestbooks_post_it_inventory_id` (`post_it_inventory_id`),
    CONSTRAINT `fk_guestbooks_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_guestbooks_writer_user_id` FOREIGN KEY (`writer_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_guestbooks_post_it_inventory_id` FOREIGN KEY (`post_it_inventory_id`) REFERENCES `user_asset_inventories` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='공간 방명록';

CREATE TABLE `guestbook_replies` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '방명록 답장 ID',
    `guestbook_id` BIGINT NOT NULL COMMENT '방명록 ID',
    `writer_user_id` BIGINT NOT NULL COMMENT '작성자 사용자 ID',
    `content` VARCHAR(1000) NOT NULL COMMENT '답장 내용',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '공개 범위',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    `deleted_at` DATETIME NULL COMMENT '삭제 일시',
    PRIMARY KEY (`id`),
    KEY `idx_guestbook_replies_guestbook_id` (`guestbook_id`),
    KEY `idx_guestbook_replies_writer_user_id` (`writer_user_id`),
    CONSTRAINT `fk_guestbook_replies_guestbook_id` FOREIGN KEY (`guestbook_id`) REFERENCES `guestbooks` (`id`),
    CONSTRAINT `fk_guestbook_replies_writer_user_id` FOREIGN KEY (`writer_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='방명록 답장';

CREATE TABLE `feed_events` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '피드 이벤트 ID',
    `actor_user_id` BIGINT NOT NULL COMMENT '활동 사용자 ID',
    `space_id` BIGINT NULL COMMENT '관련 공간 ID',
    `book_id` BIGINT NULL COMMENT '관련 도서 ID',
    `reading_record_id` BIGINT NULL COMMENT '관련 독서기록 ID',
    `event_type` VARCHAR(50) NOT NULL COMMENT '이벤트 타입',
    `target_type` VARCHAR(50) NOT NULL COMMENT '대상 타입',
    `target_id` BIGINT NOT NULL COMMENT '대상 ID',
    `visibility_scope` VARCHAR(20) NOT NULL DEFAULT 'PUBLIC' COMMENT '공개 범위',
    `occurred_at` DATETIME NOT NULL COMMENT '발생 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    KEY `idx_feed_events_actor_user_id` (`actor_user_id`),
    KEY `idx_feed_events_space_id` (`space_id`),
    KEY `idx_feed_events_book_id` (`book_id`),
    KEY `idx_feed_events_reading_record_id` (`reading_record_id`),
    CONSTRAINT `fk_feed_events_actor_user_id` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_feed_events_space_id` FOREIGN KEY (`space_id`) REFERENCES `spaces` (`id`),
    CONSTRAINT `fk_feed_events_book_id` FOREIGN KEY (`book_id`) REFERENCES `books` (`id`),
    CONSTRAINT `fk_feed_events_reading_record_id` FOREIGN KEY (`reading_record_id`) REFERENCES `reading_records` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='피드 활동 이벤트';

CREATE TABLE `notifications` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '알림 ID',
    `receiver_user_id` BIGINT NOT NULL COMMENT '수신자 사용자 ID',
    `sender_user_id` BIGINT NULL COMMENT '발신자 사용자 ID',
    `type` VARCHAR(50) NOT NULL COMMENT '알림 타입',
    `target_type` VARCHAR(50) NULL COMMENT '대상 타입',
    `target_id` BIGINT NULL COMMENT '대상 ID',
    `message` VARCHAR(255) NOT NULL COMMENT '알림 메시지',
    `is_read` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '읽음 여부',
    `read_at` DATETIME NULL COMMENT '읽은 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    PRIMARY KEY (`id`),
    KEY `idx_notifications_receiver_user_id` (`receiver_user_id`),
    KEY `idx_notifications_sender_user_id` (`sender_user_id`),
    CONSTRAINT `fk_notifications_receiver_user_id` FOREIGN KEY (`receiver_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_notifications_sender_user_id` FOREIGN KEY (`sender_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='알림';

CREATE TABLE `reports` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '신고 ID',
    `reporter_user_id` BIGINT NOT NULL COMMENT '신고자 사용자 ID',
    `reported_user_id` BIGINT NULL COMMENT '신고 대상 사용자 ID',
    `target_type` VARCHAR(50) NOT NULL COMMENT '신고 대상 타입',
    `target_id` BIGINT NOT NULL COMMENT '신고 대상 ID',
    `reason_type` VARCHAR(50) NOT NULL COMMENT '신고 사유 타입',
    `reason_detail` VARCHAR(500) NULL COMMENT '신고 상세 사유',
    `status` VARCHAR(20) NOT NULL DEFAULT 'PENDING' COMMENT '처리 상태',
    `is_blinded_to_reporter` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '신고자에게 가림 여부',
    `is_globally_blinded` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '전체 가림 여부',
    `reviewed_by_user_id` BIGINT NULL COMMENT '검토자 사용자 ID',
    `reviewed_at` DATETIME NULL COMMENT '검토 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_reports_reporter_target` (`reporter_user_id`, `target_type`, `target_id`),
    KEY `idx_reports_reporter_user_id` (`reporter_user_id`),
    KEY `idx_reports_reported_user_id` (`reported_user_id`),
    KEY `idx_reports_reviewed_by_user_id` (`reviewed_by_user_id`),
    CONSTRAINT `fk_reports_reporter_user_id` FOREIGN KEY (`reporter_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_reports_reported_user_id` FOREIGN KEY (`reported_user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_reports_reviewed_by_user_id` FOREIGN KEY (`reviewed_by_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='신고';

CREATE TABLE `book_promotions` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '도서 프로모션 ID',
    `book_id` BIGINT NOT NULL COMMENT '도서 ID',
    `promotion_type` VARCHAR(30) NOT NULL COMMENT '프로모션 타입',
    `title` VARCHAR(100) NOT NULL COMMENT '프로모션 제목',
    `description` VARCHAR(500) NULL COMMENT '프로모션 설명',
    `image_url` VARCHAR(500) NULL COMMENT '이미지 URL',
    `landing_url` VARCHAR(500) NULL COMMENT '랜딩 URL',
    `sponsor_name` VARCHAR(100) NULL COMMENT '광고주/스폰서명',
    `display_order` INT NOT NULL DEFAULT 0 COMMENT '노출 순서',
    `starts_at` DATETIME NOT NULL COMMENT '시작 일시',
    `ends_at` DATETIME NULL COMMENT '종료 일시',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '활성 여부',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    KEY `idx_book_promotions_book_id` (`book_id`),
    CONSTRAINT `fk_book_promotions_book_id` FOREIGN KEY (`book_id`) REFERENCES `books` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='책 추천/광고 프로모션';

CREATE TABLE `guest_migration_imports` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '게스트 이전 ID',
    `user_id` BIGINT NOT NULL COMMENT '사용자 ID',
    `migration_status` VARCHAR(30) NOT NULL DEFAULT 'PENDING' COMMENT '이전 상태',
    `payload` TEXT NOT NULL COMMENT '게스트 로컬 데이터 JSON',
    `imported_space_id` BIGINT NULL COMMENT '이전된 공간 ID',
    `failure_reason` VARCHAR(500) NULL COMMENT '실패 사유',
    `requested_at` DATETIME NOT NULL COMMENT '요청 일시',
    `completed_at` DATETIME NULL COMMENT '완료 일시',
    `created_at` DATETIME NOT NULL COMMENT '생성 일시',
    `updated_at` DATETIME NOT NULL COMMENT '수정 일시',
    PRIMARY KEY (`id`),
    KEY `idx_guest_migration_imports_user_id` (`user_id`),
    KEY `idx_guest_migration_imports_imported_space_id` (`imported_space_id`),
    CONSTRAINT `fk_guest_migration_imports_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
    CONSTRAINT `fk_guest_migration_imports_imported_space_id` FOREIGN KEY (`imported_space_id`) REFERENCES `spaces` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='게스트 로컬 데이터 정식 계정 이전';

