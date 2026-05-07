package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;

import java.sql.SQLException;
import java.util.Set;

import javax.sql.DataSource;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfSystemProperty;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.admin.entity.SettlementAdjustment;
import com.dotshelf.domain.admin.repository.SettlementAdjustmentRepository;
import com.dotshelf.domain.auth.entity.GuestMigrationImport;
import com.dotshelf.domain.auth.repository.GuestMigrationImportRepository;
import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;
import com.dotshelf.domain.space.repository.SpaceGuestbookPostItWhitelistRepository;
import com.dotshelf.support.PostgresTestContainerSupport;

@DataJpaTest
@ActiveProfiles("test")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@EnabledIfSystemProperty(named = "runPostgresTests", matches = "true")
class PostgresContainerRepositoryTest extends PostgresTestContainerSupport {

    @Autowired
    private DataSource dataSource;

    @Autowired
    private SettlementAdjustmentRepository settlementAdjustmentRepository;

    @Autowired
    private GuestMigrationImportRepository guestMigrationImportRepository;

    @Autowired
    private SpaceGuestbookPostItWhitelistRepository whitelistRepository;

    @Test
    @DisplayName("PostgreSQL Testcontainer에서 admin schema, LOB, element collection 매핑을 검증한다")
    void postgresContainer_persistsSchemaLobAndElementCollectionMappings() throws SQLException {
        assertThat(databaseProductName()).containsIgnoringCase("PostgreSQL");

        SettlementAdjustment adjustment = settlementAdjustmentRepository.saveAndFlush(SettlementAdjustment.builder()
                .userId(2L)
                .deltaPages(100)
                .reason("postgres-check")
                .memo("admin schema mapping")
                .adjustedByUserId(1L)
                .build());
        GuestMigrationImport migration = guestMigrationImportRepository.saveAndFlush(GuestMigrationImport.builder()
                .userId(2L)
                .payload("{\"books\":[{\"title\":\"데미안\"}]}")
                .guestSessionId("guest-postgres")
                .build());
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.saveAndFlush(
                SpaceGuestbookPostItWhitelist.builder()
                        .spaceId(10L)
                        .allowAll(false)
                        .allowedAssetIds(Set.of(1L, 2L, 3L))
                        .build()
        );

        assertThat(adjustment.getId()).isNotNull();
        assertThat(migration.getPayload()).contains("데미안");
        assertThat(whitelistRepository.findById(whitelist.getSpaceId()))
                .isPresent()
                .get()
                .satisfies(saved -> {
                    assertThat(saved.isAllowAll()).isFalse();
                    assertThat(saved.getAllowedAssetIds()).containsExactlyInAnyOrder(1L, 2L, 3L);
                });
    }

    private String databaseProductName() throws SQLException {
        try (var connection = dataSource.getConnection()) {
            return connection.getMetaData().getDatabaseProductName();
        }
    }
}
