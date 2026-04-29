package com.dotshelf.domain.user.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.user.entity.UserBlock;

public interface UserBlockRepository extends JpaRepository<UserBlock, Long> {

    boolean existsByBlockerUserIdAndBlockedUserId(Long blockerUserId, Long blockedUserId);

    Optional<UserBlock> findByBlockerUserIdAndBlockedUserId(Long blockerUserId, Long blockedUserId);

    void deleteByBlockerUserIdAndBlockedUserId(Long blockerUserId, Long blockedUserId);

    List<UserBlock> findByBlockerUserId(Long blockerUserId);

    List<UserBlock> findByBlockerUserIdOrBlockedUserIdOrderByIdDesc(
            Long blockerUserId,
            Long blockedUserId,
            Pageable pageable
    );

    /** 양방향 차단 관계 단일 쿼리 (MR #60 #5). */
    @Query("SELECT count(b) > 0 FROM UserBlock b WHERE "
            + "(b.blockerUserId = :a AND b.blockedUserId = :b) OR "
            + "(b.blockerUserId = :b AND b.blockedUserId = :a)")
    boolean existsBlockBetween(@Param("a") Long a, @Param("b") Long b);

    /** viewer가 차단한 targets 중에서 일치하는 것만 (MR #60 #4 batch). */
    @Query("SELECT b.blockedUserId FROM UserBlock b "
            + "WHERE b.blockerUserId = :viewerId AND b.blockedUserId IN :targetIds")
    List<Long> findBlockedTargetIds(
            @Param("viewerId") Long viewerId,
            @Param("targetIds") Collection<Long> targetIds
    );
}
