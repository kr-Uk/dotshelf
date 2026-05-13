package com.dotshelf.domain.asset.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.time.Instant;
import java.util.List;

import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetCreateRequest;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetListItemResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetListResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetResponse;
import com.dotshelf.domain.asset.dto.AssetDtos.AdminAssetUpdateRequest;
import com.dotshelf.domain.asset.service.AdminAssetService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class AdminAssetControllerTest {

    @Mock
    private AdminAssetService adminAssetService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new AdminAssetController(adminAssetService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("에셋 생성은 request body와 current userId를 서비스로 전달한다")
    void createAsset_passesRequestAndCurrentUser() throws Exception {
        given(adminAssetService.createAsset(eq(1L), any(AdminAssetCreateRequest.class), eq("127.0.0.1")))
                .willReturn(response(false));

        mockMvc.perform(post("/v1/admin/assets")
                        .header("X-Test-Admin-User-Id", "1")
                        .with(request -> {
                            request.setRemoteAddr("127.0.0.1");
                            return request;
                        })
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "code": "DEEP_SEA_CHAIR_001",
                                  "name": "깊은바다 의자",
                                  "categoryCode": "FURNITURE",
                                  "assetType": "CHAIR",
                                  "collectionCode": "DEEP_SEA",
                                  "pricePages": 300
                                }
                                """))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.httpStatus").value(201))
                .andExpect(jsonPath("$.result.collectionCode").value("DEEP_SEA"));

        then(adminAssetService).should().createAsset(eq(1L), any(AdminAssetCreateRequest.class), eq("127.0.0.1"));
    }

    @Test
    @DisplayName("에셋 수정은 path assetId와 request body를 서비스로 전달한다")
    void updateAsset_passesRequestAndAssetId() throws Exception {
        given(adminAssetService.updateAsset(eq(1L), eq(10L), any(AdminAssetUpdateRequest.class), eq("127.0.0.1")))
                .willReturn(response(false));

        mockMvc.perform(patch("/v1/admin/assets/10")
                        .header("X-Test-Admin-User-Id", "1")
                        .with(request -> {
                            request.setRemoteAddr("127.0.0.1");
                            return request;
                        })
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "name": "깊은바다 의자 v2",
                                  "collectionCode": "DEEP_SEA"
                                }
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.code").value("DEEP_SEA_CHAIR_001"));

        then(adminAssetService).should().updateAsset(eq(1L), eq(10L), any(AdminAssetUpdateRequest.class), eq("127.0.0.1"));
    }

    @Test
    @DisplayName("에셋 목록 조회는 쿼리 파라미터를 그대로 서비스로 전달한다")
    void searchAssets_passesQueryParams() throws Exception {
        AdminAssetListItemResponse item = new AdminAssetListItemResponse(
                10L, "DEEP_SEA_CHAIR_001", "깊은바다 의자", "CHAIR", "DEEP_SEA",
                "NORMAL", "DEEPSEA", "floor", 1, 300, true, true, false, false,
                Instant.parse("2026-05-11T00:00:00Z"));
        given(adminAssetService.searchAssets(eq(1L), eq("DEEP_SEA"), eq("CHAIR"), eq("의자"), eq(false), eq(null), eq(20)))
                .willReturn(new AdminAssetListResponse(List.of(item), null, 1L));

        mockMvc.perform(get("/v1/admin/assets")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("collection", "DEEP_SEA")
                        .param("type", "CHAIR")
                        .param("q", "의자")
                        .param("active", "false")
                        .param("limit", "20"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].id").value(10))
                .andExpect(jsonPath("$.result.items[0].hasAssetUrl").value(true))
                .andExpect(jsonPath("$.result.totalCount").value(1));
    }

    @Test
    @DisplayName("에셋 상세 조회는 path assetId를 서비스로 전달한다")
    void getAsset_passesAssetId() throws Exception {
        given(adminAssetService.getAssetDetail(1L, 10L)).willReturn(response(false));

        mockMvc.perform(get("/v1/admin/assets/10")
                        .header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10))
                .andExpect(jsonPath("$.result.assetUrl").value("https://cdn.test/chair.glb"));
    }

    @Test
    @DisplayName("에셋 공개는 path assetId와 current userId를 서비스로 전달한다")
    void publishAsset_passesAssetIdAndCurrentUser() throws Exception {
        given(adminAssetService.publishAsset(1L, 10L, "127.0.0.1")).willReturn(response(true));

        mockMvc.perform(put("/v1/admin/assets/10/publish")
                        .header("X-Test-Admin-User-Id", "1")
                        .with(request -> {
                            request.setRemoteAddr("127.0.0.1");
                            return request;
                        }))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        then(adminAssetService).should().publishAsset(1L, 10L, "127.0.0.1");
    }

    private AdminAssetResponse response(boolean active) {
        return new AdminAssetResponse(
                10L,
                "DEEP_SEA_CHAIR_001",
                "깊은바다 의자",
                "CHAIR",
                "DEEP_SEA",
                300, // pricePages
                "DotShelf Studio", // supplier
                "DEEPSEA", // theme
                "floor", // placement
                null, // compatibleTargets (V27 이후 일반 Asset 은 null)
                "https://cdn.test/chair.glb",
                "https://cdn.test/chair.png",
                null, // previewBundleUrl
                null, // previewBundleContentType
                null, // previewBundleSize
                null, // previewBundleHash
                "model/gltf-binary",
                100L,
                "sha256:test",
                1,
                active);
    }
}
