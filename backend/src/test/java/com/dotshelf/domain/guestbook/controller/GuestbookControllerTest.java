package com.dotshelf.domain.guestbook.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

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

import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookListResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookResponse;
import com.dotshelf.domain.guestbook.service.GuestbookService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class GuestbookControllerTest {

    @Mock
    private GuestbookService guestbookService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new GuestbookController(guestbookService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("방명록 목록은 cursor/limit/current userId를 전달한다")
    void getGuestbooks_passesPagination() throws Exception {
        given(guestbookService.getGuestbooks(1L, 10L, "c1", 5))
                .willReturn(new GuestbookListResponse(List.of(guestbookResponse(99L)), "99"));

        mockMvc.perform(get("/v1/spaces/10/guestbooks")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.guestbooks[0].id").value(99))
                .andExpect(jsonPath("$.result.nextCursor").value("99"));
    }

    @Test
    @DisplayName("방명록 생성은 content 필수값을 검증한다")
    void create_validatesContent() throws Exception {
        given(guestbookService.create(any(), any(), any(GuestbookRequest.class))).willReturn(guestbookResponse(99L));

        mockMvc.perform(post("/v1/spaces/10/guestbooks")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"postItInventoryId":7,"content":"hello","visibilityScope":"PUBLIC","anonymous":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.content").value("hello"));

        mockMvc.perform(post("/v1/spaces/10/guestbooks")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("방명록 삭제는 current userId와 guestbookId를 전달한다")
    void delete_passesIds() throws Exception {
        mockMvc.perform(delete("/v1/guestbooks/99").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        then(guestbookService).should().delete(1L, 99L);
    }

    @Test
    @DisplayName("방명록 답장은 content 필수값을 검증한다")
    void reply_validatesContent() throws Exception {
        given(guestbookService.reply(any(), any(), any(GuestbookReplyRequest.class)))
                .willReturn(replyResponse(30L));

        mockMvc.perform(post("/v1/guestbooks/99/replies")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"reply","visibilityScope":"PUBLIC"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(30));

        mockMvc.perform(post("/v1/guestbooks/99/replies")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("답장 삭제는 guestbookId/replyId/current userId를 전달한다")
    void deleteReply_passesIds() throws Exception {
        mockMvc.perform(delete("/v1/guestbooks/99/replies/30").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        then(guestbookService).should().deleteReply(1L, 99L, 30L);
    }

    private GuestbookResponse guestbookResponse(Long id) {
        return new GuestbookResponse(id, 10L, 1L, "tester", null, false, 7L, null, "hello", "PUBLIC", 0, null, null);
    }

    private GuestbookReplyResponse replyResponse(Long id) {
        return new GuestbookReplyResponse(id, 99L, 2L, "owner", null, null, "reply", "PUBLIC", null);
    }
}
