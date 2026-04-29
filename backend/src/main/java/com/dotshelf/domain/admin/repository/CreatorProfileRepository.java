package com.dotshelf.domain.admin.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.CreatorProfile;

public interface CreatorProfileRepository extends JpaRepository<CreatorProfile, Long> {

    Optional<CreatorProfile> findByUserId(Long userId);

    List<CreatorProfile> findAllByOrderByIdDesc();

    Page<CreatorProfile> findAllByOrderByIdDesc(Pageable pageable);
}
