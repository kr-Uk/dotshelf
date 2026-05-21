using System;
using App.Domain.Payment;

namespace App.Application.Payment
{
    /// <summary>
    /// Features 의 Charge/Pending/History 화면이 통과하는 단일 UseCase.
    /// 외부 IPaymentRepository (Infrastructure) 와 도메인 (Domain.Payment) 사이를 잇는다.
    /// MonoBehaviour 가 아니라 순수 C# — Features 가 인스턴스 생성하거나 Bootstrap composition root 에서 주입.
    /// </summary>
    public sealed class PaymentUseCase
    {
        private readonly IPaymentRepository repository;

        public PaymentUseCase(IPaymentRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// 충전 주문 생성. 응답으로 paymentPageUrl + orderId 를 받으면 호출자가 외부 브라우저를 열고 PendingScreen 으로 전환.
        /// </summary>
        public void StartCharge(
            ChargePreset preset,
            string buyerName,
            string buyerTel,
            string buyerEmail,
            Action<PaymentChargeStartResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            if (string.IsNullOrWhiteSpace(buyerName))
            {
                onError?.Invoke(new PaymentApiError
                {
                    Code = "INVALID_BUYER",
                    Message = "구매자 이름을 입력해주세요.",
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(buyerTel))
            {
                onError?.Invoke(new PaymentApiError
                {
                    Code = "INVALID_BUYER",
                    Message = "구매자 전화번호를 입력해주세요.",
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(buyerEmail))
            {
                onError?.Invoke(new PaymentApiError
                {
                    Code = "INVALID_BUYER",
                    Message = "구매자 이메일을 입력해주세요.",
                });
                return;
            }

            repository.CreateChargeOrder(preset, buyerName.Trim(), buyerTel.Trim(), buyerEmail.Trim(),
                onSuccess, onError);
        }

        /// <summary>
        /// 결제 주문 상태 1회 조회. PendingScreen 이 2초 간격으로 호출.
        /// 결과의 IsTerminal 이 true 면 폴링 중단.
        /// </summary>
        public void PollOrderStatus(
            string orderId,
            Action<PaymentPollResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                onError?.Invoke(new PaymentApiError
                {
                    Code = "INVALID_ORDER_ID",
                    Message = "주문 ID 가 비어 있습니다.",
                });
                return;
            }

            repository.GetOrderStatus(orderId.Trim(), onSuccess, onError);
        }

        /// <summary>
        /// 페이지 충전·사용 내역 조회. HistoryScreen 진입 시 1회 호출. cursor paging 은 Phase 2 follow-up.
        /// </summary>
        public void FetchTransactions(
            int limit,
            Action<PaymentTransactionsResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            repository.GetTransactions(Math.Max(1, limit), onSuccess, onError);
        }
    }
}
