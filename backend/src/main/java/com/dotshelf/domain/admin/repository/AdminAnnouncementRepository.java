package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.AdminAnnouncement;

public interface AdminAnnouncementRepository extends JpaRepository<AdminAnnouncement, Long> {

    List<AdminAnnouncement> findAllByOrderByIdDesc();

    Page<AdminAnnouncement> findAllByOrderByIdDesc(Pageable pageable);
}
