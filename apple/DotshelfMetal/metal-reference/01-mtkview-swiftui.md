# MTKView + SwiftUI Integration (macOS)

## NSViewRepresentable 패턴

```swift
struct MetalView: NSViewRepresentable {
    func makeCoordinator() -> Coordinator { Coordinator() }
    
    func makeNSView(context: Context) -> MTKView {
        let mtkView = MTKView()
        mtkView.device = MTLCreateSystemDefaultDevice()
        mtkView.colorPixelFormat = .bgra8Unorm
        mtkView.depthStencilPixelFormat = .depth32Float
        mtkView.clearColor = MTLClearColor(red: 0.5, green: 0.5, blue: 0.5, alpha: 1.0)
        mtkView.enableSetNeedsDisplay = false
        mtkView.isPaused = false
        mtkView.preferredFramesPerSecond = 60
        
        if let renderer = Renderer(mtkView: mtkView) {
            context.coordinator.renderer = renderer
            mtkView.delegate = renderer
        }
        return mtkView
    }
    
    func updateNSView(_ nsView: MTKView, context: Context) {}
    
    class Coordinator {
        var renderer: Renderer?  // MUST be strong ref - MTKView.delegate is weak!
    }
}
```

## 핵심 주의사항

1. **MTKView.delegate는 weak** - Coordinator에서 strong reference를 유지해야 함. 안 하면 Renderer가 즉시 해제되어 검은 화면.

2. **pixelFormat은 반드시 MTKView에 먼저 설정** - pipeline descriptor의 pixelFormat과 반드시 일치해야 함. 불일치 시 fatalError.

3. **SwiftUI layout** - MetalView가 윈도우를 채우려면:
   ```swift
   MetalView()
       .ignoresSafeArea()  // 또는
       .frame(maxWidth: .infinity, maxHeight: .infinity)
   ```
   `.frame(minWidth:)` 만으로는 확장 안 될 수 있음.

4. **drawableSizeWillChange** - 첫 draw 전에 호출됨. 여기서 aspect ratio 설정.

5. **Input handling** - MTKView는 기본적으로 firstResponder가 아님:
   ```swift
   override var acceptsFirstResponder: Bool { true }
   ```
