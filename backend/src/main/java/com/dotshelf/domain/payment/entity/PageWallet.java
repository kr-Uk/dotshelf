package com.dotshelf.domain.payment.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "page_wallets")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PageWallet extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private Long userId;

    @Column(nullable = false)
    private int balancePages = 0;

    @Builder
    private PageWallet(Long userId, int balancePages) {
        this.userId = userId;
        this.balancePages = balancePages;
    }

    public void charge(int pages) {
        this.balancePages += pages;
    }

    public void use(int pages) {
        this.balancePages -= pages;
    }
}
