import Link from "next/link";
import { Metadata } from "next";

import {
  LegalOrderedList,
  LegalPage,
  LegalSection,
  LegalUnorderedList,
} from "@/components/legal/legal-page";

export const metadata: Metadata = {
  title: "환불 규정 | DotShelf",
  description:
    "DotShelf 의 포인트 충전 및 꾸미기 아이템 구매에 대한 환불 신청 조건, 절차, 처리 기간 안내.",
};

export default function RefundPolicyPage() {
  return (
    <LegalPage
      title="환불 규정"
      subtitle="포인트 충전 및 꾸미기 아이템 구매에 대한 환불 신청 조건·절차·처리 기간 요약."
      effectiveDate="2026-05-14"
      status="초안"
    >
      <LegalSection title="1. 청약철회 신청 조건">
        <LegalUnorderedList
          items={[
            <>
              <strong>포인트 충전</strong>: 충전일로부터 7일 이내, 사용하지 않은
              부분에 한해 청약철회 가능. (전자상거래법 제17조)
            </>,
            <>
              <strong>꾸미기 아이템 구매</strong>: 구매일로부터 7일 이내, 사용을
              시작하지 않은 아이템에 한해 환불 가능. 책장 공간에 배치하거나
              미리보기 이상의 상호작용을 시작한 경우 청약철회가 제한될 수 있음
              (전자상거래법 제17조 제2항 제5호).
            </>,
            <>
              <strong>회사 귀책 환불</strong>: 회사의 귀책 사유로 서비스를
              이용할 수 없게 된 경우, 미사용 포인트 전액 및 미사용 아이템 잔여
              가치 환불.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="2. 환불 방식">
        <LegalOrderedList
          items={[
            <>
              회원이 결제한 수단으로 동일하게 진행함을 원칙으로 합니다.
            </>,
            <>
              카드 결제는 결제 취소로 처리되며, 카드사 정책에 따라 영업일 기준
              3~7일 소요됩니다.
            </>,
            <>
              카드 유효기간 만료 등 결제 취소가 기술적으로 불가능한 경우에는
              회원이 지정한 계좌로 송금합니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="3. 서비스 종료 시 잔여 가치 환불">
        <p>
          회사가 본 서비스의 전부 또는 일부를 종료하는 경우, 회사는 종료일로부터{" "}
          <strong>최소 30일 이전</strong> 에 통지하며, 다음의 잔여 가치를
          환불합니다.
        </p>
        <LegalUnorderedList
          items={[
            <>
              <strong>미사용 포인트</strong>: 충전 금액 전액
            </>,
            <>
              <strong>사용 시작한 아이템</strong>: 종료 통지일 기준 잔여
              유효기간에 비례한 가치 (자동 갱신으로 인해 유효기간이 무기한에
              가까운 경우, 회사와 회원이 합리적으로 협의한 기준에 따라 산정).
            </>,
          ]}
        />
        <p>
          환불 신청은 종료 공지일로부터{" "}
          <strong>최소 6개월간</strong> 접수합니다.
        </p>
      </LegalSection>

      <LegalSection title="4. 환불 신청 방법">
        <p>
          앱 내 설정 화면의 &quot;문의·환불 신청&quot; 메뉴 또는 회사 대표
          이메일을 통해 접수해 주십시오. 접수 후 영업일 기준 3~7일 이내 검토하여
          처리 결과를 회신합니다.
        </p>
      </LegalSection>

      <LegalSection title="5. 관련 약관·법령">
        <p>
          본 환불 규정은 결제·아이템 이용약관 (제4조 환불 / 제5조 서비스 종료
          시 처리) 의 요약본입니다. 자세한 조항은 결제·아이템 이용약관 전문을
          확인해 주십시오.
        </p>
        <LegalUnorderedList
          items={[
            <Link
              key="terms"
              href="/legal/payment-terms"
              className="underline hover:text-wood"
            >
              결제·아이템 이용약관 전문 보기
            </Link>,
            <span key="biz">
              사업자 정보 및 민원 담당자 연락처는{" "}
              <Link
                href="/legal/business-info"
                className="underline hover:text-wood"
              >
                사업자 정보 페이지
              </Link>{" "}
              참고
            </span>,
            "전자상거래법 제17조 (청약철회), 제18조 (효과)",
            "콘텐츠산업진흥법 제27조~제29조 (이용자 보호)",
          ]}
        />
      </LegalSection>
    </LegalPage>
  );
}
