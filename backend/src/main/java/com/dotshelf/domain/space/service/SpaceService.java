package com.dotshelf.domain.space.service;

import java.time.LocalDate;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceLayoutSnapshot;
import com.dotshelf.domain.space.entity.SpaceLike;
import com.dotshelf.domain.space.entity.SpaceVisit;
import com.dotshelf.domain.space.repository.SpaceLayoutSnapshotRepository;
import com.dotshelf.domain.space.repository.SpaceLikeRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceTemplateRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SpaceService {

    private final SpaceRepository spaceRepository;
    private final SpaceTemplateRepository spaceTemplateRepository;
    private final SpaceVisitRepository spaceVisitRepository;
    private final SpaceLikeRepository spaceLikeRepository;
    private final SpaceLayoutSnapshotRepository layoutSnapshotRepository;
    private final FollowRepository followRepository;
    private final UserService userService;

    @Transactional
    public SpaceResponse getMySpace(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return SpaceResponse.from(findOrCreateMySpace(currentUserId));
    }

    public SpaceResponse getSpace(Long spaceId) {
        return SpaceResponse.from(getSpaceEntity(spaceId));
    }

    public DashboardResponse getDashboard(Long spaceId, String period) {
        Space space = getSpaceEntity(spaceId);
        return new DashboardResponse(
                SpaceResponse.from(space),
                followRepository.countByFollowingUserId(space.getOwnerUserId()),
                followRepository.countByFollowerUserId(space.getOwnerUserId()),
                0,
                0,
                0
        );
    }

    @Transactional
    public ToggleResponse recordVisit(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        if (space.getOwnerUserId().equals(currentUserId)) {
            return new ToggleResponse(false, space.getVisitCount());
        }
        LocalDate today = LocalDate.now();
        if (!spaceVisitRepository.existsBySpaceIdAndVisitorUserIdAndVisitedDate(spaceId, currentUserId, today)) {
            spaceVisitRepository.save(SpaceVisit.builder()
                    .spaceId(spaceId)
                    .visitorUserId(currentUserId)
                    .visitedDate(today)
                    .build());
            space.increaseVisitCount();
        }
        return new ToggleResponse(true, space.getVisitCount());
    }

    @Transactional
    public ToggleResponse like(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        if (!spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, currentUserId)) {
            spaceLikeRepository.save(SpaceLike.builder()
                    .spaceId(spaceId)
                    .userId(currentUserId)
                    .build());
            space.like();
        }
        return new ToggleResponse(true, space.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long spaceId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = getSpaceEntity(spaceId);
        if (spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, currentUserId)) {
            spaceLikeRepository.deleteBySpaceIdAndUserId(spaceId, currentUserId);
            space.unlike();
        }
        return new ToggleResponse(false, space.getLikeCount());
    }

    @Transactional
    public LayoutSaveResponse saveMyLayout(Long userId, LayoutSaveRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        long version = space.nextLayoutVersion();
        layoutSnapshotRepository.save(SpaceLayoutSnapshot.builder()
                .spaceId(space.getId())
                .layoutVersion(version)
                .layoutPayload(request.layoutPayload())
                .savedByUserId(currentUserId)
                .build());
        return new LayoutSaveResponse(space.getId(), version);
    }

    @Transactional
    public SpaceResponse applyTheme(Long userId, ThemeApplyRequest request) {
        Space space = findOrCreateMySpace(userService.requireUserId(userId));
        space.applyTheme(request.inventoryId());
        return SpaceResponse.from(space);
    }

    @Transactional
    public SpaceResponse updateVisibility(Long userId, VisibilityRequest request) {
        Space space = findOrCreateMySpace(userService.requireUserId(userId));
        space.changeVisibility(request.visibilityScope());
        return SpaceResponse.from(space);
    }

    @Transactional
    public SpaceResponse updateGuestbookVisibility(Long userId, VisibilityRequest request) {
        Space space = findOrCreateMySpace(userService.requireUserId(userId));
        space.changeGuestbookVisibility(request.visibilityScope());
        return SpaceResponse.from(space);
    }

    public SpaceTemplateListResponse getTemplates() {
        return new SpaceTemplateListResponse(spaceTemplateRepository.findByIsActiveTrueOrderByDisplayOrderAsc().stream()
                .map(SpaceTemplateResponse::from)
                .toList());
    }

    public Space getSpaceEntity(Long spaceId) {
        return spaceRepository.findById(spaceId)
                .orElseThrow(() -> new CustomException(ErrorCode.SPACE_NOT_FOUND));
    }

    @Transactional
    public Space findOrCreateMySpace(Long userId) {
        return spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(userId)
                .orElseGet(() -> spaceRepository.save(Space.builder()
                        .ownerUserId(userId)
                        .spaceName("나의 공간")
                        .build()));
    }
}
