namespace App.Domain.Payment
{
    /// <summary>
    /// KG이니시스 충전업종 사전 심사 통과를 위해 금액 자유 입력 대신 프리셋만 제공.
    /// 값은 페이지 수 (1P = pageUnitPriceKrw 원, 기본 1원). BE 정책 API 의 maxChargeKrw (10만원) 안에서만 선택 가능.
    /// </summary>
    public enum ChargePreset
    {
        Pages1000 = 1000,
        Pages5000 = 5000,
        Pages10000 = 10000,
        Pages30000 = 30000,
        Pages50000 = 50000,
        Pages100000 = 100000,
    }

    public static class ChargePresets
    {
        public static readonly ChargePreset[] All =
        {
            ChargePreset.Pages1000,
            ChargePreset.Pages5000,
            ChargePreset.Pages10000,
            ChargePreset.Pages30000,
            ChargePreset.Pages50000,
            ChargePreset.Pages100000,
        };

        public static int PagesOf(ChargePreset preset) => (int)preset;
    }
}
