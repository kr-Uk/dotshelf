using System.Collections.Generic;
using System.Text;
using App.Infrastructure.Catalogs;

namespace App.Bootstrap
{
    // 온보딩 Step2 "랜덤" 버튼 클릭 시 OnboardingRandomThemeBuilder 가 생성하는 runtime selection.
    //
    // 단일 RoomThemeAsset 으로 표현할 수 없는 "ThemeSlot 별 spec mix" 의 결과 — 같은 ThemeSlot 에는
    // 1개 spec 만 존재 (위치 겹침 방지 1차 가드). 각 entry 는 spec 이 어느 RoomThemeAsset 출신인지도
    // 함께 보존 (직렬화 / 디버깅 / preview baseTheme fallback 용).
    //
    // 저장 표현: RoomEditModeController.PersistLastAppliedRandomThemeSelection 이 이 객체를
    //   "themeAssetName:slotIntId:assetKey;..." 같은 평탄 string 으로 직렬화 → user-scoped
    //   PlayerPrefs 키에 저장. 복원 시 BuildFromSerialized 가 같은 5개 RoomThemeAsset 자산을
    //   catalog 에서 다시 lookup 해 객체 재구성.
    internal sealed class OnboardingRandomThemeSelection
    {
        public sealed class Entry
        {
            public ThemeSlot Slot;
            public string ThemeAssetName; // ScriptableObject.name (예: "RoomTheme_Vintage2")
            public string AssetKey;       // spec.AssetKey (1:1 prefab key)
            public RoomThemeAsset OwnerTheme; // runtime reference — 직렬화 X (복원 시 catalog lookup)
            public RoomThemeDecoSpec Spec;    // runtime reference — 직렬화 X

            // floor / wall / shelf / dashboard 어떤 카테고리 spec 출신인지 — 적용 시점 분기 위해.
            public DecoCategory Category;
        }

        public enum DecoCategory
        {
            Floor,
            Wall,
            Bookshelf, // RoomThemeAsset.BookshelfDecoSpecs 출신 (legacy — 현재는 거의 비어있음)
        }

        // ThemeSlot 별 1개씩만 — 같은 슬롯 중복 방지. Dictionary 자체가 슬롯 unique 보장.
        public readonly Dictionary<ThemeSlot, Entry> SelectedSpecs = new Dictionary<ThemeSlot, Entry>();

        // baseTheme — material / lighting / dashboard 등 *카테고리 default entries* 와
        // *RoomThemeApplier 의 material/light swap* 의 source. 가장 많은 spec 을 기여한 theme 으로
        // 자동 선택 (없으면 5개 중 첫 valid theme).
        public RoomThemeAsset BaseTheme;

        // 랜덤 seed — 디버깅 / 재현용. 사용자에게는 노출 X.
        public int Seed;

        // UI 라벨 — 좌측 패널의 "현재 선택 테마 이름" 영역 표시용.
        public string DisplayName = "랜덤 조합";
        public string Description;

        public int SlotCount => SelectedSpecs.Count;

        // PlayerPrefs 직렬화용 평탄 표현. 형식: "base:<assetName>|slotId:assetName:assetKey:category;slotId:..."
        // 구분자 (':' / '|' / ';') 는 ScriptableObject.name / assetKey 에서 사용되지 않는다는 가정 —
        // 매칭 검증은 builder.Build* 가 catalog lookup 으로 다시 잡으므로 robust.
        public string SerializeToPrefs()
        {
            var sb = new StringBuilder();
            sb.Append("base:").Append(BaseTheme != null ? BaseTheme.name : string.Empty);
            sb.Append("|seed:").Append(Seed);
            sb.Append("|specs:");
            bool first = true;
            foreach (var kv in SelectedSpecs)
            {
                if (!first) sb.Append(';');
                sb.Append((int)kv.Key)
                  .Append(':')
                  .Append(kv.Value.ThemeAssetName ?? string.Empty)
                  .Append(':')
                  .Append(kv.Value.AssetKey ?? string.Empty)
                  .Append(':')
                  .Append((int)kv.Value.Category);
                first = false;
            }
            return sb.ToString();
        }

        // 로그/요약용 — "Rug=Vintage2/vintage_rug; Lamp=Modern/modern_lamp; ..." 형태.
        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.Append("base=").Append(BaseTheme != null ? BaseTheme.name : "null");
            sb.Append(", slots=").Append(SelectedSpecs.Count);
            sb.Append(", seed=").Append(Seed);
            foreach (var kv in SelectedSpecs)
            {
                sb.Append(' ').Append(kv.Key).Append('=')
                  .Append(kv.Value.ThemeAssetName ?? "?").Append('/')
                  .Append(kv.Value.AssetKey ?? "?");
            }
            return sb.ToString();
        }
    }
}
