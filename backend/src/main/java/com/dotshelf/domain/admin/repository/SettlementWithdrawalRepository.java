package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.SettlementWithdrawal;

public interface SettlementWithdrawalRepository extends JpaRepository<SettlementWithdrawal, Long> {

    List<SettlementWithdrawal> findAllByOrderByIdDesc();
}
