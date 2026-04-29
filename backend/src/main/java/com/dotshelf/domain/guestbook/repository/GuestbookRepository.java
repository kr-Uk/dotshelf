package com.dotshelf.domain.guestbook.repository;

import java.util.List;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.guestbook.entity.Guestbook;

public interface GuestbookRepository extends JpaRepository<Guestbook, Long> {

    List<Guestbook> findBySpaceIdAndStatusOrderByIdDesc(Long spaceId, String status);

    @Query("SELECT g FROM Guestbook g "
            + "WHERE g.spaceId = :spaceId AND g.status = :status "
            + "AND (:cursorId IS NULL OR g.id < :cursorId) "
            + "ORDER BY g.id DESC")
    List<Guestbook> findGuestbookPage(
            @Param("spaceId") Long spaceId,
            @Param("status") String status,
            @Param("cursorId") Long cursorId,
            Pageable pageable
    );
}
