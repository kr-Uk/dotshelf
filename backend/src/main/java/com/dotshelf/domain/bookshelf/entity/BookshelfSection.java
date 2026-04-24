package com.dotshelf.domain.bookshelf.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(
        name = "bookshelf_sections",
        uniqueConstraints = @UniqueConstraint(columnNames = {"bookshelf_id", "row_no", "column_no"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class BookshelfSection extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long bookshelfId;

    @Column(nullable = false)
    private int rowNo;

    @Column(nullable = false)
    private int columnNo;

    @Column(nullable = false)
    private int maxItemCount = 20;

    @Column(nullable = false, length = 20)
    private String alignment = "LEFT";

    @Builder
    private BookshelfSection(Long bookshelfId, int rowNo, int columnNo, int maxItemCount) {
        this.bookshelfId = bookshelfId;
        this.rowNo = rowNo;
        this.columnNo = columnNo;
        this.maxItemCount = maxItemCount;
    }

    public void changeAlignment(String alignment) {
        this.alignment = alignment;
    }
}
