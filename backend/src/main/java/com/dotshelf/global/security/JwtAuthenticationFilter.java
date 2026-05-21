package com.dotshelf.global.security;

import java.io.IOException;
import java.time.Instant;
import java.util.List;

import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.JwtTokenProvider.AccessTokenClaims;

import tools.jackson.databind.json.JsonMapper;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

/**
 * `Authorization: Bearer ...` 헤더에서 JWT 를 꺼내 검증하고, 성공하면 `Authentication` 을
 * `SecurityContextHolder` 에 등록한다. 토큰 부재는 통과 (이후 Security 가 401 처리), 검증 실패는 즉시 응답.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class JwtAuthenticationFilter extends OncePerRequestFilter {

    private static final String AUTH_HEADER = "Authorization";
    private static final String BEARER_PREFIX = "Bearer ";

    private final JwtTokenProvider jwtTokenProvider;
    private final UserRepository userRepository;
    private final JsonMapper jsonMapper;

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain filterChain
    ) throws ServletException, IOException {
        String token = resolveToken(request);
        if (token != null) {
            try {
                AccessTokenClaims tokenClaims = jwtTokenProvider.parseAccessToken(token);
                Long userId = tokenClaims.userId();
                validateSession(tokenClaims);
                UsernamePasswordAuthenticationToken authentication = new UsernamePasswordAuthenticationToken(
                        userId,
                        null,
                        List.of(new SimpleGrantedAuthority("ROLE_USER"))
                );
                SecurityContextHolder.getContext().setAuthentication(authentication);
            } catch (CustomException e) {
                SecurityContextHolder.clearContext();
                writeError(response, e.getErrorCode());
                return;
            }
        }
        filterChain.doFilter(request, response);
    }

    private void validateSession(AccessTokenClaims tokenClaims) {
        UserRepository.SessionInvalidationView user = userRepository.findSessionInvalidationById(tokenClaims.userId())
                .orElseThrow(() -> new CustomException(ErrorCode.INVALID_TOKEN));
        if ("DELETED".equalsIgnoreCase(user.getStatus()) || user.getDeletedAt() != null) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }

        Instant sessionInvalidatedAt = user.getSessionInvalidatedAt();
        if (sessionInvalidatedAt != null && tokenClaims.issuedAt().isBefore(sessionInvalidatedAt)) {
            throw new CustomException(ErrorCode.INVALID_TOKEN);
        }
    }

    private String resolveToken(HttpServletRequest request) {
        String header = request.getHeader(AUTH_HEADER);
        if (header == null || !header.startsWith(BEARER_PREFIX)) {
            return null;
        }
        String token = header.substring(BEARER_PREFIX.length()).trim();
        return token.isEmpty() ? null : token;
    }

    private void writeError(HttpServletResponse response, ErrorCode errorCode) throws IOException {
        response.setStatus(errorCode.getStatus().value());
        response.setContentType("application/json;charset=UTF-8");
        ApiResponse<Void> body = ApiResponse.error(
                errorCode.getStatus().value(),
                errorCode.getCode(),
                errorCode.getMessage()
        );
        response.getWriter().write(jsonMapper.writeValueAsString(body));
    }
}
