package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.space.repository.SpaceLayoutSnapshotRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.TestFixtures;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
@Transactional
class UserSpaceIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private SpaceRepository spaceRepository;

    @Autowired
    private SpaceLayoutSnapshotRepository layoutSnapshotRepository;

    @Test
    @DisplayName("HTTP로 내 프로필을 조회하면 DB에 저장된 사용자 정보를 반환한다")
    void getMe_returnsPersistedUser() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("tester"));

        mockMvc.perform(get("/v1/users/me").header("X-Test-Admin-User-Id", user.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(user.getId()))
                .andExpect(jsonPath("$.result.nickname").value("tester"));
    }

    @Test
    @DisplayName("HTTP로 공간 레이아웃을 저장하면 기본 공간과 레이아웃 스냅샷이 함께 저장된다")
    void saveLayout_createsSpaceAndSnapshot() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("tester"));

        mockMvc.perform(put("/v1/spaces/me/layout")
                        .header("X-Test-Admin-User-Id", user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"layoutPayload":"{\\"items\\":[]}"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.layoutVersion").value(1));

        var space = spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(user.getId()).orElseThrow();
        assertThat(space.getSpaceName()).isEqualTo("나의 공간");
        assertThat(layoutSnapshotRepository.findAll())
                .anySatisfy(snapshot -> {
                    assertThat(snapshot.getSpaceId()).isEqualTo(space.getId());
                    assertThat(snapshot.getLayoutVersion()).isEqualTo(1);
                    assertThat(snapshot.getLayoutPayload()).isEqualTo("{\"items\":[]}");
                });
    }

    @Test
    @DisplayName("HTTP로 공간 공개 범위를 변경하면 DB의 공간 상태가 변경된다")
    void updateVisibility_updatesPersistedSpace() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("tester"));

        mockMvc.perform(patch("/v1/spaces/me/visibility")
                        .header("X-Test-Admin-User-Id", user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"visibilityScope":"PRIVATE"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.visibilityScope").value("PRIVATE"));

        var space = spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(user.getId()).orElseThrow();
        assertThat(space.getVisibilityScope()).isEqualTo("PRIVATE");
    }
}
