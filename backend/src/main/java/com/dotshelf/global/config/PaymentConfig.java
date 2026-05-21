package com.dotshelf.global.config;

import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Configuration;

import com.dotshelf.domain.payment.service.InicisPaymentProperties;
import com.dotshelf.domain.payment.service.PaymentPolicyProperties;

@Configuration
@EnableConfigurationProperties({InicisPaymentProperties.class, PaymentPolicyProperties.class})
public class PaymentConfig {
}
