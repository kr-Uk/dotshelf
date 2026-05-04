package com.dotshelf.domain.payment.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.payment.entity.PageTransaction;

public interface PageTransactionRepository extends JpaRepository<PageTransaction, Long> {
}
