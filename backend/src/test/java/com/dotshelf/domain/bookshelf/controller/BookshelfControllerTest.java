package com.dotshelf.domain.bookshelf.controller;

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

import java.math.BigDecimal;
import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfItemResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfListResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfRenderResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.CreateBookshelfRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UpdateBookshelfRequest;
import com.dotshelf.domain.bookshelf.service.BookshelfService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class BookshelfControllerTest {

    @Mock
    private BookshelfService bookshelfService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new BookshelfController(bookshelfService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("공간 책장 목록 조회는 spaceId를 서비스로 전달한다")
    void getBookshelves_passesSpaceId() throws Exception {
        given(bookshelfService.getBookshelves(10L))
                .willReturn(new BookshelfListResponse(List.of(bookshelfResponse(3L))));

        mockMvc.perform(get("/v1/spaces/10/bookshelves"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.bookshelves[0].id").value(3));
    }

    @Test
    @DisplayName("책장 렌더 조회는 current userId와 bookshelfId를 전달한다")
    void getItems_passesCurrentUserAndBookshelfId() throws Exception {
        given(bookshelfService.getItems(1L, 3L))
                .willReturn(new BookshelfRenderResponse(bookshelfResponse(3L), List.of(sectionResponse(7L)), List.of()));

        mockMvc.perform(get("/v1/bookshelves/3/items").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.bookshelf.id").value(3))
                .andExpect(jsonPath("$.result.sections[0].id").value(7));
    }

    @Test
    @DisplayName("책장 생성은 필수 spaceId를 검증하고 성공 시 응답한다")
    void create_validatesSpaceId() throws Exception {
        given(bookshelfService.create(any(), any(CreateBookshelfRequest.class)))
                .willReturn(bookshelfResponse(3L));

        mockMvc.perform(post("/v1/bookshelves")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"spaceId":10,"name":"main","visibilityScope":"PUBLIC"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.name").value("main"));

        mockMvc.perform(post("/v1/bookshelves")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"name":"main"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("spaceId")));
    }

    @Test
    @DisplayName("책장 수정은 path id와 request body를 서비스로 전달한다")
    void update_passesRequest() throws Exception {
        given(bookshelfService.update(any(), any(), any(UpdateBookshelfRequest.class)))
                .willReturn(bookshelfResponse(3L));

        mockMvc.perform(patch("/v1/bookshelves/3")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"name":"renamed","description":"desc","visibilityScope":"PRIVATE","skinInventoryId":8}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(3));
    }

    @Test
    @DisplayName("책장 삭제와 순서 변경은 성공 envelope를 반환한다")
    void deleteAndReorder_returnSuccessEnvelope() throws Exception {
        mockMvc.perform(delete("/v1/bookshelves/3").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        mockMvc.perform(put("/v1/bookshelves/order")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookshelfIds":[3,4,5]}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        then(bookshelfService).should().delete(1L, 3L);
    }

    @Test
    @DisplayName("섹션 정렬 변경은 alignment 필수값을 검증한다")
    void updateSectionAlignment_validatesAlignment() throws Exception {
        given(bookshelfService.updateSectionAlignment(any(), any(), any(), any()))
                .willReturn(new SectionResponse(7L, 0, 0, 20, "CENTER"));

        mockMvc.perform(patch("/v1/bookshelves/3/sections/7/alignment")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"alignment":"CENTER"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.alignment").value("CENTER"));

        mockMvc.perform(patch("/v1/bookshelves/3/sections/7/alignment")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"alignment":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("인테리어 오브젝트 추가는 inventory/section/slot 필수값을 검증한다")
    void addInteriorObject_validatesRequiredFields() throws Exception {
        given(bookshelfService.addInteriorObject(any(), any(), any()))
                .willReturn(itemResponse(9L));

        mockMvc.perform(post("/v1/bookshelves/3/interior-objects")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"assetInventoryId":8,"sectionId":7,"slotNo":2}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.assetInventoryId").value(8));

        mockMvc.perform(post("/v1/bookshelves/3/interior-objects")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"assetInventoryId":8}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("sectionId")));
    }

    @Test
    @DisplayName("책장 아이템 이동/삭제는 itemId와 current userId를 서비스로 전달한다")
    void moveAndRemoveItem_passCurrentUser() throws Exception {
        given(bookshelfService.moveItem(1L, 9L, 3L, 7L, 2, "FRONT", "COVER"))
                .willReturn(itemResponse(9L));
        given(bookshelfService.removeItem(1L, 9L)).willReturn(itemResponse(9L));

        mockMvc.perform(patch("/v1/bookshelf-items/9")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookshelfId":3,"sectionId":7,"slotNo":2,"facingDirection":"FRONT","displayStyle":"COVER"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(9));

        mockMvc.perform(delete("/v1/bookshelf-items/9").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(9));
    }

    private BookshelfResponse bookshelfResponse(Long id) {
        return new BookshelfResponse(id, 10L, "main", "desc", "PUBLIC", 0, null, null);
    }

    private SectionResponse sectionResponse(Long id) {
        return new SectionResponse(id, 0, 0, 20, "LEFT");
    }

    private BookshelfItemResponse itemResponse(Long id) {
        return new BookshelfItemResponse(id, 3L, 7L, "DECOR", null, 8L, 2, "FRONT", "COVER", BigDecimal.ZERO);
    }
}
