using System;
using App.Infrastructure.Api.Dtos;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Infrastructure.Api
{
    /// <summary>
    /// 결제 도메인 전용 HTTP client. NotebookApiClient 와 패턴 일관 (APIManager wrapper).
    /// 분리한 이유 — 결제 도메인이 Notebook 의 sub-feature 가 아닌 별 도메인이고, 새 endpoint
    /// 4종 (page-charge/orders, orders/{id}, transactions) 의 명확한 contract 가 필요.
    /// </summary>
    public sealed class PaymentApiClient
    {
        private readonly APIManager api;

        public PaymentApiClient(APIManager api)
        {
            this.api = api != null ? api : APIManager.Instance;
        }

        public string BaseUrl => RequireApi()?.BaseUrl ?? string.Empty;

        public Coroutine CreateChargeOrder(
            PageChargeOrderRequestDto request,
            Action<PageChargeOrderResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError = null)
        {
            return RequireApi().Post<PageChargeOrderRequestDto, PageChargeOrderResponseDto>(
                "/v1/payments/page-charge/orders", request, onSuccess, onError);
        }

        public Coroutine GetOrderStatus(
            string orderId,
            Action<PaymentOrderStatusResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError = null)
        {
            var safeOrderId = UnityWebRequest.EscapeURL(orderId ?? string.Empty);
            return RequireApi().Get<PaymentOrderStatusResponseDto>(
                $"/v1/payments/orders/{safeOrderId}", onSuccess, onError);
        }

        public Coroutine GetTransactions(
            int limit,
            Action<PageTransactionListResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError = null)
        {
            return RequireApi().Get<PageTransactionListResponseDto>(
                $"/v1/payments/transactions?limit={Mathf.Clamp(limit, 1, 500)}",
                onSuccess,
                onError);
        }

        private APIManager RequireApi()
        {
            return api != null ? api : APIManager.Instance;
        }
    }
}
