using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 온보딩 우측 RenderTexture preview 의 mock room 안에 ThemeSlot 별 prefab spawn 위치/회전/스케일
    // 클램프를 정의하는 정적 lookup. OnboardingThemePreviewRenderer.ApplyRandomSelection 이 본 테이블을
    // 사용해 instance 의 transform 을 결정.
    //
    // 좌표계: OnboardingThemePreviewRenderer 의 PreviewRoomRoot local frame (4×3×4m room, floor y=0,
    // back wall z=+2, left wall x=-2 ...). mock primitive (PreviewBookshelf / PreviewDesk 등) 의 위치와
    // 의도적으로 정렬해 랜덤 모드에서 mock 가 hide 됐을 때 같은 자리에 prefab 이 들어와 자연스러움.
    //
    // spec.LocalPosition 은 사용하지 않는다 — placedItemsRoot (world origin) frame 기준이라 preview room
    // local frame 과 부합하지 않음. 대신 본 테이블의 anchor 위치를 base 로 spec.LocalRotation /
    // spec.LocalScale 만 적용 (회전·크기는 의미 일관).
    //
    // 디자이너 보정 여지: 향후 PreviewSlotAnchorAsset (ScriptableObject) 으로 분리하면 코드 수정 없이
    // 위치 미세 조정 가능 — 본 PR 에서는 static 값으로 시작.
    internal static class OnboardingPreviewSlotAnchor
    {
        // 한 ThemeSlot 의 anchor 데이터. localPosition / localEulerAngles 는 PreviewRoomRoot frame,
        // maxBoundsSize 는 instance 의 world AABB 가 이 size 를 넘으면 비례 축소 (preview 안에 fit).
        public readonly struct Anchor
        {
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalEulerAngles;
            public readonly Vector3 MaxBoundsSize;
            // spec.LocalScale 이 (0,0,0) 인 entry 에 적용할 default — 일부 spec 이 placedItemsRoot frame
            // 의 raw scale 만 잡혀 있어 preview room 에 직접 쓰면 너무 크거나 작다.
            public readonly Vector3 DefaultScale;

            public Anchor(Vector3 pos, Vector3 euler, Vector3 maxSize, Vector3 defaultScale)
            {
                LocalPosition = pos;
                LocalEulerAngles = euler;
                MaxBoundsSize = maxSize;
                DefaultScale = defaultScale;
            }
        }

        // ThemeSlot 별 사전 정의 anchor. 정의 안 된 slot 은 false 반환 → 호출자가 origin (0,0,0) 으로 spawn.
        public static bool TryGetAnchor(ThemeSlot slot, out Anchor anchor)
        {
            switch (slot)
            {
                case ThemeSlot.Shelf:
                    // back-left, mock bookshelf 자리. 책장은 보통 1.4×1.8×0.4m.
                    anchor = new Anchor(
                        new Vector3(-1.1f, 0f, 1.55f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1.6f, 2.0f, 0.5f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Table:
                    // front-center-right, mock desk 자리. 대략 1.4×0.8×0.7m.
                    anchor = new Anchor(
                        new Vector3(0.6f, 0f, 0.5f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1.6f, 1.0f, 0.9f),
                        Vector3.one);
                    return true;
                case ThemeSlot.TableMini:
                    // front-right corner, small accent table.
                    anchor = new Anchor(
                        new Vector3(1.4f, 0f, -0.6f),
                        new Vector3(0f, -20f, 0f),
                        new Vector3(0.8f, 0.7f, 0.6f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Lamp:
                    // on desk (lamp 또는 standing lamp).
                    anchor = new Anchor(
                        new Vector3(1.05f, 0.82f, 0.5f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0.5f, 0.6f, 0.5f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Dashboard:
                    // back wall above desk.
                    anchor = new Anchor(
                        new Vector3(0.6f, 1.55f, 1.92f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1.4f, 0.85f, 0.1f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Rug:
                    // floor center — 살짝 위로 띄워 z-fight 회피.
                    anchor = new Anchor(
                        new Vector3(0f, 0.005f, 0f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(2.6f, 0.04f, 2.0f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Frame:
                    // left wall middle — 액자 / 작은 데코.
                    anchor = new Anchor(
                        new Vector3(-1.96f, 1.6f, -0.3f),
                        new Vector3(0f, 90f, 0f),
                        new Vector3(0.1f, 0.8f, 0.6f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Bed:
                    // back-right corner. 큰 가구라 wide bounds.
                    anchor = new Anchor(
                        new Vector3(1.2f, 0f, 1.3f),
                        new Vector3(0f, -90f, 0f),
                        new Vector3(1.4f, 0.9f, 2.0f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Window:
                    // back wall center, 위쪽 — 창문.
                    anchor = new Anchor(
                        new Vector3(0f, 1.7f, 1.96f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1.4f, 1.0f, 0.1f),
                        Vector3.one);
                    return true;
                case ThemeSlot.Plant:
                    // front-left corner.
                    anchor = new Anchor(
                        new Vector3(-1.5f, 0f, -1.3f),
                        new Vector3(0f, 30f, 0f),
                        new Vector3(0.7f, 1.2f, 0.7f),
                        Vector3.one);
                    return true;
                default:
                    anchor = default;
                    return false;
            }
        }
    }
}
