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
class BookLikeCountBackfillMigrationTest {

    @Container
    private static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:17-alpine")
            .withDatabaseName("dotshelf_book_like_migration_test")
            .withUsername("dotshelf_test")
            .withPassword("dotshelf_test");

    @Test
    @DisplayName("V24는 기존 user_books 좋아요 상태로 books.like_count를 보정한다")
    void v24BackfillsBookLikeCountFromUserBooks() throws SQLException {
        Flyway flywayToV23 = flyway("23");
        flywayToV23.clean();
        flywayToV23.migrate();
        seedBooksAndUserBooksBeforeV24();

        flyway("24").migrate();

        assertThat(currentFlywayVersion()).isEqualTo("24");
        assertThat(bookLikeCount(1L)).isEqualTo(2L);
        assertThat(bookLikeCount(2L)).isZero();
        assertThat(bookLikeCount(3L)).isZero();
    }

    private Flyway flyway(String targetVersion) {
        return Flyway.configure()
                .dataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword())
                .locations("classpath:db/migration")
                .target(MigrationVersion.fromVersion(targetVersion))
                .cleanDisabled(false)
                .load();
    }

    private void seedBooksAndUserBooksBeforeV24() throws SQLException {
        execute("""
                INSERT INTO public.books (
                    id, created_at, updated_at, author, bookshelf_add_count, cover_image_url,
                    description, isbn10, isbn13, language, like_count, page_count,
                    published_date, publisher, rating_count, rating_sum, title, view_count,
                    title_search, source, source_url
                ) VALUES
                    (1, NOW(), NOW(), 'author', 0, NULL, NULL, NULL, NULL, NULL,
                     99, NULL, NULL, NULL, 0, 0, 'liked book', 0, 'likedbook', 'LOCAL', NULL),
                    (2, NOW(), NOW(), 'author', 0, NULL, NULL, NULL, NULL, NULL,
                     88, NULL, NULL, NULL, 0, 0, 'unliked book', 0, 'unlikedbook', 'LOCAL', NULL),
                    (3, NOW(), NOW(), 'author', 0, NULL, NULL, NULL, NULL, NULL,
                     77, NULL, NULL, NULL, 0, 0, 'deleted like book', 0, 'deletedlikebook', 'LOCAL', NULL)
                """);
        execute("""
                INSERT INTO public.user_books (
                    id, created_at, updated_at, added_at, book_id, deleted_at,
                    is_liked, owner_rating, reading_status, selected_cover_product_id,
                    storage_status, user_id
                ) VALUES
                    (100, NOW(), NOW(), NOW(), 1, NULL, TRUE, NULL, 'READING', NULL, 'ON_SHELF', 1),
                    (101, NOW(), NOW(), NOW(), 1, NULL, TRUE, NULL, 'READING', NULL, 'ARCHIVED', 2),
                    (102, NOW(), NOW(), NOW(), 1, NOW(), TRUE, NULL, 'READING', NULL, 'DELETED', 3),
                    (103, NOW(), NOW(), NOW(), 2, NULL, FALSE, NULL, 'READING', NULL, 'ON_SHELF', 4),
                    (104, NOW(), NOW(), NOW(), 3, NULL, TRUE, NULL, 'READING', NULL, 'DELETED', 5)
                """);
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

    private long bookLikeCount(Long bookId) throws SQLException {
        try (Connection connection = connection();
             PreparedStatement statement = connection.prepareStatement("""
                     SELECT like_count
                       FROM public.books
                      WHERE id = ?
                     """)) {
            statement.setLong(1, bookId);
            try (ResultSet resultSet = statement.executeQuery()) {
                assertThat(resultSet.next()).isTrue();
                return resultSet.getLong("like_count");
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
