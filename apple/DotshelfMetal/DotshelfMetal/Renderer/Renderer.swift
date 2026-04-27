import MetalKit
import simd

struct Uniforms {
    var modelMatrix: float4x4
    var viewMatrix: float4x4
    var projectionMatrix: float4x4
    var normalMatrix: float4x4
    var cameraPositionX: Float
    var cameraPositionY: Float
    var cameraPositionZ: Float
    var _pad0: Float = 0
}

struct MaterialUniformsSwift {
    var albedoR: Float
    var albedoG: Float
    var albedoB: Float
    var metallic: Float
    var roughness: Float
    var hasAlbedoMap: Int32
    var hasNormalMap: Int32
    var hasRoughnessMap: Int32
    var alpha: Float = 1.0
    var _pad0: Float = 0
    var _pad1: Float = 0
    var _pad2: Float = 0
}

struct ShadowUniformsSwift {
    var lightViewProjectionMatrix: float4x4
    var modelMatrix: float4x4
}

struct ShadowDataSwift {
    var lightViewProjectionMatrix: float4x4
}

final class Renderer: NSObject, MTKViewDelegate {
    let device: MTLDevice
    let commandQueue: MTLCommandQueue
    private var pipelineState: MTLRenderPipelineState!
    private var shadowPipelineState: MTLRenderPipelineState!
    private var depthStencilState: MTLDepthStencilState!

    private var sceneRoot: SceneNode!
    let camera = Camera()
    let inputHandler: InputHandler
    private var lightData = LightDataSwift.defaultLighting()

    private var shadowMapTexture: MTLTexture!
    private var shadowRenderPassDescriptor: MTLRenderPassDescriptor!
    private var lightViewProjectionMatrix = float4x4.identity

    private let shadowMapSize = 2048

    init?(mtkView: MTKView, inputHandler: InputHandler) {
        guard let device = mtkView.device ?? MTLCreateSystemDefaultDevice(),
              let commandQueue = device.makeCommandQueue() else {
            return nil
        }
        self.device = device
        self.commandQueue = commandQueue
        self.inputHandler = inputHandler
        mtkView.device = device

        super.init()

        mtkView.colorPixelFormat = .bgra8Unorm
        mtkView.depthStencilPixelFormat = .depth32Float
        mtkView.clearColor = MTLClearColor(red: 0.961, green: 0.941, blue: 0.910, alpha: 1.0)

        buildPipelineState(mtkView: mtkView)
        buildShadowPipelineState()
        buildDepthStencilState()
        buildShadowMap()
        buildLightMatrices()
        inputHandler.camera = camera
        let scene = SlotSystem.buildScene(device: device)
        sceneRoot = scene.root
        inputHandler.sceneRoot = sceneRoot
        inputHandler.bookshelfState = scene.state
    }

    private func buildVertexDescriptor() -> MTLVertexDescriptor {
        let vd = MTLVertexDescriptor()
        vd.attributes[0].format = .float3
        vd.attributes[0].offset = 0
        vd.attributes[0].bufferIndex = 0
        vd.attributes[1].format = .float3
        vd.attributes[1].offset = MemoryLayout<Vertex>.offset(of: \Vertex.normal)!
        vd.attributes[1].bufferIndex = 0
        vd.attributes[2].format = .float2
        vd.attributes[2].offset = MemoryLayout<Vertex>.offset(of: \Vertex.uv)!
        vd.attributes[2].bufferIndex = 0
        vd.attributes[3].format = .float3
        vd.attributes[3].offset = MemoryLayout<Vertex>.offset(of: \Vertex.tangent)!
        vd.attributes[3].bufferIndex = 0
        vd.layouts[0].stride = MemoryLayout<Vertex>.stride
        return vd
    }

    private func buildPipelineState(mtkView: MTKView) {
        let library = device.makeDefaultLibrary()!
        let desc = MTLRenderPipelineDescriptor()
        desc.vertexFunction = library.makeFunction(name: "vertex_main")
        desc.fragmentFunction = library.makeFunction(name: "fragment_main")
        desc.vertexDescriptor = buildVertexDescriptor()
        desc.colorAttachments[0].pixelFormat = mtkView.colorPixelFormat
        desc.colorAttachments[0].isBlendingEnabled = true
        desc.colorAttachments[0].rgbBlendOperation = .add
        desc.colorAttachments[0].alphaBlendOperation = .add
        desc.colorAttachments[0].sourceRGBBlendFactor = .sourceAlpha
        desc.colorAttachments[0].sourceAlphaBlendFactor = .one
        desc.colorAttachments[0].destinationRGBBlendFactor = .oneMinusSourceAlpha
        desc.colorAttachments[0].destinationAlphaBlendFactor = .oneMinusSourceAlpha
        desc.depthAttachmentPixelFormat = mtkView.depthStencilPixelFormat
        pipelineState = try! device.makeRenderPipelineState(descriptor: desc)
    }

    private func buildShadowPipelineState() {
        let library = device.makeDefaultLibrary()!
        let desc = MTLRenderPipelineDescriptor()
        desc.vertexFunction = library.makeFunction(name: "shadow_vertex")
        desc.fragmentFunction = nil
        desc.vertexDescriptor = buildVertexDescriptor()
        desc.colorAttachments[0].pixelFormat = .invalid
        desc.depthAttachmentPixelFormat = .depth32Float
        shadowPipelineState = try! device.makeRenderPipelineState(descriptor: desc)
    }

    private func buildDepthStencilState() {
        let desc = MTLDepthStencilDescriptor()
        desc.depthCompareFunction = .less
        desc.isDepthWriteEnabled = true
        depthStencilState = device.makeDepthStencilState(descriptor: desc)
    }

    private func buildShadowMap() {
        let desc = MTLTextureDescriptor.texture2DDescriptor(
            pixelFormat: .depth32Float, width: shadowMapSize, height: shadowMapSize, mipmapped: false)
        desc.usage = [.renderTarget, .shaderRead]
        desc.storageMode = .private
        shadowMapTexture = device.makeTexture(descriptor: desc)

        shadowRenderPassDescriptor = MTLRenderPassDescriptor()
        shadowRenderPassDescriptor.depthAttachment.texture = shadowMapTexture
        shadowRenderPassDescriptor.depthAttachment.loadAction = .clear
        shadowRenderPassDescriptor.depthAttachment.storeAction = .store
        shadowRenderPassDescriptor.depthAttachment.clearDepth = 1.0
    }

    private func buildLightMatrices() {
        let lightDir = normalize(SIMD3<Float>(-0.3, -0.6, -1.0))
        let lightPos = -lightDir * 5.0 + SIMD3<Float>(0, 1, 0)
        let lightView = float4x4.lookAt(eye: lightPos, center: SIMD3<Float>(0, 1, 0), up: SIMD3<Float>(0, 1, 0))
        let size: Float = 2.0
        let lightProjection = float4x4.orthographic(left: -size, right: size, bottom: -size, top: size, near: 0.1, far: 20.0)
        lightViewProjectionMatrix = lightProjection * lightView
    }

    // MARK: - MTKViewDelegate

    func mtkView(_ view: MTKView, drawableSizeWillChange size: CGSize) {
        camera.aspect = Float(size.width / size.height)
    }

    func draw(in view: MTKView) {
        guard let drawable = view.currentDrawable,
              let renderPassDescriptor = view.currentRenderPassDescriptor,
              let commandBuffer = commandQueue.makeCommandBuffer() else { return }

        camera.update()
        sceneRoot.updateAnimations()
        sceneRoot.updateWorldTransforms()

        let renderableNodes = sceneRoot.allRenderableNodes()

        // Shadow pass
        if let shadowEncoder = commandBuffer.makeRenderCommandEncoder(descriptor: shadowRenderPassDescriptor) {
            shadowEncoder.setRenderPipelineState(shadowPipelineState)
            shadowEncoder.setDepthStencilState(depthStencilState)
            shadowEncoder.setCullMode(.front)

            for node in renderableNodes {
                guard let meshRef = node.mesh else { continue }
                var shadowUniforms = ShadowUniformsSwift(
                    lightViewProjectionMatrix: lightViewProjectionMatrix,
                    modelMatrix: node.worldTransform
                )
                shadowEncoder.setVertexBuffer(meshRef.vertexBuffer, offset: 0, index: 0)
                shadowEncoder.setVertexBytes(&shadowUniforms, length: MemoryLayout<ShadowUniformsSwift>.stride, index: 1)
                shadowEncoder.drawIndexedPrimitives(type: .triangle, indexCount: meshRef.indexCount,
                    indexType: .uint16, indexBuffer: meshRef.indexBuffer, indexBufferOffset: 0)
            }
            shadowEncoder.endEncoding()
        }

        // Main pass
        guard let renderEncoder = commandBuffer.makeRenderCommandEncoder(descriptor: renderPassDescriptor) else { return }

        renderEncoder.setRenderPipelineState(pipelineState)
        renderEncoder.setDepthStencilState(depthStencilState)
        renderEncoder.setFragmentTexture(shadowMapTexture, index: 0)

        let viewMatrix = camera.viewMatrix
        let projectionMatrix = camera.projectionMatrix
        var shadowData = ShadowDataSwift(lightViewProjectionMatrix: lightViewProjectionMatrix)

        for node in renderableNodes {
            guard let meshRef = node.mesh, let mat = node.material else { continue }

            let modelMatrix = node.worldTransform
            let normalMatrix = modelMatrix.inverse.transpose

            var uniforms = Uniforms(
                modelMatrix: modelMatrix, viewMatrix: viewMatrix,
                projectionMatrix: projectionMatrix, normalMatrix: normalMatrix,
                cameraPositionX: camera.eye.x, cameraPositionY: camera.eye.y, cameraPositionZ: camera.eye.z
            )
            var material = mat

            renderEncoder.setVertexBuffer(meshRef.vertexBuffer, offset: 0, index: 0)
            renderEncoder.setVertexBytes(&uniforms, length: MemoryLayout<Uniforms>.stride, index: 1)
            renderEncoder.setFragmentBytes(&material, length: MemoryLayout<MaterialUniformsSwift>.stride, index: 0)
            renderEncoder.setFragmentBytes(&lightData, length: MemoryLayout<LightDataSwift>.stride, index: 1)
            renderEncoder.setFragmentBytes(&uniforms, length: MemoryLayout<Uniforms>.stride, index: 2)
            renderEncoder.setFragmentBytes(&shadowData, length: MemoryLayout<ShadowDataSwift>.stride, index: 3)

            if let albedo = node.albedoTexture { renderEncoder.setFragmentTexture(albedo, index: 1) }
            if let normal = node.normalTexture { renderEncoder.setFragmentTexture(normal, index: 2) }
            if let rough = node.roughnessTexture { renderEncoder.setFragmentTexture(rough, index: 3) }

            renderEncoder.drawIndexedPrimitives(type: .triangle, indexCount: meshRef.indexCount,
                indexType: .uint16, indexBuffer: meshRef.indexBuffer, indexBufferOffset: 0)
        }

        renderEncoder.endEncoding()
        commandBuffer.present(drawable)
        commandBuffer.commit()
    }
}
