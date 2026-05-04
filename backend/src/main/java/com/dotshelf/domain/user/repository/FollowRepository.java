package com.dotshelf.domain.user.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.user.entity.Follow;

public interface FollowRepository extends JpaRepository<Follow, Long> {

    boolean existsByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    Optional<Follow> findByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    void deleteByFollowerUserIdAndFollowingUserId(Long followerUserId, Long followingUserId);

    long countByFollowingUserId(Long followingUserId);

    long countByFollowerUserId(Long followerUserId);

    List<Follow> findByFollowingUserId(Long followingUserId);

    List<Follow> findByFollowerUserId(Long followerUserId);

    @Query("SELECT f FROM Follow f "
            + "WHERE f.followingUserId = :userId "
            + "AND (:cursorId IS NULL OR f.id < :cursorId) "
            + "ORDER BY f.id DESC")
    List<Follow> findFollowersPage(
            @Param("userId") Long userId,
            @Param("cursorId") Long cursorId,
            Pageable pageable
    );

    @Query("SELECT f FROM Follow f "
            + "WHERE f.followerUserId = :userId "
            + "AND (:cursorId IS NULL OR f.id < :cursorId) "
            + "ORDER BY f.id DESC")
    List<Follow> findFollowingsPage(
            @Param("userId") Long userId,
            @Param("cursorId") Long cursorId,
            Pageable pageable
    );

    /** viewer가 팔로우 중인 targets 중에서 일치하는 것만 (MR #60 #4 batch). */
    @Query("SELECT f.followingUserId FROM Follow f "
            + "WHERE f.followerUserId = :viewerId AND f.followingUserId IN :targetIds")
    List<Long> findFollowingTargetIds(
            @Param("viewerId") Long viewerId,
            @Param("targetIds") java.util.Collection<Long> targetIds
    );

    /** targets 중 viewer를 팔로우 중인 것 (mutual 판정 batch). */
    @Query("SELECT f.followerUserId FROM Follow f "
            + "WHERE f.followingUserId = :viewerId AND f.followerUserId IN :targetIds")
    List<Long> findFollowerTargetIds(
            @Param("viewerId") Long viewerId,
            @Param("targetIds") java.util.Collection<Long> targetIds
    );

    @Query("SELECT f.followerUserId FROM Follow f WHERE f.followingUserId = :followingUserId")
    List<Long> findFollowerUserIdsByFollowingUserId(@Param("followingUserId") Long followingUserId);
}
