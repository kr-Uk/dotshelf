package com.dotshelf.domain.feed.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.feed.entity.FeedEvent;

public interface FeedEventRepository extends JpaRepository<FeedEvent, Long> {

    List<FeedEvent> findTop20ByOrderByOccurredAtDesc();
}
