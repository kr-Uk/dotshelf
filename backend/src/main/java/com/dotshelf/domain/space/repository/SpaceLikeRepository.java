package com.dotshelf.domain.space.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.SpaceLike;

public interface SpaceLikeRepository extends JpaRepository<SpaceLike, Long> {

    boolean existsBySpaceIdAndUserId(Long spaceId, Long userId);

    void deleteBySpaceIdAndUserId(Long spaceId, Long userId);
}
