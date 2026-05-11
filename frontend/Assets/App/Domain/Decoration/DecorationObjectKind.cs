namespace App.Domain.Decoration
{
    // 데코 오브젝트의 "무엇인가" 분류. CustomizationCategory("어디 놓는가" placement) 와 직교.
    // 카테고리별 표준 크기/스케일 fit 축 lookup 키로 사용된다 (DecorationSizeProfileTable).
    //
    // 향후 DB/샵 인벤토리에서 메타데이터를 내려줄 때 동일 enum 값을 그대로 매핑할 수 있도록
    // Domain 계층(UnityEngine 미참조)에 둔다 — DTO 직렬화와 enum 값이 분리되지 않는다.
    //
    // None 은 "분류 안 됨" 의미. NormalizeSpawnedScale 의 lookup 체인이 None 을 만나면 다음
    // 단계(legacy canonical → 폴더 휴리스틱) 로 fallback 해서 기존 동작이 그대로 유지된다.
    public enum DecorationObjectKind
    {
        None = 0,

        // 가구 ----
        Desk,        // 책상 / 일반 테이블
        Chair,       // 의자
        Bookshelf,   // 책장 (자체 시스템과 별개로 추가 가능성 대비)
        Storage,     // 서랍 / 캐비넷 / 콘솔
        Bed,         // 침대

        // 바닥재 / 조명 ----
        Rug,         // 러그 — 바닥 footprint 가 spec
        Lamp,        // 플로어/데스크/벽 램프 공통 — Y(키) 가 spec

        // 식물 / 작은 props ----
        Plant,       // 화분 / 화초
        Pillow,      // 쿠션 / 인형 형태
        Monitor,     // 데스크 위 모니터/노트북류
        Decoration,  // 작은 일반 prop (시계, 인형, 네온사인 등)

        // 벽/창문 ----
        Window,      // 창문 — 벽면 spec, 회전 비활성 입장
    }

    // localScale fit 시 어떤 축을 targetSize 에 맞출지. 등방 multiplier 산출의 기준 축.
    //   LongestXZ : Max(bounds.size.x, bounds.size.z) → targetSize. 일반 가구/props 의 디폴트.
    //   X         : bounds.size.x → targetSize.
    //   Z         : bounds.size.z → targetSize.
    //   Y         : bounds.size.y → targetSize. 플로어램프처럼 키가 spec 인 경우.
    public enum DecorationFitAxis
    {
        LongestXZ = 0,
        X,
        Z,
        Y,
    }
}
