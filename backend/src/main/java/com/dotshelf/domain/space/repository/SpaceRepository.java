package com.dotshelf.domain.space.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.Space;

public interface SpaceRepository extends JpaRepository<Space, Long> {

    Optional<Space> findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(Long ownerUserId);
}
