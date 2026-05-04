package com.dotshelf.global.config;

import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Configuration;

import com.dotshelf.domain.payment.service.InicisPaymentProperties;

@Configuration
@EnableConfigurationProperties(InicisPaymentProperties.class)
public class PaymentConfig {
}
