package com.dotshelf.domain.user.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.util.List;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.data.domain.PageRequest;

import com.dotshelf.support.RepositoryTestSupport;
import com.dotshelf.support.TestFixtures;

class UserBlockRepositoryTest extends RepositoryTestSupport {

    @Autowired
    private UserBlockRepository userBlockRepository;

    @Test
    @DisplayName("같은 blocker/blocked 조합은 중복 저장할 수 없다")
    void uniqueBlockerAndBlocked() {
        userBlockRepository.saveAndFlush(TestFixtures.userBlock(1L, 2L));

        assertThatThrownBy(() -> userBlockRepository.saveAndFlush(TestFixtures.userBlock(1L, 2L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("양방향 차단 여부를 단일 쿼리로 확인한다")
    void existsBlockBetween_checksBothDirections() {
        userBlockRepository.saveAndFlush(TestFixtures.userBlock(1L, 2L));

        assertThat(userBlockRepository.existsBlockBetween(1L, 2L)).isTrue();
        assertThat(userBlockRepository.existsBlockBetween(2L, 1L)).isTrue();
        assertThat(userBlockRepository.existsBlockBetween(1L, 3L)).isFalse();
    }

    @Test
    @DisplayName("viewer가 차단한 target id만 batch로 조회한다")
    void findBlockedTargetIds_returnsOnlyViewerBlockedTargets() {
        userBlockRepository.save(TestFixtures.userBlock(1L, 2L));
        userBlockRepository.save(TestFixtures.userBlock(1L, 4L));
        userBlockRepository.save(TestFixtures.userBlock(3L, 2L));
        userBlockRepository.flush();

        List<Long> blockedIds = userBlockRepository.findBlockedTargetIds(1L, List.of(2L, 3L, 4L));

        assertThat(blockedIds).containsExactlyInAnyOrder(2L, 4L);
    }

    @Test
    @DisplayName("내가 차단했거나 나를 차단한 관계를 최신순 페이지로 조회한다")
    void findByBlockerUserIdOrBlockedUserIdOrderByIdDesc_returnsRecentRelations() {
        userBlockRepository.save(TestFixtures.userBlock(1L, 2L));
        userBlockRepository.save(TestFixtures.userBlock(3L, 1L));
        userBlockRepository.save(TestFixtures.userBlock(4L, 5L));
        userBlockRepository.flush();

        var relations = userBlockRepository.findByBlockerUserIdOrBlockedUserIdOrderByIdDesc(
                1L,
                1L,
                PageRequest.of(0, 10)
        );

        assertThat(relations).hasSize(2);
        assertThat(relations.get(0).getId()).isGreaterThan(relations.get(1).getId());
    }
}
