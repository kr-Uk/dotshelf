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
@Table(name = "maintenance_settings", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class MaintenanceSetting extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private boolean enabled = false;

    @Column(length = 500)
    private String message;

    @Column(nullable = false)
    private Long updatedByUserId;

    @Builder
    private MaintenanceSetting(boolean enabled, String message, Long updatedByUserId) {
        this.enabled = enabled;
        this.message = message;
        this.updatedByUserId = updatedByUserId;
    }
}
