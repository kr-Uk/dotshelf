package com.dotshelf.domain.bookshelf.entity;

import java.time.LocalDateTime;

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
        name = "bookshelves",
        uniqueConstraints = @UniqueConstraint(columnNames = {"space_id", "display_order"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Bookshelf extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long spaceId;

    private Long bodyInventoryId;

    private Long skinInventoryId;

    @Column(nullable = false, length = 100)
    private String name;

    @Column(length = 255)
    private String description;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    @Column(nullable = false)
    private int displayOrder;

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    private LocalDateTime deletedAt;

    @Builder
    private Bookshelf(Long spaceId, Long bodyInventoryId, Long skinInventoryId, String name,
                      String description, String visibilityScope, int displayOrder) {
        this.spaceId = spaceId;
        this.bodyInventoryId = bodyInventoryId;
        this.skinInventoryId = skinInventoryId;
        this.name = name;
        this.description = description;
        this.visibilityScope = visibilityScope == null ? "PUBLIC" : visibilityScope;
        this.displayOrder = displayOrder;
    }

    public void update(String name, String description, String visibilityScope, Long skinInventoryId) {
        if (name != null && !name.isBlank()) {
            this.name = name;
        }
        if (description != null) {
            this.description = description;
        }
        if (visibilityScope != null && !visibilityScope.isBlank()) {
            this.visibilityScope = visibilityScope;
        }
        if (skinInventoryId != null) {
            this.skinInventoryId = skinInventoryId;
        }
    }

    public void changeOrder(int displayOrder) {
        this.displayOrder = displayOrder;
    }

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = LocalDateTime.now();
        moveToDeletedOrder();
    }

    public void moveToDeletedOrder() {
        this.displayOrder = Integer.MAX_VALUE - Math.toIntExact(id);
    }
}
