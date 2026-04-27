package com.dotshelf.domain.feed.repository;

import java.time.LocalDateTime;
import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.feed.entity.BookPromotion;

public interface BookPromotionRepository extends JpaRepository<BookPromotion, Long> {

    List<BookPromotion> findTop20ByIsActiveTrueAndStartsAtBeforeOrderByDisplayOrderAsc(LocalDateTime now);
}
