package com.dotshelf.global.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

/**
 * Spring Boot 4.0 classpath 에서 {@link RestClient.Builder} 가 자동 등록되지 않으므로
 * 명시적으로 prototype 성격의 Builder 를 노출한다.
 * GoogleTokenVerifier, KakaoTokenVerifier 등이 생성자 주입으로 사용.
 */
@Configuration
public class RestClientConfig {

    @Bean
    public RestClient.Builder restClientBuilder() {
        return RestClient.builder();
    }
}
