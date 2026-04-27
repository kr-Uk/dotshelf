import SwiftUI

/// 책 추가 검색 2D 오버레이 — 명세 §2 '편집' 메뉴의 책 추가 경로 (MVP는 mock 카탈로그).
/// 성공적으로 추가된 카탈로그 id는 누적 추적해 UI상 "추가됨"으로 표시.
struct BookSearchOverlay: View {
    var onAdd: (CatalogBook) -> Bool
    var onClose: () -> Void
    /// 부모가 관리하는 "이미 책장/보관함에 있는" catalog id 집합 (중복 추가 방지).
    /// 책을 완전히 삭제한 경우에만 부모가 해당 id를 제거한다.
    var existingCatalogIds: Set<UUID> = []

    @State private var query: String = ""
    @State private var errorMessage: String?

    private var results: [CatalogBook] {
        let q = query.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if q.isEmpty { return BookRecord.searchCatalog }
        return BookRecord.searchCatalog.filter {
            $0.record.title.lowercased().contains(q) ||
            $0.record.author.lowercased().contains(q) ||
            $0.record.publisher.lowercased().contains(q)
        }
    }

    var body: some View {
        ZStack {
            Color.black.opacity(0.45).ignoresSafeArea()
                .onTapGesture { onClose() }

            VStack(spacing: 0) {
                header
                Divider().opacity(0.3)
                searchField
                if let errorMessage {
                    errorBanner(errorMessage)
                }
                Divider().opacity(0.3)
                ScrollView {
                    LazyVStack(spacing: 8) {
                        ForEach(results) { cb in
                            row(for: cb)
                        }
                        if results.isEmpty {
                            Text("검색 결과가 없어요")
                                .font(.system(size: 13))
                                .foregroundStyle(.secondary)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 40)
                        }
                    }
                    .padding(16)
                }
            }
            .frame(maxWidth: 620, maxHeight: 640)
            .background(
                RoundedRectangle(cornerRadius: 22).fill(.ultraThinMaterial)
            )
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .shadow(color: .black.opacity(0.25), radius: 22, y: 10)
            .padding(30)
        }
    }

    private var header: some View {
        HStack {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("책 추가")
                .font(.system(size: 16, weight: .bold))
            Spacer()
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 20))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 14)
    }

    private var searchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            TextField("제목·저자·출판사로 검색", text: $query)
                .textFieldStyle(.plain)
                .font(.system(size: 13))
            if !query.isEmpty {
                Button { query = "" } label: {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 12))
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(Color.gray.opacity(0.1), in: Capsule())
        .padding(16)
    }

    private func errorBanner(_ msg: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 11, weight: .bold))
            Text(msg).font(.system(size: 12, weight: .medium))
            Spacer()
            Button {
                errorMessage = nil
            } label: {
                Image(systemName: "xmark").font(.system(size: 10, weight: .bold))
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 8)
        .background(Color.red.opacity(0.15))
        .foregroundStyle(.red)
    }

    @ViewBuilder
    private func row(for cb: CatalogBook) -> some View {
        let alreadyOwned = existingCatalogIds.contains(cb.id)
        HStack(spacing: 12) {
            // 미리보기 — 팔레트 인덱스로 결정적 색상
            RoundedRectangle(cornerRadius: 4)
                .fill(catalogPreviewColor(for: cb.paletteIndex))
                .frame(width: 34, height: 48)
                .shadow(color: .black.opacity(0.1), radius: 2, y: 1)

            VStack(alignment: .leading, spacing: 3) {
                Text(cb.record.title)
                    .font(.system(size: 14, weight: .semibold))
                    .lineLimit(1)
                Text("\(cb.record.author) · \(cb.record.publisher)")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                Text(cb.record.summary)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary.opacity(0.85))
                    .lineLimit(2)
            }
            Spacer()

            Button {
                guard !alreadyOwned else { return }  // 중복 추가 방지
                let ok = onAdd(cb)
                if ok {
                    errorMessage = nil
                } else {
                    errorMessage = "책장에 빈 공간이 없어요"
                }
            } label: {
                HStack(spacing: 4) {
                    if alreadyOwned {
                        Image(systemName: "checkmark").font(.system(size: 10, weight: .bold))
                    }
                    Text(alreadyOwned ? "이미 있음" : "추가")
                        .font(.system(size: 12, weight: .semibold))
                }
                .foregroundStyle(alreadyOwned ? Color.secondary : Color.white)
                .padding(.horizontal, 14)
                .padding(.vertical, 7)
                .background(
                    Capsule().fill(alreadyOwned ? Color.gray.opacity(0.18) : Color.accentColor)
                )
            }
            .buttonStyle(.plain)
            .disabled(alreadyOwned)
        }
        .padding(10)
        .background(Color.gray.opacity(0.06), in: RoundedRectangle(cornerRadius: 10))
    }

    /// 카탈로그 미리보기용 — populateBooks와 동일한 팔레트.
    private func catalogPreviewColor(for idx: Int) -> Color {
        let palette: [(Double, Double, Double)] = [
            (0.75, 0.22, 0.17), (0.18, 0.35, 0.65), (0.22, 0.55, 0.30),
            (0.55, 0.35, 0.18), (0.30, 0.25, 0.55), (0.90, 0.88, 0.82),
            (0.15, 0.15, 0.18), (0.85, 0.68, 0.20), (0.80, 0.40, 0.15),
            (0.60, 0.20, 0.45), (0.35, 0.55, 0.65), (0.70, 0.30, 0.30),
        ]
        let c = palette[idx % palette.count]
        return Color(red: c.0, green: c.1, blue: c.2)
    }
}
