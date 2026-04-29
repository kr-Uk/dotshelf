package com.dotshelf.domain.payment.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

import com.dotshelf.domain.payment.entity.PageWallet;

public interface PageWalletRepository extends JpaRepository<PageWallet, Long> {

    Optional<PageWallet> findByUserId(Long userId);

    @Query("select coalesce(sum(wallet.balancePages), 0) from PageWallet wallet")
    long sumBalancePages();
}
