package com.dotshelf.domain.payment.repository;

import java.util.List;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.payment.entity.PageTransaction;

public interface PageTransactionRepository extends JpaRepository<PageTransaction, Long> {

    List<PageTransaction> findByUserIdOrderByCreatedAtDesc(Long userId, Pageable pageable);
}
