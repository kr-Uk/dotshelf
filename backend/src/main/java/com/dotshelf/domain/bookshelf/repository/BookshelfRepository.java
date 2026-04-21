package com.dotshelf.domain.bookshelf.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.bookshelf.entity.Bookshelf;

public interface BookshelfRepository extends JpaRepository<Bookshelf, Long> {

    List<Bookshelf> findBySpaceIdAndStatusOrderByDisplayOrderAsc(Long spaceId, String status);

    long countBySpaceIdAndStatus(Long spaceId, String status);
}
