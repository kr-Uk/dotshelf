using System;
using System.Collections.Generic;
using App.Domain.Decoration;

namespace App.Application.Decoration
{
    // 데코레이션 배치 조회. EnterEditMode / MainScene 로드 시 호출하여 이전 세션의 배치 복원.
    public sealed class LoadDecorationsUseCase
    {
        private readonly IDecorationRepository repository;

        public LoadDecorationsUseCase(IDecorationRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(Action<IReadOnlyList<DecorationLayoutItem>> onSuccess, Action<string> onError)
        {
            repository.Load(onSuccess, onError);
        }
    }
}
