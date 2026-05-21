using System;

namespace App.Domain.Payment
{
    /// <summary>
    /// BE `PaymentOrder` 응답을 받아 만드는 도메인 객체. 순수 C# — UnityEngine 금지.
    /// 폴링 / pending 화면에서 사용.
    /// </summary>
    public sealed class PaymentOrder
    {
        public PaymentOrder(
            string orderId,
            string orderName,
            int pagesAmount,
            int amountKrw,
            PaymentStatus status,
            string paymentPageUrl,
            DateTimeOffset requestedAt,
            DateTimeOffset expiresAt)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                throw new ArgumentException("orderId is required.", nameof(orderId));
            }

            OrderId = orderId;
            OrderName = orderName ?? string.Empty;
            PagesAmount = pagesAmount;
            AmountKrw = amountKrw;
            Status = status;
            PaymentPageUrl = paymentPageUrl ?? string.Empty;
            RequestedAt = requestedAt;
            ExpiresAt = expiresAt;
        }

        public string OrderId { get; }

        public string OrderName { get; }

        public int PagesAmount { get; }

        public int AmountKrw { get; }

        public PaymentStatus Status { get; }

        /// <summary>
        /// BE 가 생성한 KG이니시스 결제 페이지 host URL. FE 가 `Application.OpenURL` 로 외부 브라우저 호출.
        /// </summary>
        public string PaymentPageUrl { get; }

        public DateTimeOffset RequestedAt { get; }

        /// <summary>
        /// BE 의 ORDER_TTL (30분). 사용자가 결제창에서 idle 상태로 30분 지나면 BE 가 자동으로 FAILED 처리.
        /// FE 는 별도 cancel 호출 없이 이 TTL 활용 (D3 결정).
        /// </summary>
        public DateTimeOffset ExpiresAt { get; }

        public bool IsTerminal => PaymentStatuses.IsTerminal(Status);
    }
}
