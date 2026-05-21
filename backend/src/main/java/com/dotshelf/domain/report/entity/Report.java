package com.dotshelf.domain.report.entity;

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
        name = "reports",
        uniqueConstraints = @UniqueConstraint(columnNames = {"reporter_user_id", "target_type", "target_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Report extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long reporterUserId;

    private Long reportedUserId;

    @Column(nullable = false, length = 50)
    private String targetType;

    @Column(nullable = false)
    private Long targetId;

    @Column(nullable = false, length = 50)
    private String reasonType;

    @Column(length = 500)
    private String reasonDetail;

    @Column(nullable = false, length = 20)
    private String status = "PENDING";

    @Column(nullable = false)
    private boolean isBlindedToReporter = true;

    @Column(nullable = false)
    private boolean isGloballyBlinded = false;

    private Long reviewedByUserId;

    private LocalDateTime reviewedAt;

    @Builder
    private Report(Long reporterUserId, Long reportedUserId, String targetType,
                   Long targetId, String reasonType, String reasonDetail) {
        this.reporterUserId = reporterUserId;
        this.reportedUserId = reportedUserId;
        this.targetType = targetType;
        this.targetId = targetId;
        this.reasonType = reasonType;
        this.reasonDetail = reasonDetail;
    }

    public void startReview(Long reviewerUserId) {
        this.status = "IN_REVIEW";
        this.reviewedByUserId = reviewerUserId;
        this.reviewedAt = LocalDateTime.now();
    }

    public void resolve(Long reviewerUserId, Boolean blindToReporter, Boolean globallyBlind) {
        this.status = "RESOLVED";
        this.reviewedByUserId = reviewerUserId;
        this.reviewedAt = LocalDateTime.now();
        if (blindToReporter != null) {
            this.isBlindedToReporter = blindToReporter;
        }
        if (globallyBlind != null) {
            this.isGloballyBlinded = globallyBlind;
        }
    }

    public void reject(Long reviewerUserId) {
        this.status = "REJECTED";
        this.reviewedByUserId = reviewerUserId;
        this.reviewedAt = LocalDateTime.now();
    }

    public void changeGlobalBlind(Long reviewerUserId, boolean globallyBlind) {
        this.isGloballyBlinded = globallyBlind;
        this.reviewedByUserId = reviewerUserId;
        this.reviewedAt = LocalDateTime.now();
    }
}
