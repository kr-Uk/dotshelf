package com.dotshelf.domain.auth.service.social;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;

public interface SocialTokenVerifier {

    SocialProvider supports();

    /**
     * @param tokenType null이면 provider별 하위 호환 기본 토큰 타입으로 해석한다.
     *                  명시된 tokenType이 provider에서 지원되지 않으면 INVALID_INPUT 예외를 던진다.
     * @param redirectUri provider가 authorization code 교환 시 요구하는 redirect URI.
     * @param nonce provider가 nonce claim 검증을 요구할 때 사용하는 클라이언트 검증값.
     * @param codeVerifier PKCE code_verifier (RFC 7636). null이 아니면 token 교환 호출에 그대로
     *                     forward해서 OAuth 제공자가 SHA256(code_verifier) == code_challenge 검증.
     *                     null이면 legacy server pair(client_secret) 흐름으로 fallback.
     *                     id_token / access_token 직접 전송 흐름(mobile)에서는 무시됨.
     */
    SocialUserInfo verify(String token, SocialTokenType tokenType, String redirectUri, String nonce, String codeVerifier);
}
