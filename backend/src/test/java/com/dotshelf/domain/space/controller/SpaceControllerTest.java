package com.dotshelf.domain.space.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.asset.dto.AssetDtos.DownloadManifestResponse;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookSummary;
import com.dotshelf.domain.space.dto.SpaceDtos.CategoryInventoryRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListItemResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceAssetManifestListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceMutationResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyMode;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailConfirmRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadUrlRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadUrlResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

class SpaceControllerTest {

    @Mock
    private SpaceService spaceService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);
        mockMvc = MockMvcBuilders
                .standaloneSetup(new SpaceController(spaceService))
                .setControllerAdvice(new GlobalExceptionHandler())
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .build();
    }

    @Test
    @DisplayName("내 공간 조회는 CurrentUser를 서비스에 전달한다")
    void getMySpace_withCurrentUser_returnsSpace() throws Exception {
        given(spaceService.getMySpace(1L)).willReturn(spaceResponse("PUBLIC"));

        mockMvc.perform(get("/v1/spaces/me").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10))
                .andExpect(jsonPath("$.result.spaceName").value("나의 공간"));

        then(spaceService).should().getMySpace(1L);
    }

    @Test
    @DisplayName("공간 목록 조회는 검색어/정렬/limit을 서비스에 전달한다")
    void getSpaces_passesQueryParameters() throws Exception {
        given(spaceService.getSpaces(1L, "book", "POPULAR", 5))
                .willReturn(new SpaceListResponse(List.of(spaceListItem()), "space_10"));

        mockMvc.perform(get("/v1/spaces")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("keyword", "book")
                        .param("sort", "POPULAR")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaces[0].spaceId").value(10))
                .andExpect(jsonPath("$.result.nextCursor").value("space_10"));

        then(spaceService).should().getSpaces(1L, "book", "POPULAR", 5);
    }

    @Test
    @DisplayName("공간 상세와 대시보드는 viewer userId와 spaceId를 전달한다")
    void getSpaceAndDashboard_passViewerAndSpaceId() throws Exception {
        given(spaceService.getSpace(1L, 10L)).willReturn(spaceResponse("PUBLIC"));
        given(spaceService.getDashboard(1L, 10L, "weekly"))
                .willReturn(new DashboardResponse(
                        spaceResponse("PUBLIC"),
                        3,
                        4,
                        2,
                        9,
                        5,
                        7,
                        "owner",
                        "profile.png",
                        "intro"
                ));

        mockMvc.perform(get("/v1/spaces/10").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10));

        mockMvc.perform(get("/v1/spaces/10/dashboard")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("period", "weekly"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.weeklyVisitorCount").value(7));
    }

    @Test
    @DisplayName("공간 보유 도서 조회는 sort/cursor/limit을 전달한다")
    void getOwnedBooks_passesPagingParameters() throws Exception {
        given(spaceService.getOwnedBooks(1L, 10L, "ADDED_DESC", "book_1", 3))
                .willReturn(new OwnedBookListResponse(List.of(new OwnedBookSummary(
                        100L,
                        20L,
                        "title",
                        "author",
                        "publisher",
                        LocalDate.of(2024, 1, 1),
                        "cover.png",
                        LocalDateTime.of(2026, 1, 1, 0, 0)
                )), "book_100"));

        mockMvc.perform(get("/v1/spaces/10/books")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("sort", "ADDED_DESC")
                        .param("cursor", "book_1")
                        .param("limit", "3"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].userBookId").value(100))
                .andExpect(jsonPath("$.result.nextCursor").value("book_100"));
    }

    @Test
    @DisplayName("방문 공간 asset manifest는 viewer와 spaceId를 전달한다")
    void getAssetManifests_passesViewerAndSpaceId() throws Exception {
        given(spaceService.getVisibleAssetManifests(1L, 10L))
                .willReturn(new SpaceAssetManifestListResponse(
                        10L,
                        2L,
                        List.of(new DownloadManifestResponse(20L, "DESK", "NATURAL", 3, List.of()))));

        mockMvc.perform(get("/v1/spaces/10/asset-manifests")
                        .header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaceId").value(10))
                .andExpect(jsonPath("$.result.layoutVersion").value(2))
                .andExpect(jsonPath("$.result.manifests[0].assetId").value(20));

        then(spaceService).should().getVisibleAssetManifests(1L, 10L);
    }

    @Test
    @DisplayName("공간 layout 조회는 viewer와 spaceId를 전달한다")
    void getLayout_passesViewerAndSpaceId() throws Exception {
        given(spaceService.getLayout(1L, 10L))
                .willReturn(new LayoutResponse(10L, 3L, "{\"items\":[]}"));

        mockMvc.perform(get("/v1/spaces/10/layout")
                        .header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaceId").value(10))
                .andExpect(jsonPath("$.result.layoutVersion").value(3))
                .andExpect(jsonPath("$.result.layoutPayload").value("{\"items\":[]}"));

        then(spaceService).should().getLayout(1L, 10L);
    }

    @Test
    @DisplayName("방문/좋아요/좋아요 취소는 toggle 응답을 반환한다")
    void visitLikeUnlike_returnToggleResponses() throws Exception {
        given(spaceService.recordVisit(1L, 10L)).willReturn(new ToggleResponse(true, 11));
        given(spaceService.like(1L, 10L)).willReturn(new ToggleResponse(true, 6));
        given(spaceService.unlike(1L, 10L)).willReturn(new ToggleResponse(false, 5));

        mockMvc.perform(post("/v1/spaces/10/visits").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.count").value(11));

        mockMvc.perform(post("/v1/spaces/10/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        mockMvc.perform(delete("/v1/spaces/10/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));
    }

    @Test
    @DisplayName("레이아웃 저장 요청은 layoutPayload validation을 적용한다")
    void saveLayout_blankPayload_returnsValidationError() throws Exception {
        mockMvc.perform(put("/v1/spaces/me/layout")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"layoutPayload":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("layoutPayload")));
    }

    @Test
    @DisplayName("레이아웃 저장 성공 시 저장된 layoutVersion을 반환한다")
    void saveLayout_validRequest_returnsVersion() throws Exception {
        given(spaceService.saveMyLayout(any(), any(LayoutSaveRequest.class)))
                .willReturn(new LayoutSaveResponse(10L, 2L));

        mockMvc.perform(put("/v1/spaces/me/layout")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"layoutPayload":"{\\"items\\":[]}"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaceId").value(10))
                .andExpect(jsonPath("$.result.layoutVersion").value(2));
    }

    @Test
    @DisplayName("썸네일 presigned URL 발급 요청을 서비스에 전달한다")
    void thumbnailUploadUrl_passesRequest() throws Exception {
        given(spaceService.createThumbnailUploadUrl(any(), any(ThumbnailUploadUrlRequest.class)))
                .willReturn(new ThumbnailUploadUrlResponse(
                        "https://upload.example.com/object",
                        "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg",
                        "https://cdn.test/spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg",
                        300));

        mockMvc.perform(post("/v1/spaces/me/thumbnail-upload-url")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"contentType":"image/jpeg","byteLength":1234}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.uploadUrl").value("https://upload.example.com/object"))
                .andExpect(jsonPath("$.result.key").value("spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg"))
                .andExpect(jsonPath("$.result.publicUrl").value("https://cdn.test/spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg"));

        then(spaceService).should().createThumbnailUploadUrl(any(), any(ThumbnailUploadUrlRequest.class));
    }

    @Test
    @DisplayName("썸네일 업로드 확정 요청을 서비스에 전달한다")
    void confirmThumbnail_passesKey() throws Exception {
        given(spaceService.confirmThumbnail(any(), any(ThumbnailConfirmRequest.class)))
                .willReturn(new ThumbnailUploadResponse(
                        10L,
                        "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg",
                        "https://cdn.test/spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg",
                        1L));

        mockMvc.perform(patch("/v1/spaces/me/thumbnail")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"key":"spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.spaceId").value(10))
                .andExpect(jsonPath("$.result.thumbnailKey").value("spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg"))
                .andExpect(jsonPath("$.result.thumbnailUrl").value("https://cdn.test/spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.jpg"))
                .andExpect(jsonPath("$.result.layoutVersion").value(1));

        then(spaceService).should().confirmThumbnail(any(), any(ThumbnailConfirmRequest.class));
    }

    @Test
    @DisplayName("테마 적용은 inventoryId를 서비스에 전달한다")
    void applyTheme_passesInventoryId() throws Exception {
        given(spaceService.applyTheme(any(), any(ThemeApplyRequest.class)))
                .willReturn(spaceResponse("PUBLIC", 77L));

        mockMvc.perform(patch("/v1/spaces/me/theme")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(""" 
                                {"inventoryId":77}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.currentThemeInventoryId").value(77));
    }

    @Test
    @DisplayName("카테고리별 외형 변경 엔드포인트는 각각 맞는 서비스 메서드로 연결된다")
    void categoryAppearanceEndpoints_routeToMatchingServices() throws Exception {
        given(spaceService.changeWallpaper(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(21L));
        given(spaceService.changeFloor(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(22L));
        given(spaceService.changeDesk(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(23L));
        given(spaceService.changeDashboard(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(24L));
        given(spaceService.changeNotebook(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(25L));
        given(spaceService.changeInteriorLight(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(26L));
        given(spaceService.changeExteriorLight(any(), any(CategoryInventoryRequest.class))).willReturn(mutation(27L));

        mockMvc.perform(patch("/v1/spaces/me/wallpaper").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":21}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(21));

        mockMvc.perform(patch("/v1/spaces/me/floor").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":22}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(22));

        mockMvc.perform(patch("/v1/spaces/me/desk").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":23}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(23));

        mockMvc.perform(patch("/v1/spaces/me/dashboard").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":24}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(24));

        mockMvc.perform(patch("/v1/spaces/me/notebook").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":25}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(25));

        mockMvc.perform(patch("/v1/spaces/me/interior-light").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":26}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(26));

        mockMvc.perform(patch("/v1/spaces/me/exterior-light").header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON).content("{\"inventoryId\":27}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.space.currentWallpaperInventoryId").value(27));
    }

    @Test
    @DisplayName("모드가 있는 테마 적용은 path inventoryId와 mode를 전달한다")
    void applyThemeWithMode_passesInventoryIdAndMode() throws Exception {
        given(spaceService.applyThemeWithMode(1L, 77L, ThemeApplyMode.RESET)).willReturn(mutation(77L));

        mockMvc.perform(post("/v1/spaces/me/theme/77/apply")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("mode", "RESET"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.themeReset").value(false));

        then(spaceService).should().applyThemeWithMode(1L, 77L, ThemeApplyMode.RESET);
    }

    @Test
    @DisplayName("공간 공개 범위 수정은 VisibilityRequest를 서비스에 전달한다")
    void updateVisibility_validRequest_returnsSpace() throws Exception {
        given(spaceService.updateVisibility(any(), any(VisibilityRequest.class))).willReturn(spaceResponse("PRIVATE"));

        mockMvc.perform(patch("/v1/spaces/me/visibility")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"visibilityScope":"PRIVATE"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.visibilityScope").value("PRIVATE"));
    }

    @Test
    @DisplayName("방명록 공개 범위는 validation을 적용하고 성공 시 응답을 반환한다")
    void updateGuestbookVisibility_validatesAndUpdates() throws Exception {
        mockMvc.perform(patch("/v1/spaces/me/guestbook-visibility")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("visibilityScope")));

        given(spaceService.updateGuestbookVisibility(any(), any(VisibilityRequest.class)))
                .willReturn(spaceResponse("PUBLIC"));

        mockMvc.perform(patch("/v1/spaces/me/guestbook-visibility")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"visibilityScope":"FOLLOWERS"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.guestbookVisibilityScope").value("PUBLIC"));
    }

    @Test
    @DisplayName("공간 템플릿 목록은 템플릿 배열을 반환한다")
    void getTemplates_returnsTemplateList() throws Exception {
        given(spaceService.getTemplates())
                .willReturn(new SpaceTemplateListResponse(List.of(
                        new SpaceTemplateResponse(1L, "DEFAULT", "기본", "preview.png", "{}")
                )));

        mockMvc.perform(get("/v1/spaces/templates"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.templates[0].code").value("DEFAULT"));
    }

    @Test
    @DisplayName("방명록 포스트잇 whitelist 조회/수정은 CurrentUser와 요청값을 전달한다")
    void guestbookPostItWhitelist_queryAndUpdate() throws Exception {
        given(spaceService.getGuestbookPostItWhitelist(1L))
                .willReturn(new GuestbookPostItWhitelistResponse(false, List.of(10L, 11L)));
        given(spaceService.updateGuestbookPostItWhitelist(any(), any(GuestbookPostItWhitelistRequest.class)))
                .willReturn(new GuestbookPostItWhitelistResponse(true, List.of()));

        mockMvc.perform(get("/v1/spaces/me/guestbook-postit-whitelist")
                        .header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.allowedAssetIds[0]").value(10));

        mockMvc.perform(put("/v1/spaces/me/guestbook-postit-whitelist")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"allowAll":true,"allowedAssetIds":[]}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.allowAll").value(true));
    }

    @Test
    @DisplayName("방명록 포스트잇 whitelist 수정은 allowAll 필수값을 검증한다")
    void guestbookPostItWhitelist_validatesAllowAll() throws Exception {
        mockMvc.perform(put("/v1/spaces/me/guestbook-postit-whitelist")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"allowedAssetIds":[1,2]}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("allowAll")));
    }

    private SpaceListItemResponse spaceListItem() {
        return new SpaceListItemResponse(
                10L,
                1L,
                "owner",
                "profile.png",
                "나의 공간",
                "calm",
                "desc",
                "thumb.png",
                "PUBLIC",
                10,
                5,
                3,
                true,
                false
        );
    }

    private SpaceMutationResponse mutation(Long inventoryId) {
        return new SpaceMutationResponse(spaceResponse("PUBLIC", null, inventoryId), false);
    }

    private SpaceResponse spaceResponse(String visibilityScope) {
        return spaceResponse(visibilityScope, null);
    }

    private SpaceResponse spaceResponse(String visibilityScope, Long themeInventoryId) {
        return spaceResponse(visibilityScope, themeInventoryId, null);
    }

    private SpaceResponse spaceResponse(String visibilityScope, Long themeInventoryId, Long wallpaperInventoryId) {
        return new SpaceResponse(
                10L,
                1L,
                "나의 공간",
                visibilityScope,
                "PUBLIC",
                0,
                0,
                false,
                0,
                null,
                themeInventoryId,
                wallpaperInventoryId,
                null,
                null,
                null,
                null,
                null,
                null
        );
    }
}
