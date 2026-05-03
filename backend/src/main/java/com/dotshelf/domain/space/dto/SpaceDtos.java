package com.dotshelf.domain.space.dto;

import java.util.List;

import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceTemplate;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

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
            boolean liked,
            long layoutVersion,
            Long currentThemeInventoryId,
            Long currentWallpaperInventoryId,
            Long currentFloorInventoryId,
            Long currentDeskInventoryId,
            Long currentDashboardInventoryId,
            Long currentInteriorLightInventoryId,
            Long currentExteriorLightInventoryId
    ) {

        public static SpaceResponse from(Space space) {
            return from(space, false);
        }

        public static SpaceResponse from(Space space, boolean liked) {
            return new SpaceResponse(
                    space.getId(),
                    space.getOwnerUserId(),
                    space.getSpaceName(),
                    space.getVisibilityScope(),
                    space.getGuestbookVisibilityScope(),
                    space.getVisitCount(),
                    space.getLikeCount(),
                    liked,
                    space.getLayoutVersion(),
                    space.getCurrentThemeInventoryId(),
                    space.getCurrentWallpaperInventoryId(),
                    space.getCurrentFloorInventoryId(),
                    space.getCurrentDeskInventoryId(),
                    space.getCurrentDashboardInventoryId(),
                    space.getCurrentInteriorLightInventoryId(),
                    space.getCurrentExteriorLightInventoryId()
            );
        }
    }

    /**
     * 카테고리별 PATCH 6 endpoint 공통 body. inventoryId가 null이면 해당 카테고리를
     * 기본 외형으로 되돌린다(unset).
     */
    public record CategoryInventoryRequest(Long inventoryId) {
    }

    /**
     * 카테고리별 PATCH/전체 테마 apply 공통 응답. themeReset이 true면 클라이언트가
     * 룸 전체 reload 필요(커스터마이징 불가 테마 자동 해제).
     */
    public record SpaceMutationResponse(SpaceResponse space, boolean themeReset) {

        public static SpaceMutationResponse of(Space space, boolean liked, boolean themeReset) {
            return new SpaceMutationResponse(SpaceResponse.from(space, liked), themeReset);
        }
    }

    public record DashboardResponse(
            SpaceResponse space,
            long followerCount,
            long followingCount,
            long bookshelfCount,
            long bookCount,
            long readingRecordCount,
            long weeklyVisitorCount,
            String ownerNickname,
            String ownerProfileImageUrl,
            String ownerIntroduction
    ) {
    }

    public record SpaceListItemResponse(
            Long spaceId,
            Long ownerUserId,
            String ownerNickname,
            String ownerProfileImageUrl,
            String spaceName,
            String mood,
            String description,
            String thumbnailUrl,
            String visibilityScope,
            long visitCount,
            long likeCount,
            long followerCount,
            boolean liked,
            boolean following
    ) {
    }

    public record SpaceListResponse(List<SpaceListItemResponse> spaces, String nextCursor) {
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

    /**
     * POST /v1/spaces/me/theme/{inventoryId}/apply 의 mode 값.
     * RESET: 모든 설치 오브젝트 보관함 이동 + 책장 skin 모두 초기화 후 테마 적용
     * KEEP_BLOCKERS_ONLY: 테마 기본 배치와 충돌하는 설치 오브젝트만 보관함 이동
     */
    public enum ThemeApplyMode {
        RESET,
        KEEP_BLOCKERS_ONLY
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

    public record GuestbookPostItWhitelistRequest(
            @NotNull(message = "allowAll은 필수입니다.")
            Boolean allowAll,
            List<Long> allowedAssetIds
    ) {
    }

    public record GuestbookPostItWhitelistResponse(
            boolean allowAll,
            List<Long> allowedAssetIds
    ) {
    }
}
