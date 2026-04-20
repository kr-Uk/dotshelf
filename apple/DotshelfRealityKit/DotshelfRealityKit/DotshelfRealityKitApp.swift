import SwiftUI
import RealityKit

@main
struct DotshelfRealityKitApp: App {
    init() {
        BirdComponent.registerComponent()
        BirdSystem.registerSystem()
    }

    var body: some SwiftUI.Scene {
        WindowGroup {
            ContentView()
        }
        #if os(macOS)
        .defaultSize(width: 1200, height: 800)
        #endif
    }
}
