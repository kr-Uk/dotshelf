package com.dotshelf.domain.auth.service.social;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;

public interface SocialTokenVerifier {

    SocialProvider supports();

    /**
     * @param tokenType null이면 provider별 하위 호환 기본 토큰 타입으로 해석한다.
     *                  명시된 tokenType이 provider에서 지원되지 않으면 INVALID_INPUT 예외를 던진다.
     */
    SocialUserInfo verify(String token, SocialTokenType tokenType);
}
