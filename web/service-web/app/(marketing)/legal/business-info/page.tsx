import { Metadata } from "next";

import { LegalPage, LegalSection } from "@/components/legal/legal-page";
import {
  getComplaintContact,
  getMerchantInfo,
  getResponsibilityNotice,
} from "@/lib/merchant-info";

export const metadata: Metadata = {
  title: "사업자 정보 | DotShelf",
  description:
    "DotShelf 의 사업자 등록 정보, 통신판매업 신고번호, 대표자, 사업장 주소, 연락처 및 민원 담당자 안내.",
};

export default function BusinessInfoPage() {
  const merchant = getMerchantInfo();
  const complaint = getComplaintContact();
  const responsibilityNotice = getResponsibilityNotice();

  return (
    <LegalPage
      title="사업자 정보"
      subtitle="사업자 등록 정보 / 통신판매업 신고 / 민원 담당자 / 환금성 책임 문구."
      effectiveDate="2026-05-14"
      status="정식 (사업자 정보 미설정 항목은 환경변수 주입 전까지 공란으로 표시)"
    >
      <LegalSection title="사업자 등록 정보">
        <dl className="grid grid-cols-1 sm:grid-cols-[160px_1fr] gap-y-3 gap-x-6 text-[15px]">
          <BusinessRow label="상호명" value={merchant.name} />
          <BusinessRow
            label="사업자등록번호"
            value={merchant.registrationNumber}
          />
          <BusinessRow label="대표자명" value={merchant.representative} />
          <BusinessRow label="사업장 주소" value={merchant.address} />
          <BusinessRow label="대표 전화번호" value={merchant.tel} />
          <BusinessRow label="대표 이메일" value={merchant.email} />
          <BusinessRow
            label="통신판매업 신고번호"
            value={merchant.mailOrderRegistrationNumber}
          />
        </dl>
      </LegalSection>

      <LegalSection title="민원 담당자">
        <dl className="grid grid-cols-1 sm:grid-cols-[160px_1fr] gap-y-3 gap-x-6 text-[15px]">
          <BusinessRow label="담당자 성명" value={complaint.name} />
          <BusinessRow label="민원 연락처" value={complaint.tel} />
        </dl>
        <p className="mt-4 text-[14px] text-paper-muted">
          포인트 충전·아이템 구매·환불 관련 문의는 위 민원 담당자 연락처로
          접수해 주십시오. 영업일 기준 3~7일 이내 검토 후 회신합니다.
        </p>
      </LegalSection>

      <LegalSection title="판매상품 책임 (환금성 업종)">
        <p className="font-semibold">{responsibilityNotice}</p>
        <p className="text-[14px] text-paper-muted">
          본 사이트에서 판매되는 모든 포인트·꾸미기 아이템에 대한 책임은
          상기 사업자에게 있으며, KG이니시스는 결제 처리 대행만을
          담당합니다.
        </p>
      </LegalSection>

      <LegalSection title="결제 대행">
        <p>
          본 사이트의 신용카드 결제는{" "}
          <strong>KG이니시스(주)</strong> 의 결제 대행 서비스를 통해 처리됩니다.
        </p>
      </LegalSection>
    </LegalPage>
  );
}

function BusinessRow({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-paper-muted">{label}</dt>
      <dd className={value ? "text-paper" : "text-paper-muted italic"}>
        {value || "(설정 전)"}
      </dd>
    </>
  );
}
