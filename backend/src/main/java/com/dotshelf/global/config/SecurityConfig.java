package com.dotshelf.global.config;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;

import com.dotshelf.global.security.JwtAuthenticationFilter;
import com.dotshelf.global.security.JwtProperties;
import com.dotshelf.global.security.SecurityEntryPoints;
import com.dotshelf.global.security.SocialLoginProperties;

import lombok.RequiredArgsConstructor;

@Configuration
@EnableConfigurationProperties({JwtProperties.class, SocialLoginProperties.class})
@RequiredArgsConstructor
public class SecurityConfig {

    private static final String[] PUBLIC_ENDPOINTS = {
            "/health",
            "/actuator/health",
            "/actuator/health/**",
            // Prometheus 스크레이프용. 공개 Nginx 에서 /actuator 는 404 차단되므로
            // 실제 접근은 Tailscale 내부(prod 호스트의 Prometheus)에서만 가능.
            "/actuator/prometheus",
            "/v1/auth/**",
            // Unity 클라이언트의 책장 초기 카탈로그 로딩(비인증). hasPath 기준이라 /v1/books/* 는 영향 없음.
            "/v1/books",
            "/swagger-ui/**",
            "/swagger-ui.html",
            "/v3/api-docs/**",
            "/v3/api-docs.yaml"
    };

    private final JwtAuthenticationFilter jwtAuthenticationFilter;
    private final SecurityEntryPoints securityEntryPoints;

    @Value("${admin.auth-bypass:false}")
    private boolean adminAuthBypass;

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        http
                .csrf(csrf -> csrf.disable())
                .cors(cors -> {})
                .sessionManagement(sm -> sm.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
                .authorizeHttpRequests(auth -> {
                    auth.requestMatchers(PUBLIC_ENDPOINTS).permitAll();
                    if (adminAuthBypass) {
                        auth.requestMatchers("/v1/admin/**").permitAll();
                    }
                    auth.anyRequest().authenticated();
                })
                .exceptionHandling(ex -> ex
                        .authenticationEntryPoint(securityEntryPoints.authenticationEntryPoint())
                        .accessDeniedHandler(securityEntryPoints.accessDeniedHandler()))
                .formLogin(form -> form.disable())
                .httpBasic(basic -> basic.disable())
                .addFilterBefore(jwtAuthenticationFilter, UsernamePasswordAuthenticationFilter.class);
        return http.build();
    }
}
