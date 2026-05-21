package com.dotshelf.domain.space.repository;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.LocalDateTime;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.util.ReflectionTestUtils;

import com.dotshelf.domain.space.entity.Space;

@DataJpaTest
@ActiveProfiles("test")
class SpaceRepositoryTest {

    @Autowired
    private SpaceRepository spaceRepository;

    @Test
    @DisplayName("ownerUserId 기준 첫 번째 활성 공간만 조회한다")
    void findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc_ignoresDeleted() {
        Space deleted = Space.builder().ownerUserId(1L).spaceName("deleted").build();
        ReflectionTestUtils.setField(deleted, "deletedAt", LocalDateTime.now());
        Space active = Space.builder().ownerUserId(1L).spaceName("active").build();
        spaceRepository.save(deleted);
        spaceRepository.save(active);
        spaceRepository.flush();

        var result = spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L);

        assertThat(result).isPresent();
        assertThat(result.get().getSpaceName()).isEqualTo("active");
        assertThat(spaceRepository.countByDeletedAtIsNull()).isEqualTo(1L);
    }
}
