package com.dotshelf.domain.bookshelf.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.times;

import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.CreateBookshelfRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.InteriorObjectRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.ReorderBookshelvesRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionAlignmentRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UpdateBookshelfRequest;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfSectionRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class BookshelfServiceTest {

    @Mock
    private BookshelfRepository bookshelfRepository;

    @Mock
    private BookshelfSectionRepository sectionRepository;

    @Mock
    private BookshelfItemRepository bookshelfItemRepository;

    @Mock
    private UserBookRepository userBookRepository;

    @Mock
    private BookRepository bookRepository;

    @Mock
    private UserAssetInventoryRepository inventoryRepository;

    @Mock
    private AssetRepository assetRepository;

    @Mock
    private ReadingRecordRepository readingRecordRepository;

    @Mock
    private SpaceService spaceService;

    @Mock
    private FollowRepository followRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private BookshelfService bookshelfService;

    @Test
    @DisplayName("책장 생성은 소유자 검증 후 다음 displayOrder와 4x4 기본 섹션을 만든다")
    void create_ownerCreatesBookshelfAndDefaultSections() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(10L, "ACTIVE"))
                .willReturn(List.of(TestFixtures.bookshelf(3L, 10L, 2)));
        given(bookshelfRepository.save(any(Bookshelf.class))).willAnswer(invocation -> {
            Bookshelf bookshelf = invocation.getArgument(0);
            TestFixtures.setId(bookshelf, 99L);
            return bookshelf;
        });

        var response = bookshelfService.create(1L, new CreateBookshelfRequest(10L, null, "main", "desc", "PUBLIC"));

        assertThat(response.id()).isEqualTo(99L);
        assertThat(response.displayOrder()).isEqualTo(3);
        then(sectionRepository).should(times(16)).save(any(BookshelfSection.class));
    }

    @Test
    @DisplayName("책장 생성은 공간 소유자가 아니면 FORBIDDEN을 던진다")
    void create_notOwnerThrowsForbidden() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 2L));

        assertThatThrownBy(() -> bookshelfService.create(1L, new CreateBookshelfRequest(10L, null, "main", null, null)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("책장 렌더 조회는 책/에셋/독서기록을 배치 아이템에 조립한다")
    void getItems_joinsBooksAssetsAndRecords() {
        Bookshelf bookshelf = TestFixtures.bookshelf(3L, 10L, 0);
        BookshelfSection section = TestFixtures.bookshelfSection(7L, 3L, 0, 0);
        BookshelfItem bookItem = TestFixtures.bookshelfItem(30L, 3L, 7L, 20L);
        BookshelfItem decorItem = TestFixtures.decorItem(31L, 3L, 7L, 8L);
        UserBook userBook = TestFixtures.userBook(20L, 2L, 9L);
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(bookshelf));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 2L));
        given(sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(3L)).willReturn(List.of(section));
        given(bookshelfItemRepository.findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(3L, "ACTIVE"))
                .willReturn(List.of(bookItem, decorItem));
        given(userBookRepository.findAllById(List.of(20L))).willReturn(List.of(userBook));
        given(bookRepository.findAllById(List.of(9L))).willReturn(List.of(TestFixtures.book(9L, "Book")));
        given(readingRecordRepository.findByUserBookIdInAndStatus(List.of(20L), "ACTIVE"))
                .willReturn(List.of(TestFixtures.readingRecord(40L, 20L)));
        given(inventoryRepository.findAllById(List.of(8L))).willReturn(List.of(TestFixtures.inventory(8L, 1L, 50L)));
        given(assetRepository.findAllById(List.of(50L))).willReturn(List.of(TestFixtures.asset(50L, 1L, "decor", 0)));

        var response = bookshelfService.getItems(1L, 3L);

        assertThat(response.items()).hasSize(2);
        assertThat(response.items().get(0).book().title()).isEqualTo("Book");
        assertThat(response.items().get(0).readingRecord().id()).isEqualTo(40L);
        assertThat(response.items().get(1).asset().code()).isEqualTo("decor");
    }

    @Test
    @DisplayName("책장 수정은 소유자 책장만 변경한다")
    void update_ownedBookshelf_updatesFields() {
        Bookshelf bookshelf = TestFixtures.bookshelf(3L, 10L, 0);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(bookshelf));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        var response = bookshelfService.update(1L, 3L, new UpdateBookshelfRequest("changed", "desc", "PRIVATE", 8L));

        assertThat(response.name()).isEqualTo("changed");
        assertThat(response.visibilityScope()).isEqualTo("PRIVATE");
        assertThat(response.skinInventoryId()).isEqualTo(8L);
    }

    @Test
    @DisplayName("섹션 정렬 변경은 섹션이 해당 책장에 속해야 한다")
    void updateSectionAlignment_sectionMismatchThrowsForbidden() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(TestFixtures.bookshelf(3L, 10L, 0)));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(sectionRepository.findById(7L)).willReturn(Optional.of(TestFixtures.bookshelfSection(7L, 99L, 0, 0)));

        assertThatThrownBy(() -> bookshelfService.updateSectionAlignment(
                1L, 3L, 7L, new SectionAlignmentRequest("CENTER")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("인테리어 오브젝트 추가는 인벤토리 소유권을 확인한다")
    void addInteriorObject_inventoryOwnerMismatchThrowsForbidden() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(TestFixtures.bookshelf(3L, 10L, 0)));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(sectionRepository.findById(7L)).willReturn(Optional.of(TestFixtures.bookshelfSection(7L, 3L, 0, 0)));
        given(inventoryRepository.findById(8L)).willReturn(Optional.of(TestFixtures.inventory(8L, 2L, 50L)));

        assertThatThrownBy(() -> bookshelfService.addInteriorObject(
                1L, 3L, new InteriorObjectRequest(8L, 7L, 1, null, null)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("책 아이템 제거는 item을 REMOVED로 바꾸고 userBook을 보관 처리한다")
    void removeItem_bookItemArchivesUserBook() {
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 3L, 7L, 20L);
        UserBook userBook = TestFixtures.userBook(20L, 1L, 9L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookshelfItemRepository.findById(30L)).willReturn(Optional.of(item));
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(TestFixtures.bookshelf(3L, 10L, 0)));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));

        var response = bookshelfService.removeItem(1L, 30L);

        assertThat(response.id()).isEqualTo(30L);
        assertThat(item.getStatus()).isEqualTo("REMOVED");
        assertThat(userBook.getStorageStatus()).isEqualTo("ARCHIVED");
    }

    @Test
    @DisplayName("책장 순서 변경은 서로 다른 공간의 책장이 섞이면 INVALID_INPUT을 던진다")
    void reorder_differentSpacesThrowsInvalidInput() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookshelfRepository.findById(3L)).willReturn(Optional.of(TestFixtures.bookshelf(3L, 10L, 0)));
        given(bookshelfRepository.findById(4L)).willReturn(Optional.of(TestFixtures.bookshelf(4L, 11L, 1)));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(spaceService.getSpaceEntity(11L)).willReturn(TestFixtures.space(11L, 1L));

        assertThatThrownBy(() -> bookshelfService.reorder(1L, new ReorderBookshelvesRequest(List.of(3L, 4L))))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }
}
