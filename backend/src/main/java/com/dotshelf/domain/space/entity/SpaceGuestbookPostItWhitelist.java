package com.dotshelf.domain.space.entity;

import java.util.HashSet;
import java.util.Set;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.CollectionTable;
import jakarta.persistence.Column;
import jakarta.persistence.ElementCollection;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "space_guestbook_postit_whitelists")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SpaceGuestbookPostItWhitelist extends BaseEntity {

    @Id
    private Long spaceId;

    @Column(nullable = false)
    private boolean allowAll = true;

    @ElementCollection(fetch = FetchType.LAZY)
    @CollectionTable(
            name = "space_guestbook_postit_whitelist_assets",
            joinColumns = @JoinColumn(name = "space_id")
    )
    @Column(name = "asset_id")
    private Set<Long> allowedAssetIds = new HashSet<>();

    @Builder
    private SpaceGuestbookPostItWhitelist(Long spaceId, Boolean allowAll, Set<Long> allowedAssetIds) {
        this.spaceId = spaceId;
        this.allowAll = allowAll == null || allowAll;
        this.allowedAssetIds = allowedAssetIds == null ? new HashSet<>() : new HashSet<>(allowedAssetIds);
    }

    public void update(boolean allowAll, Set<Long> newAssetIds) {
        this.allowAll = allowAll;
        this.allowedAssetIds.clear();
        if (newAssetIds != null) {
            this.allowedAssetIds.addAll(newAssetIds);
        }
    }

    public boolean allows(Long assetId) {
        if (allowAll) {
            return true;
        }
        return assetId != null && allowedAssetIds.contains(assetId);
    }
}
