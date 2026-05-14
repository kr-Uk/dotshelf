package com.dotshelf.domain.space.repository;

import java.util.Collection;
import java.util.Optional;
import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.Space;

public interface SpaceRepository extends JpaRepository<Space, Long> {

    Optional<Space> findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(Long ownerUserId);

    List<Space> findByOwnerUserIdAndDeletedAtIsNull(Long ownerUserId);

    Page<Space> findByOwnerUserIdAndDeletedAtIsNull(Long ownerUserId, Pageable pageable);

    Page<Space> findBySpaceNameContainingIgnoreCase(String spaceName, Pageable pageable);

    List<Space> findByOwnerUserIdInAndDeletedAtIsNullOrderByIdAsc(Collection<Long> ownerUserIds);

    long countByDeletedAtIsNull();
}
