using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public enum ExperienceFocusArea
    {
        QuarterView,
        Bookshelf,
        Guestbook,
        Notebook
    }

    public class BookshelfExperienceManager : MonoBehaviour
    {
        private const int GuestbookVisibleRows = 3;
        private const int GuestbookVisibleColumns = 3;
        private const int GuestbookMaxPageButtons = 4;
        private const int GuestbookMaxPinnedNotes = 96;
        private const float GuestbookPageFadeDuration = 0.18f;
        private const float GuestbookBoardPadding = 32f;
        private const float GuestbookNoteToPaginationGap = 28f;
        private const float GuestbookPaginationHeight = 40f;
        private const float GuestbookPaginationToPanelGap = 24f;
        private const float GuestbookInterfacePanelHeight = 184f;
        private const int BookAddMaxResultCount = 64;
        private const float BookAddResultRowHeight = 58f;
        private const float BookAddResultViewportHeight = 224f;
        private const int BookSearchMaxResultCount = 96;
        private const float BookSearchResultRowHeight = 46f;
        private const float BookSearchResultViewportHeight = 232f;
        private const int ShelfManageMaxRowCount = 24;
        private const float ShelfManageRowHeight = 44f;
        private const float ShelfManageViewportHeight = 150f;
        private static readonly Vector2 GuestbookBoardSize = new(1380f, 960f);
        private static readonly Vector2 GuestbookPreviewCardSize = new(248f, 142f);
        private static readonly Vector2 GuestbookFocusedNoteBaseSize = new(246f, 156f);
        private static readonly Vector2[] GuestbookPreviewOffsets =
        {
            new Vector2(-336f, 154f),
            new Vector2(286f, 126f),
            new Vector2(-38f, 12f),
            new Vector2(294f, -96f)
        };
        private static readonly float[] GuestbookPreviewRotations = { -3.4f, 2.2f, -1.6f, 3.1f };
        private static readonly float[] GuestbookPreviewScales = { 1.02f, 0.99f, 1.03f, 0.98f };
        private static readonly string[] GuestbookPreviewMessages =
        {
            "Window light was perfect.",
            "Hyunwoo stopped by at noon.",
            "Return the blue poetry book.",
            "Back next Friday."
        };
        private static readonly string[] GuestbookPreviewCaptions = { "Minji", "Hyunwoo", "Seoyeon", "Host" };
        private static readonly Vector2[] GuestbookFocusedNoteAnchors =
        {
            new Vector2(-0.84f, 0.74f),
            new Vector2(-0.08f, 0.82f),
            new Vector2(0.72f, 0.66f),
            new Vector2(-0.52f, 0.14f),
            new Vector2(0.08f, 0.22f),
            new Vector2(0.78f, 0.06f),
            new Vector2(-0.74f, -0.62f),
            new Vector2(-0.02f, -0.48f),
            new Vector2(0.62f, -0.72f)
        };
        private static readonly float[] GuestbookFocusedNoteRotations = { -3.8f, 2.6f, -4.4f, 1.7f, -1.6f, 3.8f, -2.7f, 4.1f, -3.2f };
        private static readonly float[] GuestbookFocusedNoteScales = { 1.01f, 0.98f, 1.04f, 0.97f, 1.00f, 1.03f, 0.98f, 1.05f, 0.96f };
        private static Sprite roundedUiSprite;
        private static Texture2D roundedUiTexture;

        private enum BookSearchFilter
        {
            All,
            Reading,
            Read,
            Liked
        }

        private enum ShelfBookSortMode
        {
            Title,
            Author,
            ReadingStatus,
            Liked
        }

        private enum BookshelfOverlayMode
        {
            None,
            NameEdit,
            DeleteConfirm,
            Design,
            BookAdd,
            BookAddConfirm,
            Manage,
            UnsavedConfirm
        }

        private enum BookshelfBottomNavTab
        {
            Home,
            Search,
            Add,
            Edit
        }

        private sealed class BookAddCatalogItem
        {
            public BookAddCatalogItem(string title, string author, string publisher, string summary, BookOrientation orientation = BookOrientation.Front)
            {
                Title = title;
                Author = author;
                Publisher = publisher;
                Summary = summary;
                Orientation = orientation;
            }

            public string Title { get; }
            public string Author { get; }
            public string Publisher { get; }
            public string Summary { get; }
            public BookOrientation Orientation { get; }
        }

        private static readonly BookAddCatalogItem[] BookAddCatalog =
        {
            new BookAddCatalogItem("물고기는 존재하지 않는다", "룰루 밀러", "곰출판", "혼돈과 분류, 그리고 삶을 바라보는 태도를 다루는 과학 에세이입니다.", BookOrientation.Front),
            new BookAddCatalogItem("불편한 편의점", "김호연", "나무옆의자", "서울역 근처 편의점을 배경으로 사람들이 서로에게 건네는 위로를 담은 소설입니다."),
            new BookAddCatalogItem("아주 작은 습관의 힘", "제임스 클리어", "비즈니스북스", "작은 행동을 꾸준히 쌓아 큰 변화를 만드는 방법을 설명하는 자기계발서입니다."),
            new BookAddCatalogItem("도시와 그 불확실한 벽", "무라카미 하루키", "문학동네", "현실과 비현실 사이의 벽을 오가는 장편소설입니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("파과", "구병모", "위즈덤하우스", "나이 든 킬러의 삶과 균열을 밀도 있게 그린 장편소설입니다."),
            new BookAddCatalogItem("역행자", "자청", "웅진지식하우스", "일과 성장의 규칙을 저자만의 방식으로 풀어낸 자기계발서입니다."),
            new BookAddCatalogItem("소년이 온다", "한강", "창비", "상처와 기억을 정면으로 응시하는 장편소설입니다.", BookOrientation.Front),
            new BookAddCatalogItem("채식주의자", "한강", "창비", "한 사람의 선택이 가족과 관계에 남기는 균열을 다룹니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("아몬드", "손원평", "창비", "감정을 느끼기 어려운 소년이 세상과 관계 맺는 과정을 따라갑니다."),
            new BookAddCatalogItem("달러구트 꿈 백화점", "이미예", "팩토리나인", "꿈을 사고파는 백화점을 배경으로 한 따뜻한 판타지입니다.", BookOrientation.Front),
            new BookAddCatalogItem("밝은 밤", "최은영", "문학동네", "세대와 시간을 넘어 이어지는 여성들의 기억을 그립니다."),
            new BookAddCatalogItem("구의 증명", "최진영", "은행나무", "상실 이후에도 남는 사랑과 기억을 밀도 있게 다룹니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("메리골드 마음 세탁소", "윤정은", "북로망스", "마음을 세탁한다는 상상으로 위로를 건네는 소설입니다."),
            new BookAddCatalogItem("어서 오세요, 휴남동 서점입니다", "황보름", "클레이하우스", "서점을 중심으로 사람들이 회복되는 과정을 담았습니다.", BookOrientation.Front),
            new BookAddCatalogItem("모순", "양귀자", "쓰다", "삶의 모순과 선택을 담담하게 바라보는 장편소설입니다."),
            new BookAddCatalogItem("종의 기원", "정유정", "은행나무", "인간 내면의 어둠을 추적하는 심리 스릴러입니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("7년의 밤", "정유정", "은행나무", "비극의 밤 이후 이어지는 죄책감과 추적을 그립니다."),
            new BookAddCatalogItem("살인자의 기억법", "김영하", "복복서가", "기억을 잃어가는 인물의 시점으로 전개되는 서스펜스입니다."),
            new BookAddCatalogItem("여행의 이유", "김영하", "문학동네", "여행과 삶의 태도를 돌아보는 산문집입니다.", BookOrientation.Front),
            new BookAddCatalogItem("언어의 온도", "이기주", "말글터", "일상 속 말과 글이 지닌 온기를 짚어보는 에세이입니다."),
            new BookAddCatalogItem("죽고 싶지만 떡볶이는 먹고 싶어", "백세희", "흔", "상담 기록을 바탕으로 마음의 굴곡을 솔직하게 담았습니다."),
            new BookAddCatalogItem("나는 나로 살기로 했다", "김수현", "클레이하우스", "타인의 기준에서 벗어나 자신을 지키는 태도를 말합니다."),
            new BookAddCatalogItem("기분이 태도가 되지 않게", "레몬심리", "갤리온", "감정을 다루고 관계를 안정시키는 실용 심리서입니다."),
            new BookAddCatalogItem("데미안", "헤르만 헤세", "민음사", "자기 자신에게 이르는 성장과 방황의 고전입니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("싯다르타", "헤르만 헤세", "민음사", "깨달음을 향한 여정을 간결하고 상징적으로 그립니다."),
            new BookAddCatalogItem("이방인", "알베르 카뮈", "민음사", "부조리한 세계와 인간의 태도를 묻는 소설입니다."),
            new BookAddCatalogItem("노인과 바다", "어니스트 헤밍웨이", "민음사", "한 노인의 고독한 싸움을 통해 의지와 존엄을 보여줍니다.", BookOrientation.Front),
            new BookAddCatalogItem("1984", "조지 오웰", "민음사", "감시와 통제 사회를 그린 디스토피아 고전입니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("동물농장", "조지 오웰", "민음사", "권력과 혁명의 변질을 우화로 풀어낸 작품입니다."),
            new BookAddCatalogItem("멋진 신세계", "올더스 헉슬리", "문예출판사", "안정과 행복이 통제되는 미래 사회를 다룹니다."),
            new BookAddCatalogItem("사피엔스", "유발 하라리", "김영사", "인류의 역사를 큰 흐름으로 조망하는 인문 교양서입니다.", BookOrientation.Front),
            new BookAddCatalogItem("총 균 쇠", "재레드 다이아몬드", "문학사상", "문명의 격차를 환경과 역사 조건으로 설명합니다."),
            new BookAddCatalogItem("팩트풀니스", "한스 로슬링", "김영사", "세상을 데이터로 더 정확히 보는 법을 제안합니다."),
            new BookAddCatalogItem("코스모스", "칼 세이건", "사이언스북스", "우주와 인간의 위치를 아름답게 연결한 과학 고전입니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("정의란 무엇인가", "마이클 샌델", "와이즈베리", "정의와 공정성에 대한 철학적 질문을 대중적으로 풀어냅니다."),
            new BookAddCatalogItem("돈의 심리학", "모건 하우절", "인플루엔셜", "돈을 대하는 행동과 심리를 짧은 이야기로 설명합니다."),
            new BookAddCatalogItem("원씽", "게리 켈러", "비즈니스북스", "가장 중요한 한 가지에 집중하는 실행 방식을 제안합니다."),
            new BookAddCatalogItem("딥 워크", "칼 뉴포트", "민음사", "방해를 줄이고 깊게 몰입하는 업무 방식을 설명합니다."),
            new BookAddCatalogItem("클린 코드", "로버트 C. 마틴", "인사이트", "읽기 좋은 소프트웨어 코드를 만드는 원칙을 정리합니다.", BookOrientation.Front),
            new BookAddCatalogItem("리팩터링", "마틴 파울러", "한빛미디어", "동작을 유지하면서 코드 구조를 개선하는 기법을 다룹니다."),
            new BookAddCatalogItem("실용주의 프로그래머", "앤드류 헌트", "인사이트", "소프트웨어 장인정신과 실용적 개발 태도를 말합니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("오브젝트", "조영호", "위키북스", "객체지향 설계의 책임과 협력을 국내 예제로 설명합니다."),
            new BookAddCatalogItem("함께 자라기", "김창준", "인사이트", "개발자와 팀이 학습하고 성장하는 방법을 다룹니다."),
            new BookAddCatalogItem("컨티뉴어스 디스커버리", "테레사 토레스", "한빛미디어", "고객 학습을 제품 의사결정에 연결하는 지속적 발견 방식을 설명합니다.", BookOrientation.Front),
            new BookAddCatalogItem("도메인 주도 설계", "에릭 에반스", "위키북스", "복잡한 비즈니스 도메인을 모델 중심으로 풀어내는 설계 고전입니다."),
            new BookAddCatalogItem("가상 면접 사례로 배우는 대규모 시스템 설계 기초", "알렉스 쉬", "인사이트", "확장 가능한 시스템 설계 문제를 단계별로 다룹니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("사용자를 생각하게 하지 마!", "스티브 크룩", "인사이트", "웹과 앱 사용성을 직관적으로 개선하는 UX 기본서입니다.", BookOrientation.Front),
            new BookAddCatalogItem("디자인과 인간 심리", "도널드 노먼", "학지사", "사람이 제품을 이해하고 사용하는 방식을 디자인 관점에서 설명합니다."),
            new BookAddCatalogItem("훅", "니르 이얄", "유엑스리뷰", "사용 습관을 만드는 제품 루프와 윤리적 고려를 함께 다룹니다."),
            new BookAddCatalogItem("해빗 메카닉", "제임스 클리어", "비즈니스북스", "행동 변화를 만들기 위한 반복과 환경 설계 원리를 정리합니다."),
            new BookAddCatalogItem("기록하기로 했습니다", "김신지", "휴머니스트", "일상의 감정과 장면을 꾸준히 남기는 태도를 말하는 에세이입니다.", BookOrientation.Front),
            new BookAddCatalogItem("최소한의 이웃", "허지웅", "김영사", "사회와 관계를 바라보는 시선을 담은 산문집입니다."),
            new BookAddCatalogItem("나의 하루는 4시 30분에 시작된다", "김유진", "토네이도", "시간 관리와 루틴을 실천적으로 다룬 자기계발서입니다."),
            new BookAddCatalogItem("몰입", "황농문", "알에이치코리아", "깊은 집중 상태를 만들고 유지하는 사고 방식을 설명합니다.", BookOrientation.Angled45),
            new BookAddCatalogItem("생각에 관한 생각", "대니얼 카너먼", "김영사", "인간 판단과 편향을 행동경제학 관점에서 풀어낸 책입니다."),
            new BookAddCatalogItem("타이탄의 도구들", "팀 페리스", "토네이도", "다양한 창작자와 리더의 습관과 전략을 모은 인터뷰 기반 자기계발서입니다."),
            new BookAddCatalogItem("브랜딩 불변의 법칙", "알 리스", "비즈니스맵", "브랜드 포지셔닝과 인식의 원칙을 짧고 명확하게 다룹니다."),
            new BookAddCatalogItem("하루키는 이렇게 쓴다", "나카무라 구니오", "현대문학", "무라카미 하루키 작품의 문장과 구조를 읽는 가이드입니다.", BookOrientation.Front)
        };

        private sealed class GuestbookNoteData
        {
            public string authorName;
            public string message;
            public string timestampLabel;
            public Color color;
        }

        [SerializeField] private BookshelfState state = null;
        [SerializeField] private CameraController cameraController = null;
        [SerializeField] private BookInteractionController interactionController = null;
        [SerializeField] private BookshelfGenerator bookshelfGenerator = null;
        [SerializeField] private BookshelfRuntimeDataStore dataStore = null;
        [SerializeField] private NotebookScreenController notebookScreenController = null;

        private Canvas hudCanvas;
        private Button overlayPreviousFocusButton;
        private Button overlayNextFocusButton;
        private Button bookshelfEditModeButton;
        private GameObject bookshelfIndexPanel;
        private Text bookshelfIndexText;
        private RectTransform bookshelfIndexDotsRoot;
        private Button addBookshelfButton;
        private Button openBookshelfNameEditButton;
        private Button openBookshelfDesignButton;
        private Button deleteBookshelfButton;
        private Button openBookAddButton;
        private Button openBookSearchButton;
        private Button openBookshelfManageButton;
        private GameObject bookshelfNameModalBackdrop;
        private GameObject bookshelfNameEditPanel;
        private Text bookshelfNameEditLabelText;
        private InputField bookshelfNameInputField;
        private Button saveBookshelfNameButton;
        private Button closeBookshelfNameEditButton;
        private GameObject bookshelfDeleteConfirmPanel;
        private Text bookshelfDeleteConfirmTitleText;
        private Text bookshelfDeleteConfirmMessageText;
        private Button confirmDeleteBookshelfButton;
        private Button cancelDeleteBookshelfButton;
        private GameObject bookshelfDesignPanel;
        private Text bookshelfDesignTitleText;
        private Text bookshelfDesignHintText;
        private Button defaultBookshelfDesignButton;
        private Button warmBookshelfDesignButton;
        private Button darkBookshelfDesignButton;
        private Button closeBookshelfDesignButton;
        private GameObject bookAddPanel;
        private Text bookAddTitleText;
        private Text bookAddHintText;
        private Text bookAddStatusText;
        private InputField bookAddSearchInputField;
        private Button closeBookAddButton;
        private RectTransform bookAddRowsContent;
        private ScrollRect bookAddScrollRect;
        private Scrollbar bookAddScrollbar;
        private readonly Button[] bookAddResultButtons = new Button[BookAddMaxResultCount];
        private readonly Text[] bookAddResultTexts = new Text[BookAddMaxResultCount];
        private readonly int[] bookAddResultCatalogIndices = new int[BookAddMaxResultCount];
        private GameObject bookAddConfirmPanel;
        private Text bookAddConfirmTitleText;
        private Text bookAddConfirmMetaText;
        private Text bookAddConfirmSummaryText;
        private Button bookAddConfirmUnreadButton;
        private Button bookAddConfirmReadingButton;
        private Button bookAddConfirmReadButton;
        private Button bookAddConfirmSpineButton;
        private Button bookAddConfirmAngledButton;
        private Button bookAddConfirmFrontButton;
        private Button confirmBookAddButton;
        private Button cancelBookAddConfirmButton;
        private GameObject bookSearchPanel;
        private Text bookSearchTitleText;
        private Text bookSearchStatusText;
        private InputField bookSearchInputField;
        private Button closeBookSearchButton;
        private Button bookSearchAllFilterButton;
        private Button bookSearchReadingFilterButton;
        private Button bookSearchReadFilterButton;
        private Button bookSearchLikedFilterButton;
        private RectTransform bookSearchRowsContent;
        private ScrollRect bookSearchScrollRect;
        private Scrollbar bookSearchScrollbar;
        private readonly Button[] bookSearchResultButtons = new Button[BookSearchMaxResultCount];
        private readonly Text[] bookSearchResultTexts = new Text[BookSearchMaxResultCount];
        private readonly List<BookEntity> bookSearchResults = new List<BookEntity>();
        private GameObject bookshelfManagePanel;
        private Text bookshelfManageTitleText;
        private Text bookshelfManageSummaryText;
        private Text bookshelfManageStatusText;
        private RectTransform bookshelfManageRowsContent;
        private ScrollRect bookshelfManageScrollRect;
        private Scrollbar bookshelfManageScrollbar;
        private readonly Button[] bookshelfManageRowButtons = new Button[ShelfManageMaxRowCount];
        private readonly Text[] bookshelfManageRowTexts = new Text[ShelfManageMaxRowCount];
        private Button shelfManageSortTitleButton;
        private Button shelfManageSortAuthorButton;
        private Button shelfManageSortStatusButton;
        private Button shelfManageSortLikedButton;
        private Button shelfManageVisibilityButton;
        private Button shelfManageAddButton;
        private Button shelfManageCloseButton;
        private GameObject bookshelfUnsavedConfirmPanel;
        private Text bookshelfUnsavedConfirmTitleText;
        private Text bookshelfUnsavedConfirmMessageText;
        private Button confirmBookshelfUnsavedSaveButton;
        private Button cancelBookshelfUnsavedSaveButton;
        private GameObject bookshelfStatusPanel;
        private Text bookshelfStatusTitleText;
        private Text bookshelfStatusMessageText;
        private Button bookshelfStatusAddButton;
        private int bookshelfNameInputIndex = -1;
        private int pendingDeleteBookshelfIndex = -1;
        private int pendingBookAddCatalogIndex = -1;
        private BookOrientation pendingBookAddOrientation = BookOrientation.Front;
        private App.Domain.ReadingStatus pendingBookAddReadingStatus = App.Domain.ReadingStatus.Unread;
        private bool isBookshelfNameEditPanelOpen;
        private bool isBookshelfDeleteConfirmPanelOpen;
        private bool isBookshelfDesignPanelOpen;
        private bool isBookAddPanelOpen;
        private bool isBookAddConfirmPanelOpen;
        private bool isBookSearchPanelOpen;
        private bool isBookshelfManagePanelOpen;
        private bool isBookshelfUnsavedConfirmPanelOpen;
        private bool hasUnsavedBookshelfEditChanges;
        private System.Action pendingBookshelfNavigationAfterSave;
        private BookSearchFilter activeBookSearchFilter = BookSearchFilter.All;
        private string bookAddFeedbackMessage = string.Empty;
        private bool shouldResetBookAddScrollToTop;
        private bool shouldResetBookSearchScrollToTop;
        private readonly List<Image> bookshelfIndexDots = new List<Image>();

        private GameObject guestbookPanel;
        private GameObject guestbookPreviewPanel;
        private Canvas guestbookBoardCanvas;
        [SerializeField] private bool guestbookOwnerMode = true;
        private bool isGuestbookPublic = true;
        private int guestbookVisitorCount = 123;
        private int guestbookLikeCount = 45;
        private readonly List<GuestbookNoteData> guestbookNotes = new List<GuestbookNoteData>();
        private readonly RectTransform[] guestbookNoteCards = new RectTransform[12];
        private readonly Text[] guestbookNoteMessageTexts = new Text[12];
        private readonly Text[] guestbookNoteAuthorTexts = new Text[12];
        private readonly Button[] guestbookReplyButtons = new Button[12];
        private readonly Button[] guestbookPageButtons = new Button[GuestbookMaxPageButtons];
        private readonly int[] guestbookPageButtonPageIndices = new int[GuestbookMaxPageButtons];
        private Text guestbookVisitorStatsText;
        private Text guestbookLikeStatsText;
        private InputField guestbookInputField;
        private Button guestbookSubmitButton;
        private Text guestbookCharCountText;
        private Text guestbookBoardStatusText;
        private Text guestbookEmptyStateText;
        private Text guestbookPrivacyText;
        private Button guestbookVisibilityToggleButton;
        private Button guestbookPreviousPageButton;
        private Button guestbookNextPageButton;
        private RectTransform guestbookInterfacePanelRect;
        private RectTransform guestbookHeaderRowRect;
        private RectTransform guestbookPaginationRect;
        private RectTransform guestbookInputRowRect;
        private RectTransform guestbookGridRect;
        private RectTransform guestbookGridOverlayRect;
        private CanvasGroup guestbookGridCanvasGroup;
        private Coroutine guestbookPageFadeCoroutine;
        private int guestbookCurrentPageIndex;
        private int guestbookColumnCount = 3;
        private int guestbookCardsPerPage = 9;
        private Vector2 guestbookCurrentCardSize = GuestbookFocusedNoteBaseSize;
        private readonly Color[] guestbookNoteColors =
        {
            new Color32(255, 249, 196, 255),
            new Color32(232, 245, 233, 255),
            new Color32(243, 229, 245, 255)
        };

        private GameObject bookDetailPanel;
        private Text bookPanelEyebrowText;
        private Text bookTitleText;
        private Text bookMetaText;
        private Text bookPanelHintText;
        private Button readToggleButton;
        private Button visibilityToggleButton;
        private Button writeReportButton;
        private Button readReportButton;
        private Button detailPlacementButton;
        private Button closeBookDetailButton;
        private GameObject bookFocusToolbarPanel;
        private Button bookFocusFrontButton;
        private Button bookFocusBackCoverButton;
        private Button bookFocusDetailButton;
        private Button bookFocusReportButton;
        private Button bookFocusCloseButton;
        private GameObject bookReportWritePanel;
        private Text bookReportWriteTitleText;
        private Text bookReportWriteBookText;
        private InputField bookReportTitleInputField;
        private InputField bookReportBodyInputField;
        private Button bookReportPublicButton;
        private Button bookReportPrivateButton;
        private Button saveBookReportButton;
        private Button cancelBookReportButton;
        private GameObject bookReportReadPanel;
        private Text bookReportReadTitleText;
        private Text bookReportReadMetaText;
        private Text bookReportReadBodyText;
        private Button editBookReportButton;
        private Button toggleReportVisibilityButton;
        private Button closeBookReportReadButton;
        private bool isBookReportWritePanelOpen;
        private bool isBookReportReadPanelOpen;
        private bool stagedBookReportPublic = true;
        private bool isFocusedBookShowingBackCover;
        private BookEntity activeReportBook;
        private GameObject bookEditPanel;
        private Text bookEditEyebrowText;
        private Text bookEditTitleText;
        private Text bookEditMetaText;
        private Text bookEditHintText;
        private Button deleteBookButton;
        private Button closeBookEditButton;
        private bool isBookEditPanelOpen;
        private BookEntity pendingDeleteBook;
        private GameObject sectionEditPanel;
        private Text sectionEditEyebrowText;
        private Text sectionEditTitleText;
        private Text sectionEditMetaText;
        private Text sectionEditHintText;
        private Button alignLeftButton;
        private Button alignCenterButton;
        private Button alignRightButton;
        private Button closeSectionEditButton;
        private ShelfSection activeEditSection;
        private Button openBookPlacementEditorButton;
        private GameObject bookPlacementEditorPanel;
        private Button placementSpineButton;
        private Button placementAngledButton;
        private Button placementFrontButton;
        private Button cancelPlacementButton;
        private Button confirmPlacementButton;
        private BookEntity placementEditBook;
        private BookOrientation placementOriginalOrientation;
        private BookOrientation placementStagedOrientation;
        private bool isBookPlacementEditorOpen;

        private GameObject hamburgerMenuPanel;
        private Button hamburgerButton;
        private Button focusBackButton;
        private bool isHamburgerMenuOpen;
        private GameObject bookshelfBottomNavPanel;
        private Button bookshelfNavHomeButton;
        private Button bookshelfNavSearchButton;
        private Button bookshelfNavAddButton;
        private Button bookshelfNavEditButton;

        public ExperienceFocusArea CurrentFocusArea { get; private set; } = ExperienceFocusArea.QuarterView;
        public bool IsBookshelfFocused => CurrentFocusArea == ExperienceFocusArea.Bookshelf;
        public bool IsBookshelfEditMode { get; private set; }
        public BookEntity ActiveDetailBook { get; private set; }

        public void Configure(
            BookshelfState bookshelfState,
            CameraController controller,
            BookInteractionController interaction,
            BookshelfRuntimeDataStore runtimeDataStore,
            BookshelfGenerator generator)
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }

            if (bookshelfGenerator != null)
            {
                bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
            }

            state = bookshelfState;
            cameraController = controller;
            interactionController = interaction;
            bookshelfGenerator = generator;
            dataStore = runtimeDataStore;

            if (isActiveAndEnabled)
            {
                if (cameraController != null)
                {
                    cameraController.ModeChanged += HandleCameraModeChanged;
                    cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                    SyncFromCameraMode(cameraController.CurrentMode);
                }

                if (bookshelfGenerator != null)
                {
                    bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
                }
            }
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHud();
        }

        private void Start()
        {
            if (bookshelfGenerator == null)
            {
                bookshelfGenerator = FindFirstObjectByType<BookshelfGenerator>();
                if (isActiveAndEnabled && bookshelfGenerator != null)
                {
                    bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
                    bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
                }
            }

            ConfigureNotebookScreen();
            EnsureGuestbookBoardUi();
            RefreshHud();
        }

        private void Update()
        {
            if (HandleFocusShortcuts())
            {
                return;
            }

            if (cameraController != null && cameraController.IsTransitioning)
            {
                return;
            }

            if (Mouse.current == null || IsPointerOverUi())
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (TryHitNotebook())
            {
                FocusNotebook();
                return;
            }

            if (TryHitGuestbookBoard())
            {
                FocusGuestbook();
                return;
            }

            if (CurrentFocusArea == ExperienceFocusArea.QuarterView && TryHitBookshelf())
            {
                FocusBookshelf();
                return;
            }
        }

        private void OnEnable()
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged += HandleCameraModeChanged;
                cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                SyncFromCameraMode(cameraController.CurrentMode);
            }

            if (bookshelfGenerator != null)
            {
                bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
            }
        }

        private void OnDisable()
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }

            if (bookshelfGenerator != null)
            {
                bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
            }
        }

        public void FocusQuarterView()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusQuarterViewImmediate))
            {
                return;
            }

            FocusQuarterViewImmediate();
        }

        private void FocusQuarterViewImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            PrepareQuarterViewReturn();
            cameraController.MoveToMode(CameraMode.Overview);
        }

        public void FocusBookshelf()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Frontal);
        }

        public void FocusGuestbook()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusGuestbookImmediate))
            {
                return;
            }

            FocusGuestbookImmediate();
        }

        private void FocusGuestbookImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelBookPlacementEdit(false);
            SetBookshelfEditMode(false);
            cameraController.MoveToMode(CameraMode.Whiteboard);
        }

        public void FocusNotebook()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusNotebookImmediate))
            {
                return;
            }

            FocusNotebookImmediate();
        }

        private void FocusNotebookImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelBookPlacementEdit(false);
            SetBookshelfEditMode(false);
            ConfigureNotebookScreen();
            if (CurrentFocusArea == ExperienceFocusArea.Notebook && notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(true);
                notebookScreenController.ActivateDashboard();
                return;
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(false);
                notebookScreenController.Show();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.QueueDashboardActivation();
            }

            if (notebookScreenController != null && notebookScreenController.ScreenTransform != null)
            {
                cameraController.MoveToNotebookScreen(notebookScreenController.ScreenTransform);
                return;
            }

            cameraController.MoveToMode(CameraMode.Notebook);
        }

        public void CloseNotebookFocus()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Overview);
        }

        private bool HandleFocusShortcuts()
        {
            if (cameraController != null && cameraController.IsTransitioning)
            {
                return true;
            }

            if (Keyboard.current == null)
            {
                return false;
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                FocusQuarterView();
                return true;
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                FocusBookshelf();
                return true;
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                FocusGuestbook();
                return true;
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                FocusNotebook();
                return true;
            }

            return false;
        }

        private bool CanStartCameraFocus()
        {
            return cameraController != null &&
                   !cameraController.IsTransitioning &&
                   (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
        }

        public bool CanHandleFrontalZoomInput()
        {
            var hasBlockingOverlay = HasOpenBookshelfModal() ||
                                     isBookSearchPanelOpen ||
                                     isBookEditPanelOpen ||
                                     isBookPlacementEditorOpen ||
                                     activeEditSection != null ||
                                     (bookDetailPanel != null && bookDetailPanel.activeSelf);
            return CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   state != null &&
                   !state.IsViewingBook &&
                   state.ActiveDraggedBook == null &&
                   !hasBlockingOverlay &&
                   !IsPointerOverUi();
        }

        private bool TryPromptUnsavedBookshelfChanges(System.Action afterSaveAction)
        {
            if (!IsBookshelfEditMode || !hasUnsavedBookshelfEditChanges || isBookshelfUnsavedConfirmPanelOpen)
            {
                return false;
            }

            pendingBookshelfNavigationAfterSave = afterSaveAction;
            SetBookshelfOverlayMode(BookshelfOverlayMode.UnsavedConfirm);
            RefreshHud();
            return true;
        }

        private void MarkBookshelfEditDirty()
        {
            if (IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = true;
            }
        }

        private void MarkBookshelfEditSaved()
        {
            hasUnsavedBookshelfEditChanges = false;
        }

        public void HandleBookViewed(BookEntity book)
        {
            CloseBookPlacementEditor(false);
            ActiveDetailBook = book;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            activeEditSection = null;
            isFocusedBookShowingBackCover = false;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            CloseBookReportPanelsSilently();
            RefreshHud();
        }

        public void ToggleFocusedBookDetail(BookEntity book)
        {
            if (bookDetailPanel != null && bookDetailPanel.activeSelf && ActiveDetailBook == book)
            {
                CloseBookDetailPanel();
                return;
            }

            OpenBookDetailPanel(book);
            RefreshHud();
        }

        public void OpenBookEditPanel(BookEntity book)
        {
            if (book == null ||
                dataStore == null ||
                bookEditPanel == null ||
                !IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching) ||
                (state != null && state.ActiveDraggedBook != null))
            {
                return;
            }

            CloseOverlayPanels(bookEditPanel);
            CloseBookPlacementEditor(false);
            ActiveDetailBook = book;
            pendingDeleteBook = null;
            isBookEditPanelOpen = true;
            bookEditPanel.SetActive(true);
            RefreshBookEditPanel();
            RefreshHud();
        }

        public void OpenSectionAlignmentPanel(ShelfSection section)
        {
            if (section == null ||
                sectionEditPanel == null ||
                !IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching) ||
                (state != null && state.ActiveDraggedBook != null))
            {
                return;
            }

            CloseOverlayPanels(sectionEditPanel);
            CloseBookPlacementEditor(false);
            ActiveDetailBook = null;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;
            activeEditSection = section;
            sectionEditPanel.SetActive(true);
            RefreshSectionEditPanel();
            RefreshHud();
        }

        public void HandleBookClosed()
        {
            ActiveDetailBook = null;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            activeEditSection = null;
            isFocusedBookShowingBackCover = false;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            CloseBookReportPanelsSilently();
            RefreshHud();
        }

        public BookshelfRuntimeSnapshotData CreateRuntimeSnapshot()
        {
            return dataStore != null
                ? dataStore.CreateSnapshot(CurrentFocusArea, ActiveDetailBook)
                : new BookshelfRuntimeSnapshotData();
        }

        private void OpenBookDetailPanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookDetailPanel == null)
            {
                return;
            }

            CloseOverlayPanels(bookDetailPanel);
            ActiveDetailBook = book;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;
            bookDetailPanel.SetActive(true);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void CloseBookDetailPanel()
        {
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            if (isBookEditPanelOpen)
            {
                ActiveDetailBook = null;
            }

            activeEditSection = null;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            RefreshHud();
        }

        private void OpenBookPlacementEditor()
        {
            if (state == null ||
                !state.IsViewingBook ||
                state.ActiveDraggedBook != null ||
                CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            var book = ActiveDetailBook != null ? ActiveDetailBook : state.ActiveViewedBook;
            if (book == null)
            {
                return;
            }

            CloseOverlayPanels(bookPlacementEditorPanel);
            placementEditBook = book;
            placementOriginalOrientation = book.orientation;
            placementStagedOrientation = book.orientation;
            isBookPlacementEditorOpen = true;
            ActiveDetailBook = null;
            activeEditSection = null;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;

            if (interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            RefreshHud();
        }

        private void PreviewBookPlacement(BookOrientation orientation)
        {
            if (!isBookPlacementEditorOpen ||
                placementEditBook == null ||
                state == null)
            {
                return;
            }

            state.ChangeBookOrientation(placementEditBook, orientation, this);
            placementStagedOrientation = placementEditBook.orientation;
            RefreshBookPlacementEditor();
        }

        private void ConfirmBookPlacementEdit()
        {
            CloseBookPlacementEditor(false);
            RefreshHud();
        }

        private void CancelBookPlacementEdit()
        {
            CancelBookPlacementEdit(true);
        }

        private void CancelBookPlacementEdit(bool refresh)
        {
            if (isBookPlacementEditorOpen &&
                placementEditBook != null &&
                state != null &&
                placementEditBook.orientation != placementOriginalOrientation)
            {
                state.ChangeBookOrientation(placementEditBook, placementOriginalOrientation, this);
            }

            CloseBookPlacementEditor(false);
            if (refresh)
            {
                RefreshHud();
            }
        }

        private void CloseBookPlacementEditor(bool refresh)
        {
            isBookPlacementEditorOpen = false;
            placementEditBook = null;
            placementOriginalOrientation = BookOrientation.Spine;
            placementStagedOrientation = BookOrientation.Spine;

            if (bookPlacementEditorPanel != null)
            {
                bookPlacementEditorPanel.SetActive(false);
            }

            if (openBookPlacementEditorButton != null)
            {
                openBookPlacementEditorButton.gameObject.SetActive(false);
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void DeleteActiveBook()
        {
            if (!isBookEditPanelOpen ||
                !IsBookshelfEditMode ||
                ActiveDetailBook == null ||
                state == null)
            {
                return;
            }

            if (pendingDeleteBook != ActiveDetailBook)
            {
                pendingDeleteBook = ActiveDetailBook;
                RefreshBookEditPanel();
                return;
            }

            var book = ActiveDetailBook;
            var sourceSection = book.CurrentSection;

            pendingDeleteBook = null;
            ActiveDetailBook = null;
            isBookEditPanelOpen = false;

            if (state.ActiveViewedBook == book && interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            state.RemoveBook(book);
            if (sourceSection != null)
            {
                state.LayoutSection(sourceSection, this, true);
            }

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            Destroy(book.gameObject);
            MarkBookshelfEditDirty();
            RefreshHud();
        }

        private void ToggleReadState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            meta.isRead = !meta.isRead;
            meta.readingStatus = meta.isRead ? App.Domain.ReadingStatus.Read : App.Domain.ReadingStatus.Unread;
            dataStore.ApplyBookMeta(ActiveDetailBook, meta);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void ToggleVisibilityState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            meta.isPublic = !meta.isPublic;
            dataStore.ApplyBookMeta(ActiveDetailBook, meta);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private bool TryCloseBookDetailFromFocusedBookPointer()
        {
            if (bookDetailPanel == null ||
                !bookDetailPanel.activeSelf ||
                ActiveDetailBook == null ||
                Mouse.current == null ||
                Camera.main == null)
            {
                return false;
            }

            var pointer = Mouse.current.position.ReadValue();
            var ray = Camera.main.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hitArea = hits[i].collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null)
                {
                    if (hitArea.Owner == ActiveDetailBook)
                    {
                        CloseBookDetailPanel();
                        return true;
                    }

                    continue;
                }

                var book = hits[i].collider.GetComponentInParent<BookEntity>();
                if (book == null)
                {
                    continue;
                }

                if (book == ActiveDetailBook)
                {
                    CloseBookDetailPanel();
                    return true;
                }

                return false;
            }

            return false;
        }

        public void OpenGuestbookPanel()
        {
            EnsureGuestbookBoardUi();
            CloseOverlayPanels(guestbookPanel);
            SetGuestbookFocusPresentation(true);
            RefreshGuestbookPanel();
        }

        public void CloseGuestbookPanel()
        {
            if (guestbookPageFadeCoroutine != null)
            {
                StopCoroutine(guestbookPageFadeCoroutine);
                guestbookPageFadeCoroutine = null;
            }

            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(false);
            }

            SetGuestbookFocusPresentation(false);
        }

        private void HandleCameraModeChanged(CameraMode mode)
        {
            if (mode != CameraMode.Frontal &&
                state != null &&
                state.IsViewingBook &&
                interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            SyncFromCameraMode(mode);
        }

        private void HandleCameraTransitionCompleted(CameraMode mode)
        {
            if (mode == CameraMode.Notebook && CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.EnterImmersiveDashboard();
                }
                else
                {
                    ShowNotebookScreen();
                }
            }

            RefreshHud();
        }

        private void HandleBookshelfSwitchStateChanged()
        {
            if (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching && IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            RefreshHud();
        }

        private void SyncFromCameraMode(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Frontal:
                    CurrentFocusArea = ExperienceFocusArea.Bookshelf;
                    break;
                case CameraMode.Whiteboard:
                    CurrentFocusArea = ExperienceFocusArea.Guestbook;
                    break;
                case CameraMode.Notebook:
                    CurrentFocusArea = ExperienceFocusArea.Notebook;
                    break;
                default:
                    CurrentFocusArea = ExperienceFocusArea.QuarterView;
                    break;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf && IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                CancelBookPlacementEdit(false);
            }

            if (CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                CloseOverlayPanels();
                isHamburgerMenuOpen = false;
            }
            else
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.SetImmersiveMode(false);
                }

                ShowNotebookScreen();
            }

            if (CurrentFocusArea == ExperienceFocusArea.Guestbook)
            {
                OpenGuestbookPanel();
            }
            else
            {
                CloseGuestbookPanel();
            }

            RefreshHud();
        }

        private void EnsureHud()
        {
            var canvasObject = new GameObject("ExperienceHud");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            hamburgerButton = CreateButton(canvasObject.transform, "HamburgerButton", "Menu", new Vector2(-20f, -20f), ToggleHamburgerMenu, new Vector2(72f, 44f), new Vector2(1f, 1f));
            hamburgerButton.GetComponentInChildren<Text>().fontSize = 26;
            StyleHudPanel(hamburgerButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            focusBackButton = CreateButton(canvasObject.transform, "FocusBackButton", "←", new Vector2(34f, -34f), ReturnToQuarterView, new Vector2(66f, 66f), new Vector2(0f, 1f));
            StyleHudPanel(focusBackButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            hamburgerMenuPanel = CreatePanel(canvasObject.transform, "HamburgerMenuPanel", new Vector2(-20f, -74f), new Vector2(190f, 204f), new Vector2(1f, 1f));
            StyleHudPanel(hamburgerMenuPanel.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.84f));
            CreateButton(hamburgerMenuPanel.transform, "QuarterViewButton", "Quarter View", new Vector2(-10f, -10f), () => RunMenuAction(FocusQuarterView));
            CreateButton(hamburgerMenuPanel.transform, "BookshelfButton", "Bookshelf", new Vector2(-10f, -58f), () => RunMenuAction(FocusBookshelf));
            CreateButton(hamburgerMenuPanel.transform, "NotebookButton", "Notebook", new Vector2(-10f, -106f), () => RunMenuAction(FocusNotebook));
            CreateButton(hamburgerMenuPanel.transform, "GuestbookButton", "Guestbook", new Vector2(-10f, -154f), () => RunMenuAction(FocusGuestbook));
            overlayPreviousFocusButton = CreateButton(canvasObject.transform, "OverlayPreviousFocusButton", "‹", new Vector2(220f, 0f), ShowPreviousBookshelf, new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            overlayNextFocusButton = CreateButton(canvasObject.transform, "OverlayNextFocusButton", "›", new Vector2(-220f, 0f), ShowNextBookshelf, new Vector2(52f, 52f), new Vector2(1f, 0.5f));
            PrepareBookshelfNavigationButton(overlayPreviousFocusButton);
            PrepareBookshelfNavigationButton(overlayNextFocusButton);

            bookshelfEditModeButton = CreateButton(canvasObject.transform, "BookshelfEditModeButton", "저장", new Vector2(-70f, -34f), ToggleBookshelfEditMode, new Vector2(86f, 44f), new Vector2(1f, 1f));
            bookshelfEditModeButton.GetComponentInChildren<Text>().fontSize = 15;

            bookshelfIndexPanel = CreatePanel(canvasObject.transform, "BookshelfIndexPanel", new Vector2(0f, -18f), new Vector2(252f, 72f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfIndexPanel.GetComponent<Image>());
            bookshelfIndexText = CreateLabel(bookshelfIndexPanel.transform, "BookshelfIndexText", new Vector2(0f, -14f), new Vector2(212f, 28f), TextAnchor.MiddleCenter);
            var bookshelfIndexTextRect = bookshelfIndexText.GetComponent<RectTransform>();
            if (bookshelfIndexTextRect != null)
            {
                bookshelfIndexTextRect.anchorMin = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.anchorMax = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.pivot = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.anchoredPosition = new Vector2(0f, -14f);
                bookshelfIndexTextRect.sizeDelta = new Vector2(212f, 28f);
            }

            bookshelfIndexText.fontSize = 19;
            bookshelfIndexText.fontStyle = FontStyle.Bold;
            bookshelfIndexText.color = new Color32(63, 47, 38, 255);
            bookshelfIndexDotsRoot = CreateHudContainer(bookshelfIndexPanel.transform, "BookshelfIndexDots", new Vector2(0f, -52f), new Vector2(112f, 8f), new Vector2(0.5f, 1f));
            ConfigureHorizontalLayout(bookshelfIndexDotsRoot, TextAnchor.MiddleCenter, 6f);

            bookshelfBottomNavPanel = CreatePanel(canvasObject.transform, "BookshelfBottomNavPanel", new Vector2(0f, 28f), new Vector2(600f, 90f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookshelfBottomNavPanel.GetComponent<Image>());
            bookshelfNavHomeButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavHomeButton", "⌂\n책장", 0, FocusBookshelfHomeFromBottom);
            bookshelfNavSearchButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavSearchButton", "⌕\n검색", 1, ToggleBookSearchPanel);
            bookshelfNavAddButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavAddButton", "+\n추가", 2, OpenBookAddFromBottom);
            bookshelfNavEditButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavEditButton", "✎\n편집", 3, ToggleBookshelfEditMode);

            addBookshelfButton = CreateButton(canvasObject.transform, "AddBookshelfButton", "책장 추가", new Vector2(-128f, -68f), AddBookshelf, new Vector2(92f, 34f), new Vector2(0.5f, 1f));
            addBookshelfButton.GetComponentInChildren<Text>().fontSize = 14;

            openBookshelfNameEditButton = CreateButton(canvasObject.transform, "OpenBookshelfNameEditButton", "이름 변경", new Vector2(0f, -68f), OpenBookshelfNameEditPanel, new Vector2(128f, 34f), new Vector2(0.5f, 1f));
            openBookshelfNameEditButton.GetComponentInChildren<Text>().fontSize = 14;

            deleteBookshelfButton = CreateButton(canvasObject.transform, "DeleteBookshelfButton", "책장 삭제", new Vector2(128f, -68f), DeleteActiveBookshelf, new Vector2(92f, 34f), new Vector2(0.5f, 1f));
            deleteBookshelfButton.GetComponentInChildren<Text>().fontSize = 14;

            openBookshelfDesignButton = CreateButton(canvasObject.transform, "OpenBookshelfDesignButton", "디자인변경", new Vector2(-184f, -34f), OpenBookshelfDesignPanel, new Vector2(122f, 44f), new Vector2(1f, 1f));
            openBookshelfDesignButton.GetComponentInChildren<Text>().fontSize = 14;

            openBookAddButton = CreateButton(canvasObject.transform, "OpenBookAddButton", "책 추가", new Vector2(-268f, -20f), OpenBookAddPanel, new Vector2(104f, 44f), new Vector2(1f, 1f));
            openBookAddButton.GetComponentInChildren<Text>().fontSize = 15;

            openBookSearchButton = CreateButton(canvasObject.transform, "OpenBookSearchButton", "검색", new Vector2(-144f, -20f), ToggleBookSearchPanel, new Vector2(94f, 44f), new Vector2(1f, 1f));
            openBookSearchButton.GetComponentInChildren<Text>().fontSize = 15;

            openBookshelfManageButton = CreateButton(canvasObject.transform, "OpenBookshelfManageButton", "책장관리", new Vector2(-312f, -34f), OpenBookshelfManagePanel, new Vector2(112f, 44f), new Vector2(1f, 1f));
            openBookshelfManageButton.GetComponentInChildren<Text>().fontSize = 14;

            bookshelfNameModalBackdrop = CreateModalBackdrop(canvasObject.transform, "BookshelfNameModalBackdrop", new Color(0.16f, 0.11f, 0.07f, 0.36f));
            bookshelfNameModalBackdrop.SetActive(false);

            bookshelfNameEditPanel = CreatePanel(canvasObject.transform, "BookshelfNameEditPanel", new Vector2(0f, -118f), new Vector2(380f, 92f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfNameEditPanel.GetComponent<Image>());
            bookshelfNameEditPanel.SetActive(false);
            bookshelfNameEditLabelText = CreateLabel(bookshelfNameEditPanel.transform, "BookshelfNameEditLabelText", new Vector2(20f, -20f), new Vector2(340f, 18f), TextAnchor.UpperLeft);
            bookshelfNameEditLabelText.text = "책장 이름";
            bookshelfNameEditLabelText.fontSize = 13;
            bookshelfNameEditLabelText.fontStyle = FontStyle.Bold;
            bookshelfNameEditLabelText.color = new Color32(151, 104, 64, 255);
            bookshelfNameInputField = CreateHudInputField(bookshelfNameEditPanel.transform, "BookshelfNameInputField", new Vector2(8f, -46f), new Vector2(270f, 34f), "책장 이름");
            bookshelfNameInputField.characterLimit = 18;
            saveBookshelfNameButton = CreateButton(bookshelfNameEditPanel.transform, "SaveBookshelfNameButton", "저장", new Vector2(290f, -46f), SaveBookshelfName, new Vector2(54f, 34f), new Vector2(0f, 1f));
            closeBookshelfNameEditButton = CreateButton(bookshelfNameEditPanel.transform, "CloseBookshelfNameEditButton", "×", new Vector2(348f, -46f), CloseBookshelfNameEditPanel, new Vector2(28f, 34f), new Vector2(0f, 1f));

            bookshelfDeleteConfirmPanel = CreatePanel(canvasObject.transform, "BookshelfDeleteConfirmPanel", new Vector2(0f, -132f), new Vector2(388f, 146f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfDeleteConfirmPanel.GetComponent<Image>());
            bookshelfDeleteConfirmPanel.SetActive(false);
            CreateHudImage(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(184, 72, 66, 180));
            bookshelfDeleteConfirmTitleText = CreateLabel(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteConfirmTitleText", new Vector2(20f, -32f), new Vector2(348f, 24f), TextAnchor.UpperLeft);
            bookshelfDeleteConfirmTitleText.text = "책장 삭제";
            bookshelfDeleteConfirmTitleText.fontSize = 17;
            bookshelfDeleteConfirmTitleText.fontStyle = FontStyle.Bold;
            bookshelfDeleteConfirmTitleText.color = new Color32(84, 48, 42, 255);
            bookshelfDeleteConfirmMessageText = CreateLabel(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteConfirmMessageText", new Vector2(20f, -62f), new Vector2(348f, 56f), TextAnchor.UpperLeft);
            bookshelfDeleteConfirmMessageText.fontSize = 15;
            bookshelfDeleteConfirmMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfDeleteConfirmMessageText.color = new Color32(84, 74, 66, 255);
            confirmDeleteBookshelfButton = CreateButton(bookshelfDeleteConfirmPanel.transform, "ConfirmDeleteBookshelfButton", "삭제", new Vector2(198f, -106f), ConfirmDeleteBookshelf, new Vector2(82f, 32f), new Vector2(0f, 1f));
            cancelDeleteBookshelfButton = CreateButton(bookshelfDeleteConfirmPanel.transform, "CancelDeleteBookshelfButton", "취소", new Vector2(286f, -106f), CloseBookshelfDeleteConfirmPanel, new Vector2(82f, 32f), new Vector2(0f, 1f));

            bookshelfDesignPanel = CreatePanel(canvasObject.transform, "BookshelfDesignPanel", new Vector2(0f, -148f), new Vector2(480f, 210f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfDesignPanel.GetComponent<Image>());
            bookshelfDesignPanel.SetActive(false);
            CreateHudImage(bookshelfDesignPanel.transform, "BookshelfDesignAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookshelfDesignTitleText = CreateLabel(bookshelfDesignPanel.transform, "BookshelfDesignTitleText", new Vector2(24f, -36f), new Vector2(432f, 24f), TextAnchor.UpperLeft);
            bookshelfDesignTitleText.text = "책장 디자인 변경";
            bookshelfDesignTitleText.fontSize = 17;
            bookshelfDesignTitleText.fontStyle = FontStyle.Bold;
            bookshelfDesignTitleText.color = new Color32(63, 47, 38, 255);
            bookshelfDesignHintText = CreateLabel(bookshelfDesignPanel.transform, "BookshelfDesignHintText", new Vector2(24f, -70f), new Vector2(432f, 32f), TextAnchor.UpperLeft);
            bookshelfDesignHintText.text = "현재 책장에 적용할 분위기를 선택하세요.";
            bookshelfDesignHintText.fontSize = 14;
            bookshelfDesignHintText.color = new Color32(124, 111, 101, 255);
            defaultBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "DefaultBookshelfDesignButton", "기본", new Vector2(24f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Default), new Vector2(128f, 38f), new Vector2(0f, 1f));
            warmBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "WarmBookshelfDesignButton", "웜 우드", new Vector2(176f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Warm), new Vector2(128f, 38f), new Vector2(0f, 1f));
            darkBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "DarkBookshelfDesignButton", "다크 우드", new Vector2(328f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Dark), new Vector2(128f, 38f), new Vector2(0f, 1f));
            closeBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "CloseBookshelfDesignButton", "닫기", new Vector2(24f, -166f), CloseBookshelfDesignPanel, new Vector2(432f, 34f), new Vector2(0f, 1f));

            bookAddPanel = CreatePanel(canvasObject.transform, "BookAddPanel", new Vector2(0f, -128f), new Vector2(560f, 458f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookAddPanel.GetComponent<Image>());
            bookAddPanel.SetActive(false);
            CreateHudImage(bookAddPanel.transform, "BookAddAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(87, 139, 106, 170));
            bookAddTitleText = CreateLabel(bookAddPanel.transform, "BookAddTitleText", new Vector2(24f, -36f), new Vector2(456f, 24f), TextAnchor.UpperLeft);
            bookAddTitleText.text = "책 추가";
            bookAddTitleText.fontSize = 17;
            bookAddTitleText.fontStyle = FontStyle.Bold;
            bookAddTitleText.color = new Color32(44, 66, 53, 255);
            bookAddHintText = CreateLabel(bookAddPanel.transform, "BookAddHintText", new Vector2(24f, -70f), new Vector2(512f, 34f), TextAnchor.UpperLeft);
            bookAddHintText.text = "책을 검색해 선택하고, 배치와 독서 상태를 정한 뒤 현재 책장에 추가합니다.";
            bookAddHintText.fontSize = 13;
            bookAddHintText.color = new Color32(99, 113, 103, 255);
            bookAddSearchInputField = CreateHudInputField(bookAddPanel.transform, "BookAddSearchInputField", new Vector2(24f, -104f), new Vector2(512f, 36f), "제목 또는 저자 검색");
            bookAddSearchInputField.characterLimit = 40;
            bookAddSearchInputField.onValueChanged.AddListener(HandleBookAddSearchChanged);
            closeBookAddButton = CreateButton(bookAddPanel.transform, "CloseBookAddButton", "×", new Vector2(508f, -28f), CloseBookAddPanel, new Vector2(28f, 32f), new Vector2(0f, 1f));
            var addRowsViewport = CreateHudScrollViewport(bookAddPanel.transform, "BookAddRowsViewport", new Vector2(24f, -154f), new Vector2(500f, BookAddResultViewportHeight), new Vector2(0f, 1f));
            bookAddScrollRect = addRowsViewport.GetComponent<ScrollRect>();
            bookAddRowsContent = CreateHudScrollContent(addRowsViewport.transform, "BookAddRowsContent", new Vector2(500f, BookAddMaxResultCount * BookAddResultRowHeight));
            bookAddScrollbar = CreateHudVerticalScrollbar(bookAddPanel.transform, "BookAddScrollbar", new Vector2(532f, -154f), new Vector2(8f, BookAddResultViewportHeight), new Vector2(0f, 1f));
            StyleHudScrollbar(bookAddScrollbar, new Color32(87, 139, 106, 220), new Color32(238, 231, 221, 160));
            if (bookAddScrollRect != null)
            {
                bookAddScrollRect.viewport = addRowsViewport.GetComponent<RectTransform>();
                bookAddScrollRect.content = bookAddRowsContent;
                bookAddScrollRect.verticalScrollbar = bookAddScrollbar;
                bookAddScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < BookAddMaxResultCount; i++)
            {
                var resultSlot = i;
                bookAddResultCatalogIndices[i] = -1;
                bookAddResultButtons[i] = CreateButton(bookAddRowsContent, $"BookAddResultButton_{i}", string.Empty, new Vector2(0f, -i * BookAddResultRowHeight), () => OpenBookAddConfirmPanel(resultSlot), new Vector2(500f, 50f), new Vector2(0f, 1f));
                bookAddResultTexts[i] = bookAddResultButtons[i].GetComponentInChildren<Text>();
                if (bookAddResultTexts[i] != null)
                {
                    bookAddResultTexts[i].fontSize = 13;
                    bookAddResultTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookAddResultTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                    bookAddResultTexts[i].verticalOverflow = VerticalWrapMode.Truncate;
                    var labelRect = bookAddResultTexts[i].rectTransform;
                    labelRect.offsetMin = new Vector2(16f, 2f);
                    labelRect.offsetMax = new Vector2(-14f, -2f);
                    labelRect.anchoredPosition = Vector2.zero;
                    labelRect.sizeDelta = Vector2.zero;
                }
            }

            bookAddStatusText = CreateLabel(bookAddPanel.transform, "BookAddStatusText", new Vector2(24f, -406f), new Vector2(512f, 20f), TextAnchor.UpperLeft);
            bookAddStatusText.fontSize = 13;
            bookAddStatusText.color = new Color32(99, 113, 103, 255);

            bookAddConfirmPanel = CreatePanel(canvasObject.transform, "BookAddConfirmPanel", new Vector2(0f, -132f), new Vector2(500f, 408f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookAddConfirmPanel.GetComponent<Image>());
            bookAddConfirmPanel.SetActive(false);
            CreateHudImage(bookAddConfirmPanel.transform, "BookAddConfirmAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(87, 139, 106, 170));
            bookAddConfirmTitleText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmTitleText", new Vector2(24f, -38f), new Vector2(452f, 40f), TextAnchor.UpperLeft);
            bookAddConfirmTitleText.fontSize = 20;
            bookAddConfirmTitleText.fontStyle = FontStyle.Bold;
            bookAddConfirmTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookAddConfirmTitleText.color = new Color32(44, 66, 53, 255);
            bookAddConfirmMetaText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmMetaText", new Vector2(24f, -88f), new Vector2(452f, 24f), TextAnchor.UpperLeft);
            bookAddConfirmMetaText.fontSize = 14;
            bookAddConfirmMetaText.color = new Color32(99, 113, 103, 255);
            bookAddConfirmSummaryText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmSummaryText", new Vector2(24f, -124f), new Vector2(452f, 62f), TextAnchor.UpperLeft);
            bookAddConfirmSummaryText.fontSize = 13;
            bookAddConfirmSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookAddConfirmSummaryText.color = new Color32(84, 74, 66, 255);
            var readingStatusLabel = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmReadingStatusLabel", new Vector2(24f, -196f), new Vector2(452f, 18f), TextAnchor.UpperLeft);
            readingStatusLabel.text = "독서 상태";
            readingStatusLabel.fontSize = 12;
            readingStatusLabel.color = new Color32(99, 113, 103, 255);
            bookAddConfirmUnreadButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmUnreadButton", "안 읽음", new Vector2(24f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Unread), new Vector2(104f, 34f), new Vector2(0f, 1f));
            bookAddConfirmReadingButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmReadingButton", "읽는 중", new Vector2(138f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Reading), new Vector2(104f, 34f), new Vector2(0f, 1f));
            bookAddConfirmReadButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmReadButton", "읽음", new Vector2(252f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Read), new Vector2(104f, 34f), new Vector2(0f, 1f));
            var placementLabel = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmPlacementLabel", new Vector2(24f, -270f), new Vector2(452f, 18f), TextAnchor.UpperLeft);
            placementLabel.text = "책 배치";
            placementLabel.fontSize = 12;
            placementLabel.color = new Color32(99, 113, 103, 255);
            bookAddConfirmSpineButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmSpineButton", "측면", new Vector2(24f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Spine), new Vector2(92f, 34f), new Vector2(0f, 1f));
            bookAddConfirmAngledButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmAngledButton", "45°", new Vector2(126f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Angled45), new Vector2(92f, 34f), new Vector2(0f, 1f));
            bookAddConfirmFrontButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmFrontButton", "정면", new Vector2(228f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Front), new Vector2(92f, 34f), new Vector2(0f, 1f));
            confirmBookAddButton = CreateButton(bookAddConfirmPanel.transform, "ConfirmBookAddButton", "추가", new Vector2(324f, -350f), ConfirmAddCatalogBookToShelf, new Vector2(72f, 34f), new Vector2(0f, 1f));
            cancelBookAddConfirmButton = CreateButton(bookAddConfirmPanel.transform, "CancelBookAddConfirmButton", "취소", new Vector2(404f, -350f), CloseBookAddConfirmPanel, new Vector2(72f, 34f), new Vector2(0f, 1f));

            bookSearchPanel = CreatePanel(canvasObject.transform, "BookSearchPanel", new Vector2(24f, -96f), new Vector2(360f, 438f), new Vector2(0f, 1f));
            StyleBookDetailPanel(bookSearchPanel.GetComponent<Image>());
            bookSearchPanel.SetActive(false);
            CreateHudImage(bookSearchPanel.transform, "BookSearchAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(73, 117, 147, 170));
            bookSearchTitleText = CreateLabel(bookSearchPanel.transform, "BookSearchTitleText", new Vector2(20f, -36f), new Vector2(286f, 24f), TextAnchor.UpperLeft);
            bookSearchTitleText.text = "책 찾기";
            bookSearchTitleText.fontSize = 18;
            bookSearchTitleText.fontStyle = FontStyle.Bold;
            bookSearchTitleText.color = new Color32(45, 50, 58, 255);
            closeBookSearchButton = CreateButton(bookSearchPanel.transform, "CloseBookSearchButton", "×", new Vector2(312f, -28f), CloseBookSearchPanel, new Vector2(28f, 32f), new Vector2(0f, 1f));
            bookSearchInputField = CreateHudInputField(bookSearchPanel.transform, "BookSearchInputField", new Vector2(20f, -76f), new Vector2(320f, 36f), "제목 또는 저자");
            bookSearchInputField.characterLimit = 40;
            bookSearchInputField.onValueChanged.AddListener(HandleBookSearchChanged);
            bookSearchAllFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchAllFilterButton", "전체", new Vector2(20f, -124f), () => SetBookSearchFilter(BookSearchFilter.All), new Vector2(68f, 30f), new Vector2(0f, 1f));
            bookSearchReadingFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchReadingFilterButton", "읽는 중", new Vector2(94f, -124f), () => SetBookSearchFilter(BookSearchFilter.Reading), new Vector2(78f, 30f), new Vector2(0f, 1f));
            bookSearchReadFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchReadFilterButton", "완독", new Vector2(178f, -124f), () => SetBookSearchFilter(BookSearchFilter.Read), new Vector2(68f, 30f), new Vector2(0f, 1f));
            bookSearchLikedFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchLikedFilterButton", "좋아요", new Vector2(252f, -124f), () => SetBookSearchFilter(BookSearchFilter.Liked), new Vector2(88f, 30f), new Vector2(0f, 1f));
            var searchRowsViewport = CreateHudScrollViewport(bookSearchPanel.transform, "BookSearchRowsViewport", new Vector2(20f, -166f), new Vector2(306f, BookSearchResultViewportHeight), new Vector2(0f, 1f));
            bookSearchScrollRect = searchRowsViewport.GetComponent<ScrollRect>();
            bookSearchRowsContent = CreateHudScrollContent(searchRowsViewport.transform, "BookSearchRowsContent", new Vector2(306f, BookSearchMaxResultCount * BookSearchResultRowHeight));
            bookSearchScrollbar = CreateHudVerticalScrollbar(bookSearchPanel.transform, "BookSearchScrollbar", new Vector2(332f, -166f), new Vector2(8f, BookSearchResultViewportHeight), new Vector2(0f, 1f));
            if (bookSearchScrollRect != null)
            {
                bookSearchScrollRect.viewport = searchRowsViewport.GetComponent<RectTransform>();
                bookSearchScrollRect.content = bookSearchRowsContent;
                bookSearchScrollRect.verticalScrollbar = bookSearchScrollbar;
                bookSearchScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < BookSearchMaxResultCount; i++)
            {
                var resultSlot = i;
                bookSearchResultButtons[i] = CreateButton(bookSearchRowsContent, $"BookSearchResultButton_{i}", string.Empty, new Vector2(0f, -i * BookSearchResultRowHeight), () => OpenBookSearchResult(resultSlot), new Vector2(306f, 38f), new Vector2(0f, 1f));
                bookSearchResultTexts[i] = bookSearchResultButtons[i].GetComponentInChildren<Text>();
                if (bookSearchResultTexts[i] != null)
                {
                    bookSearchResultTexts[i].fontSize = 12;
                    bookSearchResultTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookSearchResultTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                    bookSearchResultTexts[i].verticalOverflow = VerticalWrapMode.Truncate;
                    bookSearchResultTexts[i].rectTransform.offsetMin = new Vector2(14f, 1f);
                    bookSearchResultTexts[i].rectTransform.offsetMax = new Vector2(-12f, -1f);
                }
            }

            bookSearchStatusText = CreateLabel(bookSearchPanel.transform, "BookSearchStatusText", new Vector2(20f, -410f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            bookSearchStatusText.fontSize = 12;
            bookSearchStatusText.color = new Color32(99, 113, 103, 255);

            bookshelfManagePanel = CreatePanel(canvasObject.transform, "BookshelfManagePanel", new Vector2(0f, -128f), new Vector2(560f, 456f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfManagePanel.GetComponent<Image>());
            bookshelfManagePanel.SetActive(false);
            CreateHudImage(bookshelfManagePanel.transform, "BookshelfManageAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookshelfManageTitleText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageTitleText", new Vector2(24f, -38f), new Vector2(512f, 24f), TextAnchor.UpperLeft);
            bookshelfManageTitleText.text = "책장 관리";
            bookshelfManageTitleText.fontSize = 18;
            bookshelfManageTitleText.fontStyle = FontStyle.Bold;
            bookshelfManageTitleText.color = new Color32(63, 47, 38, 255);
            CreateHudImage(bookshelfManagePanel.transform, "BookshelfManageSummaryCard", new Vector2(24f, -66f), new Vector2(512f, 42f), new Vector2(0f, 1f), new Color32(255, 248, 236, 205));
            bookshelfManageSummaryText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageSummaryText", new Vector2(40f, -76f), new Vector2(480f, 24f), TextAnchor.UpperLeft);
            bookshelfManageSummaryText.fontSize = 13;
            bookshelfManageSummaryText.fontStyle = FontStyle.Bold;
            bookshelfManageSummaryText.color = new Color32(84, 74, 66, 255);
            var listLabel = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageListLabel", new Vector2(24f, -114f), new Vector2(220f, 18f), TextAnchor.UpperLeft);
            listLabel.text = "책장 목록";
            listLabel.fontSize = 12;
            listLabel.color = new Color32(124, 111, 101, 255);
            var manageRowsViewport = CreateHudScrollViewport(bookshelfManagePanel.transform, "BookshelfManageRowsViewport", new Vector2(24f, -136f), new Vector2(500f, ShelfManageViewportHeight), new Vector2(0f, 1f));
            bookshelfManageScrollRect = manageRowsViewport.GetComponent<ScrollRect>();
            bookshelfManageRowsContent = CreateHudScrollContent(manageRowsViewport.transform, "BookshelfManageRowsContent", new Vector2(500f, ShelfManageMaxRowCount * ShelfManageRowHeight));
            bookshelfManageScrollbar = CreateHudVerticalScrollbar(bookshelfManagePanel.transform, "BookshelfManageScrollbar", new Vector2(532f, -136f), new Vector2(8f, ShelfManageViewportHeight), new Vector2(0f, 1f));
            StyleHudScrollbar(bookshelfManageScrollbar, new Color32(197, 128, 67, 220), new Color32(238, 231, 221, 160));
            if (bookshelfManageScrollRect != null)
            {
                bookshelfManageScrollRect.viewport = manageRowsViewport.GetComponent<RectTransform>();
                bookshelfManageScrollRect.content = bookshelfManageRowsContent;
                bookshelfManageScrollRect.verticalScrollbar = bookshelfManageScrollbar;
                bookshelfManageScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < ShelfManageMaxRowCount; i++)
            {
                var rowIndex = i;
                bookshelfManageRowButtons[i] = CreateButton(bookshelfManageRowsContent, $"BookshelfManageRowButton_{i}", string.Empty, new Vector2(0f, -i * ShelfManageRowHeight), () => SelectManagedBookshelf(rowIndex), new Vector2(500f, 38f), new Vector2(0f, 1f));
                bookshelfManageRowTexts[i] = bookshelfManageRowButtons[i].GetComponentInChildren<Text>();
                if (bookshelfManageRowTexts[i] != null)
                {
                    bookshelfManageRowTexts[i].fontSize = 13;
                    bookshelfManageRowTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookshelfManageRowTexts[i].rectTransform.offsetMin = new Vector2(14f, 1f);
                    bookshelfManageRowTexts[i].rectTransform.offsetMax = new Vector2(-12f, -1f);
                }
            }

            var sortLabel = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageSortLabel", new Vector2(24f, -302f), new Vector2(220f, 18f), TextAnchor.UpperLeft);
            sortLabel.text = "현재 책장 정렬";
            sortLabel.fontSize = 12;
            sortLabel.color = new Color32(124, 111, 101, 255);
            shelfManageSortTitleButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortTitleButton", "제목순", new Vector2(24f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Title), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortAuthorButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortAuthorButton", "저자순", new Vector2(136f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Author), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortStatusButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortStatusButton", "상태순", new Vector2(248f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.ReadingStatus), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortLikedButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortLikedButton", "좋아요", new Vector2(360f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Liked), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageVisibilityButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageVisibilityButton", "공개 범위", new Vector2(24f, -370f), CycleManagedBookshelfVisibility, new Vector2(144f, 34f), new Vector2(0f, 1f));
            shelfManageAddButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageAddButton", "추가", new Vector2(180f, -370f), AddBookshelfFromManagePanel, new Vector2(96f, 34f), new Vector2(0f, 1f));
            shelfManageCloseButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageCloseButton", "닫기", new Vector2(288f, -370f), CloseBookshelfManagePanel, new Vector2(96f, 34f), new Vector2(0f, 1f));
            bookshelfManageStatusText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageStatusText", new Vector2(24f, -414f), new Vector2(512f, 28f), TextAnchor.UpperLeft);
            bookshelfManageStatusText.fontSize = 12;
            bookshelfManageStatusText.color = new Color32(124, 111, 101, 255);

            bookshelfUnsavedConfirmPanel = CreatePanel(canvasObject.transform, "BookshelfUnsavedConfirmPanel", new Vector2(0f, -166f), new Vector2(452f, 166f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfUnsavedConfirmPanel.GetComponent<Image>());
            bookshelfUnsavedConfirmPanel.SetActive(false);
            CreateHudImage(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookshelfUnsavedConfirmTitleText = CreateLabel(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmTitleText", new Vector2(24f, -38f), new Vector2(404f, 24f), TextAnchor.UpperLeft);
            bookshelfUnsavedConfirmTitleText.text = "수정된 사항이 있습니다.";
            bookshelfUnsavedConfirmTitleText.fontSize = 18;
            bookshelfUnsavedConfirmTitleText.fontStyle = FontStyle.Bold;
            bookshelfUnsavedConfirmTitleText.color = new Color32(63, 47, 38, 255);
            bookshelfUnsavedConfirmMessageText = CreateLabel(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmMessageText", new Vector2(24f, -74f), new Vector2(404f, 42f), TextAnchor.UpperLeft);
            bookshelfUnsavedConfirmMessageText.text = "저장 후 이동하시겠습니까?";
            bookshelfUnsavedConfirmMessageText.fontSize = 15;
            bookshelfUnsavedConfirmMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfUnsavedConfirmMessageText.color = new Color32(84, 74, 66, 255);
            confirmBookshelfUnsavedSaveButton = CreateButton(bookshelfUnsavedConfirmPanel.transform, "ConfirmBookshelfUnsavedSaveButton", "저장 후 이동", new Vector2(188f, -118f), ConfirmBookshelfUnsavedNavigation, new Vector2(122f, 34f), new Vector2(0f, 1f));
            cancelBookshelfUnsavedSaveButton = CreateButton(bookshelfUnsavedConfirmPanel.transform, "CancelBookshelfUnsavedSaveButton", "취소", new Vector2(322f, -118f), CancelBookshelfUnsavedNavigation, new Vector2(82f, 34f), new Vector2(0f, 1f));

            bookshelfStatusPanel = CreatePanel(canvasObject.transform, "BookshelfStatusPanel", new Vector2(24f, 24f), new Vector2(320f, 128f), new Vector2(0f, 0f));
            StyleBookDetailPanel(bookshelfStatusPanel.GetComponent<Image>());
            bookshelfStatusPanel.SetActive(false);
            bookshelfStatusTitleText = CreateLabel(bookshelfStatusPanel.transform, "BookshelfStatusTitleText", new Vector2(18f, -20f), new Vector2(284f, 22f), TextAnchor.UpperLeft);
            bookshelfStatusTitleText.fontSize = 17;
            bookshelfStatusTitleText.fontStyle = FontStyle.Bold;
            bookshelfStatusTitleText.color = new Color32(49, 42, 36, 255);
            bookshelfStatusMessageText = CreateLabel(bookshelfStatusPanel.transform, "BookshelfStatusMessageText", new Vector2(18f, -50f), new Vector2(284f, 36f), TextAnchor.UpperLeft);
            bookshelfStatusMessageText.fontSize = 13;
            bookshelfStatusMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfStatusMessageText.color = new Color32(84, 74, 66, 255);
            bookshelfStatusAddButton = CreateButton(bookshelfStatusPanel.transform, "BookshelfStatusAddButton", "책 추가", new Vector2(18f, -92f), OpenBookAddFromStatusPanel, new Vector2(96f, 28f), new Vector2(0f, 1f));

            bookDetailPanel = CreatePanel(canvasObject.transform, "BookDetailPanel", new Vector2(-24f, -78f), new Vector2(380f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookDetailPanel.GetComponent<Image>());
            bookDetailPanel.SetActive(false);
            CreateHudImage(bookDetailPanel.transform, "BookPanelAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookPanelEyebrowText = CreateLabel(bookDetailPanel.transform, "BookPanelEyebrowText", new Vector2(20f, -30f), new Vector2(340f, 18f), TextAnchor.UpperLeft);
            bookPanelEyebrowText.fontSize = 13;
            bookPanelEyebrowText.fontStyle = FontStyle.Bold;
            bookPanelEyebrowText.color = new Color32(151, 104, 64, 255);
            bookTitleText = CreateLabel(bookDetailPanel.transform, "BookTitleText", new Vector2(20f, -54f), new Vector2(340f, 46f), TextAnchor.UpperLeft);
            bookTitleText.fontSize = 22;
            bookTitleText.fontStyle = FontStyle.Bold;
            bookTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookTitleText.color = new Color32(49, 42, 36, 255);
            bookMetaText = CreateLabel(bookDetailPanel.transform, "BookMetaText", new Vector2(20f, -112f), new Vector2(340f, 72f), TextAnchor.UpperLeft);
            bookMetaText.fontSize = 14;
            bookMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookMetaText.color = new Color32(84, 74, 66, 255);
            bookMetaText.lineSpacing = 1.08f;
            CreateHudImage(bookDetailPanel.transform, "BookPanelDivider", new Vector2(20f, -198f), new Vector2(340f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            bookPanelHintText = CreateLabel(bookDetailPanel.transform, "BookPanelHintText", new Vector2(20f, -214f), new Vector2(340f, 52f), TextAnchor.UpperLeft);
            bookPanelHintText.fontSize = 13;
            bookPanelHintText.color = new Color32(124, 111, 101, 255);
            bookPanelHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            writeReportButton = CreateButton(bookDetailPanel.transform, "WriteReportButton", "독후감 작성", new Vector2(20f, -282f), OpenBookReportWritePanel, new Vector2(104f, 32f), new Vector2(0f, 1f));
            readReportButton = CreateButton(bookDetailPanel.transform, "ReadReportButton", "독후감 보기", new Vector2(136f, -282f), OpenBookReportReadPanel, new Vector2(104f, 32f), new Vector2(0f, 1f));
            detailPlacementButton = CreateButton(bookDetailPanel.transform, "DetailPlacementButton", "배치 변경", new Vector2(252f, -282f), OpenBookPlacementEditor, new Vector2(108f, 32f), new Vector2(0f, 1f));
            readToggleButton = CreateButton(bookDetailPanel.transform, "ReadToggleButton", "상태 변경", new Vector2(20f, -326f), ToggleReadState, new Vector2(164f, 34f), new Vector2(0f, 1f));
            visibilityToggleButton = CreateButton(bookDetailPanel.transform, "VisibilityToggleButton", "공개 범위 변경", new Vector2(196f, -326f), ToggleVisibilityState, new Vector2(164f, 34f), new Vector2(0f, 1f));
            closeBookDetailButton = CreateButton(bookDetailPanel.transform, "CloseBookDetailButton", "닫기", new Vector2(20f, -380f), CloseBookDetailPanel, new Vector2(340f, 32f), new Vector2(0f, 1f));

            bookFocusToolbarPanel = CreatePanel(canvasObject.transform, "BookFocusToolbarPanel", new Vector2(0f, 24f), new Vector2(470f, 58f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookFocusToolbarPanel.GetComponent<Image>());
            bookFocusToolbarPanel.SetActive(false);
            bookFocusFrontButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusFrontButton", "앞면", new Vector2(14f, 0f), ShowFocusedBookFront, new Vector2(74f, 36f), new Vector2(0f, 0.5f));
            bookFocusBackCoverButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusBackCoverButton", "후면", new Vector2(94f, 0f), ShowFocusedBookBack, new Vector2(74f, 36f), new Vector2(0f, 0.5f));
            bookFocusDetailButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusDetailButton", "상세", new Vector2(174f, 0f), OpenActiveViewedBookDetail, new Vector2(74f, 36f), new Vector2(0f, 0.5f));
            bookFocusReportButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusReportButton", "독후감", new Vector2(254f, 0f), OpenActiveViewedBookReport, new Vector2(86f, 36f), new Vector2(0f, 0.5f));
            bookFocusCloseButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusCloseButton", "닫기", new Vector2(348f, 0f), CloseActiveViewedBook, new Vector2(108f, 36f), new Vector2(0f, 0.5f));

            bookReportWritePanel = CreatePanel(canvasObject.transform, "BookReportWritePanel", new Vector2(-24f, -78f), new Vector2(430f, 474f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookReportWritePanel.GetComponent<Image>());
            bookReportWritePanel.SetActive(false);
            CreateHudImage(bookReportWritePanel.transform, "BookReportWriteAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(88, 133, 117, 170));
            bookReportWriteTitleText = CreateLabel(bookReportWritePanel.transform, "BookReportWriteTitleText", new Vector2(20f, -36f), new Vector2(390f, 24f), TextAnchor.UpperLeft);
            bookReportWriteTitleText.text = "독후감 작성";
            bookReportWriteTitleText.fontSize = 18;
            bookReportWriteTitleText.fontStyle = FontStyle.Bold;
            bookReportWriteTitleText.color = new Color32(45, 50, 58, 255);
            bookReportWriteBookText = CreateLabel(bookReportWritePanel.transform, "BookReportWriteBookText", new Vector2(20f, -68f), new Vector2(390f, 28f), TextAnchor.UpperLeft);
            bookReportWriteBookText.fontSize = 13;
            bookReportWriteBookText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportWriteBookText.color = new Color32(84, 74, 66, 255);
            bookReportTitleInputField = CreateHudInputField(bookReportWritePanel.transform, "BookReportTitleInputField", new Vector2(20f, -112f), new Vector2(390f, 36f), "독후감 제목");
            bookReportTitleInputField.characterLimit = 40;
            bookReportBodyInputField = CreateHudInputField(bookReportWritePanel.transform, "BookReportBodyInputField", new Vector2(20f, -160f), new Vector2(390f, 180f), "내용을 입력하세요");
            bookReportBodyInputField.characterLimit = 500;
            bookReportBodyInputField.lineType = InputField.LineType.MultiLineNewline;
            if (bookReportBodyInputField.textComponent != null)
            {
                bookReportBodyInputField.textComponent.alignment = TextAnchor.UpperLeft;
            }

            bookReportPublicButton = CreateButton(bookReportWritePanel.transform, "BookReportPublicButton", "공개", new Vector2(20f, -356f), () => SetStagedReportVisibility(true), new Vector2(84f, 32f), new Vector2(0f, 1f));
            bookReportPrivateButton = CreateButton(bookReportWritePanel.transform, "BookReportPrivateButton", "비공개", new Vector2(112f, -356f), () => SetStagedReportVisibility(false), new Vector2(84f, 32f), new Vector2(0f, 1f));
            saveBookReportButton = CreateButton(bookReportWritePanel.transform, "SaveBookReportButton", "저장", new Vector2(250f, -420f), SaveBookReport, new Vector2(76f, 34f), new Vector2(0f, 1f));
            cancelBookReportButton = CreateButton(bookReportWritePanel.transform, "CancelBookReportButton", "취소", new Vector2(334f, -420f), CloseBookReportPanels, new Vector2(76f, 34f), new Vector2(0f, 1f));

            bookReportReadPanel = CreatePanel(canvasObject.transform, "BookReportReadPanel", new Vector2(-24f, -78f), new Vector2(430f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookReportReadPanel.GetComponent<Image>());
            bookReportReadPanel.SetActive(false);
            CreateHudImage(bookReportReadPanel.transform, "BookReportReadAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookReportReadTitleText = CreateLabel(bookReportReadPanel.transform, "BookReportReadTitleText", new Vector2(20f, -38f), new Vector2(390f, 44f), TextAnchor.UpperLeft);
            bookReportReadTitleText.fontSize = 20;
            bookReportReadTitleText.fontStyle = FontStyle.Bold;
            bookReportReadTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportReadTitleText.color = new Color32(49, 42, 36, 255);
            bookReportReadMetaText = CreateLabel(bookReportReadPanel.transform, "BookReportReadMetaText", new Vector2(20f, -92f), new Vector2(390f, 26f), TextAnchor.UpperLeft);
            bookReportReadMetaText.fontSize = 13;
            bookReportReadMetaText.color = new Color32(124, 111, 101, 255);
            CreateHudImage(bookReportReadPanel.transform, "BookReportReadDivider", new Vector2(20f, -132f), new Vector2(390f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            bookReportReadBodyText = CreateLabel(bookReportReadPanel.transform, "BookReportReadBodyText", new Vector2(20f, -150f), new Vector2(390f, 190f), TextAnchor.UpperLeft);
            bookReportReadBodyText.fontSize = 14;
            bookReportReadBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportReadBodyText.verticalOverflow = VerticalWrapMode.Truncate;
            bookReportReadBodyText.lineSpacing = 1.16f;
            bookReportReadBodyText.color = new Color32(63, 47, 38, 255);
            editBookReportButton = CreateButton(bookReportReadPanel.transform, "EditBookReportButton", "수정", new Vector2(20f, -382f), OpenBookReportWritePanel, new Vector2(86f, 32f), new Vector2(0f, 1f));
            toggleReportVisibilityButton = CreateButton(bookReportReadPanel.transform, "ToggleReportVisibilityButton", "공개 범위", new Vector2(118f, -382f), ToggleReportVisibility, new Vector2(112f, 32f), new Vector2(0f, 1f));
            closeBookReportReadButton = CreateButton(bookReportReadPanel.transform, "CloseBookReportReadButton", "닫기", new Vector2(322f, -382f), CloseBookReportPanels, new Vector2(88f, 32f), new Vector2(0f, 1f));

            openBookPlacementEditorButton = CreateButton(canvasObject.transform, "OpenBookPlacementEditorButton", "배치 변경", new Vector2(0f, 24f), OpenBookPlacementEditor, new Vector2(136f, 44f), new Vector2(0.5f, 0f));
            openBookPlacementEditorButton.GetComponentInChildren<Text>().fontSize = 16;
            openBookPlacementEditorButton.gameObject.SetActive(false);

            bookPlacementEditorPanel = CreatePanel(canvasObject.transform, "BookPlacementEditorPanel", new Vector2(0f, 24f), new Vector2(454f, 58f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookPlacementEditorPanel.GetComponent<Image>());
            bookPlacementEditorPanel.SetActive(false);
            placementSpineButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementSpineButton", "측면", new Vector2(14f, 0f), () => PreviewBookPlacement(BookOrientation.Spine), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            placementAngledButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementAngledButton", "45°", new Vector2(98f, 0f), () => PreviewBookPlacement(BookOrientation.Angled45), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            placementFrontButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementFrontButton", "정면", new Vector2(182f, 0f), () => PreviewBookPlacement(BookOrientation.Front), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            cancelPlacementButton = CreateButton(bookPlacementEditorPanel.transform, "CancelPlacementButton", "취소", new Vector2(282f, 0f), CancelBookPlacementEdit, new Vector2(70f, 36f), new Vector2(0f, 0.5f));
            confirmPlacementButton = CreateButton(bookPlacementEditorPanel.transform, "ConfirmPlacementButton", "확인", new Vector2(358f, 0f), ConfirmBookPlacementEdit, new Vector2(82f, 36f), new Vector2(0f, 0.5f));

            bookEditPanel = CreatePanel(canvasObject.transform, "BookEditPanel", new Vector2(-24f, -78f), new Vector2(360f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookEditPanel.GetComponent<Image>());
            bookEditPanel.SetActive(false);
            CreateHudImage(bookEditPanel.transform, "BookEditAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            bookEditEyebrowText = CreateLabel(bookEditPanel.transform, "BookEditEyebrowText", new Vector2(20f, -30f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            bookEditEyebrowText.fontSize = 13;
            bookEditEyebrowText.fontStyle = FontStyle.Bold;
            bookEditEyebrowText.color = new Color32(151, 104, 64, 255);
            bookEditTitleText = CreateLabel(bookEditPanel.transform, "BookEditTitleText", new Vector2(20f, -54f), new Vector2(320f, 46f), TextAnchor.UpperLeft);
            bookEditTitleText.fontSize = 20;
            bookEditTitleText.fontStyle = FontStyle.Bold;
            bookEditTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookEditTitleText.color = new Color32(49, 42, 36, 255);
            bookEditMetaText = CreateLabel(bookEditPanel.transform, "BookEditMetaText", new Vector2(20f, -112f), new Vector2(320f, 72f), TextAnchor.UpperLeft);
            bookEditMetaText.fontSize = 14;
            bookEditMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookEditMetaText.color = new Color32(84, 74, 66, 255);
            bookEditMetaText.lineSpacing = 1.08f;
            CreateHudImage(bookEditPanel.transform, "BookEditDivider", new Vector2(20f, -204f), new Vector2(320f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            bookEditHintText = CreateLabel(bookEditPanel.transform, "BookEditHintText", new Vector2(20f, -220f), new Vector2(320f, 42f), TextAnchor.UpperLeft);
            bookEditHintText.fontSize = 13;
            bookEditHintText.color = new Color32(124, 111, 101, 255);
            bookEditHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            deleteBookButton = CreateButton(bookEditPanel.transform, "DeleteBookButton", "책 삭제", new Vector2(20f, -336f), DeleteActiveBook, new Vector2(320f, 34f), new Vector2(0f, 1f));
            closeBookEditButton = CreateButton(bookEditPanel.transform, "CloseBookEditButton", "닫기", new Vector2(20f, -380f), CloseBookDetailPanel, new Vector2(320f, 32f), new Vector2(0f, 1f));

            sectionEditPanel = CreatePanel(canvasObject.transform, "SectionEditPanel", new Vector2(-24f, -78f), new Vector2(360f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(sectionEditPanel.GetComponent<Image>());
            sectionEditPanel.SetActive(false);
            CreateHudImage(sectionEditPanel.transform, "SectionEditAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170));
            sectionEditEyebrowText = CreateLabel(sectionEditPanel.transform, "SectionEditEyebrowText", new Vector2(20f, -30f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            sectionEditEyebrowText.fontSize = 13;
            sectionEditEyebrowText.fontStyle = FontStyle.Bold;
            sectionEditEyebrowText.color = new Color32(151, 104, 64, 255);
            sectionEditTitleText = CreateLabel(sectionEditPanel.transform, "SectionEditTitleText", new Vector2(20f, -54f), new Vector2(320f, 44f), TextAnchor.UpperLeft);
            sectionEditTitleText.fontSize = 20;
            sectionEditTitleText.fontStyle = FontStyle.Bold;
            sectionEditTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sectionEditTitleText.color = new Color32(49, 42, 36, 255);
            sectionEditMetaText = CreateLabel(sectionEditPanel.transform, "SectionEditMetaText", new Vector2(20f, -112f), new Vector2(320f, 88f), TextAnchor.UpperLeft);
            sectionEditMetaText.fontSize = 14;
            sectionEditMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sectionEditMetaText.color = new Color32(84, 74, 66, 255);
            sectionEditMetaText.lineSpacing = 1.08f;
            CreateHudImage(sectionEditPanel.transform, "SectionEditDivider", new Vector2(20f, -214f), new Vector2(320f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            sectionEditHintText = CreateLabel(sectionEditPanel.transform, "SectionEditHintText", new Vector2(20f, -230f), new Vector2(320f, 44f), TextAnchor.UpperLeft);
            sectionEditHintText.fontSize = 13;
            sectionEditHintText.color = new Color32(124, 111, 101, 255);
            sectionEditHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            alignLeftButton = CreateButton(sectionEditPanel.transform, "AlignLeftButton", "좌정렬", new Vector2(20f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Left), new Vector2(100f, 34f), new Vector2(0f, 1f));
            alignCenterButton = CreateButton(sectionEditPanel.transform, "AlignCenterButton", "가운데", new Vector2(130f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Center), new Vector2(100f, 34f), new Vector2(0f, 1f));
            alignRightButton = CreateButton(sectionEditPanel.transform, "AlignRightButton", "우정렬", new Vector2(240f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Right), new Vector2(100f, 34f), new Vector2(0f, 1f));
            closeSectionEditButton = CreateButton(sectionEditPanel.transform, "CloseSectionEditButton", "닫기", new Vector2(20f, -380f), CloseBookDetailPanel, new Vector2(320f, 32f), new Vector2(0f, 1f));

            guestbookPanel = null;

            RefreshHud();
        }

        private Button CreateBottomNavButton(
            Transform parent,
            string name,
            string label,
            int slotIndex,
            UnityEngine.Events.UnityAction onClick)
        {
            var x = -204f + slotIndex * 136f;
            var button = CreateButton(parent, name, label, new Vector2(x, 0f), onClick, new Vector2(116f, 68f), new Vector2(0.5f, 0.5f));
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 15;
                text.lineSpacing = 1.02f;
                text.alignment = TextAnchor.MiddleCenter;
            }

            return button;
        }

        private void ToggleHamburgerMenu()
        {
            isHamburgerMenuOpen = !isHamburgerMenuOpen;
            RefreshHud();
        }

        private void ToggleBookshelfEditMode()
        {
            if (IsBookshelfEditMode)
            {
                MarkBookshelfEditSaved();
            }

            SetBookshelfEditMode(!IsBookshelfEditMode);
        }

        private void ConfirmBookshelfUnsavedNavigation()
        {
            var nextAction = pendingBookshelfNavigationAfterSave;
            pendingBookshelfNavigationAfterSave = null;
            isBookshelfUnsavedConfirmPanelOpen = false;
            MarkBookshelfEditSaved();
            SetBookshelfEditMode(false);
            RefreshHud();
            nextAction?.Invoke();
        }

        private void CancelBookshelfUnsavedNavigation()
        {
            pendingBookshelfNavigationAfterSave = null;
            isBookshelfUnsavedConfirmPanelOpen = false;
            RefreshHud();
        }

        private void SetBookshelfOverlayMode(BookshelfOverlayMode mode)
        {
            isBookshelfNameEditPanelOpen = mode == BookshelfOverlayMode.NameEdit;
            isBookshelfDeleteConfirmPanelOpen = mode == BookshelfOverlayMode.DeleteConfirm;
            isBookshelfDesignPanelOpen = mode == BookshelfOverlayMode.Design;
            isBookAddPanelOpen = mode == BookshelfOverlayMode.BookAdd;
            isBookAddConfirmPanelOpen = mode == BookshelfOverlayMode.BookAddConfirm;
            isBookshelfManagePanelOpen = mode == BookshelfOverlayMode.Manage;
            isBookshelfUnsavedConfirmPanelOpen = mode == BookshelfOverlayMode.UnsavedConfirm;

            if (mode != BookshelfOverlayMode.NameEdit)
            {
                bookshelfNameInputIndex = -1;
            }

            if (mode != BookshelfOverlayMode.DeleteConfirm)
            {
                pendingDeleteBookshelfIndex = -1;
            }

            if (mode != BookshelfOverlayMode.BookAddConfirm)
            {
                pendingBookAddCatalogIndex = -1;
            }

            if (mode != BookshelfOverlayMode.BookAdd && mode != BookshelfOverlayMode.BookAddConfirm)
            {
                bookAddFeedbackMessage = string.Empty;
            }

            if (mode != BookshelfOverlayMode.UnsavedConfirm)
            {
                pendingBookshelfNavigationAfterSave = null;
            }
        }

        private void FocusBookshelfHomeFromBottom()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                FocusBookshelf();
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(FocusBookshelfHomeFromBottom))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            CloseOverlayPanels();
            CloseBookshelfNameEditPanel();
            CloseBookshelfDeleteConfirmPanel();
            CloseBookshelfDesignPanel();
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseBookshelfManagePanel();
            RefreshHud();
        }

        private void OpenBookAddFromBottom()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (state != null && state.IsViewingBook))
            {
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(OpenBookAddFromBottom))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            OpenBookAddPanel();
        }

        private void SetBookshelfEditMode(bool enabled)
        {
            var canEnable = enabled &&
                            CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                            (cameraController == null || !cameraController.IsTransitioning) &&
                            (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            var nextState = canEnable;
            if (IsBookshelfEditMode == nextState)
            {
                RefreshHud();
                return;
            }

            IsBookshelfEditMode = nextState;
            if (IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = false;
                CancelBookPlacementEdit(false);
                isHamburgerMenuOpen = false;
                ActiveDetailBook = null;
                isBookSearchPanelOpen = false;
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                bookSearchResults.Clear();
                if (state != null && state.IsViewingBook && interactionController != null)
                {
                    interactionController.CloseViewedBook(true);
                }

                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookEditPanel != null)
                {
                    bookEditPanel.SetActive(false);
                }

                if (bookSearchPanel != null)
                {
                    bookSearchPanel.SetActive(false);
                }

                if (sectionEditPanel != null)
                {
                    sectionEditPanel.SetActive(false);
                }

                activeEditSection = null;
            }
            else if (isBookEditPanelOpen)
            {
                ActiveDetailBook = null;
                pendingDeleteBook = null;
                isBookEditPanelOpen = false;
                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookEditPanel != null)
                {
                    bookEditPanel.SetActive(false);
                }
            }
            else if (sectionEditPanel != null && sectionEditPanel.activeSelf)
            {
                sectionEditPanel.SetActive(false);
                activeEditSection = null;
            }

            if (!IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = false;
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                CloseBookAddPanel(false);
                if (bookshelfManagePanel != null)
                {
                    bookshelfManagePanel.SetActive(false);
                }

                if (sectionEditPanel != null)
                {
                    sectionEditPanel.SetActive(false);
                }

                activeEditSection = null;
            }

            RefreshHud();
        }

        private void RunMenuAction(UnityEngine.Events.UnityAction action)
        {
            action?.Invoke();
            isHamburgerMenuOpen = false;
            RefreshHud();
        }

        private void ReturnToQuarterView()
        {
            FocusQuarterView();
        }

        private void PrepareQuarterViewReturn()
        {
            CancelBookPlacementEdit(false);
            SetBookshelfEditMode(false);
            isHamburgerMenuOpen = false;

            if (state != null && state.IsViewingBook && interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            activeEditSection = null;

            CloseOverlayPanels();
            CloseGuestbookPanel();

            if (notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(false);
                notebookScreenController.ReturnToScreenSaver();
                notebookScreenController.Show();
            }
        }

        private void RefreshHud()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;

            if (hamburgerMenuPanel != null)
            {
                hamburgerMenuPanel.SetActive(isHamburgerMenuOpen && !isBookshelfFocused);
            }

            if (hamburgerButton != null)
            {
                hamburgerButton.gameObject.SetActive(CurrentFocusArea != ExperienceFocusArea.Notebook && !isBookshelfFocused);
            }

            if (focusBackButton != null)
            {
                var showBackButton = CurrentFocusArea != ExperienceFocusArea.QuarterView &&
                                     CurrentFocusArea != ExperienceFocusArea.Notebook;
                var backRect = focusBackButton.GetComponent<RectTransform>();
                if (backRect != null)
                {
                    backRect.anchoredPosition = isBookshelfFocused ? new Vector2(34f, -34f) : new Vector2(20f, -20f);
                    backRect.sizeDelta = isBookshelfFocused ? new Vector2(66f, 66f) : new Vector2(120f, 44f);
                }

                SetButtonLabel(focusBackButton, isBookshelfFocused ? "←" : "Back");
                focusBackButton.gameObject.SetActive(showBackButton);
                focusBackButton.interactable = showBackButton && (cameraController == null || !cameraController.IsTransitioning);
                StyleHudButton(
                    focusBackButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    isBookshelfFocused ? 34 : 16,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            RefreshBookshelfOverlay();
            RefreshBookshelfNameEditPanel();
            RefreshBookSearchPanel();
            RefreshBookshelfManagePanel();
            RefreshBookshelfStatusPanel();
            RefreshGuestbookPanel();
            RefreshBookDetailPanel();
            RefreshBookFocusToolbar();
            RefreshBookReportWritePanel();
            RefreshBookReportReadPanel();
            RefreshBookEditPanel();
            RefreshSectionEditPanel();
            RefreshBookPlacementEditor();
        }

        private void RefreshBookshelfOverlay()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isPlacementEditing = isBookPlacementEditorOpen && placementEditBook != null;
            var showBookshelfOverlay = isBookshelfFocused && !isViewingBook && !isPlacementEditing;
            var canSwitchBookshelf = bookshelfGenerator != null &&
                                     bookshelfGenerator.BookshelfCount > 1 &&
                                     !isBookshelfSwitching &&
                                     !IsBookshelfEditMode &&
                                     !isDraggingBook &&
                                     !isPlacementEditing;
            if (overlayPreviousFocusButton != null)
            {
                overlayPreviousFocusButton.gameObject.SetActive(showBookshelfOverlay);
                overlayPreviousFocusButton.interactable = canSwitchBookshelf;
                StyleBookshelfNavigationButton(overlayPreviousFocusButton, canSwitchBookshelf);
            }

            if (overlayNextFocusButton != null)
            {
                overlayNextFocusButton.gameObject.SetActive(showBookshelfOverlay);
                overlayNextFocusButton.interactable = canSwitchBookshelf;
                StyleBookshelfNavigationButton(overlayNextFocusButton, canSwitchBookshelf);
            }

            if (bookshelfEditModeButton != null)
            {
                var canToggleEdit = isBookshelfFocused && !isViewingBook && !isBookshelfSwitching && !isDraggingBook && !isPlacementEditing;
                bookshelfEditModeButton.gameObject.SetActive(showBookshelfOverlay && IsBookshelfEditMode);
                bookshelfEditModeButton.interactable = canToggleEdit;
                SetButtonLabel(bookshelfEditModeButton, "저장");
                StyleHudButton(
                    bookshelfEditModeButton,
                    new Color32(255, 248, 236, 235),
                    new Color32(255, 255, 255, 245),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(63, 47, 38, 255),
                    true);
            }

            if (openBookSearchButton != null)
            {
                openBookSearchButton.gameObject.SetActive(false);
            }

            RefreshBookshelfBottomNav(showBookshelfOverlay && !isDraggingBook && !isBookshelfSwitching);
            RefreshBookshelfIndexIndicator(showBookshelfOverlay);
        }

        private void RefreshBookshelfBottomNav(bool shouldShow)
        {
            if (bookshelfBottomNavPanel == null)
            {
                return;
            }

            bookshelfBottomNavPanel.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var canInteract = CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                              (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null)) &&
                              (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            var canSearch = canInteract && !isBookPlacementEditorOpen;
            var canEdit = canInteract && !isBookPlacementEditorOpen;
            var canMutate = canInteract && !isBookPlacementEditorOpen;

            var activeTab = ResolveBookshelfBottomNavTab();
            StyleBookshelfBottomNavButton(bookshelfNavHomeButton, activeTab == BookshelfBottomNavTab.Home, canInteract, "⌂\n책장");
            StyleBookshelfBottomNavButton(bookshelfNavSearchButton, activeTab == BookshelfBottomNavTab.Search, canSearch, "⌕\n검색");
            StyleBookshelfBottomNavButton(bookshelfNavAddButton, activeTab == BookshelfBottomNavTab.Add, canMutate, "+\n추가");
            StyleBookshelfBottomNavButton(bookshelfNavEditButton, activeTab == BookshelfBottomNavTab.Edit, canEdit, "✎\n편집");
        }

        private BookshelfBottomNavTab ResolveBookshelfBottomNavTab()
        {
            if (isBookAddPanelOpen || isBookAddConfirmPanelOpen)
            {
                return BookshelfBottomNavTab.Add;
            }

            if (isBookSearchPanelOpen)
            {
                return BookshelfBottomNavTab.Search;
            }

            return IsBookshelfEditMode ? BookshelfBottomNavTab.Edit : BookshelfBottomNavTab.Home;
        }

        private static void StyleBookshelfBottomNavButton(Button button, bool isActive, bool isInteractable, string label)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = isInteractable;
            SetButtonLabel(button, label);
            StyleHudButton(
                button,
                isActive ? new Color32(197, 128, 67, 238) : new Color32(255, 248, 236, 0),
                isActive ? new Color32(214, 145, 82, 255) : new Color32(255, 255, 255, 82),
                new Color32(224, 219, 214, 52),
                15,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.lineSpacing = 1.02f;
            }
        }

        private void RefreshBookshelfIndexIndicator(bool shouldShow)
        {
            if (bookshelfIndexPanel != null)
            {
                bookshelfIndexPanel.SetActive(shouldShow && bookshelfGenerator != null);
            }

            if (!shouldShow || bookshelfGenerator == null)
            {
                return;
            }

            var totalCount = Mathf.Max(1, bookshelfGenerator.BookshelfCount);
            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, totalCount - 1);
            if (bookshelfIndexText != null)
            {
                bookshelfIndexText.text = bookshelfGenerator.GetBookshelfName(displayIndex);
            }

            RefreshBookshelfIndexDots(displayIndex, totalCount);
        }

        private void RefreshBookshelfNameEditPanel()
        {
            if (bookshelfNameEditPanel == null ||
                addBookshelfButton == null ||
                openBookshelfNameEditButton == null ||
                openBookshelfDesignButton == null ||
                deleteBookshelfButton == null ||
                openBookAddButton == null ||
                openBookshelfManageButton == null ||
                bookshelfDeleteConfirmPanel == null ||
                bookshelfDesignPanel == null ||
                bookAddPanel == null ||
                bookAddConfirmPanel == null ||
                bookshelfManagePanel == null ||
                bookshelfUnsavedConfirmPanel == null)
            {
                return;
            }

            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var canShowBookshelfOverlay = isBookshelfFocused &&
                                          !isViewingBook &&
                                          !isDraggingBook &&
                                          !isBookshelfSwitching &&
                                          bookshelfGenerator != null;
            var shouldShowEditControls = IsBookshelfEditMode && canShowBookshelfOverlay;

            var displayIndex = canShowBookshelfOverlay
                ? Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1)
                : -1;
            var canDeleteBookshelf = shouldShowEditControls && bookshelfGenerator.BookshelfCount > 1;

            addBookshelfButton.gameObject.SetActive(false);

            openBookAddButton.gameObject.SetActive(false);

            openBookshelfManageButton.gameObject.SetActive(shouldShowEditControls);
            openBookshelfManageButton.interactable = shouldShowEditControls;
            StyleHudButton(
                openBookshelfManageButton,
                new Color32(255, 248, 236, 235),
                new Color32(255, 255, 255, 245),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(63, 47, 38, 255),
                true);

            openBookshelfNameEditButton.gameObject.SetActive(false);

            deleteBookshelfButton.gameObject.SetActive(false);
            deleteBookshelfButton.interactable = canDeleteBookshelf;
            SetButtonLabel(deleteBookshelfButton, canDeleteBookshelf
                ? "책장 삭제"
                : "삭제 불가");

            openBookshelfDesignButton.gameObject.SetActive(shouldShowEditControls);
            openBookshelfDesignButton.interactable = shouldShowEditControls;
            StyleHudButton(
                openBookshelfDesignButton,
                new Color32(255, 248, 236, 235),
                new Color32(255, 255, 255, 245),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(63, 47, 38, 255),
                true);

            var showNamePanel = shouldShowEditControls && isBookshelfNameEditPanelOpen;
            var showDeletePanel = shouldShowEditControls && isBookshelfDeleteConfirmPanelOpen;
            var showDesignPanel = shouldShowEditControls && isBookshelfDesignPanelOpen;
            var showBookAddPanel = canShowBookshelfOverlay && isBookAddPanelOpen;
            var showBookAddConfirmPanel = canShowBookshelfOverlay && isBookAddConfirmPanelOpen;
            var showBookshelfManagePanel = shouldShowEditControls && isBookshelfManagePanelOpen;
            var showUnsavedConfirmPanel = isBookshelfUnsavedConfirmPanelOpen;
            var showModal = showNamePanel || showDeletePanel || showDesignPanel || showBookAddPanel || showBookAddConfirmPanel || showBookshelfManagePanel || showUnsavedConfirmPanel;
            if (bookshelfNameModalBackdrop != null)
            {
                bookshelfNameModalBackdrop.SetActive(showModal);
            }

            bookshelfNameEditPanel.SetActive(showNamePanel);
            bookshelfDeleteConfirmPanel.SetActive(showDeletePanel);
            bookshelfDesignPanel.SetActive(showDesignPanel);
            bookAddPanel.SetActive(showBookAddPanel);
            bookAddConfirmPanel.SetActive(showBookAddConfirmPanel);
            bookshelfManagePanel.SetActive(showBookshelfManagePanel);
            bookshelfUnsavedConfirmPanel.SetActive(showUnsavedConfirmPanel);
            if (showUnsavedConfirmPanel)
            {
                StyleHudButton(
                    confirmBookshelfUnsavedSaveButton,
                    new Color32(197, 128, 67, 245),
                    new Color32(214, 145, 82, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    Color.white,
                    true);
                StyleHudButton(
                    cancelBookshelfUnsavedSaveButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }

            if (showModal)
            {
                if (bookshelfNameModalBackdrop != null)
                {
                    bookshelfNameModalBackdrop.transform.SetAsLastSibling();
                }

                if (showUnsavedConfirmPanel)
                {
                    bookshelfUnsavedConfirmPanel.transform.SetAsLastSibling();
                }
                else if (showNamePanel)
                {
                    bookshelfNameEditPanel.transform.SetAsLastSibling();
                }
                else if (showDeletePanel)
                {
                    bookshelfDeleteConfirmPanel.transform.SetAsLastSibling();
                }
                else if (showDesignPanel)
                {
                    bookshelfDesignPanel.transform.SetAsLastSibling();
                }
                else if (showBookAddPanel)
                {
                    bookAddPanel.transform.SetAsLastSibling();
                }
                else if (showBookAddConfirmPanel)
                {
                    bookAddConfirmPanel.transform.SetAsLastSibling();
                }
                else
                {
                    bookshelfManagePanel.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && bookshelfBottomNavPanel != null && bookshelfBottomNavPanel.activeSelf)
                {
                    bookshelfBottomNavPanel.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && bookshelfIndexPanel != null && bookshelfIndexPanel.activeSelf)
                {
                    bookshelfIndexPanel.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && focusBackButton != null && focusBackButton.gameObject.activeSelf)
                {
                    focusBackButton.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && openBookshelfManageButton != null && openBookshelfManageButton.gameObject.activeSelf)
                {
                    openBookshelfManageButton.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && openBookshelfDesignButton != null && openBookshelfDesignButton.gameObject.activeSelf)
                {
                    openBookshelfDesignButton.transform.SetAsLastSibling();
                }

                if (!showUnsavedConfirmPanel && bookshelfEditModeButton != null && bookshelfEditModeButton.gameObject.activeSelf)
                {
                    bookshelfEditModeButton.transform.SetAsLastSibling();
                }
            }

            if (!canShowBookshelfOverlay || bookshelfGenerator == null)
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                return;
            }

            if (!shouldShowEditControls)
            {
                bookshelfNameInputIndex = -1;
                pendingDeleteBookshelfIndex = -1;
                isBookshelfNameEditPanelOpen = false;
                isBookshelfDeleteConfirmPanelOpen = false;
                isBookshelfDesignPanelOpen = false;
                isBookshelfManagePanelOpen = false;
            }
            else
            {
                RefreshBookshelfDeleteConfirmPanel(displayIndex);
                RefreshBookshelfDesignPanel(displayIndex);
                RefreshBookshelfManagePanel();
            }

            RefreshBookAddPanel(displayIndex);
            RefreshBookAddConfirmPanel();

            if (!showNamePanel)
            {
                return;
            }

            if (bookshelfNameInputField != null &&
                (!bookshelfNameInputField.isFocused || bookshelfNameInputIndex != displayIndex))
            {
                bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(displayIndex);
                bookshelfNameInputIndex = displayIndex;
            }

            if (bookshelfNameEditLabelText != null)
            {
                bookshelfNameEditLabelText.text = $"{displayIndex + 1}번 책장 이름";
            }

            if (saveBookshelfNameButton != null)
            {
                StyleHudButton(
                    saveBookshelfNameButton,
                    new Color32(197, 128, 67, 235),
                    new Color32(214, 145, 82, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    Color.white,
                    true);
            }

            if (closeBookshelfNameEditButton != null)
            {
                StyleHudButton(
                    closeBookshelfNameEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private void OpenBookshelfNameEditPanel()
        {
            if (!IsBookshelfEditMode ||
                bookshelfGenerator == null ||
                bookshelfNameInputField == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            SetBookshelfOverlayMode(BookshelfOverlayMode.NameEdit);
            bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(displayIndex);
            bookshelfNameInputIndex = displayIndex;
            RefreshBookshelfNameEditPanel();
        }

        private void CloseBookshelfNameEditPanel()
        {
            isBookshelfNameEditPanelOpen = false;
            bookshelfNameInputIndex = -1;
            if (bookshelfNameEditPanel != null)
            {
                bookshelfNameEditPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }
        }

        private void SaveBookshelfName()
        {
            if (!IsBookshelfEditMode ||
                bookshelfGenerator == null ||
                bookshelfNameInputField == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            var previousName = bookshelfGenerator.GetBookshelfName(displayIndex);
            bookshelfGenerator.SetBookshelfName(displayIndex, bookshelfNameInputField.text);
            bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(displayIndex);
            if (bookshelfNameInputField.text != previousName)
            {
                MarkBookshelfEditDirty();
            }

            SetBookshelfOverlayMode(BookshelfOverlayMode.None);
            RefreshBookshelfIndexIndicator(CurrentFocusArea == ExperienceFocusArea.Bookshelf && (state == null || !state.IsViewingBook));
            RefreshBookshelfNameEditPanel();
        }

        private void OpenBookshelfDesignPanel()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            SetBookshelfOverlayMode(BookshelfOverlayMode.Design);
            RefreshBookshelfNameEditPanel();
        }

        private void CloseBookshelfDesignPanel()
        {
            isBookshelfDesignPanelOpen = false;
            if (bookshelfDesignPanel != null)
            {
                bookshelfDesignPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void OpenBookAddPanel()
        {
            if (!CanOpenBookAddPanel())
            {
                return;
            }

            CloseOverlayPanels(bookAddPanel);
            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);

            if (bookAddSearchInputField != null)
            {
                bookAddSearchInputField.text = string.Empty;
            }

            shouldResetBookAddScrollToTop = true;
            RefreshHud();
        }

        private void CloseBookAddPanel()
        {
            CloseBookAddPanel(true);
        }

        private void CloseBookAddPanel(bool refresh)
        {
            isBookAddPanelOpen = false;
            isBookAddConfirmPanelOpen = false;
            pendingBookAddCatalogIndex = -1;
            bookAddFeedbackMessage = string.Empty;
            shouldResetBookAddScrollToTop = true;
            for (var i = 0; i < bookAddResultCatalogIndices.Length; i++)
            {
                bookAddResultCatalogIndices[i] = -1;
            }

            if (bookAddPanel != null)
            {
                bookAddPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null &&
                !isBookshelfNameEditPanelOpen &&
                !isBookshelfDeleteConfirmPanelOpen &&
                !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void HandleBookAddSearchChanged(string _)
        {
            bookAddFeedbackMessage = string.Empty;
            shouldResetBookAddScrollToTop = true;
            if (isBookAddPanelOpen)
            {
                RefreshBookAddPanel(bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1);
            }
        }

        private void RefreshBookAddPanel(int displayIndex)
        {
            if (!isBookAddPanelOpen || bookAddPanel == null)
            {
                return;
            }

            var canAdd = CanOpenBookAddPanel();
            var shelfName = bookshelfGenerator != null && displayIndex >= 0
                ? bookshelfGenerator.GetBookshelfName(displayIndex)
                : "현재 책장";
            if (bookAddTitleText != null)
            {
                bookAddTitleText.text = $"{shelfName}에 책 추가";
            }

            if (bookAddHintText != null)
            {
                bookAddHintText.text = "서버 연동 전까지 로컬 카탈로그에서 검색하고 추가합니다.";
            }

            if (bookAddSearchInputField != null)
            {
                bookAddSearchInputField.interactable = canAdd;
            }

            var query = bookAddSearchInputField != null ? bookAddSearchInputField.text : string.Empty;
            var visibleSlot = 0;
            var matchCount = 0;
            for (var i = 0; i < BookAddCatalog.Length; i++)
            {
                var item = BookAddCatalog[i];
                if (!DoesCatalogItemMatch(item, query))
                {
                    continue;
                }

                if (visibleSlot < bookAddResultButtons.Length)
                {
                    var button = bookAddResultButtons[visibleSlot];
                    bookAddResultCatalogIndices[visibleSlot] = i;
                    if (button != null)
                    {
                        button.gameObject.SetActive(true);
                        button.interactable = canAdd;
                        SetButtonLabel(button, $"{item.Title}\n{item.Author} · {item.Publisher} · {GetOrientationLabel(item.Orientation)}");
                        StyleBookAddResultButton(button);
                    }
                }

                visibleSlot++;
                matchCount++;
            }

            var visibleCount = Mathf.Min(matchCount, bookAddResultButtons.Length);
            if (bookAddRowsContent != null)
            {
                var contentHeight = Mathf.Max(BookAddResultViewportHeight, visibleCount * BookAddResultRowHeight + 2f);
                bookAddRowsContent.sizeDelta = new Vector2(0f, contentHeight);
            }

            var canScroll = visibleCount * BookAddResultRowHeight > BookAddResultViewportHeight + 1f;
            if (bookAddScrollbar != null)
            {
                bookAddScrollbar.gameObject.SetActive(canScroll);
                bookAddScrollbar.interactable = canScroll;
            }

            if (bookAddScrollRect != null)
            {
                bookAddScrollRect.enabled = canScroll;
                bookAddScrollRect.scrollSensitivity = 24f;
                if (shouldResetBookAddScrollToTop)
                {
                    Canvas.ForceUpdateCanvases();
                    bookAddScrollRect.verticalNormalizedPosition = 1f;
                    if (bookAddRowsContent != null)
                    {
                        bookAddRowsContent.anchoredPosition = Vector2.zero;
                    }

                    shouldResetBookAddScrollToTop = false;
                }
            }

            for (var i = visibleCount; i < bookAddResultButtons.Length; i++)
            {
                bookAddResultCatalogIndices[i] = -1;
                if (bookAddResultButtons[i] != null)
                {
                    bookAddResultButtons[i].gameObject.SetActive(false);
                }
            }

            if (closeBookAddButton != null)
            {
                StyleHudButton(
                    closeBookAddButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(84, 74, 66, 255),
                    true);
            }

            if (bookAddStatusText != null)
            {
                if (!string.IsNullOrWhiteSpace(bookAddFeedbackMessage))
                {
                    bookAddStatusText.text = bookAddFeedbackMessage;
                }
                else if (matchCount == 0)
                {
                    bookAddStatusText.text = "검색 결과가 없습니다.";
                }
                else
                {
                    bookAddStatusText.text = matchCount > bookAddResultButtons.Length
                        ? $"검색 결과 {matchCount}개 중 {visibleCount}개 표시 · 스크롤"
                        : canScroll
                            ? $"검색 결과 {matchCount}개 · 스크롤"
                            : $"검색 결과 {matchCount}개";
                }
            }
        }

        private void OpenBookAddConfirmPanel(int resultSlot)
        {
            if (!CanOpenBookAddPanel() ||
                resultSlot < 0 ||
                resultSlot >= bookAddResultCatalogIndices.Length)
            {
                return;
            }

            var catalogIndex = bookAddResultCatalogIndices[resultSlot];
            if (catalogIndex < 0 || catalogIndex >= BookAddCatalog.Length)
            {
                return;
            }

            pendingBookAddCatalogIndex = catalogIndex;
            pendingBookAddOrientation = BookAddCatalog[catalogIndex].Orientation;
            pendingBookAddReadingStatus = App.Domain.ReadingStatus.Unread;
            bookAddFeedbackMessage = string.Empty;
            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAddConfirm);
            RefreshBookshelfNameEditPanel();
        }

        private void SetPendingBookAddOrientation(BookOrientation orientation)
        {
            pendingBookAddOrientation = orientation;
            RefreshBookAddConfirmPanel();
        }

        private void SetPendingBookAddReadingStatus(App.Domain.ReadingStatus status)
        {
            pendingBookAddReadingStatus = status;
            RefreshBookAddConfirmPanel();
        }

        private void CloseBookAddConfirmPanel()
        {
            CloseBookAddConfirmPanelSilently();
            if (CanOpenBookAddPanel())
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
            }
            else
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void CloseBookAddConfirmPanelSilently()
        {
            isBookAddConfirmPanelOpen = false;
            pendingBookAddCatalogIndex = -1;
            if (bookAddConfirmPanel != null)
            {
                bookAddConfirmPanel.SetActive(false);
            }
        }

        private void ConfirmAddCatalogBookToShelf()
        {
            if (!CanOpenBookAddPanel() ||
                pendingBookAddCatalogIndex < 0 ||
                pendingBookAddCatalogIndex >= BookAddCatalog.Length)
            {
                CloseBookAddConfirmPanel();
                return;
            }

            AddCatalogBookToShelf(pendingBookAddCatalogIndex, pendingBookAddOrientation);
            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
            RefreshHud();
        }

        private void RefreshBookAddConfirmPanel()
        {
            if (!isBookAddConfirmPanelOpen || bookAddConfirmPanel == null)
            {
                return;
            }

            if (pendingBookAddCatalogIndex < 0 || pendingBookAddCatalogIndex >= BookAddCatalog.Length)
            {
                CloseBookAddConfirmPanel();
                return;
            }

            var item = BookAddCatalog[pendingBookAddCatalogIndex];
            if (bookAddConfirmTitleText != null)
            {
                bookAddConfirmTitleText.text = item.Title;
            }

            if (bookAddConfirmMetaText != null)
            {
                bookAddConfirmMetaText.text = $"{item.Author} · {item.Publisher}";
            }

            if (bookAddConfirmSummaryText != null)
            {
                bookAddConfirmSummaryText.text = item.Summary;
            }

            StyleBookPlacementOptionButton(bookAddConfirmSpineButton, pendingBookAddOrientation == BookOrientation.Spine);
            StyleBookPlacementOptionButton(bookAddConfirmAngledButton, pendingBookAddOrientation == BookOrientation.Angled45);
            StyleBookPlacementOptionButton(bookAddConfirmFrontButton, pendingBookAddOrientation == BookOrientation.Front);
            StyleBookAddReadingStatusButton(bookAddConfirmUnreadButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Unread);
            StyleBookAddReadingStatusButton(bookAddConfirmReadingButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Reading);
            StyleBookAddReadingStatusButton(bookAddConfirmReadButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read);
            StyleHudButton(
                confirmBookAddButton,
                new Color32(88, 133, 117, 245),
                new Color32(72, 114, 100, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                cancelBookAddConfirmButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void AddCatalogBookToShelf(int catalogIndex, BookOrientation orientation)
        {
            if (!CanOpenBookAddPanel() ||
                catalogIndex < 0 ||
                catalogIndex >= BookAddCatalog.Length)
            {
                return;
            }

            var item = BookAddCatalog[catalogIndex];
            var book = bookshelfGenerator.AddBookToActiveBookshelf(
                item.Title,
                item.Author,
                item.Publisher,
                item.Summary,
                orientation);
            if (book == null)
            {
                bookAddFeedbackMessage = "현재 책장에 빈 공간이 없습니다.";
            }
            else
            {
                if (dataStore != null)
                {
                    var meta = dataStore.GetOrCreateBookMeta(book);
                    if (meta != null)
                    {
                        meta.readingStatus = pendingBookAddReadingStatus;
                        meta.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
                    }
                }

                book.readingStatus = pendingBookAddReadingStatus;
                book.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
                bookAddFeedbackMessage = $"{item.Title} 추가 완료 · {GetReadingStatusLabel(pendingBookAddReadingStatus)} · {GetOrientationLabel(orientation)}";
            }

            RefreshHud();
        }

        private bool HasOpenBookshelfModal()
        {
            return isBookshelfNameEditPanelOpen ||
                   isBookshelfDeleteConfirmPanelOpen ||
                   isBookshelfDesignPanelOpen ||
                   isBookAddPanelOpen ||
                   isBookAddConfirmPanelOpen ||
                   isBookshelfManagePanelOpen ||
                   isBookshelfUnsavedConfirmPanelOpen;
        }

        private void ToggleBookSearchPanel()
        {
            if (isBookSearchPanelOpen)
            {
                CloseBookSearchPanel();
                return;
            }

            OpenBookSearchPanel();
        }

        private void OpenBookSearchPanel()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (state != null && state.IsViewingBook) ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(OpenBookSearchPanel))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            CloseOverlayPanels(bookSearchPanel);
            isBookSearchPanelOpen = true;
            activeBookSearchFilter = BookSearchFilter.All;
            shouldResetBookSearchScrollToTop = true;
            if (bookSearchInputField != null)
            {
                bookSearchInputField.text = string.Empty;
            }

            RefreshHud();
        }

        private void CloseBookSearchPanel()
        {
            isBookSearchPanelOpen = false;
            bookSearchResults.Clear();
            if (bookSearchPanel != null)
            {
                bookSearchPanel.SetActive(false);
            }

            RefreshHud();
        }

        private void HandleBookSearchChanged(string _)
        {
            shouldResetBookSearchScrollToTop = true;
            RefreshBookSearchPanel();
        }

        private void SetBookSearchFilter(BookSearchFilter filter)
        {
            activeBookSearchFilter = filter;
            shouldResetBookSearchScrollToTop = true;
            RefreshBookSearchPanel();
        }

        private void RefreshBookSearchPanel()
        {
            if (bookSearchPanel == null)
            {
                return;
            }

            var canShow = isBookSearchPanelOpen &&
                          CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                          !IsBookshelfEditMode &&
                          (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null)) &&
                          (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            bookSearchPanel.SetActive(canShow);
            if (!canShow)
            {
                isBookSearchPanelOpen = false;
                bookSearchResults.Clear();
                return;
            }

            if (bookSearchTitleText != null)
            {
                bookSearchTitleText.text = bookshelfGenerator != null
                    ? $"{bookshelfGenerator.DisplayBookshelfName} 책 찾기"
                    : "책 찾기";
            }

            var query = bookSearchInputField != null ? bookSearchInputField.text : string.Empty;
            bookSearchResults.Clear();
            var allBooks = new List<BookEntity>();
            CollectActiveShelfBooks(allBooks);
            for (var i = 0; i < allBooks.Count; i++)
            {
                var book = allBooks[i];
                if (DoesBookMatchSearch(book, query, activeBookSearchFilter))
                {
                    bookSearchResults.Add(book);
                }
            }

            var visibleCount = Mathf.Min(bookSearchResults.Count, bookSearchResultButtons.Length);
            if (bookSearchRowsContent != null)
            {
                var contentHeight = Mathf.Max(BookSearchResultViewportHeight, visibleCount * BookSearchResultRowHeight + 2f);
                bookSearchRowsContent.sizeDelta = new Vector2(0f, contentHeight);
            }

            var canScroll = visibleCount * BookSearchResultRowHeight > BookSearchResultViewportHeight + 1f;
            if (bookSearchScrollbar != null)
            {
                bookSearchScrollbar.gameObject.SetActive(canScroll);
                bookSearchScrollbar.interactable = canScroll;
            }

            if (bookSearchScrollRect != null)
            {
                bookSearchScrollRect.enabled = canScroll;
                bookSearchScrollRect.scrollSensitivity = 24f;
                if (shouldResetBookSearchScrollToTop)
                {
                    Canvas.ForceUpdateCanvases();
                    bookSearchScrollRect.verticalNormalizedPosition = 1f;
                    if (bookSearchRowsContent != null)
                    {
                        bookSearchRowsContent.anchoredPosition = Vector2.zero;
                    }

                    shouldResetBookSearchScrollToTop = false;
                }
            }

            for (var i = 0; i < bookSearchResultButtons.Length; i++)
            {
                var button = bookSearchResultButtons[i];
                if (button == null)
                {
                    continue;
                }

                var isVisible = i < visibleCount;
                button.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var book = bookSearchResults[i];
                var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
                var title = TryGetBookDisplayTitle(book);
                var author = meta != null ? meta.author : book.author;
                var status = meta != null ? GetReadingStatusLabel(meta.readingStatus) : GetReadingStatusLabel(book.readingStatus);
                SetButtonLabel(button, $"{title}\n{author} · {status}");
                StyleBookAddResultButton(button);
            }

            StyleBookSearchFilterButton(bookSearchAllFilterButton, activeBookSearchFilter == BookSearchFilter.All);
            StyleBookSearchFilterButton(bookSearchReadingFilterButton, activeBookSearchFilter == BookSearchFilter.Reading);
            StyleBookSearchFilterButton(bookSearchReadFilterButton, activeBookSearchFilter == BookSearchFilter.Read);
            StyleBookSearchFilterButton(bookSearchLikedFilterButton, activeBookSearchFilter == BookSearchFilter.Liked);
            StyleHudButton(
                closeBookSearchButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                16,
                new Color32(84, 74, 66, 255),
                true);

            if (bookSearchStatusText != null)
            {
                bookSearchStatusText.text = bookSearchResults.Count == 0
                    ? "일치하는 책이 없습니다."
                    : bookSearchResults.Count > bookSearchResultButtons.Length
                        ? $"{bookSearchResults.Count}권 중 {visibleCount}권 표시 · 스크롤"
                        : canScroll
                            ? $"{bookSearchResults.Count}권 · 스크롤"
                            : $"{bookSearchResults.Count}권";
            }

            bookSearchPanel.transform.SetAsLastSibling();
        }

        private void OpenBookSearchResult(int slot)
        {
            if (slot < 0 || slot >= bookSearchResults.Count)
            {
                return;
            }

            var book = bookSearchResults[slot];
            CloseBookSearchPanel();
            if (interactionController != null)
            {
                interactionController.ViewBookFromUi(book);
            }
            else
            {
                OpenBookDetailPanel(book);
            }
        }

        private void OpenBookshelfManagePanel()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            CloseOverlayPanels(bookshelfManagePanel);
            SetBookshelfOverlayMode(BookshelfOverlayMode.Manage);
            RefreshBookshelfNameEditPanel();
        }

        private void CloseBookshelfManagePanel()
        {
            isBookshelfManagePanelOpen = false;
            if (bookshelfManagePanel != null)
            {
                bookshelfManagePanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void RefreshBookshelfManagePanel()
        {
            if (!isBookshelfManagePanelOpen ||
                bookshelfManagePanel == null ||
                bookshelfGenerator == null)
            {
                return;
            }

            var activeIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            if (bookshelfManageTitleText != null)
            {
                bookshelfManageTitleText.text = "책장 관리";
            }

            if (bookshelfManageSummaryText != null)
            {
                bookshelfManageSummaryText.text = BuildBookshelfManageSummary(activeIndex);
            }

            if (bookshelfManageRowsContent != null)
            {
                var rowContentHeight = Mathf.Max(ShelfManageViewportHeight, bookshelfGenerator.BookshelfCount * ShelfManageRowHeight + 2f);
                bookshelfManageRowsContent.sizeDelta = new Vector2(0f, rowContentHeight);
            }

            var canScrollBookshelves = bookshelfGenerator.BookshelfCount * ShelfManageRowHeight > ShelfManageViewportHeight + 1f;
            if (bookshelfManageScrollbar != null)
            {
                bookshelfManageScrollbar.gameObject.SetActive(true);
                bookshelfManageScrollbar.interactable = canScrollBookshelves;
                if (!canScrollBookshelves)
                {
                    bookshelfManageScrollbar.size = 1f;
                    bookshelfManageScrollbar.value = 1f;
                }
            }

            if (bookshelfManageScrollRect != null)
            {
                bookshelfManageScrollRect.enabled = canScrollBookshelves;
                bookshelfManageScrollRect.scrollSensitivity = 24f;
            }

            for (var i = 0; i < bookshelfManageRowButtons.Length; i++)
            {
                var button = bookshelfManageRowButtons[i];
                if (button == null)
                {
                    continue;
                }

                var showRow = i < bookshelfGenerator.BookshelfCount;
                button.gameObject.SetActive(showRow);
                if (!showRow)
                {
                    continue;
                }

                var isActive = i == activeIndex;
                var visibility = GetVisibilityLabel(bookshelfGenerator.GetBookshelfVisibility(i));
                SetButtonLabel(button, $"{(isActive ? "● " : "○ ")}{i + 1}. {bookshelfGenerator.GetBookshelfName(i)}   ·   {visibility}");
                StyleHudButton(
                    button,
                    isActive ? new Color32(255, 248, 236, 245) : new Color32(255, 255, 255, 230),
                    new Color32(255, 255, 255, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    isActive ? new Color32(63, 47, 38, 255) : new Color32(84, 74, 66, 255),
                    isActive);
            }

            var canSortBooks = CanMutateBookshelfCollection() && bookshelfGenerator.ActiveBookshelfBookCount > 1;
            if (shelfManageSortTitleButton != null)
            {
                shelfManageSortTitleButton.interactable = canSortBooks;
            }

            if (shelfManageSortAuthorButton != null)
            {
                shelfManageSortAuthorButton.interactable = canSortBooks;
            }

            if (shelfManageSortStatusButton != null)
            {
                shelfManageSortStatusButton.interactable = canSortBooks;
            }

            if (shelfManageSortLikedButton != null)
            {
                shelfManageSortLikedButton.interactable = canSortBooks;
            }

            StyleBookshelfManageSortButton(shelfManageSortTitleButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortAuthorButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortStatusButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortLikedButton, canSortBooks);

            StyleHudButton(
                shelfManageVisibilityButton,
                new Color32(238, 248, 241, 235),
                new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(53, 94, 73, 255),
                true);
            SetButtonLabel(shelfManageVisibilityButton, $"공개: {GetVisibilityLabel(bookshelfGenerator.GetBookshelfVisibility(activeIndex))}");
            StyleHudButton(
                shelfManageAddButton,
                new Color32(238, 248, 241, 235),
                new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(53, 94, 73, 255),
                true);
            StyleHudButton(
                shelfManageCloseButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(84, 74, 66, 255),
                true);

            if (bookshelfManageStatusText != null)
            {
                bookshelfManageStatusText.text = $"목록을 스크롤해 책장을 선택하고, 정렬은 현재 책장 안에서 슬롯 기준으로 다시 배치합니다.";
            }
        }

        private void SelectManagedBookshelf(int rowIndex)
        {
            if (bookshelfGenerator == null || rowIndex < 0 || rowIndex >= bookshelfGenerator.BookshelfCount)
            {
                return;
            }

            if (rowIndex != bookshelfGenerator.DisplayBookshelfIndex &&
                bookshelfGenerator.ShowBookshelf(rowIndex))
            {
                RefreshHud();
                return;
            }

            RefreshBookshelfManagePanel();
        }

        private void CycleManagedBookshelfVisibility()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator == null)
            {
                return;
            }

            bookshelfGenerator.CycleActiveBookshelfVisibility();
            MarkBookshelfEditDirty();
            RefreshHud();
        }

        private void AddBookshelfFromManagePanel()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator == null)
            {
                return;
            }

            if (bookshelfGenerator.AddBookshelf())
            {
                MarkBookshelfEditDirty();
                isBookshelfManagePanelOpen = true;
                RefreshHud();
            }
        }

        private string BuildBookshelfManageSummary(int activeIndex)
        {
            if (bookshelfGenerator == null)
            {
                return "현재 책장 정보를 불러오는 중";
            }

            var total = 0;
            var reading = 0;
            var read = 0;
            var liked = 0;
            if (state != null)
            {
                for (var i = 0; i < state.Sections.Count; i++)
                {
                    var section = state.Sections[i];
                    if (section == null)
                    {
                        continue;
                    }

                    for (var j = 0; j < section.books.Count; j++)
                    {
                        var book = section.books[j];
                        if (book == null)
                        {
                            continue;
                        }

                        total++;
                        var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
                        var status = meta != null ? meta.readingStatus : book.readingStatus;
                        if (status == App.Domain.ReadingStatus.Reading)
                        {
                            reading++;
                        }
                        else if (status == App.Domain.ReadingStatus.Read)
                        {
                            read++;
                        }

                        if (meta != null ? meta.isLiked : book.isLiked)
                        {
                            liked++;
                        }
                    }
                }
            }

            return $"{bookshelfGenerator.GetBookshelfName(activeIndex)} · {total}권   읽는 중 {reading}   완독 {read}   좋아요 {liked}";
        }

        private void SortActiveBookshelfBooks(ShelfBookSortMode sortMode)
        {
            if (!CanMutateBookshelfCollection() || state == null || state.Sections.Count == 0)
            {
                return;
            }

            var visualSections = new List<ShelfSection>();
            var originalBySection = new Dictionary<ShelfSection, List<BookEntity>>();
            var originalBooks = new List<BookEntity>();
            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                visualSections.Add(section);
                originalBySection[section] = new List<BookEntity>(section.books);
                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book != null && !originalBooks.Contains(book))
                    {
                        originalBooks.Add(book);
                    }
                }
            }

            if (originalBooks.Count < 2)
            {
                RefreshBookshelfManagePanel();
                return;
            }

            var sortedBooks = new List<BookEntity>(originalBooks);
            sortedBooks.Sort((left, right) => CompareBooksForSort(left, right, sortMode));
            if (AreBooksInSameOrder(originalBooks, sortedBooks))
            {
                RefreshBookshelfManagePanel();
                return;
            }

            visualSections.Sort(CompareShelfSectionsForVisualOrder);
            for (var i = 0; i < state.Sections.Count; i++)
            {
                state.Sections[i]?.books.Clear();
            }

            for (var i = 0; i < sortedBooks.Count; i++)
            {
                sortedBooks[i]?.SetSection(null);
            }

            var sectionCursor = 0;
            for (var i = 0; i < sortedBooks.Count; i++)
            {
                var book = sortedBooks[i];
                if (book == null)
                {
                    continue;
                }

                var placed = false;
                for (var sectionIndex = sectionCursor; sectionIndex < visualSections.Count; sectionIndex++)
                {
                    var targetSection = visualSections[sectionIndex];
                    if (state.TryPlaceBookAt(book, targetSection, targetSection.books.Count, state, false))
                    {
                        sectionCursor = sectionIndex;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    RestoreBookshelfSectionOrder(originalBySection);
                    state.LayoutAll(state, true);
                    RefreshHud();
                    return;
                }
            }

            state.LayoutAll(state, true);
            MarkBookshelfEditDirty();
            RefreshHud();
        }

        private void RestoreBookshelfSectionOrder(Dictionary<ShelfSection, List<BookEntity>> originalBySection)
        {
            if (state == null || originalBySection == null)
            {
                return;
            }

            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                section.books.Clear();
                if (!originalBySection.TryGetValue(section, out var originalBooks))
                {
                    continue;
                }

                for (var j = 0; j < originalBooks.Count; j++)
                {
                    var book = originalBooks[j];
                    if (book == null)
                    {
                        continue;
                    }

                    section.books.Add(book);
                    book.SetSection(section);
                }
            }
        }

        private int CompareBooksForSort(BookEntity left, BookEntity right, ShelfBookSortMode sortMode)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var primary = 0;
            switch (sortMode)
            {
                case ShelfBookSortMode.Author:
                    primary = CompareSortText(GetBookSortAuthor(left), GetBookSortAuthor(right));
                    break;
                case ShelfBookSortMode.ReadingStatus:
                    primary = GetReadingStatusSortWeight(GetBookReadingStatus(left)).CompareTo(GetReadingStatusSortWeight(GetBookReadingStatus(right)));
                    break;
                case ShelfBookSortMode.Liked:
                    var leftLiked = IsBookLiked(left);
                    var rightLiked = IsBookLiked(right);
                    primary = leftLiked == rightLiked ? 0 : leftLiked ? -1 : 1;
                    break;
                default:
                    primary = CompareSortText(TryGetBookDisplayTitle(left), TryGetBookDisplayTitle(right));
                    break;
            }

            if (primary != 0)
            {
                return primary;
            }

            var titleCompare = CompareSortText(TryGetBookDisplayTitle(left), TryGetBookDisplayTitle(right));
            return titleCompare != 0 ? titleCompare : CompareSortText(left.bookId, right.bookId);
        }

        private string GetBookSortAuthor(BookEntity book)
        {
            if (book == null)
            {
                return string.Empty;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null && !string.IsNullOrWhiteSpace(meta.author)
                ? meta.author
                : book.author;
        }

        private App.Domain.ReadingStatus GetBookReadingStatus(BookEntity book)
        {
            if (book == null)
            {
                return App.Domain.ReadingStatus.Unread;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null ? meta.readingStatus : book.readingStatus;
        }

        private bool IsBookLiked(BookEntity book)
        {
            if (book == null)
            {
                return false;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null ? meta.isLiked : book.isLiked;
        }

        private static int GetReadingStatusSortWeight(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Reading:
                    return 0;
                case App.Domain.ReadingStatus.Unread:
                    return 1;
                case App.Domain.ReadingStatus.Read:
                    return 2;
                default:
                    return 3;
            }
        }

        private static int CompareShelfSectionsForVisualOrder(ShelfSection left, ShelfSection right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var rowCompare = right.row.CompareTo(left.row);
            return rowCompare != 0 ? rowCompare : left.column.CompareTo(right.column);
        }

        private static bool AreBooksInSameOrder(IReadOnlyList<BookEntity> left, IReadOnlyList<BookEntity> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareSortText(string left, string right)
        {
            var leftEmpty = string.IsNullOrWhiteSpace(left);
            var rightEmpty = string.IsNullOrWhiteSpace(right);
            if (leftEmpty && rightEmpty)
            {
                return 0;
            }

            if (leftEmpty)
            {
                return 1;
            }

            if (rightEmpty)
            {
                return -1;
            }

            return string.Compare(left.Trim(), right.Trim(), System.StringComparison.CurrentCultureIgnoreCase);
        }

        private void RefreshBookshelfStatusPanel()
        {
            if (bookshelfStatusPanel == null)
            {
                return;
            }

            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var showEmpty = isBookshelfFocused &&
                            !isViewingBook &&
                            !isDraggingBook &&
                            !IsBookshelfEditMode &&
                            !isBookSearchPanelOpen &&
                            !isBookPlacementEditorOpen &&
                            !HasOpenBookshelfModal() &&
                            bookshelfGenerator != null &&
                            bookshelfGenerator.ActiveBookshelfBookCount == 0;
            var showSwitching = isBookshelfFocused && isSwitching;
            var show = showEmpty || showSwitching;
            bookshelfStatusPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            if (bookshelfStatusTitleText != null)
            {
                bookshelfStatusTitleText.text = showSwitching ? "책장 이동 중" : "빈 책장";
            }

            if (bookshelfStatusMessageText != null)
            {
                bookshelfStatusMessageText.text = showSwitching
                    ? "새 책장 화면을 준비하고 있습니다."
                    : "여기에 첫 책을 추가해보세요.";
            }

            if (bookshelfStatusAddButton != null)
            {
                bookshelfStatusAddButton.gameObject.SetActive(showEmpty);
                StyleHudButton(
                    bookshelfStatusAddButton,
                    new Color32(238, 248, 241, 235),
                    new Color32(247, 255, 250, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    new Color32(53, 94, 73, 255),
                    true);
            }
        }

        private void OpenBookAddFromStatusPanel()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                return;
            }

            OpenBookAddPanel();
        }

        private void CollectActiveShelfBooks(List<BookEntity> target)
        {
            target.Clear();
            if (state == null)
            {
                return;
            }

            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book != null && !target.Contains(book))
                    {
                        target.Add(book);
                    }
                }
            }
        }

        private bool DoesBookMatchSearch(BookEntity book, string query, BookSearchFilter filter)
        {
            if (book == null)
            {
                return false;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (filter == BookSearchFilter.Reading && (meta == null || meta.readingStatus != App.Domain.ReadingStatus.Reading))
            {
                return false;
            }

            if (filter == BookSearchFilter.Read && (meta == null || meta.readingStatus != App.Domain.ReadingStatus.Read))
            {
                return false;
            }

            if (filter == BookSearchFilter.Liked && (meta == null || !meta.isLiked))
            {
                return false;
            }

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            return ContainsCatalogText(TryGetBookDisplayTitle(book), normalizedQuery) ||
                   ContainsCatalogText(meta != null ? meta.author : book.author, normalizedQuery) ||
                   ContainsCatalogText(meta != null ? meta.publisher : book.publisher, normalizedQuery);
        }

        private static void StyleBookSearchFilterButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(45, 50, 58, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(64, 70, 79, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                12,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private static void StyleBookAddReadingStatusButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(88, 133, 117, 245) : new Color32(255, 255, 255, 238),
                isActive ? new Color32(72, 114, 100, 255) : new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                12,
                isActive ? Color.white : new Color32(44, 66, 53, 255),
                true);
        }

        private static void StyleBookshelfManageSortButton(Button button, bool isEnabled)
        {
            StyleHudButton(
                button,
                isEnabled ? new Color32(255, 255, 255, 238) : new Color32(224, 219, 214, 120),
                isEnabled ? new Color32(255, 248, 236, 255) : new Color32(224, 219, 214, 150),
                new Color32(224, 219, 214, 140),
                12,
                isEnabled ? new Color32(63, 47, 38, 255) : new Color32(142, 133, 124, 190),
                true);
        }

        private static string GetVisibilityLabel(App.Domain.VisibilityScope visibility)
        {
            switch (visibility)
            {
                case App.Domain.VisibilityScope.Followers:
                    return "친구 공개";
                case App.Domain.VisibilityScope.Private:
                    return "비공개";
                default:
                    return "공개";
            }
        }

        private static bool DoesCatalogItemMatch(BookAddCatalogItem item, string query)
        {
            if (item == null)
            {
                return false;
            }

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            return ContainsCatalogText(item.Title, normalizedQuery) ||
                   ContainsCatalogText(item.Author, normalizedQuery) ||
                   ContainsCatalogText(item.Publisher, normalizedQuery);
        }

        private static bool ContainsCatalogText(string source, string query)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void StyleBookAddResultButton(Button button)
        {
            StyleHudButton(
                button,
                new Color32(255, 255, 255, 238),
                new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(44, 66, 53, 255),
                false);

            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.lineSpacing = 1.04f;
            }
        }

        private void RefreshBookshelfDesignPanel(int displayIndex)
        {
            if (!isBookshelfDesignPanelOpen ||
                bookshelfGenerator == null ||
                bookshelfDesignPanel == null)
            {
                return;
            }

            var style = bookshelfGenerator.GetBookshelfDesignStyle(displayIndex);
            StyleBookshelfDesignButton(defaultBookshelfDesignButton, BookshelfThemeStyle.Default, style == BookshelfThemeStyle.Default);
            StyleBookshelfDesignButton(warmBookshelfDesignButton, BookshelfThemeStyle.Warm, style == BookshelfThemeStyle.Warm);
            StyleBookshelfDesignButton(darkBookshelfDesignButton, BookshelfThemeStyle.Dark, style == BookshelfThemeStyle.Dark);
            StyleHudButton(
                closeBookshelfDesignButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void SetBookshelfDesignStyle(BookshelfThemeStyle style)
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            if (bookshelfGenerator.SetActiveBookshelfDesignStyle(style))
            {
                MarkBookshelfEditDirty();
                RefreshHud();
            }
        }

        private static void StyleBookshelfDesignButton(Button button, BookshelfThemeStyle style, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            Color32 background;
            Color32 highlighted;
            Color32 textColor;
            switch (style)
            {
                case BookshelfThemeStyle.Warm:
                    background = isActive ? new Color32(220, 151, 90, 255) : new Color32(255, 237, 210, 245);
                    highlighted = new Color32(239, 171, 106, 255);
                    textColor = isActive ? Color.white : new Color32(122, 70, 34, 255);
                    break;
                case BookshelfThemeStyle.Dark:
                    background = isActive ? new Color32(86, 67, 55, 255) : new Color32(232, 224, 216, 245);
                    highlighted = new Color32(105, 83, 68, 255);
                    textColor = isActive ? Color.white : new Color32(70, 54, 45, 255);
                    break;
                default:
                    background = isActive ? new Color32(197, 128, 67, 255) : new Color32(255, 255, 255, 235);
                    highlighted = isActive ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255);
                    textColor = isActive ? Color.white : new Color32(63, 47, 38, 255);
                    break;
            }

            StyleHudButton(
                button,
                background,
                highlighted,
                new Color32(224, 219, 214, 140),
                14,
                textColor,
                true);
        }

        private void RefreshBookshelfDeleteConfirmPanel(int displayIndex)
        {
            if (!isBookshelfDeleteConfirmPanelOpen ||
                bookshelfGenerator == null ||
                bookshelfDeleteConfirmPanel == null)
            {
                return;
            }

            var bookshelfName = bookshelfGenerator.GetBookshelfName(displayIndex);
            var bookCount = Mathf.Max(0, bookshelfGenerator.ActiveBookshelfBookCount);
            if (bookshelfDeleteConfirmMessageText != null)
            {
                bookshelfDeleteConfirmMessageText.text =
                    bookCount > 0
                        ? $"{bookshelfName}을 정말로 삭제하시겠습니까?\n이 책장에 있는 책 {bookCount}권은 인접한 책장으로 이동됩니다."
                        : $"{bookshelfName}을 정말로 삭제하시겠습니까?";
            }

            StyleHudButton(
                confirmDeleteBookshelfButton,
                new Color32(184, 72, 66, 255),
                new Color32(159, 56, 51, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                cancelDeleteBookshelfButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void AddBookshelf()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            CloseBookshelfNameEditPanel();
            CloseBookshelfDeleteConfirmPanel();
            CloseBookshelfDesignPanel();
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseOverlayPanels();
            pendingDeleteBookshelfIndex = -1;
            ActiveDetailBook = null;
            activeEditSection = null;

            if (bookshelfGenerator.AddBookshelf())
            {
                MarkBookshelfEditDirty();
                RefreshHud();
            }
        }

        private void DeleteActiveBookshelf()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator.BookshelfCount <= 1)
            {
                pendingDeleteBookshelfIndex = -1;
                isBookshelfDeleteConfirmPanelOpen = false;
                RefreshHud();
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            pendingDeleteBookshelfIndex = displayIndex;
            SetBookshelfOverlayMode(BookshelfOverlayMode.DeleteConfirm);
            RefreshHud();
        }

        private void ConfirmDeleteBookshelf()
        {
            if (!CanMutateBookshelfCollection() ||
                !isBookshelfDeleteConfirmPanelOpen ||
                bookshelfGenerator.BookshelfCount <= 1)
            {
                CloseBookshelfDeleteConfirmPanel();
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            if (pendingDeleteBookshelfIndex != displayIndex)
            {
                CloseBookshelfDeleteConfirmPanel();
                return;
            }

            CloseBookshelfNameEditPanel();
            CloseBookshelfDesignPanel();
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseOverlayPanels();
            ActiveDetailBook = null;
            activeEditSection = null;
            pendingDeleteBookshelfIndex = -1;
            isBookshelfDeleteConfirmPanelOpen = false;
            if (bookshelfDeleteConfirmPanel != null)
            {
                bookshelfDeleteConfirmPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null)
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            if (bookshelfGenerator.DeleteActiveBookshelf())
            {
                MarkBookshelfEditDirty();
            }

            RefreshHud();
        }

        private void CloseBookshelfDeleteConfirmPanel()
        {
            isBookshelfDeleteConfirmPanelOpen = false;
            pendingDeleteBookshelfIndex = -1;
            if (bookshelfDeleteConfirmPanel != null)
            {
                bookshelfDeleteConfirmPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private bool CanMutateBookshelfCollection()
        {
            return IsBookshelfEditMode &&
                   CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   bookshelfGenerator != null &&
                   !bookshelfGenerator.IsBookshelfSwitching &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private bool CanOpenBookAddPanel()
        {
            return CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   bookshelfGenerator != null &&
                   !bookshelfGenerator.IsBookshelfSwitching &&
                   !isBookPlacementEditorOpen &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private void RefreshBookshelfIndexDots(int displayIndex, int totalCount)
        {
            if (bookshelfIndexDotsRoot == null)
            {
                return;
            }

            var visibleDotCount = Mathf.Max(0, totalCount);
            if (visibleDotCount == 0)
            {
                for (var i = 0; i < bookshelfIndexDots.Count; i++)
                {
                    if (bookshelfIndexDots[i] != null)
                    {
                        bookshelfIndexDots[i].gameObject.SetActive(false);
                    }
                }

                return;
            }

            var activeDotIndex = Mathf.Clamp(displayIndex, 0, visibleDotCount - 1);
            var spacing = visibleDotCount <= 6 ? 6f : visibleDotCount <= 12 ? 4f : 2f;
            var inactiveWidth = visibleDotCount <= 8
                ? 8f
                : Mathf.Clamp((156f - 18f - spacing * (visibleDotCount - 1)) / Mathf.Max(1, visibleDotCount - 1), 4f, 7f);
            var activeWidth = visibleDotCount <= 8 ? 24f : Mathf.Clamp(inactiveWidth * 2.5f, 10f, 18f);
            var dotHeight = Mathf.Clamp(inactiveWidth, 4f, 8f);
            var totalDotsWidth = activeWidth + inactiveWidth * (visibleDotCount - 1) + spacing * (visibleDotCount - 1);
            bookshelfIndexDotsRoot.sizeDelta = new Vector2(Mathf.Min(156f, totalDotsWidth), 8f);
            var layoutGroup = bookshelfIndexDotsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.spacing = spacing;
            }

            while (bookshelfIndexDots.Count < visibleDotCount)
            {
                var dot = CreateHudImage(
                    bookshelfIndexDotsRoot,
                    $"BookshelfIndexDot_{bookshelfIndexDots.Count + 1}",
                    Vector2.zero,
                    new Vector2(8f, 8f),
                    new Vector2(0.5f, 0.5f),
                    new Color32(255, 255, 255, 96));
                var layoutElement = dot.gameObject.AddComponent<LayoutElement>();
                bookshelfIndexDots.Add(dot);
            }

            for (var i = 0; i < bookshelfIndexDots.Count; i++)
            {
                var dot = bookshelfIndexDots[i];
                if (dot == null)
                {
                    continue;
                }

                var isActive = i == activeDotIndex;
                dot.gameObject.SetActive(i < visibleDotCount);
                dot.color = isActive
                    ? new Color32(197, 128, 67, 235)
                    : new Color32(96, 74, 61, 70);

                var width = isActive ? activeWidth : inactiveWidth;
                var layoutElement = dot.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = width;
                    layoutElement.minWidth = width;
                    layoutElement.preferredHeight = dotHeight;
                    layoutElement.minHeight = dotHeight;
                }

                var rect = dot.rectTransform;
                rect.sizeDelta = new Vector2(width, dotHeight);
            }
        }

        private string TryGetBookDisplayTitle(BookEntity book)
        {
            if (book == null)
            {
                return string.Empty;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (meta != null && !string.IsNullOrWhiteSpace(meta.title))
            {
                return meta.title;
            }

            return string.IsNullOrWhiteSpace(book.bookId) ? "Untitled Book" : book.bookId;
        }

        private static string GetSectionAlignmentLabel(ShelfSectionAlignment alignment)
        {
            switch (alignment)
            {
                case ShelfSectionAlignment.Center:
                    return "가운데";
                case ShelfSectionAlignment.Right:
                    return "우정렬";
                default:
                    return "좌정렬";
            }
        }

        private static string GetReadingStatusLabel(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Read:
                    return "읽음";
                case App.Domain.ReadingStatus.Reading:
                    return "읽는 중";
                default:
                    return "안 읽음";
            }
        }

        private static string GetOrientationLabel(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Front:
                    return "정면";
                case BookOrientation.Angled45:
                    return "45도";
                default:
                    return "측면";
            }
        }

        private void ShowPreviousBookshelf()
        {
            SwitchBookshelf(-1);
        }

        private void ShowNextBookshelf()
        {
            SwitchBookshelf(1);
        }

        private void SwitchBookshelf(int direction)
        {
            if (direction == 0 ||
                CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                isBookPlacementEditorOpen ||
                bookshelfGenerator == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                if (TryPromptUnsavedBookshelfChanges(() => SwitchBookshelf(direction)))
                {
                    return;
                }

                SetBookshelfEditMode(false);
            }

            if (state != null && state.ActiveDraggedBook != null)
            {
                return;
            }

            if (interactionController != null &&
                state != null &&
                state.IsViewingBook)
            {
                interactionController.CloseViewedBook(true);
            }

            ActiveDetailBook = null;

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            var switched = direction > 0
                ? bookshelfGenerator.ShowNextBookshelf()
                : bookshelfGenerator.ShowPreviousBookshelf();

            if (switched)
            {
                RefreshHud();
            }
        }

        private void RefreshBookDetailPanel()
        {
            if (bookDetailPanel == null || !bookDetailPanel.activeSelf || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            bookTitleText.text = TryGetBookDisplayTitle(ActiveDetailBook);
            var section = ActiveDetailBook.CurrentSection;
            if (bookPanelEyebrowText != null)
            {
                bookPanelEyebrowText.text = "책 상세";
            }

            bookMetaText.text = $"읽기 상태  {GetReadingStatusLabel(meta.readingStatus)}\n" +
                                $"공개 범위  {(meta.isPublic ? "공개" : "비공개")}\n" +
                                $"저장 위치  {(section != null ? $"{section.row + 1}행 {section.column + 1}열" : "책장 밖")}";

            if (bookPanelHintText != null)
            {
                bookPanelHintText.text = meta.memoPreview;
            }

            if (readToggleButton != null)
            {
                readToggleButton.gameObject.SetActive(true);
            }

            if (visibilityToggleButton != null)
            {
                visibilityToggleButton.gameObject.SetActive(true);
            }

            var hasReport = !string.IsNullOrWhiteSpace(meta.reportBody);
            if (writeReportButton != null)
            {
                SetButtonLabel(writeReportButton, hasReport ? "독후감 수정" : "독후감 작성");
                StyleHudButton(
                    writeReportButton,
                    new Color32(255, 255, 255, 245),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    new Color32(63, 47, 38, 255),
                    true);
            }

            if (readReportButton != null)
            {
                readReportButton.interactable = hasReport;
                StyleHudButton(
                    readReportButton,
                    hasReport ? new Color32(255, 255, 255, 245) : new Color32(238, 231, 221, 142),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 120),
                    13,
                    hasReport ? new Color32(63, 47, 38, 255) : new Color32(124, 111, 101, 142),
                    true);
            }

            if (detailPlacementButton != null)
            {
                var canPlace = state != null && state.IsViewingBook;
                detailPlacementButton.gameObject.SetActive(canPlace);
                detailPlacementButton.interactable = canPlace;
                StyleHudButton(
                    detailPlacementButton,
                    new Color32(255, 255, 255, 245),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    new Color32(63, 47, 38, 255),
                    true);
            }

            if (closeBookDetailButton != null)
            {
                StyleHudButton(
                    closeBookDetailButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }

            SetButtonLabel(readToggleButton, $"상태: {GetReadingStatusLabel(meta.readingStatus)}");
            StyleHudButton(
                readToggleButton,
                meta.isRead ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 245),
                meta.isRead ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                meta.isRead ? Color.white : new Color32(63, 47, 38, 255),
                meta.isRead);

            SetButtonLabel(visibilityToggleButton, meta.isPublic ? "비공개로 변경" : "공개로 변경");
            StyleHudButton(
                visibilityToggleButton,
                meta.isPublic ? new Color32(88, 133, 117, 255) : new Color32(255, 255, 255, 245),
                meta.isPublic ? new Color32(72, 114, 100, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                meta.isPublic ? Color.white : new Color32(63, 47, 38, 255),
                meta.isPublic);

        }

        private void RefreshBookFocusToolbar()
        {
            if (bookFocusToolbarPanel == null)
            {
                return;
            }

            var canShow = CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                          state != null &&
                          state.IsViewingBook &&
                          state.ActiveViewedBook != null &&
                          !IsBookshelfEditMode &&
                          !isBookPlacementEditorOpen &&
                          !isBookReportWritePanelOpen &&
                          !isBookReportReadPanelOpen;
            bookFocusToolbarPanel.SetActive(canShow);
            if (!canShow)
            {
                return;
            }

            StyleBookPlacementOptionButton(bookFocusFrontButton, !isFocusedBookShowingBackCover);
            StyleBookPlacementOptionButton(bookFocusBackCoverButton, isFocusedBookShowingBackCover);
            StyleHudButton(
                bookFocusDetailButton,
                new Color32(255, 255, 255, 235),
                new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(63, 47, 38, 255),
                true);
            StyleHudButton(
                bookFocusReportButton,
                new Color32(238, 248, 241, 235),
                new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(53, 94, 73, 255),
                true);
            StyleHudButton(
                bookFocusCloseButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            bookFocusToolbarPanel.transform.SetAsLastSibling();
        }

        private void ShowFocusedBookFront()
        {
            interactionController?.ShowViewedBookFront();
            isFocusedBookShowingBackCover = false;
            RefreshBookFocusToolbar();
        }

        private void ShowFocusedBookBack()
        {
            interactionController?.ShowViewedBookBack();
            isFocusedBookShowingBackCover = true;
            RefreshBookFocusToolbar();
        }

        private void OpenActiveViewedBookDetail()
        {
            var book = state != null ? state.ActiveViewedBook : null;
            if (book != null)
            {
                OpenBookDetailPanel(book);
            }
        }

        private void OpenActiveViewedBookReport()
        {
            var book = state != null ? state.ActiveViewedBook : ActiveDetailBook;
            if (book == null)
            {
                return;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (meta != null && !string.IsNullOrWhiteSpace(meta.reportBody))
            {
                OpenBookReportReadPanel(book);
            }
            else
            {
                OpenBookReportWritePanel(book);
            }
        }

        private void CloseActiveViewedBook()
        {
            if (interactionController != null && state != null && state.IsViewingBook)
            {
                interactionController.CloseViewedBook(true);
            }
        }

        private void OpenBookReportWritePanel()
        {
            OpenBookReportWritePanel(ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null));
        }

        private void OpenBookReportWritePanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookReportWritePanel == null)
            {
                return;
            }

            CloseOverlayPanels(bookReportWritePanel);
            activeReportBook = book;
            ActiveDetailBook = book;
            isBookReportReadPanelOpen = false;
            isBookReportWritePanelOpen = true;

            var meta = dataStore.GetOrCreateBookMeta(book);
            stagedBookReportPublic = meta == null || meta.isPublic;
            if (bookReportTitleInputField != null)
            {
                bookReportTitleInputField.text = meta != null && !string.IsNullOrWhiteSpace(meta.reportTitle)
                    ? meta.reportTitle
                    : TryGetBookDisplayTitle(book);
            }

            if (bookReportBodyInputField != null)
            {
                bookReportBodyInputField.text = meta != null ? meta.reportBody : string.Empty;
            }

            RefreshHud();
        }

        private void OpenBookReportReadPanel()
        {
            OpenBookReportReadPanel(ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null));
        }

        private void OpenBookReportReadPanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookReportReadPanel == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            if (meta == null || string.IsNullOrWhiteSpace(meta.reportBody))
            {
                OpenBookReportWritePanel(book);
                return;
            }

            CloseOverlayPanels(bookReportReadPanel);
            activeReportBook = book;
            ActiveDetailBook = book;
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = true;
            RefreshHud();
        }

        private void RefreshBookReportWritePanel()
        {
            if (bookReportWritePanel == null)
            {
                return;
            }

            var show = isBookReportWritePanelOpen &&
                       activeReportBook != null &&
                       CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                       !IsBookshelfEditMode;
            bookReportWritePanel.SetActive(show);
            if (!show)
            {
                return;
            }

            if (bookReportWriteBookText != null)
            {
                var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(activeReportBook) : null;
                bookReportWriteBookText.text = meta != null
                    ? $"{TryGetBookDisplayTitle(activeReportBook)} · {meta.author}"
                    : TryGetBookDisplayTitle(activeReportBook);
            }

            StyleReportVisibilityButton(bookReportPublicButton, stagedBookReportPublic);
            StyleReportVisibilityButton(bookReportPrivateButton, !stagedBookReportPublic);
            StyleHudButton(
                saveBookReportButton,
                new Color32(88, 133, 117, 245),
                new Color32(72, 114, 100, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                cancelBookReportButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            bookReportWritePanel.transform.SetAsLastSibling();
        }

        private void RefreshBookReportReadPanel()
        {
            if (bookReportReadPanel == null)
            {
                return;
            }

            var show = isBookReportReadPanelOpen &&
                       activeReportBook != null &&
                       CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                       !IsBookshelfEditMode;
            bookReportReadPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(activeReportBook) : null;
            if (meta == null)
            {
                return;
            }

            if (bookReportReadTitleText != null)
            {
                bookReportReadTitleText.text = string.IsNullOrWhiteSpace(meta.reportTitle)
                    ? TryGetBookDisplayTitle(activeReportBook)
                    : meta.reportTitle;
            }

            if (bookReportReadMetaText != null)
            {
                var date = string.IsNullOrWhiteSpace(meta.reportUpdatedLabel) ? "방금" : meta.reportUpdatedLabel;
                bookReportReadMetaText.text = $"{TryGetBookDisplayTitle(activeReportBook)} · {date} · {(meta.isPublic ? "공개" : "비공개")}";
            }

            if (bookReportReadBodyText != null)
            {
                bookReportReadBodyText.text = string.IsNullOrWhiteSpace(meta.reportBody)
                    ? "작성된 독후감이 없습니다."
                    : meta.reportBody;
            }

            StyleHudButton(
                editBookReportButton,
                new Color32(45, 50, 58, 245),
                new Color32(64, 70, 79, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                toggleReportVisibilityButton,
                meta.isPublic ? new Color32(238, 248, 241, 235) : new Color32(255, 255, 255, 235),
                meta.isPublic ? new Color32(247, 255, 250, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                meta.isPublic ? new Color32(53, 94, 73, 255) : new Color32(63, 47, 38, 255),
                true);
            SetButtonLabel(toggleReportVisibilityButton, meta.isPublic ? "비공개로 변경" : "공개로 변경");
            StyleHudButton(
                closeBookReportReadButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            bookReportReadPanel.transform.SetAsLastSibling();
        }

        private void SetStagedReportVisibility(bool isPublic)
        {
            stagedBookReportPublic = isPublic;
            RefreshBookReportWritePanel();
        }

        private void SaveBookReport()
        {
            if (activeReportBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (meta == null)
            {
                return;
            }

            var nextReportTitle = bookReportTitleInputField != null ? bookReportTitleInputField.text.Trim() : string.Empty;
            var nextReportBody = bookReportBodyInputField != null ? bookReportBodyInputField.text.Trim() : string.Empty;
            meta.isPublic = stagedBookReportPublic;

            if (string.IsNullOrWhiteSpace(nextReportBody))
            {
                meta.reportTitle = string.Empty;
                meta.reportBody = string.Empty;
                meta.reportUpdatedLabel = string.Empty;
                meta.memoPreview = string.Empty;
                meta.isRead = false;
                meta.readingStatus = App.Domain.ReadingStatus.Unread;

                dataStore.ApplyBookMeta(activeReportBook, meta);
                isBookReportWritePanelOpen = true;
                isBookReportReadPanelOpen = false;
                RefreshHud();
                return;
            }

            meta.reportTitle = nextReportTitle;
            meta.reportBody = nextReportBody;
            meta.reportUpdatedLabel = System.DateTime.Now.ToString("yyyy.MM.dd");
            meta.memoPreview = meta.reportBody.Length > 92 ? $"{meta.reportBody.Substring(0, 92)}..." : meta.reportBody;
            meta.isRead = true;
            meta.readingStatus = App.Domain.ReadingStatus.Read;

            dataStore.ApplyBookMeta(activeReportBook, meta);
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = true;
            RefreshHud();
        }

        private void ToggleReportVisibility()
        {
            if (activeReportBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (meta == null)
            {
                return;
            }

            meta.isPublic = !meta.isPublic;
            dataStore.ApplyBookMeta(activeReportBook, meta);
            RefreshHud();
        }

        private void CloseBookReportPanels()
        {
            CloseBookReportPanelsSilently();
            RefreshHud();
        }

        private void CloseBookReportPanelsSilently()
        {
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = false;
            activeReportBook = null;
            if (bookReportWritePanel != null)
            {
                bookReportWritePanel.SetActive(false);
            }

            if (bookReportReadPanel != null)
            {
                bookReportReadPanel.SetActive(false);
            }
        }

        private static void StyleReportVisibilityButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(88, 133, 117, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(72, 114, 100, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                13,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void RefreshBookEditPanel()
        {
            if (bookEditPanel == null || !bookEditPanel.activeSelf || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            var section = ActiveDetailBook.CurrentSection;
            if (bookEditEyebrowText != null)
            {
                bookEditEyebrowText.text = "책장 편집";
            }

            if (bookEditTitleText != null)
            {
                bookEditTitleText.text = TryGetBookDisplayTitle(ActiveDetailBook);
            }

            if (bookEditMetaText != null)
            {
                bookEditMetaText.text = $"위치      {(section != null ? $"{section.row + 1}행 {section.column + 1}열" : "책장 밖")}\n" +
                                        $"상태      {GetReadingStatusLabel(meta.readingStatus)}\n" +
                                        $"공개      {(meta.isPublic ? "공개" : "비공개")}";
            }

            if (bookEditHintText != null)
            {
                bookEditHintText.text = "삭제하면 이 책은 현재 책장 배치에서 제거됩니다.";
            }

            if (deleteBookButton != null)
            {
                var isConfirmingDelete = pendingDeleteBook == ActiveDetailBook;
                deleteBookButton.gameObject.SetActive(true);
                SetButtonLabel(deleteBookButton, isConfirmingDelete ? "한 번 더 눌러 삭제" : "책 삭제");
                StyleHudButton(
                    deleteBookButton,
                    isConfirmingDelete ? new Color32(184, 72, 66, 255) : new Color32(255, 255, 255, 235),
                    isConfirmingDelete ? new Color32(159, 56, 51, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    14,
                    isConfirmingDelete ? Color.white : new Color32(120, 38, 35, 255),
                    true);
            }

            if (closeBookEditButton != null)
            {
                StyleHudButton(
                    closeBookEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private void SetActiveSectionAlignment(ShelfSectionAlignment alignment)
        {
            if (!IsBookshelfEditMode ||
                activeEditSection == null ||
                state == null)
            {
                return;
            }

            var previous = activeEditSection.alignment;
            state.SetSectionAlignment(activeEditSection, alignment, this, true);
            if (previous != activeEditSection.alignment)
            {
                MarkBookshelfEditDirty();
            }

            RefreshSectionEditPanel();
            RefreshHud();
        }

        private void RefreshSectionEditPanel()
        {
            if (sectionEditPanel == null || !sectionEditPanel.activeSelf || activeEditSection == null)
            {
                return;
            }

            if (sectionEditEyebrowText != null)
            {
                sectionEditEyebrowText.text = "책장 편집";
            }

            if (sectionEditTitleText != null)
            {
                sectionEditTitleText.text = "섹션 정렬";
            }

            if (sectionEditMetaText != null)
            {
                sectionEditMetaText.text = $"선택 위치  {activeEditSection.row + 1}행 {activeEditSection.column + 1}열\n" +
                                           $"책 수      {activeEditSection.books.Count}권\n" +
                                           $"현재 정렬  {GetSectionAlignmentLabel(activeEditSection.alignment)}";
            }

            if (sectionEditHintText != null)
            {
                sectionEditHintText.text = "섹션 안의 책을 좌, 가운데, 우 기준으로 재배치합니다.";
            }

            StyleSectionAlignmentButton(alignLeftButton, activeEditSection.alignment == ShelfSectionAlignment.Left);
            StyleSectionAlignmentButton(alignCenterButton, activeEditSection.alignment == ShelfSectionAlignment.Center);
            StyleSectionAlignmentButton(alignRightButton, activeEditSection.alignment == ShelfSectionAlignment.Right);

            if (closeSectionEditButton != null)
            {
                StyleHudButton(
                    closeSectionEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private static void StyleSectionAlignmentButton(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            StyleHudButton(
                button,
                isActive ? new Color32(197, 128, 67, 255) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                14,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void RefreshBookPlacementEditor()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var isFocusToolbarVisible = bookFocusToolbarPanel != null && bookFocusToolbarPanel.activeSelf;
            var canOpenEditor = isBookshelfFocused &&
                                isViewingBook &&
                                !IsBookshelfEditMode &&
                                !isDraggingBook &&
                                !isBookshelfSwitching &&
                                !isBookPlacementEditorOpen &&
                                !isFocusToolbarVisible &&
                                (ActiveDetailBook != null || state.ActiveViewedBook != null);

            if (openBookPlacementEditorButton != null)
            {
                openBookPlacementEditorButton.gameObject.SetActive(canOpenEditor);
                openBookPlacementEditorButton.interactable = canOpenEditor;
                StyleHudButton(
                    openBookPlacementEditorButton,
                    new Color32(255, 255, 255, 238),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (bookPlacementEditorPanel == null)
            {
                return;
            }

            if (isBookPlacementEditorOpen && placementEditBook == null)
            {
                CloseBookPlacementEditor(false);
            }

            var showPanel = isBookPlacementEditorOpen &&
                            placementEditBook != null &&
                            isBookshelfFocused &&
                            !isViewingBook &&
                            !IsBookshelfEditMode &&
                            !isDraggingBook &&
                            !isBookshelfSwitching;

            bookPlacementEditorPanel.SetActive(showPanel);
            if (!showPanel)
            {
                return;
            }

            placementStagedOrientation = placementEditBook.orientation;
            StyleBookPlacementOptionButton(placementSpineButton, placementStagedOrientation == BookOrientation.Spine);
            StyleBookPlacementOptionButton(placementAngledButton, placementStagedOrientation == BookOrientation.Angled45);
            StyleBookPlacementOptionButton(placementFrontButton, placementStagedOrientation == BookOrientation.Front);
            StyleHudButton(
                cancelPlacementButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            StyleHudButton(
                confirmPlacementButton,
                new Color32(197, 128, 67, 245),
                new Color32(214, 145, 82, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            bookPlacementEditorPanel.transform.SetAsLastSibling();
        }

        private static void StyleBookPlacementOptionButton(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            StyleHudButton(
                button,
                isActive ? new Color32(45, 50, 58, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(64, 70, 79, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void EnsureGuestbookBoardUi()
        {
            if (guestbookBoardCanvas != null)
            {
                return;
            }

            var guestbookBoard = FindFirstObjectByType<GuestbookBoardHitArea>();
            if (guestbookBoard == null)
            {
                return;
            }

            SeedGuestbookNotes();

            var canvasObject = new GameObject("GuestbookBoardCanvas");
            canvasObject.transform.SetParent(guestbookBoard.transform, false);
            canvasObject.transform.localPosition = new Vector3(-0.058f, 0f, 0f);
            canvasObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            canvasObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            guestbookBoardCanvas = canvasObject.AddComponent<Canvas>();
            guestbookBoardCanvas.renderMode = RenderMode.WorldSpace;
            guestbookBoardCanvas.worldCamera = Camera.main;
            guestbookBoardCanvas.sortingOrder = 5;
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1720f, 1220f);
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);

            guestbookPreviewPanel = CreateWorldPanel(canvasObject.transform, "GuestbookPreviewPanel", GuestbookBoardSize);
            var previewRect = guestbookPreviewPanel.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;
            guestbookPreviewPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            guestbookPreviewPanel.GetComponent<Image>().raycastTarget = false;
            guestbookPreviewPanel.AddComponent<RectMask2D>();
            CreateGuestbookPreviewNotes(guestbookPreviewPanel.transform);

            guestbookPanel = CreateWorldPanel(canvasObject.transform, "GuestbookBoardPanel", GuestbookBoardSize);
            var boardRect = guestbookPanel.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            StyleGuestbookBoardSurface(guestbookPanel);
            guestbookPanel.SetActive(false);

            var boardContentRoot = CreateWorldContainer(
                guestbookPanel.transform,
                "BoardContentRoot",
                GuestbookBoardSize - Vector2.one * GuestbookBoardPadding * 2f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            boardContentRoot.gameObject.AddComponent<RectMask2D>();

            var noteAreaSize = new Vector2(
                boardContentRoot.sizeDelta.x,
                boardContentRoot.sizeDelta.y - GuestbookInterfacePanelHeight - GuestbookPaginationHeight - GuestbookPaginationToPanelGap - GuestbookNoteToPaginationGap);

            guestbookGridRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookNoteLayer",
                noteAreaSize,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero);
            guestbookGridRect.gameObject.AddComponent<RectMask2D>();
            guestbookGridCanvasGroup = guestbookGridRect.gameObject.AddComponent<CanvasGroup>();
            guestbookGridCanvasGroup.alpha = 1f;

            guestbookGridOverlayRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookNoteOverlay",
                noteAreaSize,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero);

            guestbookEmptyStateText = CreateWorldLabel(
                guestbookGridOverlayRect,
                "EmptyState",
                Vector2.zero,
                new Vector2(760f, 44f),
                22,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleCenter);
            guestbookEmptyStateText.fontStyle = FontStyle.Bold;
            var emptyRect = guestbookEmptyStateText.rectTransform;
            emptyRect.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRect.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRect.pivot = new Vector2(0.5f, 0.5f);
            emptyRect.anchoredPosition = Vector2.zero;

            guestbookPrivacyText = CreateWorldLabel(
                guestbookGridOverlayRect,
                "PrivacyText",
                Vector2.zero,
                new Vector2(760f, 52f),
                24,
                new Color32(141, 110, 99, 255),
                TextAnchor.MiddleCenter);
            guestbookPrivacyText.fontStyle = FontStyle.Bold;
            var privacyRect = guestbookPrivacyText.rectTransform;
            privacyRect.anchorMin = new Vector2(0.5f, 0.5f);
            privacyRect.anchorMax = new Vector2(0.5f, 0.5f);
            privacyRect.pivot = new Vector2(0.5f, 0.5f);
            privacyRect.anchoredPosition = Vector2.zero;

            for (var i = 0; i < guestbookNoteCards.Length; i++)
            {
                var noteCard = CreateWorldPanel(guestbookGridRect, $"GuestbookNote_{i}", GuestbookFocusedNoteBaseSize);
                var noteRect = noteCard.GetComponent<RectTransform>();
                noteRect.anchorMin = new Vector2(0.5f, 0.5f);
                noteRect.anchorMax = new Vector2(0.5f, 0.5f);
                noteRect.pivot = new Vector2(0.5f, 0.5f);
                noteRect.anchoredPosition = Vector2.zero;
                noteCard.AddComponent<UiHoverScaleEffect>();
                StyleGuestbookCard(noteCard);

                guestbookNoteMessageTexts[i] = CreateWorldLabel(
                    noteCard.transform,
                    "Message",
                    new Vector2(20f, -22f),
                    new Vector2(320f, 104f),
                    22,
                    new Color32(78, 59, 51, 255));
                guestbookNoteMessageTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                guestbookNoteMessageTexts[i].verticalOverflow = VerticalWrapMode.Truncate;

                guestbookNoteAuthorTexts[i] = CreateWorldLabel(
                    noteCard.transform,
                    "Author",
                    new Vector2(20f, -146f),
                    new Vector2(180f, 24f),
                    16,
                    new Color32(138, 125, 118, 255),
                    TextAnchor.MiddleLeft);

                var slotIndex = i;
                guestbookReplyButtons[i] = CreateButton(
                    noteCard.transform,
                    "ReplyButton",
                    "Reply",
                    new Vector2(-20f, 20f),
                    () => ReplyToGuestbookNote(slotIndex),
                    new Vector2(92f, 34f),
                    new Vector2(1f, 0f));
                StyleGuestbookActionButton(
                    guestbookReplyButtons[i],
                    new Color32(255, 255, 255, 255),
                    new Color32(241, 235, 230, 255),
                    new Color32(240, 236, 232, 255),
                    14,
                    new Color32(78, 59, 51, 255));

                guestbookNoteCards[i] = noteRect;
            }

            guestbookPaginationRect = CreateWorldContainer(
                boardContentRoot,
                "PaginationRoot",
                new Vector2(boardContentRoot.sizeDelta.x - 56f, GuestbookPaginationHeight),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, GuestbookInterfacePanelHeight + GuestbookPaginationToPanelGap));
            ConfigureHorizontalLayout(guestbookPaginationRect, TextAnchor.MiddleCenter, 14f);

            guestbookPreviousPageButton = CreateButton(
                guestbookPaginationRect,
                "GuestbookPrevButton",
                "< Prev",
                Vector2.zero,
                () => ChangeGuestbookPage(-1),
                new Vector2(96f, 40f),
                new Vector2(0.5f, 0.5f));
            ConfigureFixedLayoutElement(guestbookPreviousPageButton.GetComponent<RectTransform>(), 96f, 40f);

            var pageNumberRowRect = CreateWorldContainer(
                guestbookPaginationRect,
                "PageNumbersRow",
                new Vector2(0f, 40f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            ConfigureHorizontalLayout(pageNumberRowRect, TextAnchor.MiddleCenter, 12f);
            ConfigureContentSizeFitter(pageNumberRowRect);
            ConfigureFixedLayoutElement(pageNumberRowRect, -1f, 40f, 72f);

            guestbookNextPageButton = CreateButton(
                guestbookPaginationRect,
                "GuestbookNextButton",
                "Next >",
                Vector2.zero,
                () => ChangeGuestbookPage(1),
                new Vector2(96f, 40f),
                new Vector2(0.5f, 0.5f));
            ConfigureFixedLayoutElement(guestbookNextPageButton.GetComponent<RectTransform>(), 96f, 40f);

            for (var i = 0; i < guestbookPageButtons.Length; i++)
            {
                var slotIndex = i;
                guestbookPageButtons[i] = CreateButton(
                    pageNumberRowRect,
                    $"GuestbookPageButton_{i}",
                    (i + 1).ToString(),
                    Vector2.zero,
                    () => GoToGuestbookPageFromSlot(slotIndex),
                    new Vector2(48f, 40f),
                    new Vector2(0.5f, 0.5f));
                ConfigureFixedLayoutElement(guestbookPageButtons[i].GetComponent<RectTransform>(), 48f, 40f);
                guestbookPageButtonPageIndices[i] = i;
            }

            guestbookInterfacePanelRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookInterfacePanel",
                new Vector2(boardContentRoot.sizeDelta.x, GuestbookInterfacePanelHeight),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero);
            var interfacePanelImage = guestbookInterfacePanelRect.gameObject.AddComponent<Image>();
            StyleGuestbookInterfacePanel(interfacePanelImage);

            guestbookHeaderRowRect = CreateWorldContainer(
                guestbookInterfacePanelRect,
                "HeaderRow",
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 44f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f));

            guestbookVisitorStatsText = CreateWorldLabel(
                guestbookHeaderRowRect,
                "VisitorStats",
                new Vector2(0f, -2f),
                new Vector2(176f, 40f),
                24,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);
            guestbookVisitorStatsText.fontStyle = FontStyle.Bold;

            guestbookLikeStatsText = CreateWorldLabel(
                guestbookHeaderRowRect,
                "LikeStats",
                new Vector2(192f, -2f),
                new Vector2(152f, 40f),
                24,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);
            guestbookLikeStatsText.fontStyle = FontStyle.Bold;

            guestbookVisibilityToggleButton = CreateButton(
                guestbookHeaderRowRect,
                "GuestbookVisibilityToggle",
                "Board Open",
                Vector2.zero,
                ToggleGuestbookVisibility,
                new Vector2(188f, 44f),
                new Vector2(1f, 1f));
            StyleGuestbookActionButton(
                guestbookVisibilityToggleButton,
                new Color32(141, 110, 99, 255),
                new Color32(123, 95, 85, 255),
                new Color32(187, 171, 162, 255),
                18,
                Color.white,
                true);

            guestbookInputRowRect = CreateWorldContainer(
                guestbookInterfacePanelRect,
                "InputRow",
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 72f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -84f));

            guestbookBoardStatusText = CreateWorldLabel(
                guestbookInterfacePanelRect,
                "BoardStatus",
                new Vector2(28f, -162f),
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 18f),
                15,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleLeft);

            guestbookInputField = CreateWorldInputField(
                guestbookInputRowRect,
                "GuestbookInputField",
                Vector2.zero,
                new Vector2(guestbookInputRowRect.sizeDelta.x - 164f, 72f),
                "Write a note... (max 30 chars)");
            guestbookInputField.characterLimit = 30;
            guestbookInputField.onValueChanged.AddListener(UpdateGuestbookCharCount);

            guestbookSubmitButton = CreateButton(
                guestbookInputRowRect,
                "GuestbookSubmitButton",
                "Pin",
                Vector2.zero,
                SubmitGuestbookMessage,
                new Vector2(148f, 72f),
                new Vector2(1f, 1f));
            StyleGuestbookActionButton(
                guestbookSubmitButton,
                new Color32(141, 110, 99, 255),
                new Color32(123, 95, 85, 255),
                new Color32(187, 171, 162, 255),
                20,
                Color.white,
                true);

            guestbookCharCountText = CreateWorldLabel(
                guestbookInputField.transform,
                "CharCount",
                new Vector2(guestbookInputField.GetComponent<RectTransform>().sizeDelta.x - 90f, -48f),
                new Vector2(70f, 18f),
                14,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleRight);

            guestbookInterfacePanelRect.SetAsLastSibling();
            UpdateGuestbookCharCount(string.Empty);
            SetGuestbookFocusPresentation(CurrentFocusArea == ExperienceFocusArea.Guestbook);
            RefreshGuestbookPanel();
        }

        private void CreateGuestbookPreviewNotes(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = 0; i < GuestbookPreviewMessages.Length; i++)
            {
                var noteCard = CreateWorldPanel(parent, $"PreviewNote_{i}", GuestbookPreviewCardSize);
                var noteRect = noteCard.GetComponent<RectTransform>();
                noteRect.anchorMin = new Vector2(0.5f, 0.5f);
                noteRect.anchorMax = new Vector2(0.5f, 0.5f);
                noteRect.pivot = new Vector2(0.5f, 0.5f);
                noteRect.anchoredPosition = GuestbookPreviewOffsets[i];
                noteRect.localRotation = Quaternion.Euler(0f, 0f, GuestbookPreviewRotations[i]);
                noteRect.localScale = Vector3.one * GuestbookPreviewScales[i];
                StyleGuestbookCard(noteCard);

                var cardImage = noteCard.GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = GetGuestbookNoteColor(i);
                    cardImage.raycastTarget = false;
                }

                var messageText = CreateWorldLabel(
                    noteCard.transform,
                    "PreviewMessage",
                    new Vector2(18f, -22f),
                    new Vector2(GuestbookPreviewCardSize.x - 36f, 70f),
                    20,
                    new Color32(78, 59, 51, 255));
                messageText.text = GuestbookPreviewMessages[i];
                messageText.raycastTarget = false;

                var captionText = CreateWorldLabel(
                    noteCard.transform,
                    "PreviewCaption",
                    new Vector2(18f, -104f),
                    new Vector2(140f, 18f),
                    13,
                    new Color32(138, 125, 118, 255),
                    TextAnchor.MiddleLeft);
                captionText.text = GuestbookPreviewCaptions[i];
                captionText.raycastTarget = false;
            }
        }

        private void SetGuestbookFocusPresentation(bool isFocused)
        {
            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(isFocused);
            }

            if (guestbookPreviewPanel != null)
            {
                guestbookPreviewPanel.SetActive(!isFocused);
            }
        }

        private void SeedGuestbookNotes()
        {
            if (guestbookNotes.Count > 0)
            {
                return;
            }

            if (dataStore != null)
            {
                var entries = dataStore.GetGuestbookEntries();
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    guestbookNotes.Add(new GuestbookNoteData
                    {
                        authorName = entry.authorName,
                        message = entry.message.Length > 30 ? entry.message.Substring(0, 30) : entry.message,
                        timestampLabel = entry.timestampLabel,
                        color = GetGuestbookNoteColor(i)
                    });
                }
            }

            if (guestbookNotes.Count == 0)
            {
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Minji", message = "The room feels warm and calm.", timestampLabel = "Today", color = GetGuestbookNoteColor(0) });
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Hyunwoo", message = "I will leave a book pick next time.", timestampLabel = "1h ago", color = GetGuestbookNoteColor(1) });
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Seoyeon", message = "The afternoon light is perfect here.", timestampLabel = "Yesterday", color = GetGuestbookNoteColor(2) });
            }
        }

        private void RefreshGuestbookPanel(bool animatePageChange = false)
        {
            if (guestbookPanel == null || guestbookBoardCanvas == null)
            {
                return;
            }

            RefreshGuestbookGridMetrics();

            if (guestbookVisitorStatsText != null)
            {
                guestbookVisitorStatsText.text = $"Visitors {guestbookVisitorCount}";
            }

            if (guestbookLikeStatsText != null)
            {
                guestbookLikeStatsText.text = $"Likes {guestbookLikeCount}";
            }

            var canWrite = isGuestbookPublic || guestbookOwnerMode;
            if (guestbookInputField != null)
            {
                guestbookInputField.interactable = canWrite;
            }

            if (guestbookSubmitButton != null)
            {
                guestbookSubmitButton.interactable = canWrite;
            }

            if (guestbookVisibilityToggleButton != null)
            {
                guestbookVisibilityToggleButton.interactable = guestbookOwnerMode;
                var buttonLabel = guestbookVisibilityToggleButton.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    buttonLabel.text = isGuestbookPublic ? "Board Open" : "Board Closed";
                }
            }

            RefreshGuestbookPagination();

            if (guestbookPageFadeCoroutine != null)
            {
                StopCoroutine(guestbookPageFadeCoroutine);
                guestbookPageFadeCoroutine = null;
            }

            if (animatePageChange && guestbookPanel.activeSelf && guestbookGridCanvasGroup != null)
            {
                guestbookPageFadeCoroutine = StartCoroutine(AnimateGuestbookPageTransition());
            }
            else
            {
                if (guestbookGridCanvasGroup != null)
                {
                    guestbookGridCanvasGroup.alpha = 1f;
                }

                ApplyGuestbookPageContent();
            }

            if (guestbookBoardStatusText != null && string.IsNullOrWhiteSpace(guestbookBoardStatusText.text))
            {
                guestbookBoardStatusText.text = canWrite
                    ? "Leave a short note and pin it to the board."
                    : "Board is closed right now.";
            }
        }

        private void RefreshGuestbookGridMetrics()
        {
            if (guestbookGridRect == null)
            {
                return;
            }

            guestbookColumnCount = GuestbookVisibleColumns;
            guestbookCardsPerPage = guestbookColumnCount * GuestbookVisibleRows;

            var noteLayerSize = new Vector2(
                GuestbookBoardSize.x - GuestbookBoardPadding * 2f,
                GuestbookBoardSize.y - GuestbookBoardPadding * 2f - GuestbookInterfacePanelHeight - GuestbookPaginationHeight - GuestbookPaginationToPanelGap - GuestbookNoteToPaginationGap);

            guestbookGridRect.sizeDelta = noteLayerSize;
            if (guestbookGridOverlayRect != null)
            {
                guestbookGridOverlayRect.sizeDelta = noteLayerSize;
            }

            var cardScale = Mathf.Clamp01(Mathf.Min(
                noteLayerSize.x / (GuestbookFocusedNoteBaseSize.x * 3.9f),
                noteLayerSize.y / (GuestbookFocusedNoteBaseSize.y * 3.35f)));
            cardScale = Mathf.Max(0.84f, cardScale);

            var cardWidth = Mathf.Round(GuestbookFocusedNoteBaseSize.x * cardScale);
            var cardHeight = Mathf.Round(GuestbookFocusedNoteBaseSize.y * cardScale);
            guestbookCurrentCardSize = new Vector2(cardWidth, cardHeight);

            for (var i = 0; i < guestbookNoteCards.Length; i++)
            {
                UpdateGuestbookCardLayout(i, cardWidth, cardHeight);
            }

            if (guestbookEmptyStateText != null)
            {
                guestbookEmptyStateText.rectTransform.sizeDelta = new Vector2(noteLayerSize.x - 80f, 44f);
            }

            if (guestbookPrivacyText != null)
            {
                guestbookPrivacyText.rectTransform.sizeDelta = new Vector2(noteLayerSize.x - 80f, 52f);
            }

            guestbookCurrentPageIndex = Mathf.Clamp(guestbookCurrentPageIndex, 0, GetGuestbookTotalPages() - 1);

            if (guestbookInterfacePanelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(guestbookInterfacePanelRect);
            }
        }

        private void UpdateGuestbookCardLayout(int index, float cardWidth, float cardHeight)
        {
            if (index < 0 || index >= guestbookNoteCards.Length || guestbookNoteCards[index] == null)
            {
                return;
            }

            guestbookNoteCards[index].sizeDelta = new Vector2(cardWidth, cardHeight);

            var messageText = guestbookNoteMessageTexts[index];
            if (messageText != null)
            {
                messageText.rectTransform.anchoredPosition = new Vector2(18f, -20f);
                messageText.rectTransform.sizeDelta = new Vector2(cardWidth - 36f, cardHeight - 84f);
                messageText.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardHeight * 0.12f), 18, 21);
            }

            var authorText = guestbookNoteAuthorTexts[index];
            if (authorText != null)
            {
                authorText.rectTransform.anchoredPosition = new Vector2(18f, -(cardHeight - 40f));
                authorText.rectTransform.sizeDelta = new Vector2(cardWidth - 132f, 20f);
                authorText.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardHeight * 0.09f), 14, 15);
            }

            var replyButton = guestbookReplyButtons[index];
            if (replyButton != null)
            {
                var replyRect = replyButton.GetComponent<RectTransform>();
                replyRect.anchorMin = new Vector2(1f, 0f);
                replyRect.anchorMax = new Vector2(1f, 0f);
                replyRect.pivot = new Vector2(1f, 0f);
                replyRect.anchoredPosition = new Vector2(-18f, 18f);
                replyRect.sizeDelta = new Vector2(Mathf.Clamp(cardWidth * 0.36f, 82f, 94f), 32f);

                var replyLabel = replyButton.GetComponentInChildren<Text>();
                if (replyLabel != null)
                {
                    replyLabel.fontSize = 13;
                }
            }
        }

        private void ApplyGuestbookPageContent()
        {
            var showPrivacyMessage = !isGuestbookPublic;
            var noteCount = showPrivacyMessage ? 0 : guestbookNotes.Count;
            var startIndex = guestbookCurrentPageIndex * guestbookCardsPerPage;

            if (guestbookPrivacyText != null)
            {
                guestbookPrivacyText.text = "Board is currently closed.";
                guestbookPrivacyText.gameObject.SetActive(showPrivacyMessage);
            }

            if (guestbookEmptyStateText != null)
            {
                guestbookEmptyStateText.text = "No notes yet. Pin the first memo.";
                guestbookEmptyStateText.gameObject.SetActive(!showPrivacyMessage && noteCount == 0);
            }

            for (var slotIndex = 0; slotIndex < guestbookNoteCards.Length; slotIndex++)
            {
                if (guestbookNoteCards[slotIndex] == null)
                {
                    continue;
                }

                var noteIndex = startIndex + slotIndex;
                var isVisible = !showPrivacyMessage && slotIndex < guestbookCardsPerPage && noteIndex < noteCount;
                guestbookNoteCards[slotIndex].gameObject.SetActive(isVisible);
                guestbookNoteCards[slotIndex].anchoredPosition = Vector2.zero;
                guestbookNoteCards[slotIndex].localRotation = Quaternion.identity;

                var hoverEffect = guestbookNoteCards[slotIndex].GetComponent<UiHoverScaleEffect>();
                if (hoverEffect != null)
                {
                    hoverEffect.SetBaseScale(1f);
                }
                else
                {
                    guestbookNoteCards[slotIndex].localScale = Vector3.one;
                }

                if (!isVisible)
                {
                    continue;
                }

                var note = guestbookNotes[noteIndex];
                var cardImage = guestbookNoteCards[slotIndex].GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = note.color.a > 0f ? note.color : GetGuestbookNoteColor(noteIndex);
                }

                if (guestbookNoteMessageTexts[slotIndex] != null)
                {
                    guestbookNoteMessageTexts[slotIndex].text = note.message;
                }

                if (guestbookNoteAuthorTexts[slotIndex] != null)
                {
                    guestbookNoteAuthorTexts[slotIndex].text = $"by {note.authorName}";
                }

                if (guestbookReplyButtons[slotIndex] != null)
                {
                    guestbookReplyButtons[slotIndex].gameObject.SetActive(true);
                    guestbookReplyButtons[slotIndex].interactable = guestbookOwnerMode;
                }

                guestbookNoteCards[slotIndex].anchoredPosition = GetGuestbookFocusedNotePosition(slotIndex, note, noteIndex);
                guestbookNoteCards[slotIndex].localRotation = Quaternion.Euler(0f, 0f, GetGuestbookFocusedNoteRotation(slotIndex, note, noteIndex));
                var noteScale = GetGuestbookFocusedNoteScale(slotIndex, note, noteIndex);
                if (hoverEffect != null)
                {
                    hoverEffect.SetBaseScale(noteScale);
                }
                else
                {
                    guestbookNoteCards[slotIndex].localScale = Vector3.one * noteScale;
                }
                guestbookNoteCards[slotIndex].SetSiblingIndex(slotIndex);
            }
        }

        private Vector2 GetGuestbookFocusedNotePosition(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var anchor = GuestbookFocusedNoteAnchors[slotIndex % GuestbookFocusedNoteAnchors.Length];
            var horizontalSpan = Mathf.Max(0f, (guestbookGridRect.sizeDelta.x - guestbookCurrentCardSize.x - 120f) * 0.5f);
            var verticalSpan = Mathf.Max(0f, (guestbookGridRect.sizeDelta.y - guestbookCurrentCardSize.y - 88f) * 0.5f);
            var seed = GetGuestbookNoteSeed(noteIndex, note);

            var basePosition = new Vector2(anchor.x * horizontalSpan, anchor.y * verticalSpan);
            var jitter = new Vector2(
                GetGuestbookNoteVariation(seed, 0.37f, -18f, 18f),
                GetGuestbookNoteVariation(seed, 1.13f, -14f, 14f));

            return new Vector2(
                Mathf.Clamp(basePosition.x + jitter.x, -horizontalSpan, horizontalSpan),
                Mathf.Clamp(basePosition.y + jitter.y, -verticalSpan, verticalSpan));
        }

        private float GetGuestbookFocusedNoteRotation(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var seed = GetGuestbookNoteSeed(noteIndex, note);
            var baseRotation = GuestbookFocusedNoteRotations[slotIndex % GuestbookFocusedNoteRotations.Length];
            var rotationJitter = GetGuestbookNoteVariation(seed, 0.79f, -0.9f, 0.9f);
            return Mathf.Clamp(baseRotation + rotationJitter, -5f, 5f);
        }

        private float GetGuestbookFocusedNoteScale(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var seed = GetGuestbookNoteSeed(noteIndex, note);
            var baseScale = GuestbookFocusedNoteScales[slotIndex % GuestbookFocusedNoteScales.Length];
            var scaleJitter = GetGuestbookNoteVariation(seed, 1.61f, -0.015f, 0.015f);
            return Mathf.Clamp(baseScale + scaleJitter, 0.95f, 1.05f);
        }

        private static int GetGuestbookNoteSeed(int noteIndex, GuestbookNoteData note)
        {
            unchecked
            {
                var seed = noteIndex * 397;
                seed = (seed * 397) ^ (note?.authorName?.GetHashCode() ?? 0);
                seed = (seed * 397) ^ (note?.message?.GetHashCode() ?? 0);
                return seed;
            }
        }

        private static float GetGuestbookNoteVariation(int seed, float salt, float min, float max)
        {
            var value = Mathf.Sin(seed * 0.0173f + salt * 12.9898f) * 43758.5453f;
            value -= Mathf.Floor(value);
            return Mathf.Lerp(min, max, value);
        }

        private void RefreshGuestbookPagination()
        {
            var totalPages = isGuestbookPublic ? GetGuestbookTotalPages() : 1;
            guestbookCurrentPageIndex = Mathf.Clamp(guestbookCurrentPageIndex, 0, totalPages - 1);

            if (guestbookPreviousPageButton != null)
            {
                guestbookPreviousPageButton.interactable = isGuestbookPublic && guestbookCurrentPageIndex > 0;
                StyleGuestbookActionButton(
                    guestbookPreviousPageButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(232, 225, 220, 255),
                    16,
                    new Color32(78, 59, 51, 255));
            }

            if (guestbookNextPageButton != null)
            {
                guestbookNextPageButton.interactable = isGuestbookPublic && guestbookCurrentPageIndex < totalPages - 1;
                StyleGuestbookActionButton(
                    guestbookNextPageButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(232, 225, 220, 255),
                    16,
                    new Color32(78, 59, 51, 255));
            }

            var windowStart = Mathf.Clamp(
                guestbookCurrentPageIndex - 1,
                0,
                Mathf.Max(0, totalPages - GuestbookMaxPageButtons));

            for (var i = 0; i < guestbookPageButtons.Length; i++)
            {
                if (guestbookPageButtons[i] == null)
                {
                    continue;
                }

                var pageIndex = windowStart + i;
                var isVisible = pageIndex < totalPages;
                guestbookPageButtons[i].gameObject.SetActive(isVisible);
                guestbookPageButtonPageIndices[i] = isVisible ? pageIndex : -1;

                if (!isVisible)
                {
                    continue;
                }

                var isCurrentPage = pageIndex == guestbookCurrentPageIndex;
                var pageLabel = guestbookPageButtons[i].GetComponentInChildren<Text>();
                if (pageLabel != null)
                {
                    pageLabel.text = (pageIndex + 1).ToString();
                }

                guestbookPageButtons[i].interactable = isGuestbookPublic && !isCurrentPage;
                StyleGuestbookActionButton(
                    guestbookPageButtons[i],
                    isCurrentPage ? new Color32(141, 110, 99, 255) : new Color32(255, 255, 255, 235),
                    isCurrentPage ? new Color32(123, 95, 85, 255) : new Color32(241, 235, 230, 255),
                    isCurrentPage ? new Color32(141, 110, 99, 255) : new Color32(232, 225, 220, 255),
                    16,
                    isCurrentPage ? Color.white : new Color32(78, 59, 51, 255),
                    isCurrentPage);
            }

            if (guestbookPaginationRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(guestbookPaginationRect);
            }
        }

        private int GetGuestbookTotalPages()
        {
            return Mathf.Max(1, Mathf.CeilToInt(guestbookNotes.Count / Mathf.Max(1f, guestbookCardsPerPage)));
        }

        private void ChangeGuestbookPage(int direction)
        {
            if (!isGuestbookPublic)
            {
                return;
            }

            GoToGuestbookPage(guestbookCurrentPageIndex + direction);
        }

        private void GoToGuestbookPageFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= guestbookPageButtonPageIndices.Length)
            {
                return;
            }

            GoToGuestbookPage(guestbookPageButtonPageIndices[slotIndex]);
        }

        private void GoToGuestbookPage(int pageIndex)
        {
            var clampedPageIndex = Mathf.Clamp(pageIndex, 0, GetGuestbookTotalPages() - 1);
            if (clampedPageIndex == guestbookCurrentPageIndex)
            {
                return;
            }

            guestbookCurrentPageIndex = clampedPageIndex;
            RefreshGuestbookPanel(true);
        }

        private IEnumerator AnimateGuestbookPageTransition()
        {
            yield return FadeCanvasGroup(guestbookGridCanvasGroup, guestbookGridCanvasGroup.alpha, 0f, GuestbookPageFadeDuration * 0.5f);
            ApplyGuestbookPageContent();
            yield return FadeCanvasGroup(guestbookGridCanvasGroup, guestbookGridCanvasGroup.alpha, 1f, GuestbookPageFadeDuration * 0.5f);
            guestbookPageFadeCoroutine = null;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            var elapsed = 0f;
            canvasGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private void UpdateGuestbookCharCount(string value)
        {
            if (guestbookCharCountText != null)
            {
                guestbookCharCountText.text = $"{(value ?? string.Empty).Length}/30";
            }
        }

        private void SubmitGuestbookMessage()
        {
            if (guestbookInputField == null)
            {
                return;
            }

            var trimmed = (guestbookInputField.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (guestbookBoardStatusText != null)
                {
                    guestbookBoardStatusText.text = "Write a short note before pinning it.";
                }

                return;
            }

            if (trimmed.Length > 30)
            {
                trimmed = trimmed.Substring(0, 30);
            }

            guestbookNotes.Insert(0, new GuestbookNoteData
            {
                authorName = guestbookOwnerMode ? "Host" : "Visitor",
                message = trimmed,
                timestampLabel = "Just now",
                color = GetGuestbookNoteColor(0)
            });

            if (guestbookNotes.Count > GuestbookMaxPinnedNotes)
            {
                guestbookNotes.RemoveAt(guestbookNotes.Count - 1);
            }

            guestbookCurrentPageIndex = 0;
            guestbookInputField.text = string.Empty;

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = "A new note was pinned to the board.";
            }

            RefreshGuestbookPanel();
        }

        private void ReplyToGuestbookNote(int slotIndex)
        {
            var noteIndex = guestbookCurrentPageIndex * guestbookCardsPerPage + slotIndex;
            if (!guestbookOwnerMode || noteIndex < 0 || noteIndex >= guestbookNotes.Count)
            {
                return;
            }

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = $"Reply draft prepared for {guestbookNotes[noteIndex].authorName}.";
            }
        }

        private void ToggleGuestbookVisibility()
        {
            if (!guestbookOwnerMode)
            {
                return;
            }

            isGuestbookPublic = !isGuestbookPublic;
            guestbookCurrentPageIndex = 0;

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = isGuestbookPublic
                    ? "Board visibility changed to public."
                    : "Board visibility changed to private.";
            }

            RefreshGuestbookPanel();
        }

        private Color GetGuestbookNoteColor(int noteIndex)
        {
            return guestbookNoteColors[Mathf.Abs(noteIndex) % guestbookNoteColors.Length];
        }

        private static void StyleGuestbookBoardSurface(GameObject panel)
        {
            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(245, 241, 232, 255);
            image.raycastTarget = true;
            AddSoftShadow(panel, new Color(0f, 0f, 0f, 0.08f), new Vector2(0f, -10f));
        }

        private static void StyleGuestbookInterfacePanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 251, 246, 232);
            image.raycastTarget = true;
            AddSoftShadow(image.gameObject, new Color(0f, 0f, 0f, 0.07f), new Vector2(0f, -6f));
        }

        private static void StyleGuestbookCard(GameObject noteCard)
        {
            var image = noteCard.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 249, 196, 255);
            image.raycastTarget = true;
            AddSoftShadow(noteCard, new Color(0f, 0f, 0f, 0.08f), new Vector2(6f, -8f));
            EnsureGuestbookTapeStrip(noteCard.transform, "TapeLeft", new Vector2(0.31f, 1f), new Vector2(68f, 20f), -7f);
            EnsureGuestbookTapeStrip(noteCard.transform, "TapeRight", new Vector2(0.69f, 1f), new Vector2(68f, 20f), 6f);
        }

        private static void EnsureGuestbookTapeStrip(Transform parent, string name, Vector2 anchor, Vector2 size, float rotationZ)
        {
            if (parent == null)
            {
                return;
            }

            var tapeTransform = parent.Find(name);
            Image tapeImage;
            RectTransform tapeRect;
            if (tapeTransform == null)
            {
                var tapeObject = new GameObject(name);
                tapeObject.transform.SetParent(parent, false);
                tapeRect = tapeObject.AddComponent<RectTransform>();
                tapeImage = tapeObject.AddComponent<Image>();
            }
            else
            {
                tapeRect = tapeTransform as RectTransform;
                tapeImage = tapeTransform.GetComponent<Image>();
                if (tapeImage == null)
                {
                    tapeImage = tapeTransform.gameObject.AddComponent<Image>();
                }
            }

            ApplyRoundedSprite(tapeImage);
            tapeImage.color = new Color32(255, 255, 255, 102);
            tapeImage.raycastTarget = false;

            tapeRect.anchorMin = anchor;
            tapeRect.anchorMax = anchor;
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.anchoredPosition = new Vector2(0f, -12f);
            tapeRect.sizeDelta = size;
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            tapeRect.localScale = Vector3.one;
            tapeRect.SetAsFirstSibling();
        }

        private static void StyleGuestbookActionButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                ApplyRoundedSprite(image);
                image.color = background;
            }

            AddSoftShadow(button.gameObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -2f));

            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = highlightedBackground;
            colors.pressedColor = Color.Lerp(highlightedBackground, Color.black, 0.12f);
            colors.selectedColor = highlightedBackground;
            colors.disabledColor = disabledBackground;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.color = textColor;
                label.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private static RectTransform CreateWorldContainer(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void ConfigureHorizontalLayout(RectTransform rectTransform, TextAnchor alignment, float spacing)
        {
            if (rectTransform == null)
            {
                return;
            }

            var layoutGroup = rectTransform.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layoutGroup.childAlignment = alignment;
            layoutGroup.spacing = spacing;
            layoutGroup.padding = new RectOffset();
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childScaleWidth = false;
            layoutGroup.childScaleHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
        }

        private static void ConfigureContentSizeFitter(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            var fitter = rectTransform.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureFixedLayoutElement(RectTransform rectTransform, float width, float height, float minWidth = -1f)
        {
            if (rectTransform == null)
            {
                return;
            }

            var layoutElement = rectTransform.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
            }

            if (width >= 0f)
            {
                layoutElement.preferredWidth = width;
                layoutElement.minWidth = minWidth >= 0f ? minWidth : width;
            }
            else if (minWidth >= 0f)
            {
                layoutElement.minWidth = minWidth;
            }

            if (height >= 0f)
            {
                layoutElement.preferredHeight = height;
                layoutElement.minHeight = height;
            }
        }

        private static void ApplyRoundedSprite(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GetRoundedUiSprite();
            image.type = Image.Type.Sliced;
        }

        private static Sprite GetRoundedUiSprite()
        {
            if (roundedUiSprite == null)
            {
                const int textureSize = 32;
                const float cornerRadius = 11f;
                roundedUiTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                {
                    name = "GeneratedUiSprite",
                    hideFlags = HideFlags.HideAndDontSave
                };

                roundedUiTexture.wrapMode = TextureWrapMode.Clamp;
                roundedUiTexture.filterMode = FilterMode.Bilinear;
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var nearestX = Mathf.Clamp(x, cornerRadius, textureSize - 1f - cornerRadius);
                        var nearestY = Mathf.Clamp(y, cornerRadius, textureSize - 1f - cornerRadius);
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                        var alpha = Mathf.Clamp01(cornerRadius + 0.5f - distance);
                        roundedUiTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                roundedUiTexture.Apply();

                roundedUiSprite = Sprite.Create(
                    roundedUiTexture,
                    new Rect(0f, 0f, textureSize, textureSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(12f, 12f, 12f, 12f));
                roundedUiSprite.name = "GeneratedUiSprite";
                roundedUiSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return roundedUiSprite;
        }

        private static void AddSoftShadow(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Shadow shadow = null;
            var shadows = target.GetComponents<Shadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void AddSoftOutline(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void StyleHudPanel(Image image, Color background)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = background;
            AddSoftShadow(image.gameObject, new Color(0f, 0f, 0f, 0.12f), new Vector2(0f, -5f));
        }

        private static void StyleBookDetailPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 248, 236, 242);
            AddSoftShadow(image.gameObject, new Color(46f / 255f, 30f / 255f, 20f / 255f, 0.20f), new Vector2(0f, -9f));
            AddSoftOutline(image.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));
        }

        private static void StyleBookshelfIndexPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 248, 236, 214);
            AddSoftShadow(image.gameObject, new Color(46f / 255f, 30f / 255f, 20f / 255f, 0.18f), new Vector2(0f, -7f));
            AddSoftOutline(image.gameObject, new Color32(255, 255, 255, 132), new Vector2(1f, -1f));
        }

        private static void PrepareBookshelfNavigationButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            var hoverEffect = button.GetComponent<UiHoverScaleEffect>();
            if (hoverEffect == null)
            {
                hoverEffect = button.gameObject.AddComponent<UiHoverScaleEffect>();
            }

            hoverEffect.SetBaseScale(Vector3.one);
            AddSoftOutline(button.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));
        }

        private static void StyleBookshelfNavigationButton(Button button, bool isInteractable)
        {
            if (button == null)
            {
                return;
            }

            var background = isInteractable
                ? new Color32(255, 248, 236, 222)
                : new Color32(238, 231, 221, 96);
            var highlighted = new Color32(255, 255, 255, 245);
            var disabled = new Color32(224, 216, 205, 92);
            var textColor = isInteractable
                ? new Color32(66, 49, 39, 255)
                : new Color32(124, 111, 101, 142);

            StyleHudButton(button, background, highlighted, disabled, 34, textColor, true);
            AddSoftOutline(button.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            }
        }

        private static void StyleHudButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            StyleGuestbookActionButton(button, background, highlightedBackground, disabledBackground, fontSize, textColor, isBold);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void CloseOverlayPanels(GameObject except = null)
        {
            if (bookPlacementEditorPanel != null && bookPlacementEditorPanel != except)
            {
                CancelBookPlacementEdit(false);
            }

            if (bookDetailPanel != null && bookDetailPanel != except)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookSearchPanel != null && bookSearchPanel != except)
            {
                isBookSearchPanelOpen = false;
                bookSearchPanel.SetActive(false);
                bookSearchResults.Clear();
            }

            if (bookReportWritePanel != null && bookReportWritePanel != except)
            {
                bookReportWritePanel.SetActive(false);
                isBookReportWritePanelOpen = false;
            }

            if (bookReportReadPanel != null && bookReportReadPanel != except)
            {
                bookReportReadPanel.SetActive(false);
                isBookReportReadPanelOpen = false;
            }

            if (except != bookReportWritePanel && except != bookReportReadPanel)
            {
                activeReportBook = null;
            }

            if (bookEditPanel != null && bookEditPanel != except)
            {
                bookEditPanel.SetActive(false);
                if (isBookEditPanelOpen)
                {
                    ActiveDetailBook = null;
                    pendingDeleteBook = null;
                    isBookEditPanelOpen = false;
                }
            }

            if (sectionEditPanel != null && sectionEditPanel != except)
            {
                sectionEditPanel.SetActive(false);
                activeEditSection = null;
            }

            if (guestbookPanel != null && guestbookPanel != except)
            {
                guestbookPanel.SetActive(false);
            }

            if (bookAddPanel != null && bookAddPanel != except)
            {
                CloseBookAddPanel(false);
            }

            if (bookAddConfirmPanel != null && bookAddConfirmPanel != except)
            {
                isBookAddConfirmPanelOpen = false;
                pendingBookAddCatalogIndex = -1;
                bookAddConfirmPanel.SetActive(false);
            }

            if (bookshelfManagePanel != null && bookshelfManagePanel != except)
            {
                isBookshelfManagePanelOpen = false;
                bookshelfManagePanel.SetActive(false);
            }

            if (bookshelfUnsavedConfirmPanel != null && bookshelfUnsavedConfirmPanel != except)
            {
                isBookshelfUnsavedConfirmPanelOpen = false;
                pendingBookshelfNavigationAfterSave = null;
                bookshelfUnsavedConfirmPanel.SetActive(false);
            }
        }

        private static GameObject CreateWorldPanel(Transform parent, string name, Vector2 size)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Text CreateWorldLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color)
        {
            return CreateWorldLabel(parent, name, anchoredPosition, size, fontSize, color, TextAnchor.UpperLeft);
        }

        private static Text CreateWorldLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.text = string.Empty;
            text.supportRichText = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static InputField CreateWorldInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholder)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = Color.white;

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            AddSoftShadow(inputObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -3f));

            var colors = inputField.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.99f, 0.99f, 0.99f, 1f);
            colors.pressedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.selectedColor = new Color(0.99f, 0.99f, 0.99f, 1f);
            colors.disabledColor = new Color(0.95f, 0.94f, 0.92f, 1f);
            colors.fadeDuration = 0.1f;
            inputField.colors = colors;

            var placeholderText = CreateWorldLabel(
                inputObject.transform,
                "Placeholder",
                new Vector2(18f, -20f),
                new Vector2(size.x - 132f, 32f),
                19,
                new Color32(167, 154, 146, 255),
                TextAnchor.MiddleLeft);
            placeholderText.text = placeholder;
            var text = CreateWorldLabel(
                inputObject.transform,
                "Text",
                new Vector2(18f, -20f),
                new Vector2(size.x - 132f, 32f),
                19,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private static InputField CreateHudInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholder)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = new Color32(255, 255, 255, 242);
            inputObject.AddComponent<RectMask2D>();

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            inputField.selectionColor = new Color32(197, 128, 67, 96);

            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            AddSoftOutline(inputObject, new Color32(205, 186, 168, 126), new Vector2(1f, -1f));

            var colors = inputField.colors;
            colors.normalColor = new Color32(255, 255, 255, 242);
            colors.highlightedColor = new Color32(255, 248, 236, 255);
            colors.pressedColor = new Color32(255, 248, 236, 255);
            colors.selectedColor = new Color32(255, 248, 236, 255);
            colors.disabledColor = new Color32(238, 231, 221, 142);
            colors.fadeDuration = 0.1f;
            inputField.colors = colors;

            var placeholderText = CreateInputLabel(inputObject.transform, "Placeholder", placeholder, size, new Color32(151, 136, 124, 255));
            var text = CreateInputLabel(inputObject.transform, "Text", string.Empty, size, new Color32(63, 47, 38, 255));
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private static GameObject CreateModalBackdrop(Transform parent, string name, Color color)
        {
            var backdropObject = new GameObject(name);
            backdropObject.transform.SetParent(parent, false);

            var image = backdropObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            var rect = backdropObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return backdropObject;
        }

        private static Text CreateInputLabel(Transform parent, string name, string value, Vector2 parentSize, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontSize = 15;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(34f, 3f);
            rect.offsetMax = new Vector2(-14f, -3f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(parent, name, label, anchoredPosition, onClick, new Vector2(170f, 40f), new Vector2(1f, 1f));
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.14f, 0.86f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var labelText = CreateLabel(buttonObject.transform, "Label", Vector2.zero, size, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.fontSize = 18;

            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
            image.raycastTarget = false;

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static RectTransform CreateHudContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static GameObject CreateHudScrollViewport(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var viewportObject = new GameObject(name);
            viewportObject.transform.SetParent(parent, false);

            var image = viewportObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.002f);
            image.raycastTarget = true;
            viewportObject.AddComponent<RectMask2D>();

            var rect = viewportObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            var scrollRect = viewportObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 22f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return viewportObject;
        }

        private static RectTransform CreateHudScrollContent(Transform parent, string name, Vector2 size)
        {
            var contentObject = new GameObject(name);
            contentObject.transform.SetParent(parent, false);

            var rect = contentObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, size.y);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Scrollbar CreateHudVerticalScrollbar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var scrollbarObject = new GameObject(name);
            scrollbarObject.transform.SetParent(parent, false);

            var trackImage = scrollbarObject.AddComponent<Image>();
            ApplyRoundedSprite(trackImage);
            trackImage.color = new Color32(238, 231, 221, 150);
            trackImage.raycastTarget = true;

            var rect = scrollbarObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            var handleObject = new GameObject("Handle");
            handleObject.transform.SetParent(scrollbarObject.transform, false);
            var handleImage = handleObject.AddComponent<Image>();
            ApplyRoundedSprite(handleImage);
            handleImage.color = new Color32(73, 117, 147, 210);
            handleImage.raycastTarget = true;

            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = new Vector2(1f, 1f);
            handleRect.offsetMax = new Vector2(-1f, -1f);
            handleRect.localScale = Vector3.one;

            var scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            return scrollbar;
        }

        private static void StyleHudScrollbar(Scrollbar scrollbar, Color32 handleColor, Color32 trackColor)
        {
            if (scrollbar == null)
            {
                return;
            }

            var trackImage = scrollbar.GetComponent<Image>();
            if (trackImage != null)
            {
                ApplyRoundedSprite(trackImage);
                trackImage.color = trackColor;
            }

            if (scrollbar.handleRect == null)
            {
                return;
            }

            var handleImage = scrollbar.handleRect.GetComponent<Image>();
            if (handleImage != null)
            {
                ApplyRoundedSprite(handleImage);
                handleImage.color = handleColor;
            }
        }

        private static Image CreateHudImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = color;
            image.raycastTarget = false;

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return image;
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = alignment;
            text.text = string.Empty;
            text.raycastTarget = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.one;
            rect.pivot = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            return text;
        }

        private bool TryHitGuestbookBoard()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<GuestbookBoardHitArea>(ray);
        }

        private bool TryHitNotebook()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<NotebookHitArea>(ray);
        }

        private bool TryHitBookshelf()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null || Mouse.current == null)
            {
                return false;
            }

            return TryHitBookshelf(Mouse.current.position.ReadValue());
        }

        private bool TryHitBookshelf(Vector2 pointer)
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                if (collider.GetComponentInParent<BookEntity>() != null ||
                    collider.GetComponent<BookshelfFocusHitArea>() != null)
                {
                    return true;
                }

                if (collider.GetComponent<NotebookHitArea>() != null ||
                    collider.GetComponent<GuestbookBoardHitArea>() != null)
                {
                    return false;
                }
            }

            return false;
        }

        private void ConfigureNotebookScreen()
        {
            if (notebookScreenController == null)
            {
                notebookScreenController = FindFirstObjectByType<NotebookScreenController>();
            }

            if (notebookScreenController == null || Camera.main == null)
            {
                return;
            }

            var screenTransform = notebookScreenController.transform.Find("Screen");
            notebookScreenController.Configure(
                screenTransform,
                Camera.main,
                () => notebookScreenController.ReturnToScreenSaver(),
                CloseNotebookFocus);
        }

        private void ShowNotebookScreen()
        {
            if (notebookScreenController == null)
            {
                ConfigureNotebookScreen();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.Show();
            }
        }

        private void HideNotebookScreen()
        {
            if (notebookScreenController != null)
            {
                notebookScreenController.Hide();
            }
        }

        private static bool TryHitClosest<T>(Ray ray) where T : Component
        {
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0)
            {
                return false;
            }

            var closestDistance = float.MaxValue;
            Collider closestCollider = null;
            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[i].distance;
                closestCollider = hits[i].collider;
            }

            return closestCollider != null && closestCollider.GetComponent<T>() != null;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
