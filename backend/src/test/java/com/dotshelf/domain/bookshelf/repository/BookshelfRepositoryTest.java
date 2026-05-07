package com.dotshelf.domain.bookshelf.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class BookshelfRepositoryTest {

    @Autowired
    private BookshelfRepository bookshelfRepository;

    @Autowired
    private BookshelfSectionRepository sectionRepository;

    @Autowired
    private BookshelfItemRepository itemRepository;

    @Test
    @DisplayName("책장은 활성 상태만 displayOrder 오름차순으로 조회한다")
    void findActiveBookshelves_ordered() {
        Bookshelf first = bookshelfRepository.save(TestFixtures.bookshelf(null, 1L, 0));
        Bookshelf deleted = bookshelfRepository.save(TestFixtures.bookshelf(null, 1L, 1));
        deleted.delete();
        Bookshelf second = bookshelfRepository.save(TestFixtures.bookshelf(null, 1L, 2));
        bookshelfRepository.flush();

        assertThat(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(1L, "ACTIVE"))
                .extracting(Bookshelf::getId)
                .containsExactly(first.getId(), second.getId());
        assertThat(bookshelfRepository.countBySpaceIdAndStatus(1L, "ACTIVE")).isEqualTo(2);
    }

    @Test
    @DisplayName("같은 공간의 displayOrder 중복은 unique 제약으로 막는다")
    void uniqueSpaceAndDisplayOrder() {
        bookshelfRepository.saveAndFlush(TestFixtures.bookshelf(null, 1L, 0));

        assertThatThrownBy(() -> bookshelfRepository.saveAndFlush(TestFixtures.bookshelf(null, 1L, 0)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("섹션은 row/column 오름차순으로 조회한다")
    void section_orderedByRowAndColumn() {
        sectionRepository.save(TestFixtures.bookshelfSection(null, 1L, 1, 1));
        sectionRepository.save(TestFixtures.bookshelfSection(null, 1L, 0, 1));
        sectionRepository.save(TestFixtures.bookshelfSection(null, 1L, 0, 0));

        assertThat(sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(1L))
                .extracting("rowNo", "columnNo")
                .containsExactly(
                        org.assertj.core.groups.Tuple.tuple(0, 0),
                        org.assertj.core.groups.Tuple.tuple(0, 1),
                        org.assertj.core.groups.Tuple.tuple(1, 1)
                );
    }

    @Test
    @DisplayName("책장 아이템은 active 상태만 section/slot 순서로 조회한다")
    void item_findActiveOrdered() {
        itemRepository.save(TestFixtures.bookshelfItem(null, 1L, 2L, 10L));
        itemRepository.save(TestFixtures.bookshelfItem(null, 1L, 1L, 11L));
        var removed = TestFixtures.bookshelfItem(null, 1L, 3L, 12L);
        removed.remove();
        itemRepository.save(removed);

        assertThat(itemRepository.findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(1L, "ACTIVE"))
                .extracting("bookshelfSectionId")
                .containsExactly(1L, 2L);
        assertThat(itemRepository.countByBookshelfIdAndStatus(1L, "ACTIVE")).isEqualTo(2);
    }
}
