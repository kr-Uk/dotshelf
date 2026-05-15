// 사업자 정보 / 민원 담당자 정보. KG이니시스 충전업종 사전 심사 요건의
// "사이트 하단 상점정보 표기 필수 사항" + 환금성 책임 문구 대응.
//
// 단일 진실 소스: BE `payment.policy.*` 설정 (Spring `PaymentPolicyProperties`).
// service-web 은 BE 정책 API (`GET /v1/payments/policy`) 를 호출해 가져오는 것이 정공법이지만,
// Phase 1 출시 단계에서는 정적 env (`NEXT_PUBLIC_MERCHANT_*` / `NEXT_PUBLIC_COMPLAINT_*`) 를
// 우선 사용해 빌드 타임 주입한다. 실값 확보 전엔 빈 문자열로 렌더링 (placeholder).
// 향후 BE 정책 API 호출 패턴으로 옮길 수 있도록 데이터 형태는 BE 응답과 1:1 매핑.

export type MerchantInfo = {
  name: string;
  registrationNumber: string;
  representative: string;
  address: string;
  tel: string;
  email: string;
  mailOrderRegistrationNumber: string;
};

export type ComplaintContact = {
  name: string;
  tel: string;
};

export function getMerchantInfo(): MerchantInfo {
  return {
    name: env("NEXT_PUBLIC_MERCHANT_NAME"),
    registrationNumber: env("NEXT_PUBLIC_MERCHANT_REGISTRATION_NUMBER"),
    representative: env("NEXT_PUBLIC_MERCHANT_REPRESENTATIVE"),
    address: env("NEXT_PUBLIC_MERCHANT_ADDRESS"),
    tel: env("NEXT_PUBLIC_MERCHANT_TEL"),
    email: env("NEXT_PUBLIC_MERCHANT_EMAIL"),
    mailOrderRegistrationNumber: env(
      "NEXT_PUBLIC_MERCHANT_MAIL_ORDER_REGISTRATION_NUMBER",
    ),
  };
}

export function getComplaintContact(): ComplaintContact {
  return {
    name: env("NEXT_PUBLIC_COMPLAINT_CONTACT_NAME"),
    tel: env("NEXT_PUBLIC_COMPLAINT_CONTACT_TEL"),
  };
}

export function getResponsibilityNotice(): string {
  return (
    env("NEXT_PUBLIC_RESPONSIBILITY_NOTICE") ||
    "DotShelf에서 운영되는 사이트 내 판매되는 모든 상품은 DotShelf에서 책임지고 있습니다."
  );
}

function env(key: string): string {
  // NEXT_PUBLIC_* 는 next 빌드 시점에 정적 inline 되므로 직접 process.env[key] 접근 불가.
  // 명시적 switch 로 inline 강제.
  switch (key) {
    case "NEXT_PUBLIC_MERCHANT_NAME":
      return process.env.NEXT_PUBLIC_MERCHANT_NAME ?? "";
    case "NEXT_PUBLIC_MERCHANT_REGISTRATION_NUMBER":
      return process.env.NEXT_PUBLIC_MERCHANT_REGISTRATION_NUMBER ?? "";
    case "NEXT_PUBLIC_MERCHANT_REPRESENTATIVE":
      return process.env.NEXT_PUBLIC_MERCHANT_REPRESENTATIVE ?? "";
    case "NEXT_PUBLIC_MERCHANT_ADDRESS":
      return process.env.NEXT_PUBLIC_MERCHANT_ADDRESS ?? "";
    case "NEXT_PUBLIC_MERCHANT_TEL":
      return process.env.NEXT_PUBLIC_MERCHANT_TEL ?? "";
    case "NEXT_PUBLIC_MERCHANT_EMAIL":
      return process.env.NEXT_PUBLIC_MERCHANT_EMAIL ?? "";
    case "NEXT_PUBLIC_MERCHANT_MAIL_ORDER_REGISTRATION_NUMBER":
      return (
        process.env.NEXT_PUBLIC_MERCHANT_MAIL_ORDER_REGISTRATION_NUMBER ?? ""
      );
    case "NEXT_PUBLIC_COMPLAINT_CONTACT_NAME":
      return process.env.NEXT_PUBLIC_COMPLAINT_CONTACT_NAME ?? "";
    case "NEXT_PUBLIC_COMPLAINT_CONTACT_TEL":
      return process.env.NEXT_PUBLIC_COMPLAINT_CONTACT_TEL ?? "";
    case "NEXT_PUBLIC_RESPONSIBILITY_NOTICE":
      return process.env.NEXT_PUBLIC_RESPONSIBILITY_NOTICE ?? "";
    default:
      return "";
  }
}
