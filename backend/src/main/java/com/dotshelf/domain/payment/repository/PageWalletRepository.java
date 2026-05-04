package com.dotshelf.domain.payment.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.payment.entity.PageWallet;

import jakarta.persistence.LockModeType;

public interface PageWalletRepository extends JpaRepository<PageWallet, Long> {

    Optional<PageWallet> findByUserId(Long userId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select wallet from PageWallet wallet where wallet.userId = :userId")
    Optional<PageWallet> findByUserIdForUpdate(@Param("userId") Long userId);

    @Query("select coalesce(sum(wallet.balancePages), 0) from PageWallet wallet")
    long sumBalancePages();
}
