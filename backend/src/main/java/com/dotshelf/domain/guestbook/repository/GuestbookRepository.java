package com.dotshelf.domain.guestbook.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.guestbook.entity.Guestbook;

public interface GuestbookRepository extends JpaRepository<Guestbook, Long> {

    List<Guestbook> findBySpaceIdAndStatusOrderByIdDesc(Long spaceId, String status);
}
