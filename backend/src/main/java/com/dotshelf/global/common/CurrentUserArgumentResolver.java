package com.dotshelf.global.common;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.MethodParameter;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.bind.support.WebDataBinderFactory;
import org.springframework.web.context.request.NativeWebRequest;
import org.springframework.web.method.support.HandlerMethodArgumentResolver;
import org.springframework.web.method.support.ModelAndViewContainer;

/**
 * `@CurrentUser Long userId` 에 SecurityContext 의 principal(userId) 을 주입. 테스트 인증이 켜져 있으면
 * 명시된 테스트 헤더의 userId 를 fallback 으로 사용한다. 인증되지 않았거나 principal 이 Long 이 아니면 null 을
 * 반환한다.
 */
@Component
public class CurrentUserArgumentResolver implements HandlerMethodArgumentResolver {

    private final boolean testAuthEnabled;
    private final String testAuthHeaderName;

    public CurrentUserArgumentResolver(
            @Value("${admin.test-auth.enabled:false}") boolean testAuthEnabled,
            @Value("${admin.test-auth.header-name:X-Test-Admin-User-Id}") String testAuthHeaderName
    ) {
        this.testAuthEnabled = testAuthEnabled;
        this.testAuthHeaderName = testAuthHeaderName;
    }

    @Override
    public boolean supportsParameter(MethodParameter parameter) {
        return parameter.hasParameterAnnotation(CurrentUser.class)
                && Long.class.isAssignableFrom(parameter.getParameterType());
    }

    @Override
    public Object resolveArgument(
            MethodParameter parameter,
            ModelAndViewContainer mavContainer,
            NativeWebRequest webRequest,
            WebDataBinderFactory binderFactory
    ) {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication != null && authentication.isAuthenticated()) {
            Object principal = authentication.getPrincipal();
            if (principal instanceof Long userId) {
                return userId;
            }
        }
        if (testAuthEnabled) {
            return resolveTestUserId(webRequest);
        }
        return null;
    }

    private Long resolveTestUserId(NativeWebRequest webRequest) {
        String headerValue = webRequest.getHeader(testAuthHeaderName);
        if (headerValue == null || headerValue.isBlank()) {
            return null;
        }
        try {
            long userId = Long.parseLong(headerValue.trim());
            return userId > 0 ? userId : null;
        } catch (NumberFormatException exception) {
            return null;
        }
    }
}
