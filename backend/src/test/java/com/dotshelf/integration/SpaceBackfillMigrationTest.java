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
class SpaceBackfillMigrationTest {

    @Container
    private static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:17-alpine")
            .withDatabaseName("dotshelf_migration_test")
            .withUsername("dotshelf_test")
            .withPassword("dotshelf_test");

    @Test
    @DisplayName("V20은 기존 활성 사용자 중 활성 Space가 없는 계정만 기본 공간을 backfill한다")
    void v20BackfillsDefaultSpacesForExistingActiveUsersOnly() throws SQLException {
        Flyway flywayToV19 = flyway("19");
        flywayToV19.clean();
        flywayToV19.migrate();
        seedUsersAndSpacesBeforeV20();

        flyway("20").migrate();

        assertThat(currentFlywayVersion()).isEqualTo("20");
        assertDefaultSpaceBackfilled(1L);
        assertThat(activeSpaceCount(2L)).isEqualTo(1);
        assertDefaultSpaceBackfilled(3L);
        assertThat(totalSpaceCount(3L)).isEqualTo(2);
        assertThat(totalSpaceCount(4L)).isZero();
        assertThat(totalSpaceCount(5L)).isZero();
        assertThat(totalSpaceCount(6L)).isZero();
    }

    private Flyway flyway(String targetVersion) {
        return Flyway.configure()
                .dataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword())
                .locations("classpath:db/migration")
                .target(MigrationVersion.fromVersion(targetVersion))
                .cleanDisabled(false)
                .load();
    }

    private void seedUsersAndSpacesBeforeV20() throws SQLException {
        execute("""
                INSERT INTO public.users (
                    id, created_at, updated_at, email, nickname, deleted_at,
                    introduction, is_onboarded, last_login_at, profile_image_url,
                    role, status, session_invalidated_at
                ) VALUES
                    (1, NOW(), NOW(), 'active-missing@example.com', 'active-missing', NULL,
                     NULL, FALSE, NULL, NULL, 'USER', 'ACTIVE', NULL),
                    (2, NOW(), NOW(), 'active-existing@example.com', 'active-existing', NULL,
                     NULL, FALSE, NULL, NULL, 'USER', 'ACTIVE', NULL),
                    (3, NOW(), NOW(), 'active-deleted-space@example.com', 'active-deleted-space', NULL,
                     NULL, FALSE, NULL, NULL, 'USER', 'ACTIVE', NULL),
                    (4, NOW(), NOW(), 'status-deleted@example.com', 'status-deleted', NULL,
                     NULL, FALSE, NULL, NULL, 'USER', 'DELETED', NULL),
                    (5, NOW(), NOW(), 'deleted-at@example.com', 'deleted-at', NOW(),
                     NULL, FALSE, NULL, NULL, 'USER', 'ACTIVE', NULL),
                    (6, NOW(), NOW(), 'lower-status-deleted@example.com', 'lower-status-deleted', NULL,
                     NULL, FALSE, NULL, NULL, 'USER', 'deleted', NULL)
                """);
        execute("""
                INSERT INTO public.spaces (
                    id, created_at, updated_at, current_theme_inventory_id, deleted_at,
                    guestbook_visibility_scope, layout_version, like_count, owner_user_id,
                    space_name, template_id, visibility_scope, visit_count, thumbnail_url
                ) VALUES
                    (100, NOW(), NOW(), NULL, NULL, 'PUBLIC', 7, 2, 2,
                     '기존 공간', NULL, 'PUBLIC', 3, 'https://example.com/existing.png'),
                    (101, NOW(), NOW(), NULL, NOW(), 'PUBLIC', 1, 0, 3,
                     '삭제된 공간', NULL, 'PUBLIC', 0, NULL)
                """);
    }

    private void assertDefaultSpaceBackfilled(Long ownerUserId) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement("""
                     SELECT space_name, visibility_scope, guestbook_visibility_scope,
                            layout_version, like_count, visit_count, thumbnail_url
                       FROM public.spaces
                      WHERE owner_user_id = ?
                        AND deleted_at IS NULL
                     """)) {
            statement.setLong(1, ownerUserId);
            try (ResultSet resultSet = statement.executeQuery()) {
                assertThat(resultSet.next()).isTrue();
                assertThat(resultSet.getString("space_name")).isEqualTo("나의 공간");
                assertThat(resultSet.getString("visibility_scope")).isEqualTo("PUBLIC");
                assertThat(resultSet.getString("guestbook_visibility_scope")).isEqualTo("PUBLIC");
                assertThat(resultSet.getLong("layout_version")).isZero();
                assertThat(resultSet.getLong("like_count")).isZero();
                assertThat(resultSet.getLong("visit_count")).isZero();
                assertThat(resultSet.getString("thumbnail_url")).isNull();
                assertThat(resultSet.next()).isFalse();
            }
        }
    }

    private int activeSpaceCount(Long ownerUserId) throws SQLException {
        return count("""
                SELECT COUNT(*)
                  FROM public.spaces
                 WHERE owner_user_id = ?
                   AND deleted_at IS NULL
                """, ownerUserId);
    }

    private int totalSpaceCount(Long ownerUserId) throws SQLException {
        return count("""
                SELECT COUNT(*)
                  FROM public.spaces
                 WHERE owner_user_id = ?
                """, ownerUserId);
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

    private int count(String sql, Long ownerUserId) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement(sql)) {
            statement.setLong(1, ownerUserId);
            try (ResultSet resultSet = statement.executeQuery()) {
                assertThat(resultSet.next()).isTrue();
                return resultSet.getInt(1);
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
