package com.dotshelf.domain.auth.service.social;

import com.dotshelf.domain.auth.entity.SocialProvider;

public record SocialUserInfo(
        SocialProvider provider,
        String providerUserId,
        String email,
        String nickname,
        String profileImageUrl
) {
}
