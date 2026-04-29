package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.PolicyVersion;

public interface PolicyVersionRepository extends JpaRepository<PolicyVersion, Long> {

    List<PolicyVersion> findAllByOrderByIdDesc();

    Page<PolicyVersion> findAllByOrderByIdDesc(Pageable pageable);
}
