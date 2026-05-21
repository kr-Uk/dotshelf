package com.dotshelf.domain.user.entity;

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
        name = "follows",
        uniqueConstraints = @UniqueConstraint(columnNames = {"follower_user_id", "following_user_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Follow extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long followerUserId;

    @Column(nullable = false)
    private Long followingUserId;

    @Builder
    private Follow(Long followerUserId, Long followingUserId) {
        this.followerUserId = followerUserId;
        this.followingUserId = followingUserId;
    }
}
