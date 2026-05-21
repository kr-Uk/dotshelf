using System;

namespace App.Domain.Payment
{
    /// <summary>
    /// BE `PageTransactionResponse` 의 도메인 표현. 내역 화면에서 사용.
    /// </summary>
    public sealed class PageTransactionView
    {
        public PageTransactionView(
            long id,
            string type,
            int pagesDelta,
            int balanceAfter,
            string reason,
            long? paymentOrderId,
            DateTimeOffset createdAt)
        {
            Id = id;
            Type = type ?? string.Empty;
            PagesDelta = pagesDelta;
            BalanceAfter = balanceAfter;
            Reason = reason ?? string.Empty;
            PaymentOrderId = paymentOrderId;
            CreatedAt = createdAt;
        }

        public long Id { get; }

        public string Type { get; }

        /// <summary>충전(+) 또는 사용(-) 페이지 변동량.</summary>
        public int PagesDelta { get; }

        /// <summary>해당 거래 후 잔액.</summary>
        public int BalanceAfter { get; }

        /// <summary>예: "INICIS_PAYMENT" (BE enum name 그대로).</summary>
        public string Reason { get; }

        /// <summary>결제 주문과 연결된 거래면 orderId(BE Long PK), 아니면 null.</summary>
        public long? PaymentOrderId { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
