package com.dotshelf.domain.auth.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataIntegrityViolationException;

import com.dotshelf.domain.auth.entity.AuthRefreshToken;
import com.dotshelf.domain.auth.entity.GuestMigrationImport;
import com.dotshelf.support.RepositoryTestSupport;

class AuthRefreshTokenRepositoryTest extends RepositoryTestSupport {

    @Autowired
    private AuthRefreshTokenRepository authRefreshTokenRepository;

    @Autowired
    private GuestMigrationImportRepository guestMigrationImportRepository;

    @Test
    @DisplayName("refresh token은 tokenHash로 조회한다")
    void findByTokenHash() {
        authRefreshTokenRepository.saveAndFlush(refreshToken(1L, "hash-1", Instant.now().plusSeconds(3600)));

        var result = authRefreshTokenRepository.findByTokenHash("hash-1");

        assertThat(result).isPresent();
        assertThat(result.get().getUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("활성 refresh token 조회는 revoke되지 않고 만료되지 않은 토큰만 반환한다")
    void findByUserIdAndRevokedAtIsNullAndExpiresAtAfter_filtersActiveTokens() {
        Instant now = Instant.now();
        AuthRefreshToken active = authRefreshTokenRepository.save(refreshToken(1L, "active", now.plusSeconds(3600)));
        AuthRefreshToken revoked = authRefreshTokenRepository.save(refreshToken(1L, "revoked", now.plusSeconds(3600)));
        authRefreshTokenRepository.save(refreshToken(1L, "expired", now.minusSeconds(1)));
        authRefreshTokenRepository.save(refreshToken(2L, "other-user", now.plusSeconds(3600)));
        revoked.revoke();
        authRefreshTokenRepository.flush();

        var tokens = authRefreshTokenRepository.findByUserIdAndRevokedAtIsNullAndExpiresAtAfter(1L, now);

        assertThat(tokens)
                .extracting(AuthRefreshToken::getId)
                .containsExactly(active.getId());
    }

    @Test
    @DisplayName("refresh token hash는 중복 저장할 수 없다")
    void uniqueTokenHash() {
        Instant now = Instant.now();
        authRefreshTokenRepository.saveAndFlush(refreshToken(1L, "same", now.plusSeconds(3600)));

        assertThatThrownBy(() -> authRefreshTokenRepository.saveAndFlush(
                refreshToken(2L, "same", now.plusSeconds(3600))
        )).isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("게스트 마이그레이션 import는 payload와 기본 상태를 저장한다")
    void guestMigrationImport_save() {
        var saved = guestMigrationImportRepository.saveAndFlush(GuestMigrationImport.builder()
                .userId(1L)
                .payload("{\"books\":[]}")
                .guestSessionId("guest-1")
                .build());

        assertThat(saved.getId()).isNotNull();
        assertThat(saved.getMigrationStatus()).isEqualTo("PENDING");
        assertThat(saved.getRequestedAt()).isNotNull();
        assertThat(saved.getPayload()).contains("books");
    }

    private AuthRefreshToken refreshToken(Long userId, String tokenHash, Instant expiresAt) {
        return AuthRefreshToken.builder()
                .userId(userId)
                .tokenHash(tokenHash)
                .expiresAt(expiresAt)
                .build();
    }
}
