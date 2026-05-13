using UnityEngine;

namespace Dotshelf.Scripts.RoomTheme
{
    /// <summary>
    /// wall renderer 의 face 크기에 비례한 _BaseMap_ST 를 MaterialPropertyBlock 으로 적용해
    /// 모든 wall 이 동일 world-space texel density 를 갖도록 보정.
    ///
    /// 문제 배경: BookshelfGenerator.CreateBox 가 unit cube 를 비대칭 localScale 로 늘려 wall 을
    /// 만들기 때문에, sharedMaterial 의 tiling=(1,1) 만으로는 face 크기가 다른 BackWall strip
    /// (≈ 1m × 2.8m) 과 LeftWall/RightWall (5m × 2.8m) 에서 한 cycle 의 물리 크기가 달라져
    /// 좌/우 벽이 5배 stretch 되어 보임.
    ///
    /// 해결: 각 wall 의 face 크기를 renderer.bounds 로 측정 → tiling 을 face 크기/patternWorldSize
    /// 로 계산 → MPB 로 override. MPB 는 renderer 단위로 적용되며 sharedMaterial swap 에 영향
    /// 받지 않아 (RoomEditModeController 의 카테고리별 wallpaper 교체에도 유지) 한 번 set 하면
    /// 이후 wallpaper 변경에도 살아남음.
    /// </summary>
    public static class WallTilingNormalizer
    {
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        // 메인 스레드 전용 — Unity 객체 조작은 단일 스레드라 정적 재사용 안전. GC 압박 회피.
        private static readonly MaterialPropertyBlock SharedBlock = new MaterialPropertyBlock();

        /// <summary>
        /// 주어진 wall renderer 에 face 크기 비례 tiling 을 MPB 로 설정.
        /// </summary>
        /// <param name="wallRenderer">대상 wall renderer (LeftWall/RightWall/BackWall_* 등).</param>
        /// <param name="patternWorldSize">텍스처 한 cycle 의 world-space 크기 (m). x 는 가로,
        /// y 는 세로. 예: (1.0, 2.8) → 가로 1m 당 1 cycle, 세로 RoomHeight 당 1 cycle.</param>
        public static void Apply(Renderer wallRenderer, Vector2 patternWorldSize)
        {
            if (wallRenderer == null) return;
            if (patternWorldSize.x <= 0f || patternWorldSize.y <= 0f) return;

            // bounds.size 는 world-space AABB. 축 정렬된 wall 의 경우:
            //   - height = Y (룸 세로)
            //   - 가로 face dim = max(X, Z) (작은 쪽이 wall thickness)
            // CreateBox 가 PrimitiveType.Cube 의 mesh extents=0.5 를 그대로 두고 localScale 로
            // 크기를 만들기 때문에 bounds.size == lossyScale 와 일치.
            var size = wallRenderer.bounds.size;
            float faceWidth = size.x >= size.z ? size.x : size.z;
            float faceHeight = size.y;
            if (faceWidth <= 0f || faceHeight <= 0f) return;

            float tilingU = faceWidth / patternWorldSize.x;
            float tilingV = faceHeight / patternWorldSize.y;
            var st = new Vector4(tilingU, tilingV, 0f, 0f);

            wallRenderer.GetPropertyBlock(SharedBlock);
            // URP Lit 은 _BaseMap_ST, Built-in/legacy 호환을 위해 _MainTex_ST 둘 다 set.
            SharedBlock.SetVector(BaseMapStId, st);
            SharedBlock.SetVector(MainTexStId, st);
            wallRenderer.SetPropertyBlock(SharedBlock);
        }
    }
}
