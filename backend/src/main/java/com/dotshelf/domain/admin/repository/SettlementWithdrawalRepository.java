package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.admin.entity.SettlementWithdrawal;

public interface SettlementWithdrawalRepository extends JpaRepository<SettlementWithdrawal, Long> {

    List<SettlementWithdrawal> findAllByOrderByIdDesc();

    Page<SettlementWithdrawal> findAllByOrderByIdDesc(Pageable pageable);

    @Query("""
            select coalesce(sum(withdrawal.requestedPages), 0)
            from SettlementWithdrawal withdrawal
            where upper(withdrawal.status) = upper(:status)
            """)
    long sumRequestedPagesByStatus(@Param("status") String status);
}
