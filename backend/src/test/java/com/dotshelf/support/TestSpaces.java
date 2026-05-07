package com.dotshelf.support;

import com.dotshelf.domain.space.entity.Space;

public class TestSpaces {

    public Space space(Long id, Long ownerUserId) {
        return TestFixtures.space(id, ownerUserId);
    }
}
