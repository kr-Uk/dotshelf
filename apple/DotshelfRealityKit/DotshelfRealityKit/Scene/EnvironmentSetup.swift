import RealityKit

/// 씬의 광원 참조 묶음 — 페이즈 G의 조명 프리셋 실시간 적용용.
/// Unity 프로젝트 `EnvironmentSetup.cs` 값에 맞춰 튜닝:
/// - `sunLight`  = Unity WarmDirectional  (주광 · 따뜻한 톤 · shadow)
/// - `sunRay`    = Unity AccentDirectional (쿨 필 · shadow 없음)
/// - `lamp1`     = Unity WarmPointRight   (우측 테이블 램프)
/// - `lamp2`     = Unity WarmPointLeft    (좌측 테이블 램프)
struct LightingHandles {
    let sunLight: DirectionalLight
    let sunRay: DirectionalLight
    let lamp1: PointLight
    let lamp2: PointLight

    /// 기본 intensity — Unity URP intensity를 RealityKit Lux/Lumen으로 경험치 매핑.
    /// (Unity 1.0 directional ≈ 5000 Lux, Unity 10 point ≈ 2000 Lumen 기준)
    static let baseSunIntensity: Float = 5500       // Unity WarmDirectional intensity 1.1 → ~5500 Lux
    static let baseSunRayIntensity: Float = 2200    // Unity AccentDirectional 0.45 → ~2200 Lux
    static let baseLamp1Intensity: Float = 2000     // Unity WarmPointRight intensity 10 → ~2000 Lumen
    static let baseLamp2Intensity: Float = 2000     // Unity WarmPointLeft  intensity 10 → ~2000 Lumen
}

enum EnvironmentSetup {
    @discardableResult
    static func configureLighting(root: Entity) -> LightingHandles {
        // MARK: WarmDirectional (주광)
        // Unity euler(40°, -25°, 0°) — pitch 40° yaw -25° (서쪽-위에서 아래로).
        // RealityKit RH 좌표계로 변환해 `look(at:from:)` 으로 같은 입사각 재현.
        // 파생 from 좌표: forward = (-sin25°·cos40°, -sin40°, cos25°·cos40°) [Unity LH]
        //              → RK RH에서 z 반전한 반대 벡터 = (+sin25°·cos40°, +sin40°, +cos25°·cos40°)·dist
        //              ≈ (0.324, 0.643, 0.694) · 4m = (1.3, 2.6, 2.8)
        let sunLight = DirectionalLight()
        sunLight.light.color = .init(red: 1.0, green: 0.88, blue: 0.74, alpha: 1.0)
        sunLight.light.intensity = LightingHandles.baseSunIntensity
        // 책장·책상 위로 부드러운 그림자 드리우게 shadow on
        sunLight.shadow = DirectionalLightComponent.Shadow(
            maximumDistance: 5.0, depthBias: 0.8
        )
        sunLight.look(at: [0, 0.8, 0], from: [1.3, 2.6, 2.8], relativeTo: nil)
        root.addChild(sunLight)

        // MARK: AccentDirectional (쿨 필)
        // Unity euler(18°, 145°, 0°) — pitch 18° yaw 145° (뒤쪽-위에서 앞쪽 아래로).
        // RK 좌표: (+sin145°·cos18°, +sin18°, -cos145°·cos18°) · 4m ≈ (2.2, 1.2, 3.1).
        // shadow 없음 — fill 용도, 그림자는 WarmDirectional이 담당.
        let sunRay = DirectionalLight()
        sunRay.light.color = .init(red: 0.86, green: 0.90, blue: 1.0, alpha: 1.0)
        sunRay.light.intensity = LightingHandles.baseSunRayIntensity
        sunRay.look(at: [0, 0.5, 0], from: [-2.2, 1.2, -3.1], relativeTo: nil)
        root.addChild(sunRay)

        // MARK: WarmPointRight (우측 램프)
        // Unity position (1.42, 2.0, -0.3) — 책장 앞 우측 2m 높이 램프.
        // RK z 반전: z=-0.3 → +0.3 (책장 앞쪽).
        // range 3.8 → attenuationRadius 3.8.
        let lamp1 = PointLight()
        lamp1.light.color = .init(red: 1.0, green: 0.81, blue: 0.66, alpha: 1.0)
        lamp1.light.intensity = LightingHandles.baseLamp1Intensity
        lamp1.light.attenuationRadius = 3.8
        lamp1.position = [0.9, 2.0, 0.3]
        root.addChild(lamp1)

        // MARK: WarmPointLeft (좌측 램프)
        // Unity position (-0.39, 2.0, -0.3), range 4.5.
        let lamp2 = PointLight()
        lamp2.light.color = .init(red: 1.0, green: 0.76, blue: 0.58, alpha: 1.0)
        lamp2.light.intensity = LightingHandles.baseLamp2Intensity
        lamp2.light.attenuationRadius = 4.5
        lamp2.position = [-0.9, 2.0, 0.3]
        root.addChild(lamp2)

        return LightingHandles(sunLight: sunLight, sunRay: sunRay, lamp1: lamp1, lamp2: lamp2)
    }

    /// HDRI 환경맵 (kloofendal_48d_partly_cloudy_puresky_1k.exr)
    /// - 야외 하늘 IBL: 간접광 + 반사를 한번에 처리 (Poly Haven CC0)
    /// - 통창 씬과 매칭 — 내부 간접광 톤이 자연스러운 야외 분위기로
    static func loadEnvironmentResource() -> EnvironmentResource? {
        try? EnvironmentResource.load(named: "kloofendal_48d_partly_cloudy_puresky_1k")
    }

}
