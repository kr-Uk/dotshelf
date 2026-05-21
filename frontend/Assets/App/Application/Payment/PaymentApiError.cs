namespace App.Application.Payment
{
    /// <summary>
    /// BE 결제 API 에러를 Application 레이어로 끌어올린 표현. Infrastructure 레이어의 HTTP 디테일 (status code, raw payload)
    /// 을 노출하지 않고 코드 + 메시지만 전달. Notebook 도메인의 NotebookApiError 와 같은 패턴.
    /// </summary>
    public sealed class PaymentApiError
    {
        public string Code { get; set; }

        public string Message { get; set; }
    }
}
