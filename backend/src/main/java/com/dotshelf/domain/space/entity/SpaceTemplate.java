package com.dotshelf.domain.space.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "space_templates")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SpaceTemplate extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true, length = 50)
    private String code;

    @Column(nullable = false, length = 100)
    private String name;

    @Column(length = 500)
    private String previewImageUrl;

    @Lob
    @Column(nullable = false)
    private String layoutPayload = "{}";

    @Column(nullable = false)
    private int displayOrder = 0;

    @Column(nullable = false)
    private boolean isActive = true;

    @Builder
    private SpaceTemplate(String code, String name, String previewImageUrl, String layoutPayload, int displayOrder) {
        this.code = code;
        this.name = name;
        this.previewImageUrl = previewImageUrl;
        this.layoutPayload = layoutPayload == null ? "{}" : layoutPayload;
        this.displayOrder = displayOrder;
    }
}
