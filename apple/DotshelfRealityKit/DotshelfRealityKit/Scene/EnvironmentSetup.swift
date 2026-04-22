import RealityKit

/// 씬의 광원 참조 묶음 — 페이즈 G의 조명 프리셋 실시간 적용용.
struct LightingHandles {
    let sunLight: DirectionalLight
    let sunRay: DirectionalLight
    let lamp1: PointLight
    let lamp2: PointLight

    /// 기본 intensity (프리셋의 intensityScale 곱연산 기준점).
    static let baseSunIntensity: Float = 3000
    static let baseSunRayIntensity: Float = 4000
    static let baseLamp1Intensity: Float = 500
    static let baseLamp2Intensity: Float = 300
}

enum EnvironmentSetup {
    @discardableResult
    static func configureLighting(root: Entity) -> LightingHandles {
        // 메인 방향광 (은은한 배경 조명 / fill)
        // shadow는 IBL ambient가 fill 역할을 겸하므로 생략 — GPU shadow pass 2장 → 1장.
        // 창 햇살의 sunRay 그림자가 씬의 핵심 연출이라 그쪽만 유지.
        // 그림자 진 영역이 너무 어두워지지 않도록 intensity 상향.
        let sunLight = DirectionalLight()
        sunLight.light.color = .init(red: 1.0, green: 0.96, blue: 0.9, alpha: 1.0)
        sunLight.light.intensity = LightingHandles.baseSunIntensity
        sunLight.look(at: [0, 0, 0], from: [-2, 3, 2], relativeTo: nil)
        root.addChild(sunLight)

        // 우측 창문을 통과해 책장을 비추는 햇살. 야외 HDRI가 IBL을 채워주므로 강도 다소 낮춤.
        // shadow maximumDistance는 4.5m — 책장 shadow는 유지, 창 프레임의 과한 줄무늬 그림자는 cull.
        let sunRay = DirectionalLight()
        sunRay.light.color = .init(red: 1.0, green: 0.92, blue: 0.78, alpha: 1.0)
        sunRay.light.intensity = LightingHandles.baseSunRayIntensity
        sunRay.shadow = DirectionalLightComponent.Shadow(
            maximumDistance: 4.5, depthBias: 0.8
        )
        sunRay.look(at: [0, 0.8, -0.7], from: [3.8, 1.95, 0.6], relativeTo: nil)
        root.addChild(sunRay)

        let lamp1 = PointLight()
        lamp1.light.color = .init(red: 1.0, green: 0.894, blue: 0.71, alpha: 1.0)
        lamp1.light.intensity = LightingHandles.baseLamp1Intensity
        lamp1.light.attenuationRadius = 5.0
        lamp1.position = [0.8, 2.2, 0.5]
        root.addChild(lamp1)

        let lamp2 = PointLight()
        lamp2.light.color = .init(red: 1.0, green: 0.894, blue: 0.71, alpha: 1.0)
        lamp2.light.intensity = LightingHandles.baseLamp2Intensity
        lamp2.light.attenuationRadius = 4.0
        lamp2.position = [-0.6, 1.0, 0.8]
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
