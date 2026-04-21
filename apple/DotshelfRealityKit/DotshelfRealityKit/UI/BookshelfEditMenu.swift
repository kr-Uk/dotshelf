import SwiftUI

/// 책장 편집 메뉴 — 명세 §2의 편집 버튼 진입 오버레이.
/// 책장 포커싱 뷰(frontal)에서만 노출. 좌측 상단에 패널 형태로 떠서 책장은 계속 보이게.
struct BookshelfEditMenu: View {
    let state: BookshelfState
    let archiveCount: Int

    var onAddBookshelf: () -> Void
    var onRemoveBookshelf: () -> Void
    var onAddBook: () -> Void             // 스텝 4
    var onOpenArchive: () -> Void         // 스텝 4
    var onAddDecoration: () -> Void       // 스텝 5
    var onReorder: () -> Void             // 스텝 5
    var onClose: () -> Void

    @State private var isEditingName: Bool = false
    @State private var nameDraft: String = ""
    @State private var showRemoveConfirm: Bool = false

    private var canRemove: Bool { state.bookshelves.count > 1 }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider().opacity(0.3)
            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 18) {
                    nameSection
                    visibilitySection
                    actionSection
                    Divider().opacity(0.3)
                    dangerSection
                }
                .padding(18)
            }
        }
        .frame(width: 360)
        .frame(maxHeight: 560)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.ultraThinMaterial)
                .shadow(color: .black.opacity(0.2), radius: 18, y: 6)
        )
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .alert("이 책장을 삭제할까요?", isPresented: $showRemoveConfirm) {
            Button("취소", role: .cancel) {}
            Button("삭제", role: .destructive) { onRemoveBookshelf() }
        } message: {
            Text("책·오브젝트는 다른 책장의 빈 공간으로 자동 이동돼요. 공간이 부족하면 보관함으로 옮겨집니다.")
        }
        .alert("책장 이름 변경", isPresented: $isEditingName) {
            TextField("책장 이름", text: $nameDraft)
            Button("취소", role: .cancel) { nameDraft = "" }
            Button("저장") {
                state.renameCurrentBookshelf(to: nameDraft)
                nameDraft = ""
            }
        } message: {
            Text("30자 이내")
        }
    }

    // MARK: - Header

    private var header: some View {
        HStack {
            Image(systemName: "square.stack.3d.up.fill")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("책장 편집")
                .font(.system(size: 15, weight: .bold))
            Spacer()
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 18))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Name

    private var nameSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            label("이름", systemImage: "tag")
            HStack {
                Text(state.currentBookshelfName)
                    .font(.system(size: 15, weight: .semibold))
                Spacer()
                Button {
                    nameDraft = state.currentBookshelfName
                    isEditingName = true
                } label: {
                    Label("변경", systemImage: "pencil")
                        .font(.system(size: 12, weight: .medium))
                }
                .buttonStyle(.plain)
                .foregroundStyle(.secondary)
            }
            .padding(12)
            .background(Color.gray.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
        }
    }

    // MARK: - Visibility

    private var visibilitySection: some View {
        VStack(alignment: .leading, spacing: 8) {
            label("공개 범위", systemImage: "eye")
            VStack(spacing: 6) {
                ForEach(ShelfVisibility.allCases, id: \.self) { v in
                    visibilityRow(for: v)
                }
            }
        }
    }

    @ViewBuilder
    private func visibilityRow(for v: ShelfVisibility) -> some View {
        let selected = state.currentBookshelfVisibility == v
        Button {
            state.setCurrentBookshelfVisibility(v)
        } label: {
            HStack(spacing: 10) {
                Image(systemName: v.systemImage)
                    .font(.system(size: 13, weight: .semibold))
                    .frame(width: 18)
                Text(v.label)
                    .font(.system(size: 13, weight: .medium))
                Spacer()
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundStyle(.tint)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 9)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(selected ? Color.accentColor.opacity(0.12) : Color.gray.opacity(0.05))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .strokeBorder(selected ? Color.accentColor.opacity(0.35) : .clear, lineWidth: 1)
            )
            .foregroundStyle(selected ? Color.accentColor : .primary)
        }
        .buttonStyle(.plain)
    }

    // MARK: - Action buttons

    private var actionSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            label("관리", systemImage: "slider.horizontal.3")
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 8) {
                actionCell(label: "책장 추가", systemImage: "plus.square.on.square", tint: .accentColor, action: onAddBookshelf)
                actionCell(label: "책 추가", systemImage: "book.fill", tint: .blue, action: onAddBook)
                actionCell(label: "오브젝트", systemImage: "sparkles", tint: .purple, action: onAddDecoration)
                actionCell(label: "순서 변경", systemImage: "arrow.up.arrow.down", tint: .orange, action: onReorder)
                actionCell(
                    label: archiveCount > 0 ? "보관함 \(archiveCount)" : "보관함",
                    systemImage: "archivebox",
                    tint: .secondary,
                    action: onOpenArchive
                )
            }
        }
    }

    private func actionCell(
        label: String,
        systemImage: String,
        tint: Color,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            VStack(spacing: 6) {
                Image(systemName: systemImage)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundStyle(tint)
                Text(label)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.primary)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(Color.gray.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Danger

    private var dangerSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            label("위험", systemImage: "exclamationmark.triangle")
            Button {
                showRemoveConfirm = true
            } label: {
                HStack {
                    Image(systemName: "trash")
                        .font(.system(size: 13, weight: .semibold))
                    Text("이 책장 삭제")
                        .font(.system(size: 13, weight: .semibold))
                    Spacer()
                    if !canRemove {
                        Text("마지막 책장")
                            .font(.system(size: 11))
                            .foregroundStyle(.secondary)
                    }
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 10)
                .background(
                    RoundedRectangle(cornerRadius: 10)
                        .fill(canRemove ? Color.red.opacity(0.1) : Color.gray.opacity(0.06))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .strokeBorder(canRemove ? Color.red.opacity(0.3) : .clear, lineWidth: 1)
                )
                .foregroundStyle(canRemove ? Color.red : .secondary)
            }
            .buttonStyle(.plain)
            .disabled(!canRemove)
        }
    }

    // MARK: - Helpers

    private func label(_ text: String, systemImage: String) -> some View {
        HStack(spacing: 5) {
            Image(systemName: systemImage)
                .font(.system(size: 10, weight: .semibold))
            Text(text)
                .font(.system(size: 11, weight: .semibold))
        }
        .foregroundStyle(.secondary)
    }
}
