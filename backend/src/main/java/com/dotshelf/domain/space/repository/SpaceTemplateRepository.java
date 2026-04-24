package com.dotshelf.domain.space.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.SpaceTemplate;

public interface SpaceTemplateRepository extends JpaRepository<SpaceTemplate, Long> {

    List<SpaceTemplate> findByIsActiveTrueOrderByDisplayOrderAsc();
}
