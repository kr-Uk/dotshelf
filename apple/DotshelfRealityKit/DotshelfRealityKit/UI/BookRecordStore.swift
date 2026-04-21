import Foundation
import Observation
import SwiftUI  // BookComment.avatarColor는 Color 타입이라 addComment/addReply에서 필요

/// 책 name(Entity name) → BookRecord in-memory 저장소.
/// MVP 단계에서는 샘플 시드로 시작해 메모리에만 유지. 향후 페이즈 C 이후 SwiftData로 영속화.
@Observable
final class BookRecordStore {
    private var records: [String: BookRecord] = [:]

    /// 해당 책의 레코드 조회. 없으면 샘플 시드 후 반환.
    func record(for bookName: String) -> BookRecord {
        if let existing = records[bookName] { return existing }
        let seeded = BookRecord.sample(for: bookName)
        records[bookName] = seeded
        return seeded
    }

    /// 부분 업데이트.
    func update(_ bookName: String, mutate: (inout BookRecord) -> Void) {
        var current = record(for: bookName)
        mutate(&current)
        records[bookName] = current
    }

    /// 완전 제거 (책 삭제 시 사용).
    func remove(_ bookName: String) {
        records.removeValue(forKey: bookName)
    }

    // MARK: - 독서기록 / 댓글 헬퍼 (페이즈 B)

    /// 댓글 추가 (depth 0).
    func addComment(_ bookName: String, message: String) {
        let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        update(bookName) { rec in
            rec.comments.append(BookComment(
                name: "나",
                initials: "나",
                avatarColor: .accentColor,
                time: "방금 전",
                message: trimmed
            ))
        }
    }

    /// 대댓글 추가 (depth 1 강제 — 대댓글에는 또 replies 넣지 않음).
    func addReply(_ bookName: String, parentCommentId: UUID, message: String) {
        let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        update(bookName) { rec in
            guard let idx = rec.comments.firstIndex(where: { $0.id == parentCommentId }) else { return }
            rec.comments[idx].replies.append(BookComment(
                name: "나",
                initials: "나",
                avatarColor: .accentColor,
                time: "방금 전",
                message: trimmed
            ))
        }
    }

    /// 댓글/대댓글 좋아요 토글. parentId가 nil이면 루트 댓글, 아니면 해당 루트의 대댓글.
    func toggleCommentLike(_ bookName: String, commentId: UUID, parentCommentId: UUID? = nil) {
        update(bookName) { rec in
            if let parentId = parentCommentId {
                guard let pIdx = rec.comments.firstIndex(where: { $0.id == parentId }),
                      let rIdx = rec.comments[pIdx].replies.firstIndex(where: { $0.id == commentId }) else { return }
                var reply = rec.comments[pIdx].replies[rIdx]
                reply.isLikedByMe.toggle()
                reply.likes += reply.isLikedByMe ? 1 : -1
                if reply.likes < 0 { reply.likes = 0 }
                rec.comments[pIdx].replies[rIdx] = reply
            } else {
                guard let idx = rec.comments.firstIndex(where: { $0.id == commentId }) else { return }
                rec.comments[idx].isLikedByMe.toggle()
                rec.comments[idx].likes += rec.comments[idx].isLikedByMe ? 1 : -1
                if rec.comments[idx].likes < 0 { rec.comments[idx].likes = 0 }
            }
        }
    }

    /// 독서기록 본문 업데이트 — 명세 §6 내부 상한 10,000자.
    func setReadingRecord(_ bookName: String, text: String) {
        let clipped = String(text.prefix(10_000))
        update(bookName) { $0.readingRecord = clipped }
    }

    /// 독서기록 공개 범위.
    func setRecordVisibility(_ bookName: String, visibility: RecordVisibility) {
        update(bookName) { $0.recordVisibility = visibility }
    }

    /// 독서기록 좋아요 토글.
    func toggleRecordLike(_ bookName: String) {
        update(bookName) { rec in
            rec.isRecordLikedByMe.toggle()
            rec.recordLikes += rec.isRecordLikedByMe ? 1 : -1
            if rec.recordLikes < 0 { rec.recordLikes = 0 }
        }
    }
}
