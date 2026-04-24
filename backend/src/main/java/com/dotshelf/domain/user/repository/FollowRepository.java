package com.dotshelf.domain.user.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.user.entity.Follow;

public interface FollowRepository extends JpaRepository<Follow, Long> {

    boolean existsByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    Optional<Follow> findByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    void deleteByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    long countByFollowingUserId(Long followingUserId);

    long countByFollowerUserId(Long followerUserId);

    List<Follow> findByFollowingUserId(Long followingUserId);

    List<Follow> findByFollowerUserId(Long followerUserId);
}
