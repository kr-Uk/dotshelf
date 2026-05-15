import { Metadata } from "next";

import {
  LegalOrderedList,
  LegalPage,
  LegalSection,
  LegalUnorderedList,
} from "@/components/legal/legal-page";

export const metadata: Metadata = {
  title: "개인정보 처리방침 | DotShelf",
  description:
    "DotShelf 가 수집·이용·보관·파기하는 개인정보의 항목, 목적, 보유 기간 및 이용자 권리에 관한 처리방침.",
};

export default function PrivacyPolicyPage() {
  return (
    <LegalPage
      title="개인정보 처리방침"
      subtitle="DotShelf 가 수집·이용하는 개인정보의 항목과 목적, 보유 기간, 이용자 권리에 관한 안내."
      effectiveDate="2026-05-14"
      status="초안 (변호사 검토 및 OAuth 제공자별 동의 항목 정리 진행 중)"
    >
      <LegalSection title="제1조 (수집하는 개인정보 항목)">
        <p>
          DotShelf (이하 &quot;회사&quot;) 는 서비스 제공을 위해 다음의 개인정보를
          수집합니다.
        </p>
        <LegalUnorderedList
          items={[
            <>
              <strong>소셜 로그인 시</strong>: 소셜 제공자가 제공한 식별자,
              이메일, 프로필 이미지 URL, 닉네임 (Google / Kakao / Apple — 제공자별
              필수 항목에 따라 일부 항목만 수집될 수 있음)
            </>,
            <>
              <strong>서비스 이용 중</strong>: 책장·아이템 구매·소셜 활동 기록,
              디바이스 정보, IP 주소, 접속 로그
            </>,
            <>
              <strong>결제 시</strong>: 결제 주문 식별자, 결제 금액, 결제 일시,
              구매자 이름·전화번호·이메일 (KG이니시스 결제창 입력 정보 — 회사는
              암호화된 주문 식별자만 보관, 카드 정보 자체는 보관하지 않음)
            </>,
            <>
              <strong>환불·민원 시</strong>: 환불 계좌 정보 (카드 취소가 불가한
              경우에 한함), 민원 사유 및 첨부 자료
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제2조 (개인정보의 이용 목적)">
        <LegalOrderedList
          items={[
            <>회원 가입 및 본인 확인, 계정 관리, 서비스 제공</>,
            <>
              포인트 충전 및 아이템 구매·환불·정산 (PG 결제 처리 포함)
            </>,
            <>고객 문의·민원 응대, 신고 처리</>,
            <>
              서비스 운영 및 개선을 위한 통계·분석 (개인을 식별할 수 없는 형태로
              가공)
            </>,
            <>
              관련 법령상 의무 이행 (전자상거래법, 통신비밀보호법 등)
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제3조 (개인정보의 보유 및 파기)">
        <LegalOrderedList
          items={[
            <>
              회원 탈퇴 시 회사가 수집한 개인정보는 지체 없이 파기합니다. 단,
              관련 법령에서 일정 기간 보관을 요구하는 정보 (예: 전자상거래법상
              결제·계약·청약철회 기록 5년) 는 해당 기간 동안 분리 보관 후
              파기합니다.
            </>,
            <>
              결제 정보는 결제 완료일로부터 5년간 보관됩니다.
              (전자상거래법 제6조)
            </>,
            <>
              로그·접속 기록은 통신비밀보호법에 따라 3개월간 보관됩니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제4조 (개인정보의 제3자 제공)">
        <p>
          회사는 이용자의 개인정보를 외부에 제공하지 않습니다. 단, 다음의 경우에는
          예외로 합니다.
        </p>
        <LegalUnorderedList
          items={[
            "이용자가 사전에 동의한 경우",
            "법령에 의거하거나 수사기관의 적법한 요청이 있는 경우",
            "결제 처리를 위해 PG사 (KG이니시스) 에 결제 정보를 전달하는 경우 (결제 목적 한정)",
          ]}
        />
      </LegalSection>

      <LegalSection title="제5조 (이용자의 권리)">
        <p>
          이용자는 언제든지 본인의 개인정보를 조회·수정·삭제할 수 있으며, 회원
          탈퇴를 통해 개인정보 처리 정지를 요청할 수 있습니다. 처리방침에 관한
          문의는 본 페이지 하단 또는 사업자 정보 페이지의 민원 담당자 연락처로
          접수해 주십시오.
        </p>
      </LegalSection>

      <LegalSection title="제6조 (개정 이력)">
        <p>
          본 처리방침은 <strong>2026년 5월 14일</strong> 시행합니다. 회사는 개정
          시 시행 7일 전까지 서비스 내 공지를 통해 알립니다.
        </p>
      </LegalSection>
    </LegalPage>
  );
}
