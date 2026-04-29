package com.dotshelf.domain.space.repository;

import java.util.Optional;
import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.Space;

public interface SpaceRepository extends JpaRepository<Space, Long> {

    Optional<Space> findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(Long ownerUserId);

    List<Space> findByOwnerUserIdAndDeletedAtIsNull(Long ownerUserId);

    long countByDeletedAtIsNull();
}
