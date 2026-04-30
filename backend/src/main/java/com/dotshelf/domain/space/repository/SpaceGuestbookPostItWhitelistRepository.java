package com.dotshelf.domain.space.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;

public interface SpaceGuestbookPostItWhitelistRepository
        extends JpaRepository<SpaceGuestbookPostItWhitelist, Long> {
}
