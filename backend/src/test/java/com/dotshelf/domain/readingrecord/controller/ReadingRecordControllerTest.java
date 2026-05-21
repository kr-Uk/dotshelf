package com.dotshelf.domain.readingrecord.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.Instant;
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

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordPageResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordSummary;
import com.dotshelf.domain.readingrecord.service.ReadingRecordCommentService;
import com.dotshelf.domain.readingrecord.service.ReadingRecordService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class ReadingRecordControllerTest {

    @Mock
    private ReadingRecordService readingRecordService;

    @Mock
    private ReadingRecordCommentService commentService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new ReadingRecordController(readingRecordService, commentService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("독서기록 상세 조회는 current userId와 recordId를 전달한다")
    void getRecord_passesCurrentUserAndRecordId() throws Exception {
        given(readingRecordService.getRecord(1L, 10L)).willReturn(recordResponse(10L));

        mockMvc.perform(get("/v1/reading-records/10").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10));
    }

    @Test
    @DisplayName("내 user book 독서기록 조회는 userBookId 소유권 확인을 서비스에 맡긴다")
    void getMyRecordByUserBook_passesIds() throws Exception {
        given(readingRecordService.getMyRecordByUserBook(1L, 20L)).willReturn(recordResponse(10L));

        mockMvc.perform(get("/v1/user-books/20/reading-record").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.userBookId").value(20));
    }

    @Test
    @DisplayName("사용자 독서기록 목록은 sort/cursor/limit을 전달한다")
    void getUserRecords_passesPagination() throws Exception {
        given(readingRecordService.getUserRecords(1L, 2L, "MOST_LIKED", "c1", 5))
                .willReturn(new ReadingRecordPageResponse(List.of(
                        new ReadingRecordSummary(10L, 9L, "book", "content", "PUBLIC", 3, 1, 4, Instant.now())
                ), "10"));

        mockMvc.perform(get("/v1/users/2/reading-records")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("sort", "MOST_LIKED")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].bookTitle").value("book"))
                .andExpect(jsonPath("$.result.nextCursor").value("10"));
    }

    @Test
    @DisplayName("독서기록 생성은 userBookId 필수값을 검증한다")
    void create_validatesUserBookId() throws Exception {
        given(readingRecordService.create(any(), any(ReadingRecordRequest.class))).willReturn(recordResponse(10L));

        mockMvc.perform(post("/v1/reading-records")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"userBookId":20,"title":"note","content":"body","visibilityScope":"PUBLIC"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.title").value("note"));

        mockMvc.perform(post("/v1/reading-records")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"note"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("userBookId")));
    }

    @Test
    @DisplayName("독서기록 수정은 path id와 request body를 전달한다")
    void update_passesRecordIdAndRequest() throws Exception {
        given(readingRecordService.update(any(), any(), any(ReadingRecordRequest.class))).willReturn(recordResponse(10L));

        mockMvc.perform(patch("/v1/reading-records/10")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"userBookId":20,"title":"changed","content":"body","visibilityScope":"PRIVATE"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(10));
    }

    @Test
    @DisplayName("독서기록 삭제/좋아요/좋아요 취소는 성공 envelope를 반환한다")
    void deleteAndLikes_returnEnvelope() throws Exception {
        given(readingRecordService.like(1L, 10L)).willReturn(new ToggleResponse(true, 1));
        given(readingRecordService.unlike(1L, 10L)).willReturn(new ToggleResponse(false, 0));

        mockMvc.perform(delete("/v1/reading-records/10").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        mockMvc.perform(post("/v1/reading-records/10/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        mockMvc.perform(delete("/v1/reading-records/10/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));

        then(readingRecordService).should().delete(1L, 10L);
    }

    @Test
    @DisplayName("댓글 목록과 댓글 생성은 current userId와 recordId를 전달한다")
    void comments_queryAndCreate() throws Exception {
        given(commentService.getComments(1L, 10L))
                .willReturn(new CommentListResponse(List.of(commentResponse(30L))));
        given(commentService.create(any(), any(), any(CommentRequest.class))).willReturn(commentResponse(31L));

        mockMvc.perform(get("/v1/reading-records/10/comments").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.comments[0].id").value(30));

        mockMvc.perform(post("/v1/reading-records/10/comments")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"hello"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(31));

        mockMvc.perform(post("/v1/reading-records/10/comments")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("댓글 삭제/좋아요/좋아요 취소는 commentId와 current userId를 전달한다")
    void commentMutations_passCurrentUser() throws Exception {
        given(commentService.like(1L, 30L)).willReturn(new ToggleResponse(true, 1));
        given(commentService.unlike(1L, 30L)).willReturn(new ToggleResponse(false, 0));

        mockMvc.perform(delete("/v1/comments/30").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        mockMvc.perform(post("/v1/comments/30/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        mockMvc.perform(delete("/v1/comments/30/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));
    }

    private ReadingRecordResponse recordResponse(Long id) {
        return new ReadingRecordResponse(id, 20L, "note", "body", "PUBLIC", 0, 0, null, null);
    }

    private CommentResponse commentResponse(Long id) {
        return new CommentResponse(id, 10L, 1L, null, 0, "hello", 0, false);
    }
}
