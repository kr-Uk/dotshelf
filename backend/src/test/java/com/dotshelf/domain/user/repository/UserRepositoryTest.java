package com.dotshelf.domain.user.repository;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class UserRepositoryTest {

    @Autowired
    private UserRepository userRepository;

    @Test
    @DisplayName("User 저장 시 auditing 필드와 기본 role/status/onboarded 값이 채워진다")
    void save_setsAuditingAndDefaults() {
        var saved = userRepository.saveAndFlush(TestFixtures.user("tester"));

        assertThat(saved.getId()).isNotNull();
        assertThat(saved.getCreatedAt()).isNotNull();
        assertThat(saved.getUpdatedAt()).isNotNull();
        assertThat(saved.getRole()).isEqualTo("USER");
        assertThat(saved.getStatus()).isEqualTo("ACTIVE");
        assertThat(saved.isOnboarded()).isFalse();
    }

    @Test
    @DisplayName("닉네임과 이메일 존재 여부를 조회한다")
    void existsByNicknameAndEmail() {
        userRepository.saveAndFlush(TestFixtures.user("tester"));

        assertThat(userRepository.existsByNickname("tester")).isTrue();
        assertThat(userRepository.existsByEmail("tester@example.com")).isTrue();
        assertThat(userRepository.existsByNickname("missing")).isFalse();
    }
}
