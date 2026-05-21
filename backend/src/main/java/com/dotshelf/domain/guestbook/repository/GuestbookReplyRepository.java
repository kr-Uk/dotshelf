package com.dotshelf.domain.guestbook.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.guestbook.entity.GuestbookReply;

public interface GuestbookReplyRepository extends JpaRepository<GuestbookReply, Long> {

    List<GuestbookReply> findByGuestbookIdOrderByIdAsc(Long guestbookId);

    Optional<GuestbookReply> findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(Long guestbookId);

    /** 페이지 단위 batch 조회 (MR #60 #3 N+1 fix). */
    @Query("SELECT r FROM GuestbookReply r "
            + "WHERE r.guestbookId IN :guestbookIds AND r.deletedAt IS NULL "
            + "ORDER BY r.id ASC")
    List<GuestbookReply> findActiveByGuestbookIdIn(@Param("guestbookIds") Collection<Long> guestbookIds);
}
