package com.dotshelf.domain.asset.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.data.domain.PageRequest;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.support.PostgresTestContainerSupport;
import com.dotshelf.support.TestFixtures;

/**
 * MR !158 회귀 방지 테스트.
 *
 * Hibernate 7 의 SQM 가 같은 named parameter 를 `:x is null` 가드와 `upper(:x)` / `lower(:x)` 비교에
 * 동시에 쓸 때 SQL type 을 byte[] (bytea) 로 잘못 추론 → PostgreSQL 가 `function upper(bytea) does not
 * exist` 로 SQLGrammarException 을 던져 /v1/assets 가 500 으로 죽던 회귀를 PostgreSQL Testcontainer
 * 위에서 직접 재현/방어한다.
 *
 * H2 PostgreSQL mode 는 named parameter 의 binding type 동작이 달라 이 회귀를 잡지 못한다.
 * 따라서 본 클래스는 `-DrunPostgresTests=true` 시에만 활성화되는 PostgresTestContainerSupport 를
 * 상속한다 (CI 의 selective profile + 로컬은 명시적 실행 시).
 *
 * 검증 5 케이스 — 모든 String 필터의 null/non-null 조합에서 SQLGrammarException 없이 정상 조회되는지.
 * 결과 개수보다 "쿼리 자체가 grammar error 없이 실행" 이 우선 (cast 누락 회귀의 직접 검출).
 */
@DataJpaTest
@ActiveProfiles("test")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
class AssetRepositorySearchActivePostgresTest extends PostgresTestContainerSupport {

    @Autowired
    private AssetCategoryRepository assetCategoryRepository;

    @Autowired
    private AssetRepository assetRepository;

    private AssetCategory category;

    @BeforeEach
    void setUp() {
        category = assetCategoryRepository.save(TestFixtures.assetCategory("FURNITURE"));
        // 모든 케이스가 cross-cutting 비교를 위해 같은 카테고리 안에 다양한 type/collection 보유.
        assetRepository.save(Asset.builder()
                .categoryId(category.getId())
                .code("DEEP_SEA_CHAIR")
                .name("깊은바다 의자")
                .assetScope("SPACE")
                .assetType("CHAIR")
                .collectionCode("DEEP_SEA")
                .pricePages(10)
                .build());
        assetRepository.save(Asset.builder()
                .categoryId(category.getId())
                .code("GOOD_VIBES_DESK")
                .name("산뜻 책상")
                .assetScope("SPACE")
                .assetType("DESK_SET")
                .collectionCode("GOOD_VIBES")
                .pricePages(10)
                .build());
        assetRepository.flush();
    }

    @Test
    @DisplayName("모든 String 필터 null + categoryId 만 — upper(bytea) 회귀 없이 정상 조회")
    void searchActive_allStringFiltersNull_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchActive(
                    1L, category.getId(), null, null, null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).hasSize(2);
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("assetType 만 non-null — upper(:assetType) cast 가 bytea 회귀 차단")
    void searchActive_assetTypeOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchActive(
                    1L, null, "CHAIR", null, null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("collectionCode 만 non-null — upper(:collectionCode) cast 가 bytea 회귀 차단")
    void searchActive_collectionCodeOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchActive(
                    1L, null, null, "GOOD_VIBES", null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("GOOD_VIBES_DESK");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("keyword 만 non-null — lower(concat(:keyword)) cast 가 bytea 회귀 차단")
    void searchActive_keywordOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchActive(
                    1L, null, null, null, "의자", null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("3 개 String 필터 모두 non-null + categoryId — 모든 cast 가 함께 적용되는 경로")
    void searchActive_allStringFiltersNonNull_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchActive(
                    1L, category.getId(), "CHAIR", "DEEP_SEA", "의자", null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).containsExactly("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }
}
