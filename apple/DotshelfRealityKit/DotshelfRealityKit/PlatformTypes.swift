#if os(macOS)
@_exported import AppKit
typealias PlatformColor = NSColor
#else
@_exported import UIKit
typealias PlatformColor = UIColor
#endif
