import SwiftUI
import MetalKit

final class ScrollableMTKView: MTKView {
    var inputHandler: InputHandler?

    private var dragStartLocation: NSPoint = .zero
    private var isDragging = false
    private var dragDistance: CGFloat = 0

    override var acceptsFirstResponder: Bool { true }

    override func scrollWheel(with event: NSEvent) {
        inputHandler?.handleScrollWheel(deltaY: event.scrollingDeltaY)
    }

    override func mouseDown(with event: NSEvent) {
        let loc = convert(event.locationInWindow, from: nil)
        dragStartLocation = loc
        isDragging = false
        dragDistance = 0
        inputHandler?.viewportSize = SIMD2<Float>(Float(bounds.width), Float(bounds.height))
        let flipped = NSPoint(x: loc.x, y: bounds.height - loc.y)
        inputHandler?.mouseDown(atFlipped: flipped)
    }

    override func mouseDragged(with event: NSEvent) {
        let loc = convert(event.locationInWindow, from: nil)
        let dx = loc.x - dragStartLocation.x
        let dy = loc.y - dragStartLocation.y
        dragDistance = sqrt(dx*dx + dy*dy)

        if dragDistance > 3 {
            if !isDragging {
                isDragging = true
                inputHandler?.beginDrag()
            }
            let flipped = NSPoint(x: loc.x, y: bounds.height - loc.y)
            inputHandler?.updateDrag(currentFlippedPoint: flipped, totalDx: dx, totalDy: dy)
        }
    }

    override func mouseUp(with event: NSEvent) {
        let loc = convert(event.locationInWindow, from: nil)
        let flipped = NSPoint(x: loc.x, y: bounds.height - loc.y)
        inputHandler?.endDrag(at: flipped, dragDistance: dragDistance)
        isDragging = false
    }
}

struct MetalView: NSViewRepresentable {
    let inputHandler: InputHandler

    func makeNSView(context: Context) -> ScrollableMTKView {
        let mtkView = ScrollableMTKView()
        mtkView.device = MTLCreateSystemDefaultDevice()
        mtkView.enableSetNeedsDisplay = false
        mtkView.isPaused = false
        mtkView.preferredFramesPerSecond = 60

        if let renderer = Renderer(mtkView: mtkView, inputHandler: inputHandler) {
            context.coordinator.renderer = renderer
            mtkView.delegate = renderer
            mtkView.inputHandler = inputHandler
        }

        return mtkView
    }

    func updateNSView(_ nsView: ScrollableMTKView, context: Context) {}

    func makeCoordinator() -> Coordinator { Coordinator() }

    class Coordinator {
        var renderer: Renderer?
    }
}

struct ContentView: View {
    @State private var inputHandler = InputHandler()

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            MetalView(inputHandler: inputHandler)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .ignoresSafeArea()

            if inputHandler.isViewingBook {
                Button {
                    inputHandler.dismissBook()
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "chevron.left")
                            .font(.system(size: 14, weight: .semibold))
                        Text("뒤로")
                            .font(.system(size: 14, weight: .medium))
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 10)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)
                .padding(20)
                .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
        .animation(.easeInOut(duration: 0.3), value: inputHandler.isViewingBook)
    }
}

#Preview {
    ContentView()
}
