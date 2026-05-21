package com.dotshelf.domain.admin.entity;

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
@Table(name = "creator_profiles", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class CreatorProfile extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private Long userId;

    @Column(nullable = false, length = 100)
    private String displayName;

    @Column(nullable = false, length = 30)
    private String status = "ACTIVE";

    @Builder
    private CreatorProfile(Long userId, String displayName, String status) {
        this.userId = userId;
        this.displayName = displayName;
        this.status = status == null ? "ACTIVE" : status;
    }
}
