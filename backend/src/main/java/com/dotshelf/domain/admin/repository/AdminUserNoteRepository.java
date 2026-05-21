package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.AdminUserNote;

public interface AdminUserNoteRepository extends JpaRepository<AdminUserNote, Long> {

    List<AdminUserNote> findByUserIdOrderByIdDesc(Long userId);

    Page<AdminUserNote> findByUserIdOrderByIdDesc(Long userId, Pageable pageable);
}
