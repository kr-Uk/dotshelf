package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfSectionRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.IntegrationTestSupport;
import com.dotshelf.support.TestFixtures;

class BookBookshelfIntegrationTest extends IntegrationTestSupport {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private SpaceRepository spaceRepository;

    @Autowired
    private BookRepository bookRepository;

    @Autowired
    private UserBookRepository userBookRepository;

    @Autowired
    private BookshelfRepository bookshelfRepository;

    @Autowired
    private BookshelfSectionRepository sectionRepository;

    @Autowired
    private BookshelfItemRepository itemRepository;

    @Test
    @DisplayName("HTTP로 공간→책장→책 추가→이동→보관→삭제 전체 플로우를 DB까지 검증한다")
    void bookAndBookshelf_fullHttpFlowPersistsExpectedState() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("reader"));
        var book = bookRepository.saveAndFlush(TestFixtures.book(null, "데미안"));

        mockMvc.perform(get("/v1/spaces/me").header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaceName").value("나의 공간"));
        var space = spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(user.getId()).orElseThrow();

        createBookshelf(user.getId(), space.getId(), "첫 책장");
        createBookshelf(user.getId(), space.getId(), "두 번째 책장");
        var shelves = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE");
        var firstShelf = shelves.get(0);
        var secondShelf = shelves.get(1);
        var firstSections = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(firstShelf.getId());
        var secondSections = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(secondShelf.getId());
        assertThat(firstSections).hasSize(16);

        mockMvc.perform(post("/v1/bookshelves/{bookshelfId}/books", firstShelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookId":%d,"sectionId":%d,"slotNo":1,"facingDirection":"FRONT","displayStyle":"SPINE"}
                                """.formatted(book.getId(), firstSections.get(0).getId())))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.storageStatus").value("ON_SHELF"))
                .andExpect(jsonPath("$.result.placement.slotNo").value(1));
        var userBook = userBookRepository.findByUserIdAndBookId(user.getId(), book.getId()).orElseThrow();
        assertThat(bookRepository.findById(book.getId()).orElseThrow().getBookshelfAddCount()).isEqualTo(1L);

        mockMvc.perform(get("/v1/bookshelves/{bookshelfId}/items", firstShelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].book.title").value("데미안"))
                .andExpect(jsonPath("$.result.items[0].userBook.id").value(userBook.getId()));

        mockMvc.perform(patch("/v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment",
                        firstShelf.getId(),
                        firstSections.get(0).getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"alignment":"CENTER"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.alignment").value("CENTER"));

        mockMvc.perform(post("/v1/user-books/{userBookId}/move", userBook.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookshelfId":%d,"sectionId":%d,"slotNo":2,"facingDirection":"BACK","displayStyle":"COVER"}
                                """.formatted(secondShelf.getId(), secondSections.get(0).getId())))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.placement.bookshelfId").value(secondShelf.getId()))
                .andExpect(jsonPath("$.result.placement.slotNo").value(2));
        assertThat(itemRepository.findByUserBookIdAndStatus(userBook.getId(), "ACTIVE").orElseThrow().getBookshelfId())
                .isEqualTo(secondShelf.getId());

        mockMvc.perform(put("/v1/bookshelves/order")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookshelfIds":[%d,%d]}
                                """.formatted(secondShelf.getId(), firstShelf.getId())))
                .andExpect(status().isOk());
        assertThat(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE"))
                .extracting("id")
                .containsExactly(secondShelf.getId(), firstShelf.getId());

        mockMvc.perform(post("/v1/user-books/{userBookId}/archive", userBook.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.storageStatus").value("ARCHIVED"));
        assertThat(itemRepository.findByUserBookIdAndStatus(userBook.getId(), "ACTIVE")).isEmpty();

        mockMvc.perform(get("/v1/user-books/archived")
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.books[0].id").value(userBook.getId()));

        mockMvc.perform(post("/v1/bookshelves/{bookshelfId}/books", firstShelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookId":%d,"sectionId":%d,"slotNo":3}
                                """.formatted(book.getId(), firstSections.get(1).getId())))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(userBook.getId()))
                .andExpect(jsonPath("$.result.storageStatus").value("ON_SHELF"));

        mockMvc.perform(delete("/v1/user-books/{userBookId}", userBook.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk());
        assertThat(userBookRepository.findById(userBook.getId()).orElseThrow().getStorageStatus()).isEqualTo("DELETED");
        assertThat(itemRepository.findByUserBookIdAndStatus(userBook.getId(), "ACTIVE")).isEmpty();
    }

    @Test
    @DisplayName("다른 사용자의 공간에는 책장을 만들 수 없고 DB row도 생성하지 않는다")
    void createBookshelf_otherUsersSpaceReturnsForbiddenWithoutRows() throws Exception {
        var owner = userRepository.saveAndFlush(TestFixtures.user("bookshelf-owner"));
        var other = userRepository.saveAndFlush(TestFixtures.user("bookshelf-other"));
        var space = spaceRepository.saveAndFlush(TestFixtures.space(null, owner.getId()));

        mockMvc.perform(post("/v1/bookshelves")
                        .header(TEST_USER_HEADER, other.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"spaceId":%d,"name":"권한 없는 책장","visibilityScope":"PUBLIC"}
                                """.formatted(space.getId())))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("A002"));

        assertThat(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE")).isEmpty();
        assertThat(sectionRepository.findAll()).isEmpty();
    }

    @Test
    @DisplayName("bookshelf render includes book description")
    void getItems_includesBookDescription() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("description-render"));
        var space = spaceRepository.saveAndFlush(TestFixtures.space(null, user.getId()));
        var book = bookRepository.saveAndFlush(Book.builder()
                .isbn13("9780000000001")
                .title("Description Book")
                .author("author")
                .publisher("publisher")
                .description("Book render description")
                .build());

        createBookshelf(user.getId(), space.getId(), "description shelf");
        var shelf = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE").getFirst();
        var section = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(shelf.getId()).getFirst();

        mockMvc.perform(post("/v1/bookshelves/{bookshelfId}/books", shelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookId":%d,"sectionId":%d,"slotNo":1}
                                """.formatted(book.getId(), section.getId())))
                .andExpect(status().isOk());

        mockMvc.perform(get("/v1/bookshelves/{bookshelfId}/items", shelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].book.description").value("Book render description"));
    }

    @Test
    @DisplayName("책을 다른 책장 섹션에 배치하려 하면 거부되고 userBook/item을 만들지 않는다")
    void addBook_sectionFromDifferentBookshelfReturnsForbiddenWithoutRows() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("section-owner"));
        var book = bookRepository.saveAndFlush(TestFixtures.book(null, "권한 검증 책"));
        var space = spaceRepository.saveAndFlush(TestFixtures.space(null, user.getId()));
        createBookshelf(user.getId(), space.getId(), "첫 책장");
        createBookshelf(user.getId(), space.getId(), "두 번째 책장");
        var shelves = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(space.getId(), "ACTIVE");
        var firstShelf = shelves.get(0);
        var secondShelf = shelves.get(1);
        var wrongSection = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(secondShelf.getId()).get(0);

        mockMvc.perform(post("/v1/bookshelves/{bookshelfId}/books", firstShelf.getId())
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookId":%d,"sectionId":%d,"slotNo":1}
                                """.formatted(book.getId(), wrongSection.getId())))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("A002"));

        assertThat(userBookRepository.findByUserIdAndBookId(user.getId(), book.getId())).isEmpty();
        assertThat(itemRepository.findAll()).isEmpty();
    }

    private void createBookshelf(Long userId, Long spaceId, String name) throws Exception {
        mockMvc.perform(post("/v1/bookshelves")
                        .header(TEST_USER_HEADER, userId.toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"spaceId":%d,"name":"%s","visibilityScope":"PUBLIC"}
                                """.formatted(spaceId, name)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.name").value(name));
    }
}
