using System.Collections.Generic;
using App.Application.Books;
using App.Domain;

namespace App.Infrastructure.Mock
{
    public sealed class MockBookCatalogRepository : IBookCatalogRepository
    {
        private const int Rows = 4;
        private const int Columns = 4;
        private const int BooksPerSection = 3;

        private static readonly string[] Titles =
        {
            "모순",
            "불편한 편의점",
            "아몬드",
            "작별인사",
            "파친코",
            "달러구트 꿈 백화점",
            "메리골드 마음 세탁소",
            "구의 증명",
            "천 개의 파랑",
            "쇼코의 미소",
            "지구 끝의 온실",
            "밝은 밤",
            "어서 오세요 휴남동 서점입니다",
            "종의 기원",
            "여행의 이유",
            "스토너"
        };

        private static readonly string[] Authors =
        {
            "양귀자",
            "김호연",
            "손원평",
            "김영하",
            "이민진",
            "이미예",
            "윤정은",
            "최진영",
            "천선란",
            "최은영",
            "김초엽",
            "최은영",
            "황보름",
            "정유정",
            "김영하",
            "존 윌리엄스"
        };

        private static readonly string[] Publishers =
        {
            "쓰다",
            "나무옆의자",
            "창비",
            "복복서가",
            "인플루엔셜",
            "팩토리나인",
            "북로망스",
            "은행나무",
            "허블",
            "문학동네",
            "자이언트북스",
            "문학동네",
            "클레이하우스",
            "은행나무",
            "문학동네",
            "알에이치코리아"
        };

        private static readonly string[] Descriptions =
        {
            "선택과 모순 사이에서 흔들리는 인물들의 감정을 따라가는 장편소설.",
            "동네 편의점에서 마주치는 사람들의 사연을 따뜻하게 엮은 이야기.",
            "감정을 느끼기 어려운 소년이 관계를 배우며 성장하는 소설.",
            "인간과 기억의 경계를 차분하게 묻는 SF 장편.",
            "시대의 파도 속에서 가족이 버티고 이어지는 서사.",
            "잠과 꿈을 사고파는 백화점을 배경으로 한 판타지 소설.",
            "지친 마음을 세탁하듯 돌보는 사람들의 회복 이야기.",
            "강렬한 사랑과 상실을 짧고 깊게 압축한 소설.",
            "로봇 경주마와 인간의 연대를 그린 한국 SF.",
            "작은 만남과 이별이 남기는 여운을 담은 단편집.",
            "멸망 이후의 세계에서 다시 피어나는 선택과 책임의 이야기.",
            "가족의 기억과 여성의 시간을 따라가는 장편소설.",
            "서점이라는 공간에서 삶을 다시 정리하는 사람들의 이야기.",
            "악의 기원을 추적하는 강렬한 심리 스릴러.",
            "여행이 남기는 질문과 관찰을 담은 산문.",
            "한 사람의 조용한 삶을 깊고 단단하게 따라가는 장편."
        };

        private static readonly ReadingStatus[] Statuses =
        {
            ReadingStatus.Reading,
            ReadingStatus.Completed,
            ReadingStatus.Wishlist,
            ReadingStatus.Completed
        };

        public IReadOnlyList<BookCatalogItem> GetMyBookshelfBooks()
        {
            var result = new List<BookCatalogItem>(Rows * Columns * BooksPerSection);

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    for (var slot = 0; slot < BooksPerSection; slot++)
                    {
                        var index = result.Count;
                        var seedIndex = index % Titles.Length;
                        var bookId = $"mock-book-{index + 1:000}";
                        var title = Titles[seedIndex];
                        if (index >= Titles.Length)
                        {
                            title = $"{title} #{index / Titles.Length + 1}";
                        }

                        var book = new Book(
                            bookId,
                            title,
                            Authors[seedIndex],
                            Publishers[seedIndex],
                            Descriptions[seedIndex],
                            $"mock://books/{bookId}.png");

                        var userBook = new UserBook($"user-{bookId}", bookId)
                        {
                            Status = Statuses[index % Statuses.Length],
                            Rating = 3 + index % 3,
                            OneLineReview = CreateReviewLine(title, index),
                            ReviewVisibility = index % 5 == 0 ? Visibility.Private : Visibility.Public
                        };

                        var placement = new BookPlacement($"placement-{bookId}", userBook.UserBookId, "main-shelf", row, column, slot);
                        result.Add(new BookCatalogItem(book, userBook, placement));
                    }
                }
            }

            return result;
        }

        private static string CreateReviewLine(string title, int index)
        {
            switch (index % 4)
            {
                case 0:
                    return $"{title}은 다시 읽을 문장이 많은 책.";
                case 1:
                    return $"{title}은 분위기와 인물의 결이 선명했다.";
                case 2:
                    return $"{title}은 책장에 남겨두고 싶은 이야기.";
                default:
                    return $"{title}은 팀 데모용 mock 독후감 데이터.";
            }
        }
    }
}
