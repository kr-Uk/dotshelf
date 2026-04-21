package com.dotshelf.global.config;

import org.springframework.beans.factory.config.ConfigurableListableBeanFactory;
import org.springframework.beans.factory.support.BeanDefinitionRegistry;
import org.springframework.beans.factory.support.BeanDefinitionRegistryPostProcessor;
import org.springframework.context.annotation.Configuration;

/**
 * springdoc-openapi 2.8.x 의 {@code queryDslQuerydslPredicateOperationCustomizer} 빈은
 * {@code org.springframework.data.util.TypeInformation} 를 참조한다. 이 클래스는 Spring Data 4
 * (Spring Boot 4 번들) 에서 제거되었기 때문에, JPA 의 {@code PersistenceAnnotationBeanPostProcessor}
 * 가 빈 메타데이터를 introspection 하는 순간 {@code NoClassDefFoundError} 가 터지고 앱이 기동되지 않는다.
 *
 * <p>우리 코드는 {@code @QuerydslPredicate} 어노테이션을 사용하지 않으므로 이 빈이 불필요하다.
 * 빈 후처리가 일어나기 전에 레지스트리에서 정의를 제거해 문제를 회피한다.
 *
 * <p>springdoc 이 Spring Boot 4 를 공식 지원하는 버전을 내면 이 클래스는 삭제한다.
 */
@Configuration
public class SpringDocQuerydslWorkaround implements BeanDefinitionRegistryPostProcessor {

    private static final String PROBLEM_BEAN = "queryDslQuerydslPredicateOperationCustomizer";

    @Override
    public void postProcessBeanDefinitionRegistry(BeanDefinitionRegistry registry) {
        if (registry.containsBeanDefinition(PROBLEM_BEAN)) {
            registry.removeBeanDefinition(PROBLEM_BEAN);
        }
    }

    @Override
    public void postProcessBeanFactory(ConfigurableListableBeanFactory beanFactory) {
    }
}
