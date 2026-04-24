package com.dotshelf.domain.bookshelf.entity;

import java.math.BigDecimal;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "bookshelf_items")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class BookshelfItem extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long bookshelfId;

    private Long bookshelfSectionId;

    @Column(nullable = false, length = 20)
    private String itemType;

    private Long userBookId;

    private Long assetInventoryId;

    private Integer slotNo;

    @Column(nullable = false, length = 20)
    private String facingDirection = "FRONT";

    @Column(nullable = false, length = 30)
    private String displayStyle = "SPINE";

    @Column(nullable = false, precision = 6, scale = 2)
    private BigDecimal rotationDeg = BigDecimal.ZERO;

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    @Builder
    private BookshelfItem(Long bookshelfId, Long bookshelfSectionId, String itemType,
                          Long userBookId, Long assetInventoryId, Integer slotNo,
                          String facingDirection, String displayStyle) {
        this.bookshelfId = bookshelfId;
        this.bookshelfSectionId = bookshelfSectionId;
        this.itemType = itemType;
        this.userBookId = userBookId;
        this.assetInventoryId = assetInventoryId;
        this.slotNo = slotNo;
        this.facingDirection = facingDirection == null ? "FRONT" : facingDirection;
        this.displayStyle = displayStyle == null ? "SPINE" : displayStyle;
    }

    public void move(Long bookshelfId, Long bookshelfSectionId, Integer slotNo, String displayStyle) {
        this.bookshelfId = bookshelfId;
        this.bookshelfSectionId = bookshelfSectionId;
        this.slotNo = slotNo;
        this.displayStyle = displayStyle;
    }

    public void remove() {
        this.status = "REMOVED";
    }
}
