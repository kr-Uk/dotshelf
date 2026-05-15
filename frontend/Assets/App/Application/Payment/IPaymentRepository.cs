using System;
using App.Domain.Payment;

namespace App.Application.Payment
{
    /// <summary>
    /// Payment 도메인의 외부 의존 경계. Infrastructure 의 ApiPaymentRepository 가 구현.
    /// 모든 비동기 결과는 Unity Coroutine 친화 (Action callback) 패턴 — Notebook 도메인과 일관.
    /// </summary>
    public interface IPaymentRepository
    {
        void CreateChargeOrder(
            ChargePreset preset,
            string buyerName,
            string buyerTel,
            string buyerEmail,
            Action<PaymentChargeStartResult> onSuccess,
            Action<PaymentApiError> onError);

        void GetOrderStatus(
            string orderId,
            Action<PaymentPollResult> onSuccess,
            Action<PaymentApiError> onError);

        void GetTransactions(
            int limit,
            Action<PaymentTransactionsResult> onSuccess,
            Action<PaymentApiError> onError);
    }
}
