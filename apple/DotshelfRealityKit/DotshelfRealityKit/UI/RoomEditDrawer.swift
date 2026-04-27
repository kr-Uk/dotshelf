import SwiftUI

/// 공간 편집 서랍 — 명세 §1.1.
/// 쿼터뷰 편집 버튼 → 화면 하단에서 슬라이드 업. 배경 3D는 가리지 않고 상호작용만 서랍으로 이동.
struct RoomEditDrawer: View {
    let store: RoomEditStore
    let decorStore: RoomDecorStore
    var onAddDecor: (RoomDecorKind) -> Void
    var onRemoveDecor: (UUID) -> Void
    var onClose: () -> Void

    /// 명세 §1.1 — 포스트잇은 §4 대시보드 포커싱 뷰의 '스타일 변경'에서만 편집.
    /// 책장 내 꾸미기 오브젝트는 §2 책장 포커싱 뷰의 편집 메뉴에서만.
    enum Category: String, CaseIterable, Identifiable {
        case wallpaper, indoorLight, outdoorLight, theme
        case skins, placement  // placeholder (MVP 후속)

        var id: String { rawValue }

        var label: String {
            switch self {
            case .wallpaper:    return "벽지"
            case .indoorLight:  return "내부 조명"
            case .outdoorLight: return "외부 조명"
            case .placement:    return "배치 꾸미기"
            case .skins:        return "필수 스킨"
            case .theme:        return "전체 테마"
            }
        }

        var systemImage: String {
            switch self {
            case .wallpaper:    return "square.grid.2x2"
            case .indoorLight:  return "lightbulb.fill"
            case .outdoorLight: return "sun.max.fill"
            case .placement:    return "sparkles.square.filled.on.square"
            case .skins:        return "cube"
            case .theme:        return "paintbrush.fill"
            }
        }
    }

    @State private var category: Category = .wallpaper
    @State private var pendingTheme: RoomThemePreset?
    /// 서랍이 최소화(헤더만)된 상태. 에셋 드래그·이동 시 바닥을 가리지 않도록 내릴 수 있음.
    /// isRoomEditDrawerOpen은 그대로 true로 유지 → 꾸미기 탭/드래그는 계속 허용.
    @State private var isMinimized: Bool = false

    var body: some View {
        VStack(spacing: 0) {
            Spacer()
            drawer
        }
        .alert("전체 테마를 적용할까요?", isPresented: .init(
            get: { pendingTheme != nil },
            set: { if !$0 { pendingTheme = nil } }
        )) {
            Button("취소", role: .cancel) { pendingTheme = nil }
            Button("적용") {
                if let t = pendingTheme { store.applyTheme(t) }
                pendingTheme = nil
            }
        } message: {
            Text("기존 개별 스킨·벽지·조명 설정이 모두 초기화돼요.")
        }
    }

    // MARK: - Drawer shell

    private var drawer: some View {
        VStack(spacing: 0) {
            header
            if !isMinimized {
                Divider().opacity(0.3)
                tabBar
                Divider().opacity(0.3)
                content
            }
        }
        .frame(maxWidth: .infinity)
        .frame(height: isMinimized ? 52 : 340)
        .background(
            UnevenRoundedRectangle(
                topLeadingRadius: 22, bottomLeadingRadius: 0,
                bottomTrailingRadius: 0, topTrailingRadius: 22
            )
            .fill(.ultraThinMaterial)
        )
        .shadow(color: .black.opacity(0.22), radius: 18, y: -6)
        .animation(.easeInOut(duration: 0.22), value: isMinimized)
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 10) {
            Image(systemName: "wand.and.stars")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("공간 편집")
                .font(.system(size: 15, weight: .bold))
            if isMinimized {
                Text("· 바닥이 보여요")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(.secondary)
            }
            if let active = store.activeTheme, !isMinimized {
                HStack(spacing: 4) {
                    Image(systemName: "checkmark")
                        .font(.system(size: 9, weight: .bold))
                    Text(active.label)
                        .font(.system(size: 10, weight: .semibold))
                }
                .padding(.horizontal, 7)
                .padding(.vertical, 3)
                .background(Color.accentColor.opacity(0.15), in: Capsule())
                .foregroundStyle(Color.accentColor)
            }
            Spacer()
            if !isMinimized {
                Text("자동 저장됨")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
            }

            Button(action: { isMinimized.toggle() }) {
                HStack(spacing: 4) {
                    Image(systemName: isMinimized ? "chevron.up" : "chevron.down")
                        .font(.system(size: 10, weight: .bold))
                    Text(isMinimized ? "메뉴 펴기" : "내리기")
                        .font(.system(size: 11, weight: .semibold))
                }
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .background(Color.gray.opacity(0.12), in: Capsule())
            }
            .buttonStyle(.plain)

            Button(action: onClose) {
                HStack(spacing: 5) {
                    Image(systemName: "xmark")
                        .font(.system(size: 10, weight: .bold))
                    Text("돌아가기")
                        .font(.system(size: 11, weight: .semibold))
                }
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .background(Color.gray.opacity(0.12), in: Capsule())
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
        .contentShape(Rectangle())
        .onTapGesture {
            // 최소화 상태에서 헤더 탭 → 펼치기 편의
            if isMinimized { isMinimized = false }
        }
    }

    // MARK: - Tab bar (카테고리 가로 스크롤)

    private var tabBar: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 6) {
                ForEach(Category.allCases) { c in
                    tabButton(for: c)
                }
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
        }
    }

    @ViewBuilder
    private func tabButton(for c: Category) -> some View {
        let selected = category == c
        Button { category = c } label: {
            HStack(spacing: 5) {
                Image(systemName: c.systemImage)
                    .font(.system(size: 10, weight: .semibold))
                Text(c.label)
                    .font(.system(size: 12, weight: .semibold))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Capsule().fill(selected ? Color.accentColor.opacity(0.18) : Color.gray.opacity(0.08)))
            .foregroundStyle(selected ? Color.accentColor : Color.primary)
        }
        .buttonStyle(.plain)
    }

    // MARK: - Content

    @ViewBuilder
    private var content: some View {
        switch category {
        case .wallpaper:    wallpaperGrid
        case .indoorLight:  indoorLightGrid
        case .outdoorLight: outdoorLightGrid
        case .theme:        themeGrid
        case .placement:    placementView
        case .skins: placeholderGrid
        }
    }

    // MARK: - 배치 꾸미기 탭 — 프리셋 갤러리 + 내 배치 리스트

    private var placementView: some View {
        VStack(alignment: .leading, spacing: 8) {
            placementGallery
            Divider().opacity(0.3).padding(.horizontal, 14)
            placementList
        }
        .padding(.top, 4)
    }

    private var placementGallery: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("추가")
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(.secondary)
                .padding(.horizontal, 18)

            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 8) {
                    ForEach(RoomDecorKind.allCases) { kind in
                        decorPresetCard(kind: kind)
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
            }
        }
    }

    private func decorPresetCard(kind: RoomDecorKind) -> some View {
        Button {
            onAddDecor(kind)
        } label: {
            VStack(spacing: 4) {
                ZStack {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(kind.previewColor)
                    Image(systemName: kind.systemImage)
                        .font(.system(size: 18, weight: .bold))
                        .foregroundStyle(.white.opacity(0.9))
                    if kind.isWallMounted {
                        HStack {
                            Spacer()
                            VStack {
                                Image(systemName: "square.split.1x2")
                                    .font(.system(size: 8, weight: .bold))
                                    .padding(4)
                                    .background(Color.white.opacity(0.2), in: Circle())
                                    .foregroundStyle(.white)
                                Spacer()
                            }
                        }
                        .padding(4)
                    }
                }
                .frame(width: 72, height: 72)

                Text(kind.label)
                    .font(.system(size: 10, weight: .semibold))
                    .lineLimit(1)
            }
            .padding(6)
            .background(Color.gray.opacity(0.06), in: RoundedRectangle(cornerRadius: 12))
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder
    private var placementList: some View {
        if decorStore.items.isEmpty {
            Text("추가된 배치 꾸미기가 없어요")
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
        } else {
            VStack(alignment: .leading, spacing: 4) {
                Text("배치됨 \(decorStore.items.count)")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(.secondary)
                    .padding(.horizontal, 18)

                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 6) {
                        ForEach(decorStore.items) { item in
                            placedItemChip(item: item)
                        }
                    }
                    .padding(.horizontal, 14)
                    .padding(.vertical, 4)
                }
            }
        }
    }

    private func placedItemChip(item: PlacedDecor) -> some View {
        let selected = decorStore.selectedId == item.id
        return HStack(spacing: 6) {
            Image(systemName: item.kind.systemImage)
                .font(.system(size: 10, weight: .semibold))
            Text(item.kind.label)
                .font(.system(size: 11, weight: .semibold))
            Button {
                onRemoveDecor(item.id)
            } label: {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 5)
        .background(
            Capsule().fill(selected ? Color.accentColor.opacity(0.18) : Color.gray.opacity(0.08))
        )
        .overlay(
            Capsule().strokeBorder(selected ? Color.accentColor.opacity(0.4) : .clear, lineWidth: 1)
        )
        .onTapGesture {
            decorStore.selectedId = item.id
        }
    }

    private var wallpaperGrid: some View {
        assetGrid(WallpaperPreset.allCases) { preset in
            presetCard(
                title: preset.label,
                owned: true,
                selected: store.wallpaper == preset,
                fill: preset.swiftUIColor
            ) {
                store.setWallpaper(preset)
            }
        }
    }

    private var indoorLightGrid: some View {
        assetGrid(IndoorLightPreset.allCases) { preset in
            let c = preset.lampColor
            presetCard(
                title: preset.label,
                owned: true,
                selected: store.indoorLight == preset,
                fill: Color(red: c.r, green: c.g, blue: c.b)
            ) {
                store.setIndoorLight(preset)
            }
        }
    }

    private var outdoorLightGrid: some View {
        assetGrid(OutdoorLightPreset.allCases) { preset in
            let c = preset.sunColor
            presetCard(
                title: preset.label,
                owned: true,
                selected: store.outdoorLight == preset,
                fill: Color(red: c.r, green: c.g, blue: c.b)
            ) {
                store.setOutdoorLight(preset)
            }
        }
    }

    private var themeGrid: some View {
        assetGrid(RoomThemePreset.allCases) { preset in
            presetCard(
                title: preset.label,
                owned: true,
                selected: store.activeTheme == preset,
                fill: preset.accentColor
            ) {
                pendingTheme = preset
            }
        }
    }

    private var placeholderGrid: some View {
        VStack(spacing: 8) {
            Image(systemName: "hammer")
                .font(.system(size: 26))
                .foregroundStyle(.secondary)
            Text("이 카테고리는 곧 열려요")
                .font(.system(size: 13, weight: .semibold))
            Text("책장 스킨·노트북 화면보호기·포스트잇 스타일·배치 꾸미기는 꾸미기 오브젝트 시장(§5.4) 준비 후 붙습니다.")
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.horizontal, 24)
    }

    // MARK: - Grid shell (가로 스크롤)

    private func assetGrid<Item: Identifiable, Content: View>(
        _ items: [Item], @ViewBuilder content: @escaping (Item) -> Content
    ) -> some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 10) {
                ForEach(items) { item in
                    content(item)
                }
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 12)
        }
    }

    private func presetCard(
        title: String,
        owned: Bool,
        selected: Bool,
        fill: Color,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            VStack(spacing: 6) {
                ZStack {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(owned ? fill : Color.gray.opacity(0.18))
                    if !owned {
                        Image(systemName: "cart.fill")
                            .font(.system(size: 16, weight: .bold))
                            .foregroundStyle(.white.opacity(0.85))
                    }
                    if selected {
                        Image(systemName: "checkmark.circle.fill")
                            .font(.system(size: 22, weight: .bold))
                            .foregroundStyle(.white)
                            .shadow(color: .black.opacity(0.25), radius: 4, y: 2)
                    }
                }
                .frame(width: 100, height: 100)
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .strokeBorder(selected ? Color.accentColor : .clear, lineWidth: 2)
                )

                Text(title)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(owned ? .primary : .secondary)
                    .lineLimit(1)
            }
        }
        .buttonStyle(.plain)
    }
}
