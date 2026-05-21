using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    [Serializable]
    public sealed class PageChargeOrderRequestDto
    {
        [JsonProperty("pagesAmount")]
        public int PagesAmount { get; set; }

        [JsonProperty("amountKrw")]
        public int AmountKrw { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("buyerName")]
        public string BuyerName { get; set; }

        [JsonProperty("buyerTel")]
        public string BuyerTel { get; set; }

        [JsonProperty("buyerEmail")]
        public string BuyerEmail { get; set; }
    }

    [Serializable]
    public sealed class PageChargeOrderResponseDto
    {
        [JsonProperty("orderId")]
        public string OrderId { get; set; }

        [JsonProperty("orderName")]
        public string OrderName { get; set; }

        [JsonProperty("pagesAmount")]
        public int PagesAmount { get; set; }

        [JsonProperty("amountKrw")]
        public int AmountKrw { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("paymentPageUrl")]
        public string PaymentPageUrl { get; set; }

        [JsonProperty("requestedAt")]
        public string RequestedAt { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; }
    }

    [Serializable]
    public sealed class PaymentOrderStatusResponseDto
    {
        [JsonProperty("orderId")]
        public string OrderId { get; set; }

        [JsonProperty("providerTransactionId")]
        public string ProviderTransactionId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("pagesAmount")]
        public int PagesAmount { get; set; }

        [JsonProperty("amountKrw")]
        public int AmountKrw { get; set; }

        [JsonProperty("wallet")]
        public WalletResponseDto Wallet { get; set; }
    }

    [Serializable]
    public sealed class PageTransactionResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("pagesDelta")]
        public int PagesDelta { get; set; }

        [JsonProperty("balanceAfter")]
        public int BalanceAfter { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("paymentOrderId")]
        public long? PaymentOrderId { get; set; }

        // Deprecated, Phase 2 에서 BE 가 제거 예정. NotebookApiDtos 의 backward-compat 필드와 동일 패턴.
        [JsonProperty("referenceType")]
        public string ReferenceType { get; set; }

        [JsonProperty("referenceId")]
        public long? ReferenceId { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class PageTransactionListResponseDto
    {
        [JsonProperty("transactions")]
        public List<PageTransactionResponseDto> Transactions { get; set; }
    }
}
