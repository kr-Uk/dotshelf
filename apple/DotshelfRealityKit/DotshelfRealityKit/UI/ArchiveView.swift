import SwiftUI

/// 보관함 2D 풀 오버레이 — 페이즈 C 스텝 4.
/// 보관된 책을 현재 책장으로 복구하거나 영구 삭제.
struct ArchiveView: View {
    let store: ArchiveStore
    var onRestore: (ArchiveStore.Item) -> Bool
    var onDeletePermanently: (ArchiveStore.Item) -> Void
    var onClose: () -> Void

    @State private var errorMessage: String?
    @State private var pendingDelete: ArchiveStore.Item?

    var body: some View {
        ZStack {
            Color.black.opacity(0.45).ignoresSafeArea()
                .onTapGesture { onClose() }

            VStack(spacing: 0) {
                header
                Divider().opacity(0.3)
                if let errorMessage {
                    errorBanner(errorMessage)
                }
                ScrollView {
                    if store.items.isEmpty {
                        emptyState
                    } else {
                        LazyVStack(spacing: 8) {
                            ForEach(store.sortedItems) { item in
                                row(for: item)
                            }
                        }
                        .padding(16)
                    }
                }
            }
            .frame(maxWidth: 560, maxHeight: 640)
            .background(
                RoundedRectangle(cornerRadius: 22).fill(.ultraThinMaterial)
            )
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .shadow(color: .black.opacity(0.25), radius: 22, y: 10)
            .padding(30)
        }
        .alert("이 책을 영구 삭제할까요?", isPresented: .init(
            get: { pendingDelete != nil },
            set: { if !$0 { pendingDelete = nil } }
        )) {
            Button("취소", role: .cancel) { pendingDelete = nil }
            Button("삭제", role: .destructive) {
                if let item = pendingDelete {
                    onDeletePermanently(item)
                }
                pendingDelete = nil
            }
        } message: {
            Text("복구할 수 없어요.")
        }
    }

    private var header: some View {
        HStack {
            Image(systemName: "archivebox.fill")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("보관함")
                .font(.system(size: 16, weight: .bold))
            Text("\(store.items.count)권")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(.secondary)
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

    private var emptyState: some View {
        VStack(spacing: 10) {
            Image(systemName: "tray")
                .font(.system(size: 32))
                .foregroundStyle(.secondary)
            Text("보관함이 비어있어요")
                .font(.system(size: 14, weight: .semibold))
            Text("책을 보관함으로 옮기면 여기에 모여요.")
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 80)
    }

    @ViewBuilder
    private func row(for item: ArchiveStore.Item) -> some View {
        HStack(spacing: 12) {
            RoundedRectangle(cornerRadius: 4)
                .fill(Color(red: Double(item.tintR), green: Double(item.tintG), blue: Double(item.tintB)))
                .frame(width: 32, height: 44)
                .shadow(color: .black.opacity(0.1), radius: 2, y: 1)

            VStack(alignment: .leading, spacing: 3) {
                Text(item.title)
                    .font(.system(size: 14, weight: .semibold))
                    .lineLimit(1)
                Text(formattedArchivedAt(item.archivedAt))
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
            }

            Spacer()

            Button {
                let ok = onRestore(item)
                if !ok {
                    errorMessage = "현재 책장에 빈 공간이 없어요"
                } else {
                    errorMessage = nil
                }
            } label: {
                Label("꺼내기", systemImage: "tray.and.arrow.up")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 7)
                    .background(Color.accentColor.opacity(0.15), in: Capsule())
                    .foregroundStyle(Color.accentColor)
            }
            .buttonStyle(.plain)

            Button {
                pendingDelete = item
            } label: {
                Image(systemName: "trash")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(8)
                    .background(Color.red.opacity(0.1), in: Circle())
                    .foregroundStyle(.red)
            }
            .buttonStyle(.plain)
        }
        .padding(10)
        .background(Color.gray.opacity(0.06), in: RoundedRectangle(cornerRadius: 10))
    }

    private func formattedArchivedAt(_ date: Date) -> String {
        let fmt = RelativeDateTimeFormatter()
        fmt.unitsStyle = .short
        fmt.locale = Locale(identifier: "ko_KR")
        return "\(fmt.localizedString(for: date, relativeTo: Date())) 보관"
    }
}
