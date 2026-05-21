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
 * MR !158 회귀 방지의 운영자 콘솔 검색 쪽 짝.
 *
 * AssetRepository.searchForAdmin 도 searchActive 와 동일하게 String 파라미터가 null 일 때
 * Hibernate 7 SQM 이 SQL type 을 byte[] (bytea) 로 잘못 추론해 PostgreSQL 의
 * `function upper(bytea) does not exist` 로 죽던 회귀가 있었다 (admin 목록 화면이 500 으로 떨어져
 * "총 0건" 으로 보이던 사고). 같은 cast(:x as string) 가드를 admin 쿼리에도 적용했고, 본 테스트가
 * PostgreSQL Testcontainer 위에서 직접 재현/방어한다.
 *
 * H2 PostgreSQL mode 는 named parameter 의 binding type 동작이 달라 이 회귀를 못 잡으므로
 * PostgresTestContainerSupport 를 상속한다 (`-DrunPostgresTests=true` 명시 실행).
 */
@DataJpaTest
@ActiveProfiles("test")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
class AssetRepositorySearchForAdminPostgresTest extends PostgresTestContainerSupport {

    @Autowired
    private AssetCategoryRepository assetCategoryRepository;

    @Autowired
    private AssetRepository assetRepository;

    private AssetCategory category;

    @BeforeEach
    void setUp() {
        category = assetCategoryRepository.save(TestFixtures.assetCategory("FURNITURE"));
        assetRepository.save(Asset.builder()
                .categoryId(category.getId())
                .code("DEEP_SEA_CHAIR")
                .name("깊은바다 의자")
                .assetScope("SPACE")
                .assetType("CHAIR")
                .collectionCode("DEEP_SEA")
                .pricePages(10)
                .build());
        // 비공개(draft) 자산이 admin 목록에 잡히는지도 확인하려고 isActive=false 도 한 건 둔다.
        Asset draft = Asset.builder()
                .categoryId(category.getId())
                .code("GOOD_VIBES_DESK")
                .name("산뜻 책상")
                .assetScope("SPACE")
                .assetType("DESK_SET")
                .collectionCode("GOOD_VIBES")
                .pricePages(10)
                .isActive(false)
                .build();
        assetRepository.save(draft);
        assetRepository.flush();
    }

    @Test
    @DisplayName("모든 String 필터 null + active null — upper(bytea) 회귀 없이 정상 조회 (draft 포함)")
    void searchForAdmin_allStringFiltersNull_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    null, null, null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).hasSize(2);
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("assetType 만 non-null — upper(:assetType) cast 가 bytea 회귀 차단")
    void searchForAdmin_assetTypeOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    null, "CHAIR", null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("collectionCode 만 non-null — upper(:collectionCode) cast 가 bytea 회귀 차단")
    void searchForAdmin_collectionCodeOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    null, null, "GOOD_VIBES", null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("GOOD_VIBES_DESK");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("keyword 만 non-null — lower(concat(:keyword)) cast 가 bytea 회귀 차단")
    void searchForAdmin_keywordOnly_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    null, null, null, "의자", PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).contains("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("active=false 만 non-null — draft 만 잡힌다 (admin 목록 본연의 동작)")
    void searchForAdmin_activeFalseOnly_returnsOnlyDrafts() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    Boolean.FALSE, null, null, null, PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).containsExactly("GOOD_VIBES_DESK");
        }).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("모든 필터 non-null — 모든 cast 가 함께 적용되는 경로")
    void searchForAdmin_allFiltersNonNull_doesNotThrowBytea() {
        assertThatCode(() -> {
            var result = assetRepository.searchForAdmin(
                    Boolean.TRUE, "CHAIR", "DEEP_SEA", "의자", PageRequest.of(0, 10));
            assertThat(result).isNotNull();
            assertThat(result.getContent()).extracting(Asset::getCode).containsExactly("DEEP_SEA_CHAIR");
        }).doesNotThrowAnyException();
    }
}
