using System;
using System.Collections.Generic;
using App.Domain.Decoration;

namespace App.Application.Decoration
{
    // 데코레이션 배치 일괄 저장. 명시적 "저장" 버튼 흐름에서만 호출 — 매 배치/이동/회전마다 호출되지
    // 않음 (네트워크 부담 + 사용자가 의도하지 않은 부분 저장 방지).
    public sealed class SaveDecorationsUseCase
    {
        private readonly IDecorationRepository repository;

        public SaveDecorationsUseCase(IDecorationRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(IReadOnlyList<DecorationLayoutItem> items, Action onSuccess, Action<string> onError)
        {
            if (items == null) items = Array.Empty<DecorationLayoutItem>();
            repository.Save(items, onSuccess, onError);
        }
    }
}
