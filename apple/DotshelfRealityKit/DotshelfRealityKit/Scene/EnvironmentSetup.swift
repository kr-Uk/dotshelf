import RealityKit

enum EnvironmentSetup {
    static func configureLighting(root: Entity) {
        // 메인 방향광 (은은한 배경 조명)
        let sunLight = DirectionalLight()
        sunLight.light.color = .init(red: 1.0, green: 0.96, blue: 0.9, alpha: 1.0)
        sunLight.light.intensity = 2000
        sunLight.shadow = DirectionalLightComponent.Shadow(
            maximumDistance: 10, depthBias: 0.5
        )
        sunLight.look(at: [0, 0, 0], from: [-2, 3, 2], relativeTo: nil)
        root.addChild(sunLight)

        // 책장 반대편(앞-위)에서 내리쬐는 강한 햇빛 — 따뜻한 오후 햇살 느낌
        let sunRay = DirectionalLight()
        sunRay.light.color = .init(red: 1.0, green: 0.90, blue: 0.75, alpha: 1.0)
        sunRay.light.intensity = 5500
        sunRay.shadow = DirectionalLightComponent.Shadow(
            maximumDistance: 10, depthBias: 0.8
        )
        // 오른쪽-위-앞에서 왼쪽-아래-뒤(책장 쪽)로 비추기
        sunRay.look(at: [-1.0, 0.3, -0.8], from: [2.5, 3.2, 2.0], relativeTo: nil)
        root.addChild(sunRay)

        let lamp1 = PointLight()
        lamp1.light.color = .init(red: 1.0, green: 0.894, blue: 0.71, alpha: 1.0)
        lamp1.light.intensity = 500
        lamp1.light.attenuationRadius = 5.0
        lamp1.position = [0.8, 2.2, 0.5]
        root.addChild(lamp1)

        let lamp2 = PointLight()
        lamp2.light.color = .init(red: 1.0, green: 0.894, blue: 0.71, alpha: 1.0)
        lamp2.light.intensity = 300
        lamp2.light.attenuationRadius = 4.0
        lamp2.position = [-0.6, 1.0, 0.8]
        root.addChild(lamp2)
    }

    /// 샘플 HDRI 환경맵 (brown_photostudio_04_4k.exr)
    /// - 간접광 + 반사를 한번에 처리하는 IBL(Image-Based Lighting)
    /// - Poly Haven에서 다운로드한 CC0 에셋
    static func loadEnvironmentResource() -> EnvironmentResource? {
        try? EnvironmentResource.load(named: "brown_photostudio_04_4k")
    }

}
