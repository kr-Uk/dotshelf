import SwiftUI

enum ReadingStatus {
    case notStarted, reading, completed

    var label: String {
        switch self {
        case .notStarted: return "읽기 전"
        case .reading: return "읽는 중"
        case .completed: return "완독"
        }
    }

    var systemImage: String {
        switch self {
        case .notStarted: return "hourglass"
        case .reading: return "book.fill"
        case .completed: return "checkmark.seal.fill"
        }
    }

    var tint: Color {
        switch self {
        case .notStarted: return .gray
        case .reading: return .blue
        case .completed: return .green
        }
    }
}

struct BookComment: Identifiable {
    let id = UUID()
    let name: String
    let initials: String
    let avatarColor: Color
    let time: String
    let message: String
}

struct BookRecord {
    let title: String
    let author: String
    let rating: Double     // 0 ~ 5
    let startedAt: String?
    let finishedAt: String?
    let status: ReadingStatus
    let personalNote: String
    var comments: [BookComment]
}

struct BookReadingPanel: View {
    let bookName: String
    @State private var record: BookRecord
    @State private var commentInput: String = ""

    init(bookName: String) {
        self.bookName = bookName
        self._record = State(initialValue: Self.makeRecord(for: bookName))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    bookInfoSection
                    Divider()
                    readingRecordSection
                    Divider()
                    commentsSection
                }
                .padding(20)
            }
            commentComposer
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(.regularMaterial)
        }
        .frame(width: 380)
        .frame(maxHeight: 560)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.ultraThinMaterial)
                .shadow(color: .black.opacity(0.18), radius: 18, y: 6)
        )
        .clipShape(RoundedRectangle(cornerRadius: 18))
    }

    // MARK: - 책 정보
    private var bookInfoSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(record.title)
                .font(.system(size: 22, weight: .bold))
            Text(record.author)
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.secondary)
            HStack(spacing: 6) {
                StarRating(rating: record.rating)
                Text(String(format: "%.1f", record.rating))
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.secondary)
            }
        }
    }

    // MARK: - 독서 기록
    private var readingRecordSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionHeader("독서 기록", systemImage: "book.closed.fill")

            HStack(spacing: 8) {
                Image(systemName: record.status.systemImage)
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(record.status.tint)
                Text(record.status.label)
                    .font(.system(size: 13, weight: .semibold))
                Spacer()
            }
            .padding(10)
            .background(record.status.tint.opacity(0.12), in: RoundedRectangle(cornerRadius: 10))

            if record.startedAt != nil || record.finishedAt != nil {
                HStack(spacing: 10) {
                    if let start = record.startedAt {
                        dateChip(label: "시작", value: start)
                    }
                    if let finish = record.finishedAt {
                        dateChip(label: "완독", value: finish)
                    }
                }
            }

            if !record.personalNote.isEmpty {
                Text(record.personalNote)
                    .font(.system(size: 12))
                    .italic()
                    .foregroundStyle(.primary.opacity(0.85))
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(
                        RoundedRectangle(cornerRadius: 10)
                            .fill(Color.yellow.opacity(0.12))
                    )
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .strokeBorder(Color.yellow.opacity(0.3), lineWidth: 1)
                    )
            }
        }
    }

    // MARK: - 댓글
    private var commentsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            sectionHeader("댓글 \(record.comments.count)", systemImage: "bubble.left.fill")
            VStack(spacing: 8) {
                ForEach(record.comments) { comment in
                    CommentRow(comment: comment)
                }
            }
        }
    }

    private var commentComposer: some View {
        HStack(spacing: 8) {
            TextField("댓글 달기…", text: $commentInput)
                .textFieldStyle(.plain)
                .font(.system(size: 13))
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.gray.opacity(0.12), in: Capsule())
            Button(action: addComment) {
                Image(systemName: "paperplane.fill")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(.white)
                    .padding(9)
                    .background(
                        Circle().fill(commentInput.trimmingCharacters(in: .whitespaces).isEmpty
                                      ? Color.gray.opacity(0.4)
                                      : Color.accentColor)
                    )
            }
            .buttonStyle(.plain)
            .disabled(commentInput.trimmingCharacters(in: .whitespaces).isEmpty)
        }
    }

    // MARK: - Helpers

    private func sectionHeader(_ text: String, systemImage: String) -> some View {
        HStack(spacing: 5) {
            Image(systemName: systemImage)
                .font(.system(size: 11, weight: .semibold))
            Text(text)
                .font(.system(size: 12, weight: .semibold))
        }
        .foregroundStyle(.secondary)
    }

    private func dateChip(label: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(.secondary)
            Text(value)
                .font(.system(size: 12, weight: .medium))
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(Color.gray.opacity(0.12), in: RoundedRectangle(cornerRadius: 8))
    }

    private func addComment() {
        let trimmed = commentInput.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        record.comments.append(BookComment(
            name: "나",
            initials: "나",
            avatarColor: .accentColor,
            time: "방금 전",
            message: trimmed
        ))
        commentInput = ""
    }

    // MARK: - Fake 데이터 (책 이름 해시로 결정적 매핑)

    static func makeRecord(for bookName: String) -> BookRecord {
        let templates: [BookRecord] = [
            BookRecord(
                title: "달러구트 꿈 백화점",
                author: "이미예",
                rating: 4.5,
                startedAt: "2026-02-14",
                finishedAt: "2026-03-10",
                status: .completed,
                personalNote: "꿈이라는 소재를 이렇게 따뜻하게 풀어내다니. 잠자기 전에 읽기 좋은 책.",
                comments: [
                    BookComment(name: "지호", initials: "J", avatarColor: .orange, time: "2일 전", message: "이 책 나도 완독! 2권도 추천"),
                    BookComment(name: "수민", initials: "S", avatarColor: .cyan, time: "어제", message: "대기자 수 많던데 ㅠ"),
                ]
            ),
            BookRecord(
                title: "코스모스",
                author: "칼 세이건",
                rating: 4.8,
                startedAt: "2025-11-02",
                finishedAt: nil,
                status: .reading,
                personalNote: "우주 얘기인데 왜 이렇게 시적으로 쓰는지… 챕터 하나씩 천천히.",
                comments: [
                    BookComment(name: "민준", initials: "M", avatarColor: .green, time: "3일 전", message: "4장부터 속도 붙더라"),
                ]
            ),
            BookRecord(
                title: "미드나잇 라이브러리",
                author: "매트 헤이그",
                rating: 4.2,
                startedAt: "2026-01-20",
                finishedAt: "2026-02-05",
                status: .completed,
                personalNote: "\"다른 삶을 살았다면\" 이라는 질문에 대한 답.",
                comments: [
                    BookComment(name: "유나", initials: "Y", avatarColor: .pink, time: "1주 전", message: "영화도 나온대 💕"),
                    BookComment(name: "지호", initials: "J", avatarColor: .orange, time: "5일 전", message: "결말 스포 금지!"),
                    BookComment(name: "수민", initials: "S", avatarColor: .cyan, time: "3일 전", message: "빌려줘요"),
                ]
            ),
            BookRecord(
                title: "작별하지 않는다",
                author: "한강",
                rating: 4.6,
                startedAt: nil,
                finishedAt: nil,
                status: .notStarted,
                personalNote: "",
                comments: []
            ),
            BookRecord(
                title: "사피엔스",
                author: "유발 하라리",
                rating: 4.4,
                startedAt: "2025-09-10",
                finishedAt: "2025-12-22",
                status: .completed,
                personalNote: "인류사의 큰 흐름을 한 권으로. 두 번 읽었음.",
                comments: [
                    BookComment(name: "민준", initials: "M", avatarColor: .green, time: "1달 전", message: "호모 데우스도 재밌던데"),
                ]
            ),
            BookRecord(
                title: "아몬드",
                author: "손원평",
                rating: 4.3,
                startedAt: "2026-03-25",
                finishedAt: nil,
                status: .reading,
                personalNote: "감정을 느끼지 못하는 아이. 초반부터 몰입됨.",
                comments: []
            ),
        ]
        let idx = abs(bookName.hashValue) % templates.count
        return templates[idx]
    }
}

// MARK: - Sub views

private struct StarRating: View {
    let rating: Double

    var body: some View {
        HStack(spacing: 2) {
            ForEach(0..<5) { i in
                let fill = max(0, min(1, rating - Double(i)))
                Image(systemName: fill >= 0.9 ? "star.fill" : (fill >= 0.3 ? "star.leadinghalf.filled" : "star"))
                    .font(.system(size: 12))
                    .foregroundStyle(.yellow)
            }
        }
    }
}

private struct CommentRow: View {
    let comment: BookComment

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Circle()
                .fill(comment.avatarColor.gradient)
                .overlay(
                    Text(comment.initials)
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white)
                )
                .frame(width: 28, height: 28)

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 5) {
                    Text(comment.name)
                        .font(.system(size: 12, weight: .semibold))
                    Text("·")
                        .foregroundStyle(.tertiary)
                    Text(comment.time)
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                }
                Text(comment.message)
                    .font(.system(size: 12))
                    .foregroundStyle(.primary.opacity(0.85))
            }
            Spacer(minLength: 0)
        }
        .padding(10)
        .background(Color.gray.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
    }
}
