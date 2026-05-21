package com.dotshelf.domain.admin.entity;

import java.time.Instant;

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
@Table(name = "policy_versions", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PolicyVersion extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 50)
    private String policyType;

    @Column(nullable = false, length = 30)
    private String version;

    @Column(nullable = false, length = 150)
    private String title;

    @Column(columnDefinition = "text")
    private String body;

    private Instant effectiveAt;

    @Column(nullable = false)
    private boolean isActive = true;

    private Long publishedByUserId;

    @Builder
    private PolicyVersion(String policyType, String version, String title, String body,
                          Instant effectiveAt, boolean isActive, Long publishedByUserId) {
        this.policyType = policyType;
        this.version = version;
        this.title = title;
        this.body = body;
        this.effectiveAt = effectiveAt;
        this.isActive = isActive;
        this.publishedByUserId = publishedByUserId;
    }
}
