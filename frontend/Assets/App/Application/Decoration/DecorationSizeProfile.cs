using System.Collections.Generic;
using App.Domain.Decoration;

namespace App.Application.Decoration
{
    // 카테고리(Kind) 별 표준 크기 + fit 축. 순수 데이터.
    // 정책 (RoomEditModeController.NormalizeSpawnedScale 가 이 테이블을 사용):
    //   - 새로 spawn 된 prefab 의 Renderer.bounds 에서 FitAxis 에 해당하는 길이를 측정
    //   - measured / TargetMeters 의 역수가 등방 multiplier (모든 축에 동일 적용 → 비율 보존)
    //   - 사용자 ScaleOverride(=multiplier) 가 0 이 아니면 그 값을 추가 곱
    public readonly struct DecorationSizeProfile
    {
        public readonly DecorationObjectKind Kind;
        public readonly float TargetMeters;
        public readonly DecorationFitAxis Axis;

        public DecorationSizeProfile(DecorationObjectKind kind, float targetMeters, DecorationFitAxis axis)
        {
            Kind = kind;
            TargetMeters = targetMeters;
            Axis = axis;
        }
    }

    // Kind → DecorationSizeProfile 룩업. 디자이너 변경이 거의 없는 정책이라 코드 상수로 둔다.
    // 변경 빈도가 잦아지면 ScriptableObject 로 승격 — 그 때도 NormalizeSpawnedScale 의 호출
    // 시그니처는 동일.
    //
    // 향후 DB 가 Kind 별 표준 크기를 내려주는 단계가 오면 IDecorationMetadataProvider 가
    // DecorationMetadata.SizeProfileOverride 를 덧붙이는 식으로 확장 (Profile 자체는 lookup
    // 결과로 유지하고, override 가 우선되게).
    public static class DecorationSizeProfileTable
    {
        // 표준 크기 — Unity 월드 미터 기준. 책상 1.2m, 책장 1.5m, 러그 1.4m, 플로어램프 키 1.3m 등.
        // 작은 prop 류는 0.30~0.50m 로 차등 (clock vs pillow vs plant).
        // 변경 시 같은 Kind 를 쓰는 모든 항목 크기에 영향 → 점진 변경 권장.
        private static readonly Dictionary<DecorationObjectKind, DecorationSizeProfile> profiles =
            new Dictionary<DecorationObjectKind, DecorationSizeProfile>
            {
                { DecorationObjectKind.Desk,       new DecorationSizeProfile(DecorationObjectKind.Desk,       1.20f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Chair,      new DecorationSizeProfile(DecorationObjectKind.Chair,      0.55f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Bookshelf,  new DecorationSizeProfile(DecorationObjectKind.Bookshelf,  1.50f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Storage,    new DecorationSizeProfile(DecorationObjectKind.Storage,    0.80f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Bed,        new DecorationSizeProfile(DecorationObjectKind.Bed,        1.40f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Rug,        new DecorationSizeProfile(DecorationObjectKind.Rug,        1.40f, DecorationFitAxis.LongestXZ) },
                // Lamp 만 Y 축 fit — 플로어램프는 키가 spec, XZ 는 좁아 LongestXZ 쓰면 너무 커진다.
                { DecorationObjectKind.Lamp,       new DecorationSizeProfile(DecorationObjectKind.Lamp,       1.30f, DecorationFitAxis.Y)         },
                { DecorationObjectKind.Plant,      new DecorationSizeProfile(DecorationObjectKind.Plant,      0.45f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Pillow,     new DecorationSizeProfile(DecorationObjectKind.Pillow,     0.45f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Monitor,    new DecorationSizeProfile(DecorationObjectKind.Monitor,    0.50f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Decoration, new DecorationSizeProfile(DecorationObjectKind.Decoration, 0.30f, DecorationFitAxis.LongestXZ) },
                { DecorationObjectKind.Window,     new DecorationSizeProfile(DecorationObjectKind.Window,     1.00f, DecorationFitAxis.LongestXZ) },
            };

        // None 은 lookup 실패 → 호출부가 다음 fallback 단계(legacy canonical / 폴더 휴리스틱)
        // 로 진행. None 을 dictionary 에 넣으면 fallback 진입을 막아버려 의도와 어긋나므로 의도적 제외.
        public static bool TryGet(DecorationObjectKind kind, out DecorationSizeProfile profile)
        {
            return profiles.TryGetValue(kind, out profile);
        }
    }
}
