package com.dotshelf.domain.guestbook.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.guestbook.entity.GuestbookReply;

public interface GuestbookReplyRepository extends JpaRepository<GuestbookReply, Long> {

    List<GuestbookReply> findByGuestbookIdOrderByIdAsc(Long guestbookId);
}
