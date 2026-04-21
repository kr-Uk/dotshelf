package com.dotshelf.domain.space.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.SpaceLayoutSnapshot;

public interface SpaceLayoutSnapshotRepository extends JpaRepository<SpaceLayoutSnapshot, Long> {
}
