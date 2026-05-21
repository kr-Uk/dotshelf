using System;
using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 룸의 각 벽에서 placement 가 막힌 영역(obstruction zone)을 추적.
    /// 책장(BackWall)/대시보드(LeftWall)가 차지한 wall surface 영역을 기록 →
    /// wall deco placement 시 비점유 영역만 허용.
    ///
    /// 좌표계 — 각 wall surface 의 local 2D rect (u, v):
    ///   - Back/Front wall: u = world X (룸 폭), v = world Y (높이)
    ///   - Left/Right wall: u = world Z (룸 깊이), v = world Y (높이)
    ///
    /// Build(roomRoot) 호출로 씬의 BookshelfRoot/GuestbookBoard transform 의 renderer
    /// bounds 를 runtime 측정해 자동 등록. 룸 재생성/씬 reload 시 다시 호출.
    /// 정적 hardcoded constants 를 안 쓰는 이유: BookshelfGenerator 의 alcove width/height
    /// 가 instance state(width/height field) 의존이라 컴파일 타임 측위 불가.
    /// </summary>
    public sealed class WallObstructionRegistry
    {
        private readonly Dictionary<WallSide, List<Rect>> obstructionsByWall = new();

        public WallObstructionRegistry()
        {
            foreach (WallSide ws in Enum.GetValues(typeof(WallSide)))
            {
                obstructionsByWall[ws] = new List<Rect>();
            }
        }

        // 씬의 obstruction GameObject 들을 측정해 wall 별 rect 등록. 호출 전 등록 내역은 모두 clear.
        public void Build(Transform roomRoot)
        {
            foreach (var list in obstructionsByWall.Values) list.Clear();
            if (roomRoot == null) return;

            // BackWall: 책장 (BookshelfRoot 의 자식 mesh 들의 통합 bounds)
            var bookshelf = roomRoot.Find("BookshelfRoot");
            if (bookshelf != null)
            {
                var bounds = ComputeWorldBounds(bookshelf);
                if (bounds.HasValue)
                {
                    obstructionsByWall[WallSide.Back].Add(ProjectWorldBoundsToWallSurface(bounds.Value, WallSide.Back));
                }
            }

            // LeftWall: 대시보드 (GuestbookBoard frame + surface)
            var dashboard = roomRoot.Find("GuestbookBoard");
            if (dashboard != null)
            {
                var bounds = ComputeWorldBounds(dashboard);
                if (bounds.HasValue)
                {
                    obstructionsByWall[WallSide.Left].Add(ProjectWorldBoundsToWallSurface(bounds.Value, WallSide.Left));
                }
            }
        }

        // candidateRect 가 wall 의 모든 obstruction 과 안 겹치면 placement 허용.
        public bool IsRectFreeOnWall(WallSide wall, Rect candidateRect)
        {
            if (!obstructionsByWall.TryGetValue(wall, out var list)) return true;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Overlaps(candidateRect)) return false;
            }
            return true;
        }

        public IReadOnlyList<Rect> GetObstructions(WallSide wall)
        {
            return obstructionsByWall.TryGetValue(wall, out var list)
                ? (IReadOnlyList<Rect>)list
                : Array.Empty<Rect>();
        }

        private static Bounds? ComputeWorldBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // World AABB → wall surface 의 (u, v) Rect.
        // wall 별 axis 매핑:
        //   - Back/Front: u=X, v=Y
        //   - Left/Right: u=Z, v=Y
        private static Rect ProjectWorldBoundsToWallSurface(Bounds worldBounds, WallSide wall)
        {
            float vMin = worldBounds.min.y;
            float vMax = worldBounds.max.y;
            float uMin, uMax;
            switch (wall)
            {
                case WallSide.Back:
                case WallSide.Front:
                    uMin = worldBounds.min.x;
                    uMax = worldBounds.max.x;
                    break;
                case WallSide.Left:
                case WallSide.Right:
                    uMin = worldBounds.min.z;
                    uMax = worldBounds.max.z;
                    break;
                default:
                    return Rect.zero;
            }
            return new Rect(uMin, vMin, uMax - uMin, vMax - vMin);
        }
    }
}
