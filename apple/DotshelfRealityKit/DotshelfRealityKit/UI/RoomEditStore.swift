import Foundation
import Observation
import SwiftUI

// MARK: - Presets

enum WallpaperPreset: String, CaseIterable, Identifiable {
    case warmBeige, softPlaster, forestGreen, terracotta, midnightNavy, paleBlue

    var id: String { rawValue }

    var label: String {
        switch self {
        case .warmBeige:    return "따뜻한 베이지"
        case .softPlaster:  return "부드러운 석고"
        case .forestGreen:  return "숲 그린"
        case .terracotta:   return "테라코타"
        case .midnightNavy: return "미드나잇 네이비"
        case .paleBlue:     return "페일 블루"
        }
    }

    /// sRGB 성분 — 벽 baseColor tint로 즉시 적용.
    var tint: (r: Double, g: Double, b: Double) {
        switch self {
        case .warmBeige:    return (0.92, 0.86, 0.77)
        case .softPlaster:  return (0.93, 0.90, 0.85)
        case .forestGreen:  return (0.40, 0.55, 0.42)
        case .terracotta:   return (0.78, 0.48, 0.38)
        case .midnightNavy: return (0.22, 0.28, 0.42)
        case .paleBlue:     return (0.78, 0.86, 0.92)
        }
    }

    var swiftUIColor: Color {
        let t = tint
        return Color(red: t.r, green: t.g, blue: t.b)
    }
}

enum IndoorLightPreset: String, CaseIterable, Identifiable {
    case warmCozy, neutralStudy, coolCrisp

    var id: String { rawValue }

    var label: String {
        switch self {
        case .warmCozy:     return "아늑한 주광"
        case .neutralStudy: return "중립 작업등"
        case .coolCrisp:    return "청량 낮광"
        }
    }

    /// 램프 color
    var lampColor: (r: Double, g: Double, b: Double) {
        switch self {
        case .warmCozy:     return (1.00, 0.89, 0.71)
        case .neutralStudy: return (1.00, 0.98, 0.95)
        case .coolCrisp:    return (0.90, 0.94, 1.00)
        }
    }

    var intensityScale: Double {
        switch self {
        case .warmCozy:     return 1.0
        case .neutralStudy: return 1.2
        case .coolCrisp:    return 1.4
        }
    }
}

enum OutdoorLightPreset: String, CaseIterable, Identifiable {
    case morning, midday, goldenHour, dusk

    var id: String { rawValue }

    var label: String {
        switch self {
        case .morning:    return "아침"
        case .midday:     return "한낮"
        case .goldenHour: return "골든 아워"
        case .dusk:       return "땅거미"
        }
    }

    var sunColor: (r: Double, g: Double, b: Double) {
        switch self {
        case .morning:    return (1.00, 0.95, 0.82)
        case .midday:     return (1.00, 0.98, 0.92)
        case .goldenHour: return (1.00, 0.82, 0.60)
        case .dusk:       return (0.85, 0.68, 0.78)
        }
    }

    var intensityScale: Double {
        switch self {
        case .morning:    return 1.0
        case .midday:     return 1.2
        case .goldenHour: return 0.9
        case .dusk:       return 0.55
        }
    }
}

enum RoomThemePreset: String, CaseIterable, Identifiable {
    case cozyLibrary, modernStudio, vintageLounge, monochromeLoft

    var id: String { rawValue }

    var label: String {
        switch self {
        case .cozyLibrary:    return "코지 라이브러리"
        case .modernStudio:   return "모던 스튜디오"
        case .vintageLounge:  return "빈티지 라운지"
        case .monochromeLoft: return "모노크롬 로프트"
        }
    }

    /// 전체 테마 적용 시 개별 카테고리에 매핑되는 프리셋 번들.
    var bundle: (wall: WallpaperPreset, indoor: IndoorLightPreset, outdoor: OutdoorLightPreset) {
        switch self {
        case .cozyLibrary:    return (.warmBeige,    .warmCozy,     .goldenHour)
        case .modernStudio:   return (.softPlaster,  .neutralStudy, .midday)
        case .vintageLounge:  return (.terracotta,   .warmCozy,     .dusk)
        case .monochromeLoft: return (.midnightNavy, .coolCrisp,    .morning)
        }
    }

    var accentColor: Color {
        let bundle = bundle
        let t = bundle.wall.tint
        return Color(red: t.r, green: t.g, blue: t.b)
    }
}

// MARK: - Store

/// 공간 편집 서랍(§1.1)의 적용 상태. 명세 Auto-save 원칙대로 선택 즉시 반영.
/// MVP — 메모리 only. SwiftData 도입은 후속.
@Observable
final class RoomEditStore {
    var wallpaper: WallpaperPreset = .warmBeige
    var indoorLight: IndoorLightPreset = .warmCozy
    var outdoorLight: OutdoorLightPreset = .goldenHour
    /// 전체 테마가 적용된 상태(.some) vs 개별 커스텀(.none).
    /// 전체 테마 적용 후 개별 카테고리를 건드리면 자동으로 nil로 전환.
    var activeTheme: RoomThemePreset?

    /// 전체 테마를 적용. 개별 카테고리도 번들에 맞춰 일괄 갱신.
    func applyTheme(_ theme: RoomThemePreset) {
        let b = theme.bundle
        wallpaper = b.wall
        indoorLight = b.indoor
        outdoorLight = b.outdoor
        activeTheme = theme
    }

    /// 개별 카테고리 변경 — 테마 세트와 어긋나면 activeTheme 해제.
    func setWallpaper(_ v: WallpaperPreset) {
        wallpaper = v
        if let t = activeTheme, t.bundle.wall != v { activeTheme = nil }
    }

    func setIndoorLight(_ v: IndoorLightPreset) {
        indoorLight = v
        if let t = activeTheme, t.bundle.indoor != v { activeTheme = nil }
    }

    func setOutdoorLight(_ v: OutdoorLightPreset) {
        outdoorLight = v
        if let t = activeTheme, t.bundle.outdoor != v { activeTheme = nil }
    }
}
