package com.dotshelf.domain.auth.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class UserSocialAccountRepositoryTest {

    @Autowired
    private UserSocialAccountRepository userSocialAccountRepository;

    @Test
    @DisplayName("provider와 providerUserId로 소셜 계정을 조회한다")
    void findByProviderAndProviderUserId() {
        userSocialAccountRepository.saveAndFlush(TestFixtures.socialAccount(1L, SocialProvider.GOOGLE, "google-1"));

        var result = userSocialAccountRepository.findByProviderAndProviderUserId(SocialProvider.GOOGLE, "google-1");

        assertThat(result).isPresent();
        assertThat(result.get().getUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("같은 provider와 providerUserId는 중복 저장할 수 없다")
    void uniqueProviderAndProviderUserId() {
        userSocialAccountRepository.saveAndFlush(TestFixtures.socialAccount(1L, SocialProvider.GOOGLE, "google-1"));

        assertThatThrownBy(() -> userSocialAccountRepository.saveAndFlush(
                TestFixtures.socialAccount(2L, SocialProvider.GOOGLE, "google-1")
        )).isInstanceOf(DataIntegrityViolationException.class);
    }
}
