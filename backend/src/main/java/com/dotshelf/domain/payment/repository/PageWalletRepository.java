package com.dotshelf.domain.payment.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.payment.entity.PageWallet;

public interface PageWalletRepository extends JpaRepository<PageWallet, Long> {

    Optional<PageWallet> findByUserId(Long userId);
}
