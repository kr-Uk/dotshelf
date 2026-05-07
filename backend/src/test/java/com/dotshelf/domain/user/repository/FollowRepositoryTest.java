package com.dotshelf.domain.user.repository;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.data.domain.PageRequest;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class FollowRepositoryTest {

    @Autowired
    private FollowRepository followRepository;

    @Test
    @DisplayName("팔로워 페이지는 id 역순으로 cursor 이전 항목만 조회한다")
    void findFollowersPage_usesCursorAndDescendingOrder() {
        var first = followRepository.save(TestFixtures.follow(1L, 9L));
        var second = followRepository.save(TestFixtures.follow(2L, 9L));
        var third = followRepository.save(TestFixtures.follow(3L, 9L));
        followRepository.flush();

        var firstPage = followRepository.findFollowersPage(9L, null, PageRequest.of(0, 2));
        var nextPage = followRepository.findFollowersPage(9L, firstPage.getLast().getId(), PageRequest.of(0, 2));

        assertThat(firstPage).extracting("id").containsExactly(third.getId(), second.getId());
        assertThat(nextPage).extracting("id").containsExactly(first.getId());
    }

    @Test
    @DisplayName("viewer가 팔로우 중인 target id만 배치 조회한다")
    void findFollowingTargetIds_returnsOnlyMatchedTargets() {
        followRepository.save(TestFixtures.follow(1L, 2L));
        followRepository.save(TestFixtures.follow(1L, 3L));
        followRepository.save(TestFixtures.follow(4L, 2L));
        followRepository.flush();

        List<Long> result = followRepository.findFollowingTargetIds(1L, List.of(2L, 4L));

        assertThat(result).containsExactly(2L);
    }
}
