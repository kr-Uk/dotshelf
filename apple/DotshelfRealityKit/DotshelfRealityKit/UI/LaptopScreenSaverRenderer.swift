import Foundation
import RealityKit
import SceneKit
import Metal
import QuartzCore
import SwiftUI

#if os(macOS)
import AppKit
typealias PlatformImage = NSImage
#else
import UIKit
typealias PlatformImage = UIImage
#endif

/// 노트북 디스플레이 전용 60fps 화면 보호기 렌더러.
/// - `SCNScene`(3D 책 spinning + 조명) → `SCNRenderer`로 offscreen 렌더
/// - 출력 frame을 `LowLevelTexture`의 GPU 텍스처에 직접 쓰고 `TextureResource`로 래핑
/// - `Timer` 16.67ms 틱으로 render loop 구동, `pause()`로 노트북 포커싱 중 절전
///
/// 생성 후 `start(on:)`으로 laptop_screen_glow `ModelEntity`에 texture를 장착한다.
@MainActor
final class LaptopScreenSaverRenderer {

    // 출력 해상도 — laptop_screen_glow plane 비율(1.6:1)과 일치
    private let outputWidth: Int = 1024
    private let outputHeight: Int = 640

    private let device: MTLDevice
    private let commandQueue: MTLCommandQueue
    private let lowLevelTexture: LowLevelTexture
    let textureResource: TextureResource

    private let scnRenderer: SCNRenderer
    private let scene: SCNScene
    private let brandTextGeometry: SCNText
    private let clockTextGeometry: SCNText

    private var timer: Timer?
    private var isPaused: Bool = false
    private let startTime: CFTimeInterval = CACurrentMediaTime()
    private var lastClockSecond: Int = -1

    // MARK: - Init

    init?() {
        guard let device = MTLCreateSystemDefaultDevice(),
              let cq = device.makeCommandQueue() else {
            print("⚠️ Metal device/queue 생성 실패 — 화면 보호기 비활성")
            return nil
        }
        self.device = device
        self.commandQueue = cq

        // LowLevelTexture — render target 용.
        let descriptor = LowLevelTexture.Descriptor(
            pixelFormat: .rgba8Unorm,
            width: outputWidth, height: outputHeight,
            depth: 1,
            mipmapLevelCount: 1,
            textureUsage: [.renderTarget, .shaderRead]
        )
        do {
            self.lowLevelTexture = try LowLevelTexture(descriptor: descriptor)
            self.textureResource = try TextureResource(from: lowLevelTexture)
        } catch {
            print("⚠️ LowLevelTexture 생성 실패: \(error)")
            return nil
        }

        let scene = SCNScene()
        self.scene = scene
        self.scnRenderer = SCNRenderer(device: device, options: nil)
        self.scnRenderer.scene = scene
        self.scnRenderer.autoenablesDefaultLighting = false

        // SCNText geometries — 매 프레임 string 갱신할 수 있게 참조 보관
        self.brandTextGeometry = SCNText(string: "dotshelf", extrusionDepth: 0.02)
        self.clockTextGeometry = SCNText(string: "--:--:--", extrusionDepth: 0.01)

        buildScene()
    }

    // MARK: - Public API

    func start(on screen: ModelEntity) {
        attachMaterial(to: screen)
        startRenderLoop()
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    func pause() { isPaused = true }
    func resume() { isPaused = false }

    // MARK: - Scene building

    private func buildScene() {
        let root = scene.rootNode

        // 배경: 따뜻한 크림 그라데이션 이미지
        if let bgImage = Self.makeBackgroundImage(size: CGSize(width: outputWidth, height: outputHeight)) {
            scene.background.contents = bgImage
        } else {
            scene.background.contents = PlatformColor(red: 0.97, green: 0.89, blue: 0.78, alpha: 1)
        }

        // 카메라
        let cameraNode = SCNNode()
        let camera = SCNCamera()
        camera.fieldOfView = 38
        camera.zNear = 0.1
        camera.zFar = 50
        cameraNode.camera = camera
        cameraNode.position = SCNVector3(0, 0.15, 3.4)
        cameraNode.look(at: SCNVector3(0, 0, 0))
        root.addChildNode(cameraNode)

        // 조명
        addAmbientLight(to: root)
        addDirectional(to: root, color: .white, intensity: 1100,
                       position: SCNVector3(2, 3, 2), target: SCNVector3(0, 0, 0),
                       castsShadow: true)
        addDirectional(to: root,
                       color: PlatformColor(red: 0.95, green: 0.75, blue: 0.62, alpha: 1),
                       intensity: 500,
                       position: SCNVector3(-2.5, 1.2, 1.5), target: SCNVector3(0, 0, 0))
        addDirectional(to: root,
                       color: PlatformColor(red: 0.80, green: 0.78, blue: 0.95, alpha: 1),
                       intensity: 400,
                       position: SCNVector3(0, 2, -3), target: SCNVector3(0, 0, 0))

        // 기울임 부모 (-25° X) — spin 축이 기울어진 Y 되도록
        let tilt = SCNNode()
        tilt.eulerAngles = SCNVector3(-Float.pi * 25 / 180, 0, 0)
        tilt.position = SCNVector3(0, 0.25, 0)
        root.addChildNode(tilt)

        // 책
        let book = makeBookNode()
        tilt.addChildNode(book)

        // 연속 회전 (10초/회)
        let spin = SCNAction.repeatForever(
            SCNAction.rotate(by: .pi * 2, around: SCNVector3(0, 1, 0), duration: 10)
        )
        book.runAction(spin)

        // 하단 브랜드/시계 — SCNText geometry (background에 씬 렌더될 때 같이 그려짐)
        setupBottomText(parent: root)
    }

    private func addAmbientLight(to parent: SCNNode) {
        let node = SCNNode()
        let light = SCNLight()
        light.type = .ambient
        light.color = PlatformColor(red: 1.00, green: 0.92, blue: 0.82, alpha: 1)
        light.intensity = 500
        node.light = light
        parent.addChildNode(node)
    }

    private func addDirectional(
        to parent: SCNNode,
        color: PlatformColor,
        intensity: CGFloat,
        position: SCNVector3,
        target: SCNVector3,
        castsShadow: Bool = false
    ) {
        let node = SCNNode()
        let light = SCNLight()
        light.type = .directional
        light.color = color
        light.intensity = intensity
        light.castsShadow = castsShadow
        if castsShadow {
            light.shadowMode = .deferred
            light.shadowRadius = 3
            light.shadowSampleCount = 4
        }
        node.light = light
        node.position = position
        node.look(at: target)
        parent.addChildNode(node)
    }

    // MARK: - Book geometry

    private func makeBookNode() -> SCNNode {
        let w: CGFloat = 1.0
        let h: CGFloat = 1.46
        let d: CGFloat = 0.22

        let box = SCNBox(width: w, height: h, length: d, chamferRadius: 0.012)

        let cover = makeCoverMaterial()
        let spine = makeSpineMaterial()
        let pages = makePagesMaterial()

        box.materials = [
            cover,   // 0 front — 앞 표지
            cover,   // 1 back — 뒤 표지
            pages,   // 2 top — 페이지 단면
            pages,   // 3 bottom — 페이지 단면
            spine,   // 4 left — 책등
            pages,   // 5 right — 페이지 단면 (펼치는 쪽)
        ]
        return SCNNode(geometry: box)
    }

    private func makeCoverMaterial() -> SCNMaterial {
        let m = SCNMaterial()
        m.lightingModel = .physicallyBased
        m.diffuse.contents = Self.makeCoverImage(size: CGSize(width: 600, height: 880))
        m.roughness.contents = 0.45
        m.metalness.contents = 0.05
        return m
    }

    private func makeSpineMaterial() -> SCNMaterial {
        let m = SCNMaterial()
        m.lightingModel = .physicallyBased
        m.diffuse.contents = Self.makeSpineImage(size: CGSize(width: 180, height: 880))
        m.roughness.contents = 0.50
        m.metalness.contents = 0.05
        return m
    }

    private func makePagesMaterial() -> SCNMaterial {
        let m = SCNMaterial()
        m.lightingModel = .physicallyBased
        m.diffuse.contents = PlatformColor(red: 0.96, green: 0.92, blue: 0.84, alpha: 1)
        m.roughness.contents = 0.80
        return m
    }

    // MARK: - Bottom text (SCNText — always faces camera via billboard)

    private func setupBottomText(parent: SCNNode) {
        #if os(macOS)
        let brandFont = NSFont(name: "Georgia-Bold", size: 0.45)
            ?? NSFont.systemFont(ofSize: 0.45, weight: .black)
        let clockFont = NSFont(name: "Menlo", size: 0.13) ?? NSFont.systemFont(ofSize: 0.13)
        #else
        let brandFont = UIFont(name: "Georgia-Bold", size: 0.45)
            ?? UIFont.systemFont(ofSize: 0.45, weight: .black)
        let clockFont = UIFont(name: "Menlo", size: 0.13) ?? UIFont.systemFont(ofSize: 0.13)
        #endif

        // Brand
        brandTextGeometry.font = brandFont
        brandTextGeometry.flatness = 0.05
        let brandMat = SCNMaterial()
        brandMat.diffuse.contents = PlatformColor(red: 0.28, green: 0.20, blue: 0.18, alpha: 1)
        brandMat.lightingModel = .constant
        brandTextGeometry.materials = [brandMat]
        let brandNode = SCNNode(geometry: brandTextGeometry)
        let brandBounds = brandTextGeometry.boundingBox
        let brandWidth = CGFloat(brandBounds.max.x - brandBounds.min.x)
        brandNode.position = SCNVector3(-Float(brandWidth) / 2, -1.35, 0)
        parent.addChildNode(brandNode)

        // Clock
        clockTextGeometry.font = clockFont
        clockTextGeometry.flatness = 0.05
        let clockMat = SCNMaterial()
        clockMat.diffuse.contents = PlatformColor(red: 0.42, green: 0.32, blue: 0.28, alpha: 0.85)
        clockMat.lightingModel = .constant
        clockTextGeometry.materials = [clockMat]
        let clockNode = SCNNode(geometry: clockTextGeometry)
        clockNode.position = SCNVector3(-0.3, -1.6, 0)
        clockNode.name = "clockNode"
        parent.addChildNode(clockNode)
    }

    private func updateClockIfNeeded() {
        let now = Date()
        let second = Int(now.timeIntervalSince1970)
        guard second != lastClockSecond else { return }
        lastClockSecond = second

        let f = DateFormatter()
        f.dateFormat = "HH:mm:ss"
        let text = f.string(from: now)
        clockTextGeometry.string = text

        // 다시 중앙 정렬 (geometry bounds 변경)
        if let node = scene.rootNode.childNode(withName: "clockNode", recursively: true) {
            let b = clockTextGeometry.boundingBox
            let w = CGFloat(b.max.x - b.min.x)
            node.position = SCNVector3(-Float(w) / 2, -1.6, 0)
        }
    }

    // MARK: - Render loop

    private func attachMaterial(to screen: ModelEntity) {
        var material = UnlitMaterial()
        material.color = .init(tint: .white, texture: .init(textureResource))
        material.blending = .opaque
        screen.model?.materials = [material]
    }

    private func startRenderLoop() {
        timer?.invalidate()
        // 60 Hz 근사 (macOS/iOS 공통 — CADisplayLink 없이 간편하게)
        let t = Timer.scheduledTimer(withTimeInterval: 1.0 / 60.0, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.onTick()
            }
        }
        // scroll/drag 중에도 실행되도록 common mode 등록
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    private func onTick() {
        guard !isPaused else { return }
        updateClockIfNeeded()
        renderOneFrame()
    }

    private func renderOneFrame() {
        let now = CACurrentMediaTime() - startTime

        guard let commandBuffer = commandQueue.makeCommandBuffer() else { return }

        // LowLevelTexture의 GPU texture를 render target으로 확보
        let mtlTexture = lowLevelTexture.replace(using: commandBuffer)

        let passDescriptor = MTLRenderPassDescriptor()
        passDescriptor.colorAttachments[0].texture = mtlTexture
        passDescriptor.colorAttachments[0].loadAction = .clear
        passDescriptor.colorAttachments[0].storeAction = .store
        passDescriptor.colorAttachments[0].clearColor = MTLClearColor(
            red: 0.98, green: 0.90, blue: 0.80, alpha: 1.0
        )

        let viewport = CGRect(x: 0, y: 0, width: outputWidth, height: outputHeight)
        scnRenderer.render(atTime: now,
                           viewport: viewport,
                           commandBuffer: commandBuffer,
                           passDescriptor: passDescriptor)

        commandBuffer.commit()
    }

    // MARK: - Baked images (한 번만 생성)

    private static func makeBackgroundImage(size: CGSize) -> PlatformImage? {
        let view = LinearGradient(
            colors: [
                Color(red: 0.99, green: 0.95, blue: 0.88),
                Color(red: 0.97, green: 0.89, blue: 0.78),
                Color(red: 1.00, green: 0.82, blue: 0.72),
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
        .frame(width: size.width, height: size.height)

        let renderer = ImageRenderer(content: view)
        renderer.scale = 1.0
        renderer.isOpaque = true
        #if os(macOS)
        return renderer.nsImage
        #else
        return renderer.uiImage
        #endif
    }

    private static func makeCoverImage(size: CGSize) -> PlatformImage? {
        let view = ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.82, green: 0.42, blue: 0.30),
                    Color(red: 0.58, green: 0.24, blue: 0.16),
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(.white.opacity(0.22), lineWidth: 3)
                .padding(24)
            VStack(spacing: 10) {
                Rectangle()
                    .fill(Color(red: 0.96, green: 0.82, blue: 0.45))
                    .frame(height: 3)
                    .padding(.horizontal, 60)
                    .padding(.top, 80)
                Spacer(minLength: 0)
                Text("D")
                    .font(.system(size: 340, weight: .black, design: .serif))
                    .foregroundStyle(.white.opacity(0.92))
                Spacer(minLength: 0)
                Text("dotshelf")
                    .font(.system(size: 40, weight: .bold, design: .serif))
                    .foregroundStyle(.white.opacity(0.82))
                Rectangle()
                    .fill(Color(red: 0.96, green: 0.82, blue: 0.45))
                    .frame(height: 3)
                    .padding(.horizontal, 60)
                    .padding(.bottom, 80)
            }
        }
        .frame(width: size.width, height: size.height)

        let renderer = ImageRenderer(content: view)
        renderer.scale = 1.0
        renderer.isOpaque = true
        #if os(macOS)
        return renderer.nsImage
        #else
        return renderer.uiImage
        #endif
    }

    private static func makeSpineImage(size: CGSize) -> PlatformImage? {
        let view = ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.68, green: 0.32, blue: 0.22),
                    Color(red: 0.48, green: 0.18, blue: 0.12),
                ],
                startPoint: .top, endPoint: .bottom
            )
            Text("dotshelf")
                .font(.system(size: 24, weight: .semibold, design: .serif))
                .foregroundStyle(.white.opacity(0.80))
                .rotationEffect(.degrees(-90))
                .fixedSize()
        }
        .frame(width: size.width, height: size.height)

        let renderer = ImageRenderer(content: view)
        renderer.scale = 1.0
        renderer.isOpaque = true
        #if os(macOS)
        return renderer.nsImage
        #else
        return renderer.uiImage
        #endif
    }
}
