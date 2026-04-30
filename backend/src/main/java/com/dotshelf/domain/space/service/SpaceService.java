package com.dotshelf.domain.space.service;

import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.dto.BookDtos.OwnedBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookSummary;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListItemResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;
import com.dotshelf.domain.space.entity.SpaceLayoutSnapshot;
import com.dotshelf.domain.space.entity.SpaceLike;
import com.dotshelf.domain.space.entity.SpaceVisit;
import com.dotshelf.domain.space.repository.SpaceGuestbookPostItWhitelistRepository;
import com.dotshelf.domain.space.repository.SpaceLayoutSnapshotRepository;
import com.dotshelf.domain.space.repository.SpaceLikeRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceTemplateRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.CursorParser;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Service
@Slf4j
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SpaceService {

    private static final int DEFAULT_LIST_LIMIT = 20;
    private static final int MAX_LIST_LIMIT = 50;
    private static final int MEMORY_SORT_WARNING_THRESHOLD = 500;

    private final SpaceRepository spaceRepository;
    private final SpaceTemplateRepository spaceTemplateRepository;
    private final SpaceVisitRepository spaceVisitRepository;
    private final SpaceLikeRepository spaceLikeRepository;
    private final SpaceLayoutSnapshotRepository layoutSnapshotRepository;
    private final SpaceGuestbookPostItWhitelistRepository whitelistRepository;
    private final FollowRepository followRepository;
    private final UserBlockRepository userBlockRepository;
    private final UserBookRepository userBookRepository;
    private final UserRepository userRepository;
    private final UserService userService;

    @Transactional
    public SpaceResponse getMySpace(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    public SpaceResponse getSpace(Long currentUserId, Long spaceId) {
        Space space = getSpaceEntity(spaceId);
        return SpaceResponse.from(space, isLiked(spaceId, currentUserId));
    }

    public SpaceListResponse getSpaces(Long userId, String keyword, String sort, int limit) {
        int validLimit = validateListLimit(limit);
        String normalizedKeyword = keyword == null ? null : keyword.trim().toLowerCase();
        List<Space> spaces = spaceRepository.findAll().stream()
                .filter(space -> space.getDeletedAt() == null)
                .filter(space -> "PUBLIC".equalsIgnoreCase(space.getVisibilityScope()))
                .filter(space -> normalizedKeyword == null
                        || normalizedKeyword.isBlank()
                        || space.getSpaceName().toLowerCase().contains(normalizedKeyword))
                .sorted(spaceComparator(sort))
                .limit(validLimit)
                .toList();
        Map<Long, User> owners = ownersById(spaces);
        return new SpaceListResponse(spaces.stream()
                .map(space -> toListItem(space, owners.get(space.getOwnerUserId()), userId))
                .toList(), null);
    }

    public DashboardResponse getDashboard(Long currentUserId, Long spaceId, String period) {
        Space space = getSpaceEntity(spaceId);
        User owner = userService.getUser(space.getOwnerUserId());
        long weeklyVisitorCount = spaceVisitRepository
                .countDistinctVisitorsBySpaceIdSince(spaceId, LocalDate.now().minusDays(7));
        return new DashboardResponse(
                SpaceResponse.from(space, isLiked(spaceId, currentUserId)),
                followRepository.countByFollowingUserId(space.getOwnerUserId()),
                followRepository.countByFollowerUserId(space.getOwnerUserId()),
                0,
                0,
                0,
                weeklyVisitorCount,
                owner.getNickname(),
                owner.getProfileImageUrl(),
                owner.getIntroduction()
        );
    }

    private boolean isLiked(Long spaceId, Long viewerUserId) {
        return viewerUserId != null
                && spaceLikeRepository.existsBySpaceIdAndUserId(spaceId, viewerUserId);
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
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        space.applyTheme(request.inventoryId());
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    @Transactional
    public SpaceResponse updateVisibility(Long userId, VisibilityRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        space.changeVisibility(request.visibilityScope());
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    @Transactional
    public SpaceResponse updateGuestbookVisibility(Long userId, VisibilityRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        space.changeGuestbookVisibility(request.visibilityScope());
        return SpaceResponse.from(space, isLiked(space.getId(), currentUserId));
    }

    public SpaceTemplateListResponse getTemplates() {
        return new SpaceTemplateListResponse(spaceTemplateRepository.findByIsActiveTrueOrderByDisplayOrderAsc().stream()
                .map(SpaceTemplateResponse::from)
                .toList());
    }

    @Transactional
    public GuestbookPostItWhitelistResponse getGuestbookPostItWhitelist(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(space.getId()).orElse(null);
        if (whitelist == null) {
            return new GuestbookPostItWhitelistResponse(true, List.of());
        }
        return new GuestbookPostItWhitelistResponse(
                whitelist.isAllowAll(),
                new ArrayList<>(whitelist.getAllowedAssetIds())
        );
    }

    @Transactional
    public GuestbookPostItWhitelistResponse updateGuestbookPostItWhitelist(
            Long userId,
            GuestbookPostItWhitelistRequest request
    ) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = findOrCreateMySpace(currentUserId);
        Set<Long> newAssetIds = request.allowedAssetIds() == null
                ? Set.of()
                : new HashSet<>(request.allowedAssetIds());
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(space.getId())
                .orElseGet(() -> SpaceGuestbookPostItWhitelist.builder()
                        .spaceId(space.getId())
                        .allowAll(true)
                        .build());
        whitelist.update(Boolean.TRUE.equals(request.allowAll()), newAssetIds);
        SpaceGuestbookPostItWhitelist saved = whitelistRepository.save(whitelist);
        return new GuestbookPostItWhitelistResponse(
                saved.isAllowAll(),
                new ArrayList<>(saved.getAllowedAssetIds())
        );
    }

    /**
     * 방명록·답장 작성 시 화이트리스트 enforcement에 사용. allowAll=true 또는 명시 허용된 assetId만 통과.
     * S4 stage에서 GuestbookService가 호출.
     *
     * <p>MR #60 #2 — 정책 명시: 화이트리스트 row가 아예 없는 공간은 <b>default-allow</b>로 처리한다.
     * 마이그레이션 V9 적용 직후 기존 공간들은 row가 없을 수 있으므로,
     * 신규 공간(row 미생성)도 기존 공간처럼 모든 포스트잇을 허용해야 한다.
     * 공간 주인이 화이트리스트를 설정/저장한 시점부터만 enforcement가 적용된다.
     */
    public boolean isPostItAllowed(Long spaceId, Long postItInventoryId) {
        SpaceGuestbookPostItWhitelist whitelist = whitelistRepository.findById(spaceId).orElse(null);
        if (whitelist == null) {
            return true;
        }
        return whitelist.allows(postItInventoryId);
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

    private Map<Long, User> ownersById(List<Space> spaces) {
        List<Long> ownerIds = spaces.stream()
                .map(Space::getOwnerUserId)
                .distinct()
                .toList();
        if (ownerIds.isEmpty()) {
            return Map.of();
        }
        return userRepository.findAllById(ownerIds).stream()
                .collect(Collectors.toMap(User::getId, Function.identity()));
    }

    private SpaceListItemResponse toListItem(Space space, User owner, Long currentUserId) {
        boolean liked = currentUserId != null && spaceLikeRepository.existsBySpaceIdAndUserId(space.getId(), currentUserId);
        boolean following = currentUserId != null
                && !space.getOwnerUserId().equals(currentUserId)
                && followRepository.existsByFollowerUserIdAndFollowingUserId(currentUserId, space.getOwnerUserId());
        return new SpaceListItemResponse(
                space.getId(),
                space.getOwnerUserId(),
                owner == null ? null : owner.getNickname(),
                owner == null ? null : owner.getProfileImageUrl(),
                space.getSpaceName(),
                "공개 서재",
                null,
                null,
                space.getVisibilityScope(),
                space.getVisitCount(),
                space.getLikeCount(),
                followRepository.countByFollowingUserId(space.getOwnerUserId()),
                liked,
                following
        );
    }

    private Comparator<Space> spaceComparator(String sort) {
        if ("RECENT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Space::getId, Comparator.reverseOrder());
        }
        if ("VISIT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Space::getVisitCount)
                    .reversed()
                    .thenComparing(Space::getId, Comparator.reverseOrder());
        }
        return Comparator.comparing(Space::getLikeCount)
                .reversed()
                .thenComparing(Space::getVisitCount, Comparator.reverseOrder())
                .thenComparing(Space::getId, Comparator.reverseOrder());
    }

    private int validateListLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_LIST_LIMIT;
        }
        return Math.min(limit, MAX_LIST_LIMIT);
    }

    /**
     * 대시보드 \"보유 책 풀 목록\" 모달용. v1.0 갱신 — 공간 주인의 활성 보유 책을 정렬·페이지네이션해서 반환.
     * <p>구현: 사용자당 보유 책 평균 ~50권 이내 가정으로 메모리 정렬 + 슬라이싱. 1000권 넘으면 server-side ORDER BY로 전환.
     * <p>권한: dashboard 진입 권한이 있는 viewer만 호출 가능 (controller가 진입 시 visibility 체크). 차단 관계만 추가 검증.
     */
    public OwnedBookListResponse getOwnedBooks(Long viewerUserId, Long spaceId, String sort, String cursor, int limit) {
        Space space = getSpaceEntity(spaceId);
        ensureNotBlockedAgainstOwner(viewerUserId, space.getOwnerUserId());

        int validLimit = validateListLimit(limit);
        Long cursorId = CursorParser.parseId(cursor);

        List<OwnedBookSummary> all = userBookRepository.findOwnedBookSummaries(space.getOwnerUserId());
        // (MR #61 리뷰 #5) 메모리 정렬 fallback 한도. 가정이 무너지면 즉시 알 수 있도록 경고.
        // 임계점 도달 시 server-side ORDER BY + cursor 기반 페이징으로 전환 필요.
        if (all.size() > MEMORY_SORT_WARNING_THRESHOLD) {
            log.warn("[OwnedBooks] memory-sort threshold 초과: userId={} count={}. server-side ORDER BY 전환 검토 필요.",
                    space.getOwnerUserId(), all.size());
        }
        List<OwnedBookSummary> sorted = sortOwnedBooks(all, sort);

        int startIdx = 0;
        if (cursorId != null) {
            for (int i = 0; i < sorted.size(); i++) {
                if (cursorId.equals(sorted.get(i).userBookId())) {
                    startIdx = i + 1;
                    break;
                }
            }
        }
        int endIdx = Math.min(startIdx + validLimit, sorted.size());
        boolean hasMore = endIdx < sorted.size();
        String nextCursor = hasMore ? String.valueOf(sorted.get(endIdx - 1).userBookId()) : null;

        return new OwnedBookListResponse(sorted.subList(startIdx, endIdx), nextCursor);
    }

    private void ensureNotBlockedAgainstOwner(Long viewerUserId, Long ownerUserId) {
        if (viewerUserId == null || viewerUserId.equals(ownerUserId)) {
            return;
        }
        if (userBlockRepository.existsBlockBetween(viewerUserId, ownerUserId)) {
            throw new CustomException(ErrorCode.SPACE_ACCESS_BLOCKED);
        }
    }

    private static List<OwnedBookSummary> sortOwnedBooks(List<OwnedBookSummary> items, String sort) {
        Comparator<OwnedBookSummary> primary = switch (sort == null ? "ADDED_DESC" : sort.toUpperCase()) {
            case "PUBLISHED_DESC" ->
                    Comparator.comparing(OwnedBookSummary::publishedDate,
                            Comparator.nullsLast(Comparator.reverseOrder()));
            case "PUBLISHED_ASC" ->
                    Comparator.comparing(OwnedBookSummary::publishedDate,
                            Comparator.nullsLast(Comparator.naturalOrder()));
            case "TITLE_ASC" ->
                    Comparator.comparing(OwnedBookSummary::title,
                            Comparator.nullsLast(Comparator.naturalOrder()));
            default ->
                    Comparator.comparing(OwnedBookSummary::addedAt,
                            Comparator.nullsLast(Comparator.reverseOrder()));
        };
        Comparator<OwnedBookSummary> tiebreaker = Comparator.comparing(
                OwnedBookSummary::userBookId, Comparator.reverseOrder());
        return items.stream().sorted(primary.thenComparing(tiebreaker)).toList();
    }

}
