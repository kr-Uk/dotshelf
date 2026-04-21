import SwiftUI

enum ReadingStatus: CaseIterable {
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
    var likes: Int = 0
    var isLikedByMe: Bool = false
    /// 대댓글 — 명세 §6에서 depth 1까지만 허용 (대댓글에는 또 다른 replies 없음).
    var replies: [BookComment] = []
}

/// 독서기록 공개 범위 — 명세 §6.
enum RecordVisibility: CaseIterable {
    case `public`
    case followers
    case `private`

    var label: String {
        switch self {
        case .public: return "전체 공개"
        case .followers: return "팔로워 공개"
        case .private: return "비공개"
        }
    }

    var systemImage: String {
        switch self {
        case .public: return "globe"
        case .followers: return "person.2"
        case .private: return "lock"
        }
    }
}

struct BookRecord {
    let title: String
    let author: String
    let publisher: String
    let summary: String     // 책 소개 — 후면에 표시
    var rating: Double      // 0 ~ 5, 0.5 단위
    let startedAt: String?
    let finishedAt: String?
    var status: ReadingStatus
    let personalNote: String
    var comments: [BookComment]
    var liked: Bool = false
    var isArchived: Bool = false
    /// 독서기록 본문 — 빈 문자열이면 미작성 상태. 명세 §6 내부 상한 10,000자.
    var readingRecord: String = ""
    var recordLikes: Int = 0
    var isRecordLikedByMe: Bool = false
    var recordVisibility: RecordVisibility = .public

    /// 책 이름 해시 기반 결정적 샘플. 실제 데이터 연동 전까지 사용.
    static func sample(for bookName: String) -> BookRecord {
        let templates: [BookRecord] = [
            BookRecord(
                title: "달러구트 꿈 백화점",
                author: "이미예",
                publisher: "팩토리나인",
                summary: "잠들어야만 입장할 수 있는 꿈 백화점에서, 사람들은 자신에게 꼭 맞는 꿈을 구매한다. 잠든 밤마다 펼쳐지는 따뜻한 판타지.",
                rating: 4.5,
                startedAt: "2026-02-14",
                finishedAt: "2026-03-10",
                status: .completed,
                personalNote: "꿈이라는 소재를 이렇게 따뜻하게 풀어내다니. 잠자기 전에 읽기 좋은 책.",
                comments: [
                    BookComment(name: "지호", initials: "J", avatarColor: .orange, time: "2일 전", message: "이 책 나도 완독! 2권도 추천", likes: 3, replies: [
                        BookComment(name: "나", initials: "나", avatarColor: .accentColor, time: "2일 전", message: "2권은 결이 좀 다르다고 하던데, 시도해볼게요")
                    ]),
                    BookComment(name: "수민", initials: "S", avatarColor: .cyan, time: "어제", message: "대기자 수 많던데 ㅠ", likes: 1),
                ],
                readingRecord: """
이 책은 \"꿈을 산다\"는 기발한 설정을 통해 일상 속 작은 위로를 말한다. 달러구트 씨가 꿈 백화점의 주인으로 사람들에게 꼭 필요한 꿈을 건네는 장면들은, 내가 오늘 하루를 어떤 감정으로 닫고 싶은지를 돌아보게 했다.

특히 좋았던 챕터는 \"누구에게나 필요한 꿈\" 부분. 잊고 있던 사람의 목소리를 꿈에서 듣는 장면에서 한참을 멈춰 있었다. 잠드는 시간을 조금 더 좋아하게 된다.
"""
            ),
            BookRecord(
                title: "코스모스",
                author: "칼 세이건",
                publisher: "사이언스북스",
                summary: "우주의 탄생에서 인류의 자리까지, 과학과 시의 언어로 풀어낸 현대의 고전. 별의 먼지에서 시작된 우리 이야기.",
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
                publisher: "인플루엔셜",
                summary: "삶과 죽음 사이, 자정의 도서관에서는 살아보지 못한 인생들을 하나씩 펼쳐볼 수 있다. 후회와 가능성에 대한 이야기.",
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
                publisher: "문학동네",
                summary: "제주 4·3의 기억을 담아낸 한강의 장편. 흩어진 이들의 이야기를 눈 덮인 풍경 위에 조용히 포갠다.",
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
                publisher: "김영사",
                summary: "인지 혁명부터 과학 혁명까지, 호모 사피엔스가 지구를 지배하게 된 과정을 통사적으로 재구성한 대중 역사서.",
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
                publisher: "창비",
                summary: "감정을 느끼지 못하는 소년 윤재가 또 다른 상처를 지닌 친구를 만나며 세계를 배워 나가는 성장 소설.",
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

/// 책 시장/검색 카탈로그 — 페이즈 C 스텝 4 mock용.
/// 각 항목은 기본 박스 책 파라미터(크기·색상 팔레트 인덱스)를 함께 가지고 있어 동일 제목은 시각적으로 일관.
struct CatalogBook: Identifiable {
    let id = UUID()
    let record: BookRecord
    let paletteIndex: Int
    let thickness: Float
    let height: Float
}

extension BookRecord {
    /// 정적 카탈로그. 실제 구현에선 API로 대체.
    static let searchCatalog: [CatalogBook] = [
        CatalogBook(
            record: BookRecord(
                title: "달러구트 꿈 백화점",
                author: "이미예",
                publisher: "팩토리나인",
                summary: "잠들어야만 입장할 수 있는 꿈 백화점의 따뜻한 판타지.",
                rating: 4.5, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 0, thickness: 0.034, height: 0.23
        ),
        CatalogBook(
            record: BookRecord(
                title: "코스모스",
                author: "칼 세이건",
                publisher: "사이언스북스",
                summary: "우주의 탄생부터 인류의 자리까지, 과학과 시의 언어로.",
                rating: 4.8, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 1, thickness: 0.042, height: 0.26
        ),
        CatalogBook(
            record: BookRecord(
                title: "미드나잇 라이브러리",
                author: "매트 헤이그",
                publisher: "인플루엔셜",
                summary: "살아보지 못한 인생을 선택할 수 있다면.",
                rating: 4.2, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 2, thickness: 0.030, height: 0.22
        ),
        CatalogBook(
            record: BookRecord(
                title: "작별하지 않는다",
                author: "한강",
                publisher: "문학동네",
                summary: "제주 4·3의 기억을 조용히 포개는 장편.",
                rating: 4.6, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 3, thickness: 0.028, height: 0.21
        ),
        CatalogBook(
            record: BookRecord(
                title: "사피엔스",
                author: "유발 하라리",
                publisher: "김영사",
                summary: "인지 혁명부터 과학 혁명까지 인류사의 큰 흐름.",
                rating: 4.4, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 4, thickness: 0.045, height: 0.27
        ),
        CatalogBook(
            record: BookRecord(
                title: "아몬드",
                author: "손원평",
                publisher: "창비",
                summary: "감정을 느끼지 못하는 소년의 성장 이야기.",
                rating: 4.3, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 5, thickness: 0.032, height: 0.22
        ),
        CatalogBook(
            record: BookRecord(
                title: "파친코",
                author: "이민진",
                publisher: "인플루엔셜",
                summary: "4대에 걸친 재일 한국인 가족의 대하 서사.",
                rating: 4.5, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 6, thickness: 0.044, height: 0.26
        ),
        CatalogBook(
            record: BookRecord(
                title: "팩트풀니스",
                author: "한스 로슬링",
                publisher: "김영사",
                summary: "세상은 우리가 생각하는 것보다 나아지고 있다.",
                rating: 4.1, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 7, thickness: 0.038, height: 0.24
        ),
        CatalogBook(
            record: BookRecord(
                title: "나미야 잡화점의 기적",
                author: "히가시노 게이고",
                publisher: "현대문학",
                summary: "시간을 넘나드는 편지가 닿는 작은 잡화점.",
                rating: 4.4, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 8, thickness: 0.036, height: 0.23
        ),
        CatalogBook(
            record: BookRecord(
                title: "오직 두 사람",
                author: "김영하",
                publisher: "문학동네",
                summary: "예기치 못한 관계들이 빚는 일곱 편의 단편.",
                rating: 4.0, startedAt: nil, finishedAt: nil,
                status: .notStarted, personalNote: "", comments: []
            ),
            paletteIndex: 9, thickness: 0.028, height: 0.22
        ),
    ]
}

