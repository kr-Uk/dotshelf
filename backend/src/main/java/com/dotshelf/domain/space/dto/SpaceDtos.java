package com.dotshelf.domain.space.dto;

import java.util.List;

import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceTemplate;

import jakarta.validation.constraints.NotBlank;

public final class SpaceDtos {

    private SpaceDtos() {
    }

    public record SpaceResponse(
            Long id,
            Long ownerUserId,
            String spaceName,
            String visibilityScope,
            String guestbookVisibilityScope,
            long visitCount,
            long likeCount,
            long layoutVersion,
            Long currentThemeInventoryId
    ) {

        public static SpaceResponse from(Space space) {
            return new SpaceResponse(
                    space.getId(),
                    space.getOwnerUserId(),
                    space.getSpaceName(),
                    space.getVisibilityScope(),
                    space.getGuestbookVisibilityScope(),
                    space.getVisitCount(),
                    space.getLikeCount(),
                    space.getLayoutVersion(),
                    space.getCurrentThemeInventoryId()
            );
        }
    }

    public record DashboardResponse(
            SpaceResponse space,
            long followerCount,
            long followingCount,
            long bookshelfCount,
            long bookCount,
            long readingRecordCount
    ) {
    }

    public record LayoutSaveRequest(
            @NotBlank(message = "layoutPayload은 필수입니다.")
            String layoutPayload,
            Long baseLayoutVersion
    ) {
    }

    public record LayoutSaveResponse(Long spaceId, long layoutVersion) {
    }

    public record VisibilityRequest(
            @NotBlank(message = "visibilityScope은 필수입니다.")
            String visibilityScope
    ) {
    }

    public record ThemeApplyRequest(Long inventoryId) {
    }

    public record SpaceTemplateResponse(Long id, String code, String name, String previewImageUrl, String layoutPayload) {

        public static SpaceTemplateResponse from(SpaceTemplate template) {
            return new SpaceTemplateResponse(
                    template.getId(),
                    template.getCode(),
                    template.getName(),
                    template.getPreviewImageUrl(),
                    template.getLayoutPayload()
            );
        }
    }

    public record SpaceTemplateListResponse(List<SpaceTemplateResponse> templates) {
    }
}
