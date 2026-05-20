package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;

import org.flywaydb.core.Flyway;
import org.flywaydb.core.api.MigrationVersion;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfSystemProperty;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

@Testcontainers(disabledWithoutDocker = true)
@EnabledIfSystemProperty(named = "runPostgresTests", matches = "true")
class ThemeFixedDefaultsMigrationTest {

    @Container
    private static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:17-alpine")
            .withDatabaseName("dotshelf_theme_defaults_migration_test")
            .withUsername("dotshelf_test")
            .withPassword("dotshelf_test");

    @Test
    @DisplayName("V34 seeds theme fixed defaults and backfills spaces without overwriting overrides")
    void v34SeedsThemeFixedDefaultsAndBackfillsSpaces() throws SQLException {
        Flyway flywayToV33 = flyway("33");
        flywayToV33.clean();
        flywayToV33.migrate();
        seedSpaceThemeBeforeV34();

        flyway("34").migrate();

        assertThat(currentFlywayVersion()).isEqualTo("34");
        Long floorAssetId = assetId("SPACE_FLOOR");
        Long interiorLightAssetId = assetId("SPACE_INTERIOR_LIGHT");
        Long exteriorLightAssetId = assetId("SPACE_EXTERIOR_LIGHT");
        assertThat(themeTargetId("THEME_SPACE", "floor")).isEqualTo(floorAssetId);
        assertThat(themeTargetId("THEME_SPACE", "dashboard")).isEqualTo(7004L);
        assertThat(themeTargetId("THEME_SPACE", "interiorLight")).isEqualTo(interiorLightAssetId);
        assertThat(themeTargetId("THEME_SPACE", "exteriorLight")).isEqualTo(exteriorLightAssetId);
        assertThat(themeTargetId("THEME_FANTASY", "dashboard")).isEqualTo(assetId("FANTASY_DASHBOARD_SKIN"));
        assertThat(assetText("SPACE_FLOOR", "unity_resource_key")).isEqualTo("legacy:space-floor");
        assertThat(assetText("SPACE_FLOOR", "asset_url")).isEqualTo("https://cdn.example.com/space-floor.glb");
        assertThat(assetText("FANTASY_DASHBOARD_SKIN", "unity_resource_key")).isNotBlank();

        assertThat(inventoryId(9001L, 7002L)).isNotNull();
        assertThat(inventoryId(9001L, floorAssetId)).isNotNull();
        assertThat(inventoryId(9001L, interiorLightAssetId)).isNotNull();
        assertThat(inventoryId(9001L, exteriorLightAssetId)).isNotNull();
        assertThat(inventoryIdOrNull(9001L, 7011L)).isNull();

        assertSpaceBackfilledPreservingDeskOverride(
                inventoryId(9001L, 7002L),
                inventoryId(9001L, floorAssetId),
                inventoryId(9001L, 7004L),
                inventoryId(9001L, 7005L),
                inventoryId(9001L, interiorLightAssetId),
                inventoryId(9001L, exteriorLightAssetId));
        assertWrongTypeThemeTargetWasNotBackfilled();
    }

    private Flyway flyway(String targetVersion) {
        return Flyway.configure()
                .dataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword())
                .locations("classpath:db/migration")
                .target(MigrationVersion.fromVersion(targetVersion))
                .cleanDisabled(false)
                .load();
    }

    private void seedSpaceThemeBeforeV34() throws SQLException {
        execute("""
                INSERT INTO public.users (
                    id, created_at, updated_at, email, nickname, deleted_at,
                    introduction, is_onboarded, last_login_at, profile_image_url,
                    role, status, session_invalidated_at
                ) VALUES
                    (9001, NOW(), NOW(), 'theme-defaults@example.com', 'theme-defaults', NULL,
                     NULL, TRUE, NULL, NULL, 'USER', 'ACTIVE', NULL),
                    (9002, NOW(), NOW(), 'theme-defaults-wrong-type@example.com', 'theme-defaults-wrong-type', NULL,
                     NULL, TRUE, NULL, NULL, 'USER', 'ACTIVE', NULL)
                """);
        execute("""
                INSERT INTO public.asset_categories (
                    id, created_at, updated_at, code, name, display_order, is_active, parent_category_id
                ) VALUES
                    (7000, NOW(), NOW(), 'TEST_THEME_DEFAULTS', 'Test Theme Defaults', 1, TRUE, NULL)
                ON CONFLICT (code) DO NOTHING
                """);
        execute("""
                INSERT INTO public.assets (
                    id, created_at, updated_at, category_id, code, name, tier, asset_scope,
                    asset_type, collection_code, unity_resource_key, asset_url, content_type,
                    file_size, file_hash, version, preview_image_url, price_pages, creator,
                    supplier, compatible_targets, released_at, valid_until, is_default,
                    is_active, allows_customization, theme, placement
                ) VALUES
                    (7001, NOW(), NOW(), 7000, 'THEME_SPACE', 'Space Theme', 'NORMAL', 'SPACE',
                     'THEME', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio',
                     lo_from_bytea(0, convert_to('{"wallpaper":7002,"desk":7003,"dashboard":7004,"bookshelfSkinDefault":7005,"sortOrder":7011}', 'UTF8')),
                     NOW(), NULL, TRUE, TRUE, FALSE, 'Space', NULL),
                    (7010, NOW(), NOW(), 7000, 'THEME_FANTASY', 'Fantasy Theme', 'NORMAL', 'SPACE',
                     'THEME', 'FANTASY', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, FALSE, 'Fantasy', NULL),
                    (7012, NOW(), NOW(), 7000, 'THEME_SPOOKY', 'Spooky Theme', 'NORMAL', 'SPACE',
                     'THEME', 'SPOOKY', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio',
                     lo_from_bytea(0, convert_to('{"wallpaper":7003}', 'UTF8')),
                     NOW(), NULL, TRUE, TRUE, FALSE, 'Spooky', NULL),
                    (7002, NOW(), NOW(), 7000, 'SPACE_WALLPAPER', 'Space Wallpaper', 'NORMAL', 'SPACE',
                     'WALLPAPER', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL),
                    (7003, NOW(), NOW(), 7000, 'SPACE_DESK_SET', 'Space Desk', 'NORMAL', 'SPACE',
                     'DESK_SET', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL),
                    (7004, NOW(), NOW(), 7000, 'SPACE_DASHBOARD', 'Space Dashboard', 'NORMAL', 'SPACE',
                     'DASHBOARD_SKIN', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL),
                    (7005, NOW(), NOW(), 7000, 'SPACE_BOOKSHELF_SKIN', 'Space Bookshelf', 'NORMAL', 'SPACE',
                     'BOOKSHELF_SKIN', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL),
                    (7007, NOW(), NOW(), 7000, 'SPACE_FLOOR', 'Existing Space Floor', 'NORMAL', 'SPACE',
                     'FLOOR', 'SPACE', 'legacy:space-floor', 'https://cdn.example.com/space-floor.glb', 'model/gltf-binary',
                     1024, 'hash-space-floor', 3, 'https://cdn.example.com/space-floor.png', 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL),
                    (7006, NOW(), NOW(), 7000, 'CUSTOM_DESK_OVERRIDE', 'Custom Desk Override', 'NORMAL', 'SPACE',
                     'DESK_SET', 'CUSTOM', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Custom', NULL),
                    (7011, NOW(), NOW(), 7000, 'UNRELATED_DECOR', 'Unrelated Decor', 'NORMAL', 'SPACE',
                     'ASSET', 'SPACE', NULL, NULL, NULL, NULL, NULL, 1, NULL, 0, 'DotShelf',
                     'DotShelf Studio', NULL, NOW(), NULL, TRUE, TRUE, TRUE, 'Space', NULL)
                """);
        execute("""
                INSERT INTO public.user_asset_inventories (
                    id, created_at, updated_at, user_id, asset_id, grant_type, acquired_at
                ) VALUES
                    (8001, NOW(), NOW(), 9001, 7001, 'DEFAULT', NOW()),
                    (8002, NOW(), NOW(), 9001, 7006, 'PURCHASE', NOW()),
                    (8010, NOW(), NOW(), 9002, 7012, 'DEFAULT', NOW()),
                    (8011, NOW(), NOW(), 9002, 7003, 'DEFAULT', NOW())
                """);
        execute("""
                INSERT INTO public.spaces (
                    id, created_at, updated_at, current_theme_inventory_id, current_desk_inventory_id,
                    deleted_at, guestbook_visibility_scope, layout_version, like_count, owner_user_id,
                    space_name, template_id, visibility_scope, visit_count, thumbnail_url
                ) VALUES
                    (9201, NOW(), NOW(), 8001, 8002, NULL, 'PUBLIC', 3, 0, 9001,
                     'Theme Defaults Room', NULL, 'PUBLIC', 0, NULL),
                    (9202, NOW(), NOW(), 8010, NULL, NULL, 'PUBLIC', 3, 0, 9002,
                     'Wrong Type Theme Defaults Room', NULL, 'PUBLIC', 0, NULL)
                """);
    }

    private Long assetId(String code) throws SQLException {
        return queryLong("""
                SELECT id
                  FROM public.assets
                 WHERE code = ?
                """, code);
    }

    private Long inventoryId(Long userId, Long assetId) throws SQLException {
        return queryLong("""
                SELECT id
                  FROM public.user_asset_inventories
                 WHERE user_id = ?
                   AND asset_id = ?
                """, userId, assetId);
    }

    private Long inventoryIdOrNull(Long userId, Long assetId) throws SQLException {
        return queryOptionalLong("""
                SELECT id
                  FROM public.user_asset_inventories
                 WHERE user_id = ?
                   AND asset_id = ?
                """, userId, assetId);
    }

    private String assetText(String code, String column) throws SQLException {
        return queryString("""
                SELECT %s
                  FROM public.assets
                 WHERE code = ?
                """.formatted(column), code);
    }

    private Long themeTargetId(String themeCode, String key) throws SQLException {
        return queryLong("""
                SELECT (convert_from(lo_get(compatible_targets), 'UTF8')::jsonb ->> ?)::bigint
                  FROM public.assets
                 WHERE code = ?
                """, key, themeCode);
    }

    private void assertSpaceBackfilledPreservingDeskOverride(
            Long wallpaperInventoryId,
            Long floorInventoryId,
            Long dashboardInventoryId,
            Long bookshelfSkinInventoryId,
            Long interiorLightInventoryId,
            Long exteriorLightInventoryId) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement("""
                     SELECT current_wallpaper_inventory_id,
                            current_floor_inventory_id,
                            current_desk_inventory_id,
                            current_dashboard_inventory_id,
                            current_bookshelf_skin_inventory_id,
                            current_interior_light_inventory_id,
                            current_exterior_light_inventory_id
                       FROM public.spaces
                      WHERE id = 9201
                     """);
             ResultSet resultSet = statement.executeQuery()) {
            assertThat(resultSet.next()).isTrue();
            assertThat(resultSet.getLong("current_wallpaper_inventory_id")).isEqualTo(wallpaperInventoryId);
            assertThat(resultSet.getLong("current_floor_inventory_id")).isEqualTo(floorInventoryId);
            assertThat(resultSet.getLong("current_desk_inventory_id")).isEqualTo(8002L);
            assertThat(resultSet.getLong("current_dashboard_inventory_id")).isEqualTo(dashboardInventoryId);
            assertThat(resultSet.getLong("current_bookshelf_skin_inventory_id")).isEqualTo(bookshelfSkinInventoryId);
            assertThat(resultSet.getLong("current_interior_light_inventory_id")).isEqualTo(interiorLightInventoryId);
            assertThat(resultSet.getLong("current_exterior_light_inventory_id")).isEqualTo(exteriorLightInventoryId);
            assertThat(resultSet.next()).isFalse();
        }
    }

    private void assertWrongTypeThemeTargetWasNotBackfilled() throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement("""
                     SELECT current_wallpaper_inventory_id
                       FROM public.spaces
                      WHERE id = 9202
                     """);
             ResultSet resultSet = statement.executeQuery()) {
            assertThat(resultSet.next()).isTrue();
            assertThat(resultSet.getObject("current_wallpaper_inventory_id")).isNull();
            assertThat(resultSet.next()).isFalse();
        }
    }

    private String currentFlywayVersion() throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement("""
                     SELECT version
                       FROM public.flyway_schema_history
                      WHERE success = TRUE
                      ORDER BY installed_rank DESC
                      LIMIT 1
                     """);
             ResultSet resultSet = statement.executeQuery()) {
            assertThat(resultSet.next()).isTrue();
            return resultSet.getString("version");
        }
    }

    private Long queryLong(String sql, Object... params) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement(sql)) {
            for (int i = 0; i < params.length; i++) {
                statement.setObject(i + 1, params[i]);
            }
            try (ResultSet resultSet = statement.executeQuery()) {
                assertThat(resultSet.next()).isTrue();
                return resultSet.getLong(1);
            }
        }
    }

    private Long queryOptionalLong(String sql, Object... params) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement(sql)) {
            for (int i = 0; i < params.length; i++) {
                statement.setObject(i + 1, params[i]);
            }
            try (ResultSet resultSet = statement.executeQuery()) {
                if (!resultSet.next()) {
                    return null;
                }
                return resultSet.getLong(1);
            }
        }
    }

    private String queryString(String sql, Object... params) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement(sql)) {
            for (int i = 0; i < params.length; i++) {
                statement.setObject(i + 1, params[i]);
            }
            try (ResultSet resultSet = statement.executeQuery()) {
                assertThat(resultSet.next()).isTrue();
                return resultSet.getString(1);
            }
        }
    }

    private void execute(String sql) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement(sql)) {
            statement.executeUpdate();
        }
    }

    private Connection connection() throws SQLException {
        return DriverManager.getConnection(
                POSTGRES.getJdbcUrl(),
                POSTGRES.getUsername(),
                POSTGRES.getPassword());
    }
}
