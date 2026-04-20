import RealityKit
// PlatformColor는 PlatformTypes.swift에서 정의 (PlatformColor / UIColor)

enum MaterialFactory {
    /// 나무 재질 (Wood049 PBR 텍스처 적용)
    /// - Color, Normal, Roughness 맵을 사용한 풀 PBR 렌더링
    /// 책장 3개가 반복 호출 → static 캐시로 텍스처 재로드 방지.
    static func woodMaterial() -> PhysicallyBasedMaterial { sharedWoodMaterial }

    private static let sharedWoodMaterial: PhysicallyBasedMaterial = {
        var material = PhysicallyBasedMaterial()

        if let colorTexture = try? TextureResource.load(named: "Wood049_1K-JPG_Color"),
           let normalTexture = try? TextureResource.load(named: "Wood049_1K-JPG_NormalGL"),
           let roughnessTexture = try? TextureResource.load(named: "Wood049_1K-JPG_Roughness") {
            material.baseColor = .init(texture: .init(colorTexture))
            material.normal = .init(texture: .init(normalTexture))
            material.roughness = .init(texture: .init(roughnessTexture))
        } else {
            material.baseColor = .init(tint: PlatformColor(red: 0.48, green: 0.35, blue: 0.20, alpha: 1.0))
            material.roughness = .init(floatLiteral: 0.75)
        }

        material.metallic = .init(floatLiteral: 0.0)
        return material
    }()

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
    /// floor + thresholdStep 2곳에서 호출 → static 캐시.
    static func floorMaterial() -> PhysicallyBasedMaterial { sharedFloorMaterial }

    private static let sharedFloorMaterial: PhysicallyBasedMaterial = {
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
    }()

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
    /// 여러 벽 엔티티에서 동일 material을 반복 호출하므로 static 캐시로 단일 인스턴스 유지.
    static func wallMaterial() -> PhysicallyBasedMaterial { sharedWallMaterial }

    private static let sharedWallMaterial: PhysicallyBasedMaterial = {
        var material = PhysicallyBasedMaterial()

        if let color = try? TextureResource.load(named: "Plaster001_1K-JPG_Color"),
           let normal = try? TextureResource.load(named: "Plaster001_1K-JPG_NormalGL"),
           let rough = try? TextureResource.load(named: "Plaster001_1K-JPG_Roughness") {
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

        material.textureCoordinateTransform = .init(
            offset: SIMD2<Float>(0, 0),
            scale: SIMD2<Float>(2, 2),
            rotation: 0
        )
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }()

    /// 창문 밖 풀밭 (Grass001 PBR 텍스처)
    static func grassMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        if let color = try? TextureResource.load(named: "Grass001_1K-JPG_Color"),
           let normal = try? TextureResource.load(named: "Grass001_1K-JPG_NormalGL"),
           let rough = try? TextureResource.load(named: "Grass001_1K-JPG_Roughness") {
            material.baseColor = .init(texture: .init(color))
            material.normal = .init(texture: .init(normal))
            material.roughness = .init(texture: .init(rough))
        } else {
            material.baseColor = .init(tint: PlatformColor(red: 0.35, green: 0.55, blue: 0.22, alpha: 1.0))
            material.roughness = .init(floatLiteral: 0.9)
        }
        // 풀밭 타일링 (넓은 면적)
        material.textureCoordinateTransform = .init(
            offset: .zero,
            scale: SIMD2<Float>(6, 6),
            rotation: 0
        )
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    /// 원거리 하늘 배경 (unlit — 조명 영향 없이 일정 밝기)
    static func skyMaterial() -> UnlitMaterial {
        return UnlitMaterial(color: PlatformColor(red: 0.64, green: 0.80, blue: 0.94, alpha: 1.0))
    }

    /// 창문 프레임: 중간톤 원목. 뒷면(실내쪽)이 완전히 어두워지지 않도록 약한 emissive를 실어
    /// 햇빛이 바깥→안쪽으로 들어오는 장면에서도 실내쪽 프레임이 너무 짙어 보이지 않게 한다.
    static func windowFrameMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        let tint = PlatformColor(red: 0.42, green: 0.30, blue: 0.22, alpha: 1.0)
        material.baseColor = .init(tint: tint)
        material.emissiveColor = .init(color: tint)
        material.emissiveIntensity = 0.25
        material.roughness = .init(floatLiteral: 0.6)
        material.metallic = .init(floatLiteral: 0.0)
        return material
    }

    /// 창문 프레임(Unlit 버전). 역광 상황에서 실내쪽 면이 어두워지는 문제를 회피 —
    /// 조명 반응을 무시하고 항상 baseColor로 렌더. 얇은 프로파일이라 음영 손실 티 적음.
    static func windowFrameUnlitMaterial() -> UnlitMaterial {
        return UnlitMaterial(color: PlatformColor(red: 0.40, green: 0.30, blue: 0.22, alpha: 1.0))
    }

    /// 창문 유리. clearcoat + specular 높여 IBL 반사 하이라이트 또렷하게.
    /// 과한 블루 tint 대신 약한 청녹 중립 톤. opacity 약간 올려 유리 면 존재감 유지.
    static func windowGlassMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(red: 0.88, green: 0.94, blue: 0.92, alpha: 1.0))
        material.blending = .transparent(opacity: .init(floatLiteral: 0.22))
        material.roughness = .init(floatLiteral: 0.02)
        material.metallic = .init(floatLiteral: 0.0)
        material.specular = .init(floatLiteral: 1.0)
        material.clearcoat = .init(floatLiteral: 1.0)
        material.clearcoatRoughness = .init(floatLiteral: 0.03)
        return material
    }

    /// 새(실루엣용 짙은 재질)
    static func birdMaterial() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(red: 0.12, green: 0.10, blue: 0.10, alpha: 1.0))
        material.roughness = .init(floatLiteral: 0.8)
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
