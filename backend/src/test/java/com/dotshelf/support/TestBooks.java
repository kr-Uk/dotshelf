package com.dotshelf.support;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;

public class TestBooks {

    public Book book(Long id, String title) {
        return TestFixtures.book(id, title);
    }

    public UserBook userBook(Long id, Long userId, Long bookId) {
        return TestFixtures.userBook(id, userId, bookId);
    }

    public Bookshelf bookshelf(Long id, Long spaceId, int displayOrder) {
        return TestFixtures.bookshelf(id, spaceId, displayOrder);
    }

    public BookshelfSection section(Long id, Long bookshelfId, int rowNo, int columnNo) {
        return TestFixtures.bookshelfSection(id, bookshelfId, rowNo, columnNo);
    }

    public BookshelfItem item(Long id, Long bookshelfId, Long sectionId, Long userBookId) {
        return TestFixtures.bookshelfItem(id, bookshelfId, sectionId, userBookId);
    }
}
