package com.dotshelf.domain.payment.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class PaymentRepositoryTest {

    @Autowired
    private PaymentOrderRepository paymentOrderRepository;

    @Autowired
    private PageWalletRepository walletRepository;

    @Autowired
    private PageTransactionRepository transactionRepository;

    @Test
    @DisplayName("주문번호는 unique 제약으로 중복 저장을 막는다")
    void paymentOrder_uniqueOrderId() {
        paymentOrderRepository.saveAndFlush(TestFixtures.paymentOrder("order-1", 1L, 1000));

        assertThatThrownBy(() -> paymentOrderRepository.saveAndFlush(TestFixtures.paymentOrder("order-1", 2L, 1000)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("orderId와 providerTransactionId로 주문을 조회한다")
    void findByOrderIdAndProviderTransactionId() {
        var order = TestFixtures.paymentOrder("order-1", 1L, 1000);
        order.complete("tid-1", "CARD", java.time.Instant.now());
        paymentOrderRepository.saveAndFlush(order);

        assertThat(paymentOrderRepository.findByOrderId("order-1")).isPresent();
        assertThat(paymentOrderRepository.findByProviderTransactionId("tid-1")).isPresent();
        assertThat(paymentOrderRepository.findByOrderIdForUpdate("order-1")).isPresent();
    }

    @Test
    @DisplayName("지갑은 userId unique 제약과 잔액 합계 쿼리를 제공한다")
    void wallet_uniqueUserAndSum() {
        walletRepository.save(TestFixtures.wallet(1L, 100));
        walletRepository.save(TestFixtures.wallet(2L, 200));
        walletRepository.flush();

        assertThat(walletRepository.findByUserId(1L)).isPresent();
        assertThat(walletRepository.findByUserIdForUpdate(1L)).isPresent();
        assertThat(walletRepository.sumBalancePages()).isEqualTo(300);
        assertThatThrownBy(() -> walletRepository.saveAndFlush(TestFixtures.wallet(1L, 10)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("페이지 트랜잭션은 충전 이력을 저장한다")
    void pageTransaction_savesChargeHistory() {
        PageTransaction transaction = transactionRepository.saveAndFlush(
                TestFixtures.pageTransaction(1L, 10L, 100, 150));

        assertThat(transaction.getType()).isEqualTo(PageTransaction.TYPE_CHARGE);
        assertThat(transaction.getBalanceAfter()).isEqualTo(150);
    }
}
