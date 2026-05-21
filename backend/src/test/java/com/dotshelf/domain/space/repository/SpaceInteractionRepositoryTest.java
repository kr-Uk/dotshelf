package com.dotshelf.domain.space.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.LocalDate;
import java.util.Set;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataIntegrityViolationException;

import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;
import com.dotshelf.domain.space.entity.SpaceLike;
import com.dotshelf.domain.space.entity.SpaceVisit;
import com.dotshelf.support.RepositoryTestSupport;

class SpaceInteractionRepositoryTest extends RepositoryTestSupport {

    @Autowired
    private SpaceLikeRepository spaceLikeRepository;

    @Autowired
    private SpaceVisitRepository spaceVisitRepository;

    @Autowired
    private SpaceGuestbookPostItWhitelistRepository whitelistRepository;

    @Test
    @DisplayName("공간 좋아요는 spaceId/userId 조합으로 존재 확인하고 삭제한다")
    void spaceLike_existsAndDelete() {
        spaceLikeRepository.saveAndFlush(SpaceLike.builder().spaceId(10L).userId(1L).build());

        assertThat(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 1L)).isTrue();

        spaceLikeRepository.deleteBySpaceIdAndUserId(10L, 1L);
        spaceLikeRepository.flush();

        assertThat(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 1L)).isFalse();
    }

    @Test
    @DisplayName("같은 사용자의 같은 날짜 공간 방문은 중복 저장할 수 없다")
    void spaceVisit_uniquePerDay() {
        LocalDate today = LocalDate.of(2026, 5, 7);
        spaceVisitRepository.saveAndFlush(SpaceVisit.builder()
                .spaceId(10L)
                .visitorUserId(1L)
                .visitedDate(today)
                .build());

        assertThatThrownBy(() -> spaceVisitRepository.saveAndFlush(SpaceVisit.builder()
                .spaceId(10L)
                .visitorUserId(1L)
                .visitedDate(today)
                .build()))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("공간 방문 통계는 날짜와 distinct visitor 기준으로 집계한다")
    void spaceVisit_countsByDateAndDistinctVisitors() {
        LocalDate today = LocalDate.of(2026, 5, 7);
        spaceVisitRepository.save(SpaceVisit.builder().spaceId(10L).visitorUserId(1L).visitedDate(today).build());
        spaceVisitRepository.save(SpaceVisit.builder().spaceId(10L).visitorUserId(2L).visitedDate(today).build());
        spaceVisitRepository.save(SpaceVisit.builder().spaceId(10L).visitorUserId(3L).visitedDate(today.minusDays(2)).build());
        spaceVisitRepository.save(SpaceVisit.builder().spaceId(11L).visitorUserId(1L).visitedDate(today).build());
        spaceVisitRepository.flush();

        assertThat(spaceVisitRepository.countByVisitedDate(today)).isEqualTo(3L);
        assertThat(spaceVisitRepository.countDistinctVisitorsBySpaceIdSince(10L, today.minusDays(1))).isEqualTo(2L);
    }

    @Test
    @DisplayName("방명록 포스트잇 whitelist는 allowAll이면 모든 asset을 허용한다")
    void whitelist_allowsAllWhenEnabled() {
        var saved = whitelistRepository.saveAndFlush(SpaceGuestbookPostItWhitelist.builder()
                .spaceId(10L)
                .allowAll(true)
                .allowedAssetIds(Set.of())
                .build());

        assertThat(saved.allows(999L)).isTrue();
    }

    @Test
    @DisplayName("방명록 포스트잇 whitelist는 지정 asset만 허용하고 update 결과를 보존한다")
    void whitelist_updatesSpecificAssetIds() {
        var saved = whitelistRepository.saveAndFlush(SpaceGuestbookPostItWhitelist.builder()
                .spaceId(10L)
                .allowAll(false)
                .allowedAssetIds(Set.of(1L, 2L))
                .build());

        saved.update(false, Set.of(3L));
        whitelistRepository.flush();

        var result = whitelistRepository.findById(10L).orElseThrow();

        assertThat(result.allows(1L)).isFalse();
        assertThat(result.allows(3L)).isTrue();
        assertThat(result.getAllowedAssetIds()).containsExactly(3L);
    }
}
