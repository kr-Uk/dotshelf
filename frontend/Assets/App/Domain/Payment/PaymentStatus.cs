namespace App.Domain.Payment
{
    /// <summary>
    /// BE `PaymentOrder.status` 와 1:1 매핑. 서버 string 값과 동일한 enum name.
    /// </summary>
    public enum PaymentStatus
    {
        Unknown,
        Ready,
        Authenticated,
        Done,
        Failed,
        Canceled,
    }

    public static class PaymentStatuses
    {
        public static PaymentStatus FromServerString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return PaymentStatus.Unknown;
            }

            switch (value)
            {
                case "READY":
                    return PaymentStatus.Ready;
                case "AUTHENTICATED":
                    return PaymentStatus.Authenticated;
                case "DONE":
                    return PaymentStatus.Done;
                case "FAILED":
                    return PaymentStatus.Failed;
                case "CANCELED":
                    return PaymentStatus.Canceled;
                default:
                    return PaymentStatus.Unknown;
            }
        }

        public static bool IsTerminal(PaymentStatus status)
        {
            return status == PaymentStatus.Done
                   || status == PaymentStatus.Failed
                   || status == PaymentStatus.Canceled;
        }
    }
}
