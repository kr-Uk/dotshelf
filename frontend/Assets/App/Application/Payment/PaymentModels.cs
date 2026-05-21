using System;
using System.Collections.Generic;
using App.Domain.Payment;

namespace App.Application.Payment
{
    /// <summary>
    /// 결제 주문 생성 결과. ChargeScreen → PendingScreen 으로 전달되는 데이터.
    /// </summary>
    public sealed class PaymentChargeStartResult
    {
        public PaymentChargeStartResult(PaymentOrder order, string fullPaymentPageUrl)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            FullPaymentPageUrl = fullPaymentPageUrl ?? string.Empty;
        }

        public PaymentOrder Order { get; }

        /// <summary>
        /// 외부 브라우저 (`Application.OpenURL`) 로 열 절대 URL. BE 응답의 paymentPageUrl 이 relative ("/v1/payments/inicis/request/{orderId}")
        /// 라 NotebookApiClient.BaseUrl 을 prefix 로 붙여 만든 absolute URL.
        /// </summary>
        public string FullPaymentPageUrl { get; }
    }

    /// <summary>
    /// 폴링 라운드의 결과. PendingScreen 이 매 tick 받아 상태 갱신.
    /// </summary>
    public sealed class PaymentPollResult
    {
        public PaymentPollResult(string orderId, PaymentStatus status, int? walletBalancePagesAfter)
        {
            OrderId = orderId ?? string.Empty;
            Status = status;
            WalletBalancePagesAfter = walletBalancePagesAfter;
        }

        public string OrderId { get; }

        public PaymentStatus Status { get; }

        /// <summary>완료(DONE) 시점에만 BE 가 wallet 잔액 포함. 그 외 null.</summary>
        public int? WalletBalancePagesAfter { get; }

        public bool IsTerminal => PaymentStatuses.IsTerminal(Status);
    }

    /// <summary>
    /// 내역 조회 결과 (list pattern, backward-compat endpoint).
    /// </summary>
    public sealed class PaymentTransactionsResult
    {
        public PaymentTransactionsResult(IReadOnlyList<PageTransactionView> transactions)
        {
            Transactions = transactions ?? Array.Empty<PageTransactionView>();
        }

        public IReadOnlyList<PageTransactionView> Transactions { get; }
    }
}
