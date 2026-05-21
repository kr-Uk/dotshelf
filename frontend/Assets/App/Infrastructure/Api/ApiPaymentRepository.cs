using System;
using System.Collections.Generic;
using App.Application.Payment;
using App.Domain.Payment;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api
{
    /// <summary>
    /// Application.IPaymentRepository 의 HTTP 구현. PaymentApiClient 의 DTO 를 도메인 객체로 매핑.
    /// </summary>
    public sealed class ApiPaymentRepository : IPaymentRepository
    {
        private const string Platform = "UNITY";
        private readonly PaymentApiClient client;

        public ApiPaymentRepository(PaymentApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void CreateChargeOrder(
            ChargePreset preset,
            string buyerName,
            string buyerTel,
            string buyerEmail,
            Action<PaymentChargeStartResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            int pages = ChargePresets.PagesOf(preset);
            var request = new PageChargeOrderRequestDto
            {
                PagesAmount = pages,
                AmountKrw = pages, // BE pageUnitPriceKrw 기본 1 — 향후 정책 변경 시 BE 가 mismatch 로 차단.
                Platform = Platform,
                BuyerName = buyerName,
                BuyerTel = buyerTel,
                BuyerEmail = buyerEmail,
            };

            client.CreateChargeOrder(
                request,
                response =>
                {
                    var order = MapToOrder(response);
                    var fullUrl = BuildAbsoluteUrl(response?.PaymentPageUrl);
                    onSuccess?.Invoke(new PaymentChargeStartResult(order, fullUrl));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void GetOrderStatus(
            string orderId,
            Action<PaymentPollResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            client.GetOrderStatus(
                orderId,
                response => onSuccess?.Invoke(new PaymentPollResult(
                    response?.OrderId,
                    PaymentStatuses.FromServerString(response?.Status),
                    response?.Wallet?.BalancePages)),
                error => onError?.Invoke(MapError(error)));
        }

        public void GetTransactions(
            int limit,
            Action<PaymentTransactionsResult> onSuccess,
            Action<PaymentApiError> onError)
        {
            client.GetTransactions(
                limit,
                response => onSuccess?.Invoke(new PaymentTransactionsResult(MapTransactions(response?.Transactions))),
                error => onError?.Invoke(MapError(error)));
        }

        private string BuildAbsoluteUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return relativePath;
            }

            string baseUrl = (client.BaseUrl ?? string.Empty).TrimEnd('/');
            string path = relativePath.StartsWith("/", StringComparison.Ordinal) ? relativePath : "/" + relativePath;
            return baseUrl + path;
        }

        private static PaymentOrder MapToOrder(PageChargeOrderResponseDto dto)
        {
            if (dto == null)
            {
                throw new InvalidOperationException("PageChargeOrderResponse 가 비어있습니다.");
            }

            return new PaymentOrder(
                dto.OrderId,
                dto.OrderName,
                dto.PagesAmount,
                dto.AmountKrw,
                PaymentStatuses.FromServerString(dto.Status),
                dto.PaymentPageUrl,
                ParseTimestamp(dto.RequestedAt),
                ParseTimestamp(dto.ExpiresAt));
        }

        private static IReadOnlyList<PageTransactionView> MapTransactions(IReadOnlyList<PageTransactionResponseDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
            {
                return Array.Empty<PageTransactionView>();
            }

            var list = new List<PageTransactionView>(dtos.Count);
            foreach (var dto in dtos)
            {
                if (dto == null)
                {
                    continue;
                }

                list.Add(new PageTransactionView(
                    dto.Id,
                    dto.Type,
                    dto.PagesDelta,
                    dto.BalanceAfter,
                    dto.Reason,
                    dto.PaymentOrderId,
                    ParseTimestamp(dto.CreatedAt)));
            }
            return list;
        }

        private static DateTimeOffset ParseTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTimeOffset.MinValue;
            }

            if (DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed;
            }
            return DateTimeOffset.MinValue;
        }

        private static PaymentApiError MapError(ApiErrorResponseDto error)
        {
            return new PaymentApiError
            {
                Code = error?.Code,
                Message = string.IsNullOrWhiteSpace(error?.Message) ? error?.Error : error.Message,
            };
        }
    }
}
