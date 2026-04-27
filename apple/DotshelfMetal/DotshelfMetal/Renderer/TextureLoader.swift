import MetalKit

struct TextureLoader {
    static func loadTexture(device: MTLDevice, name: String) -> MTLTexture? {
        let textureLoader = MTKTextureLoader(device: device)
        let options: [MTKTextureLoader.Option: Any] = [
            .textureUsage: MTLTextureUsage.shaderRead.rawValue,
            .textureStorageMode: MTLStorageMode.private.rawValue,
            .origin: MTKTextureLoader.Origin.flippedVertically
        ]

        if let url = Bundle.main.url(forResource: name, withExtension: nil) {
            return try? textureLoader.newTexture(URL: url, options: options)
        }
        return nil
    }
}
