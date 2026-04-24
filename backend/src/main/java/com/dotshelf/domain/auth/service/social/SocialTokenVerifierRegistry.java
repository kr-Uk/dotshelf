package com.dotshelf.domain.auth.service.social;

import java.util.EnumMap;
import java.util.List;
import java.util.Map;

import org.springframework.stereotype.Component;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

@Component
public class SocialTokenVerifierRegistry {

    private final Map<SocialProvider, SocialTokenVerifier> verifiers;

    public SocialTokenVerifierRegistry(List<SocialTokenVerifier> verifiers) {
        Map<SocialProvider, SocialTokenVerifier> verifierMap = new EnumMap<>(SocialProvider.class);
        for (SocialTokenVerifier verifier : verifiers) {
            if (verifierMap.put(verifier.supports(), verifier) != null) {
                throw new IllegalStateException("중복된 소셜 로그인 검증기입니다: " + verifier.supports());
            }
        }
        this.verifiers = Map.copyOf(verifierMap);
    }

    public SocialUserInfo verify(SocialProvider provider, SocialTokenType tokenType, String token) {
        SocialTokenVerifier verifier = verifiers.get(provider);
        if (verifier == null) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "지원하지 않는 소셜 로그인 provider 입니다.");
        }
        return verifier.verify(token, tokenType);
    }
}
