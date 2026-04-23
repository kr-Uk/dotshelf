package com.dotshelf.domain.auth.service.social;

import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.SocialTokenType;

public interface SocialTokenVerifier {

    SocialProvider supports();

    SocialUserInfo verify(String token, SocialTokenType tokenType);
}
