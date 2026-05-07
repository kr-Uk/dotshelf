using System;
using System.Collections.Generic;
using App.Domain.Decoration;

namespace App.Application.Decoration
{
    // 데코레이션 배치 영속화 추상화. 현재 구현체:
    //   - LocalFileDecorationRepository (Infrastructure) — JSON 로컬 파일.
    // 백엔드 API (예: PUT/GET /v1/spaces/me/decorations) 가 추가되면 ApiDecorationRepository
    // 가 같은 인터페이스를 구현하면 호출부 무수정 교체 가능.
    public interface IDecorationRepository
    {
        // 현재 배치 목록 영속화. items 가 빈 리스트면 "모두 제거" 의미로 저장.
        // onSuccess 는 서버/스토리지 commit 후 1회 호출. onError 는 사용자 표시용 짧은 메시지.
        void Save(IReadOnlyList<DecorationLayoutItem> items, Action onSuccess, Action<string> onError);

        // 저장된 배치 목록 조회. 미저장 사용자는 빈 리스트 onSuccess.
        void Load(Action<IReadOnlyList<DecorationLayoutItem>> onSuccess, Action<string> onError);
    }
}
