using System;
using System.Collections.Generic;
using App.Application.Books;
using App.Domain;
using UnityEngine;

namespace App.Infrastructure.Mock
{
    public sealed class MockBookCatalogRepository : IBookCatalogRepository
    {
        private const int Rows = 4;
        private const int Columns = 4;
        private const int BooksPerSection = 3;
        private const string MockShelfId = "bs-mock-1";
        private const string MockUserId = "me";

        private static readonly string[] Titles =
        {
            "모순",
            "불편한 편의점",
            "아몬드",
            "작별인사",
            "파친코",
            "흐르는 강물처럼",
            "메리골드 마음 세탁소",
            "구의 증명",
            "천 개의 파랑",
            "달러구트 꿈 백화점",
            "지구 끝의 온실",
            "밝은 밤",
            "어서 오세요, 휴남동 서점입니다",
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
            "셸리 리드",
            "윤정은",
            "최진영",
            "천선란",
            "이미예",
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
            "다산책방",
            "북로망스",
            "은행나무",
            "허블",
            "팩토리나인",
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
            "동네 편의점에 머무는 사람들의 사연을 따뜻하게 엮은 이야기.",
            "감정을 이해하기 어려운 소년이 관계를 배우며 성장하는 소설.",
            "인간과 기억의 경계를 차분하게 묻는 SF 장편.",
            "차별과 이주의 역사 속에서 가족이 버티고 이어지는 서사.",
            "눈과 강을 건너는 백화점을 배경으로 한 편의 깊은 소설.",
            "지친 마음을 세탁하듯 돌보는 사람들의 회복 이야기.",
            "강렬한 사랑과 상실을 짧고 깊게 담아낸 소설.",
            "로봇 경주마와 인간의 곁을 그린 한국 SF.",
            "작은 만남과 기억을 꿈으로 잇는 판타지.",
            "멸망 이후의 세계에서 다시 일어서는 선택과 책임의 이야기.",
            "가족의 기억과 시간을 따라가는 장편소설.",
            "서점이라는 공간에서 삶을 다시 정리하는 사람들의 이야기.",
            "악의 기원을 추적하는 강렬한 심리 스릴러.",
            "여행의 의미를 질문과 관찰로 풀어낸 산문.",
            "한 사람의 조용한 삶을 깊고 단단하게 따라가는 장편."
        };

        private static readonly ReadingStatus[] Statuses =
        {
            ReadingStatus.Reading,
            ReadingStatus.Read,
            ReadingStatus.Unread,
            ReadingStatus.Read
        };

        public IReadOnlyList<BookCatalogItem> GetMyBookshelfBooks()
        {
            Debug.LogWarning("LocalMock book catalog repository is serving mock bookshelf books.");
            var result = new List<BookCatalogItem>(Rows * Columns * BooksPerSection);
            var now = DateTimeOffset.Now;

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    for (var slot = 0; slot < BooksPerSection; slot++)
                    {
                        var index = result.Count;
                        result.Add(CreateCatalogItem(index, row, column, slot, now));
                    }
                }
            }

            return result;
        }

        private static BookCatalogItem CreateCatalogItem(int index, int row, int column, int slot, DateTimeOffset now)
        {
            var seedIndex = index % Titles.Length;
            var number = index + 1;
            var bookId = $"b-mock-{number:000}";
            var userBookId = $"ob-mock-{number:000}";
            var bookshelfSectionId = $"{MockShelfId}-section-{row}-{column}";
            var bookshelfItemId = $"bi-mock-{number:000}";
            var title = Titles[seedIndex];
            if (index >= Titles.Length)
            {
                title = $"{title} #{index / Titles.Length + 1}";
            }

            var book = new Book(
                bookId,
                title,
                Authors[seedIndex],
                publisher: Publishers[seedIndex],
                description: Descriptions[seedIndex],
                coverImageUrl: $"mock://books/{number:000}.png",
                isbn: $"979000000{number:0000}",
                isbn13: $"979000000{number:0000}",
                publishedDate: new DateTime(2020 + seedIndex % 5, seedIndex % 12 + 1, 1),
                pageCount: 180 + seedIndex * 17,
                language: "ko",
                viewCount: 1000 + index * 73,
                likeCount: 120 + index * 11,
                bookshelfAddCount: 200 + index * 9,
                ratingSum: (42 + index % 8) / 10m * (10 + index % 5),
                ratingCount: 10 + index % 5,
                createdAt: now.AddDays(-90 + index),
                updatedAt: now.AddDays(-7));

            var userBook = new UserBook(userBookId, MockUserId, book.BookId, now.AddDays(-30 + index), now.AddDays(-(index % 6)))
            {
                ReadingStatus = Statuses[index % Statuses.Length],
                StorageStatus = StorageStatus.OnShelf,
                OwnerRating = 3 + index % 3,
                IsLiked = index % 4 == 0
            };

            var readingRecord = new ReadingRecord($"rr-mock-{number:000}", userBook.UserBookId, now.AddDays(-20 + index), now.AddDays(-(index % 4)))
            {
                Title = $"{title} 독후감",
                Content = CreateReviewLine(title, index),
                VisibilityScope = index % 5 == 0 ? VisibilityScope.Private : VisibilityScope.Public,
                LikeCount = index % 9,
                CommentCount = index % 4,
                Status = ContentStatus.Active
            };

            var placement = new BookPlacement(bookshelfItemId, MockShelfId, bookshelfSectionId, userBook.UserBookId, row, column, slot + 1)
            {
                ItemType = ShelfItemType.Book,
                FacingDirection = FacingDirection.Front,
                DisplayStyle = DisplayStyle.Spine,
                RotationDeg = 0m,
                PosX = null,
                PosY = null,
                PosZ = null,
                ScaleX = 1m,
                ScaleY = 1m,
                Status = ShelfItemStatus.Active
            };

            return new BookCatalogItem(book, userBook, placement, readingRecord);
        }

        private static string CreateReviewLine(string title, int index)
        {
            switch (index % 4)
            {
                case 0:
                    return $"{title}는 다시 읽을 문장이 많은 책입니다.";
                case 1:
                    return $"{title}는 분위기와 인물의 결이 선명합니다.";
                case 2:
                    return $"{title}는 책장에 오래 남겨두고 싶은 이야기입니다.";
                default:
                    return $"{title}에 대한 mock 독후감 데이터입니다.";
            }
        }
    }
}
