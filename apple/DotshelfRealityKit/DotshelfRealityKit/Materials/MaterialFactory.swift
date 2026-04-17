import RealityKit
// PlatformColor는 PlatformTypes.swift에서 정의 (PlatformColor / UIColor)

enum MaterialFactory {
    /// 나무 재질 (Wood049 PBR 텍스처 적용)
    /// - Color, Normal, Roughness 맵을 사용한 풀 PBR 렌더링
    static func woodMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()

        if let colorTexture = try? TextureResource.load(named: "Wood049_1K-JPG_Color"),
           let normalTexture = try? TextureResource.load(named: "Wood049_1K-JPG_NormalGL"),
           let roughnessTexture = try? TextureResource.load(named: "Wood049_1K-JPG_Roughness") {
            material.baseColor = .init(texture: .init(colorTexture))
            material.normal = .init(texture: .init(normalTexture))
            material.roughness = .init(texture: .init(roughnessTexture))
        } else {
            // 텍스처 로드 실패 시 단색 폴백
            material.baseColor = .init(tint: PlatformColor(red: 0.48, green: 0.35, blue: 0.20, alpha: 1.0))
            material.roughness = .init(floatLiteral: 0.75)
        }

        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    // TODO: 프로덕션에서는 프로시저럴 박스 대신 glTF/GLB 모델을 로드하여
    // 6면 UV 매핑된 텍스처를 적용할 예정.
    // 파이프라인: Object Capture → USDZ → glTF 변환 → CDN → 각 플랫폼 로드
    static func bookMaterial(color: PlatformColor) -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: color)
        material.roughness = .init(floatLiteral: 0.35)
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    /// 서재 방 바닥용 어두운 나무 재질 (Wood049 재사용 + UV 타일링)
    static func floorMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()

        if let color = try? TextureResource.load(named: "Wood049_1K-JPG_Color"),
           let normal = try? TextureResource.load(named: "Wood049_1K-JPG_NormalGL"),
           let rough = try? TextureResource.load(named: "Wood049_1K-JPG_Roughness") {
            material.baseColor = .init(tint: PlatformColor(red: 0.55, green: 0.42, blue: 0.28, alpha: 1.0),
                                       texture: .init(color))
            material.normal = .init(texture: .init(normal))
            material.roughness = .init(texture: .init(rough))
        } else {
            material.baseColor = .init(tint: PlatformColor(red: 0.38, green: 0.26, blue: 0.16, alpha: 1.0))
            material.roughness = .init(floatLiteral: 0.8)
        }

        // 큰 바닥에 텍스처가 늘어지지 않도록 4배 타일링
        material.textureCoordinateTransform = .init(
            offset: SIMD2<Float>(0, 0),
            scale: SIMD2<Float>(4, 4),
            rotation: 0
        )
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    /// 화이트보드 프레임 (짙은 알루미늄)
    static func whiteboardFrameMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(red: 0.18, green: 0.18, blue: 0.20, alpha: 1.0))
        material.roughness = .init(floatLiteral: 0.4)
        material.metallic = .init(floatLiteral: 0.6)
        return material
    }

    /// 화이트보드 표면 (살짝 광택 있는 흰색)
    static func whiteboardSurfaceMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(red: 0.97, green: 0.97, blue: 0.98, alpha: 1.0))
        material.roughness = .init(floatLiteral: 0.25)
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    /// 방 벽용 플라스터 재질 (Plaster001 PBR 텍스처)
    static func wallMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()

        if let color = try? TextureResource.load(named: "Plaster001_1K-JPG_Color"),
           let normal = try? TextureResource.load(named: "Plaster001_1K-JPG_NormalGL"),
           let rough = try? TextureResource.load(named: "Plaster001_1K-JPG_Roughness") {
            // 벽면이 너무 새하얀 느낌이 되지 않도록 살짝 warm-tint
            material.baseColor = .init(
                tint: PlatformColor(red: 0.92, green: 0.88, blue: 0.80, alpha: 1.0),
                texture: .init(color)
            )
            material.normal = .init(texture: .init(normal))
            material.roughness = .init(texture: .init(rough))
        } else {
            material.baseColor = .init(tint: PlatformColor(red: 0.88, green: 0.83, blue: 0.74, alpha: 1.0))
            material.roughness = .init(floatLiteral: 0.95)
        }

        // 큰 벽에 텍스처 늘어짐 방지 — 2배 타일링
        material.textureCoordinateTransform = .init(
            offset: SIMD2<Float>(0, 0),
            scale: SIMD2<Float>(2, 2),
            rotation: 0
        )
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    static func ghostMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(white: 0.7, alpha: 1.0))
        material.blending = .transparent(opacity: .init(floatLiteral: 0.25))
        material.roughness = .init(floatLiteral: 0.5)
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }
}
