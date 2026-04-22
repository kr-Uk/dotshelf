package com.dotshelf.domain.auth.service.social;

import com.dotshelf.domain.auth.entity.SocialProvider;

public interface SocialTokenVerifier {

    SocialProvider supports();

    SocialUserInfo verify(String token);
}
