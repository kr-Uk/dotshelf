import { Metadata } from "next";

import {
  LegalOrderedList,
  LegalPage,
  LegalSection,
  LegalSubsection,
  LegalUnorderedList,
} from "@/components/legal/legal-page";

export const metadata: Metadata = {
  title: "결제·아이템 이용약관 | DotShelf",
  description:
    "DotShelf 의 포인트 충전, 꾸미기 아이템 구매, 환불, 서비스 종료 시 잔여 가치 처리 등 결제 관련 이용약관.",
};

export default function PaymentTermsPage() {
  return (
    <LegalPage
      title="결제·아이템 이용약관"
      subtitle="포인트 충전, 꾸미기 아이템 구매·환불, 서비스 종료 시 잔여 가치 처리에 관한 조항."
      effectiveDate="2026-05-14"
      status="초안 (변호사 검토 진행 중)"
    >
      <LegalSection title="제0조 (적용 범위)">
        <p>
          본 약관은 DotShelf (이하 &quot;회사&quot;) 가 제공하는 서비스의{" "}
          <strong>꾸미기 아이템 결제·환불·이용 관련 조항</strong> 만 다룹니다.
          회원가입·계정·개인정보·서비스 일반 이용 등은 별도의 회원약관 및
          개인정보 처리방침에서 정합니다.
        </p>
      </LegalSection>

      <LegalSection title="제1조 (용어 정의)">
        <p>이 약관에서 사용하는 용어의 뜻은 다음과 같습니다.</p>
        <LegalOrderedList
          items={[
            <>
              <strong>&quot;포인트&quot;</strong> 란 회원이 유료로 충전하여
              꾸미기 아이템 구매에 사용할 수 있는 서비스 내 결제 수단을
              말합니다.
            </>,
            <>
              <strong>&quot;꾸미기 아이템&quot;</strong> (이하
              &quot;아이템&quot;) 이란 회원이 자신의 책장 공간을 꾸미기 위해
              포인트로 구매하여 이용하는 디지털 콘텐츠를 말합니다. 책장 재질,
              소품, 분위기 테마 등이 포함됩니다.
            </>,
            <>
              <strong>&quot;이용권한&quot;</strong> 이란 회원이 구매한 아이템을
              본인의 책장 공간에서 사용할 수 있는 권리를 말합니다. 아이템의
              소유권 및 저작권은 회사에 있으며, 회원은 이용권한만을 가집니다.
            </>,
            <>
              <strong>&quot;유효기간&quot;</strong> 이란 회원이 구매한 포인트
              및 아이템 이용권한의 최초 효력 기간을 말하며, 본 약관이 정하는
              바에 따라 자동 갱신됩니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제2조 (포인트 충전 및 결제)">
        <LegalOrderedList
          items={[
            <>
              회원은 카드결제 등 회사가 정한 결제 수단을 통해 포인트를 충전할
              수 있습니다.
            </>,
            <>
              충전된 포인트의 <strong>유효기간은 충전일로부터 1년</strong>{" "}
              입니다. 단, <strong>본 서비스가 정상 운영되는 동안</strong>{" "}
              유효기간이 도래하기 전 자동으로 1년씩 연장되며, 회원은 추가
              비용 없이 계속 이용할 수 있습니다.
            </>,
            <>충전 포인트는 양도·환금·임대할 수 없습니다.</>,
            <>
              회원이 충전한 포인트의 환불은 제4조(환불) 규정을 따릅니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제3조 (아이템 구매 및 이용권한)">
        <LegalOrderedList
          items={[
            <>회원은 보유 포인트를 사용하여 아이템을 구매할 수 있습니다.</>,
            <>
              구매한 아이템의{" "}
              <strong>이용권한 유효기간은 구매일로부터 1년</strong> 입니다. 단,{" "}
              <strong>본 서비스가 정상 운영되는 동안</strong> 유효기간이
              도래하기 전 자동으로 1년씩 연장되며, 회원은 추가 비용 없이
              계속 이용할 수 있습니다.
            </>,
            <>
              아이템 이용권한은 회원의 계정에 귀속되며, 다른 회원에게
              양도·판매·임대할 수 없습니다.
            </>,
            <>
              <span>
                회사는 다음 각 호의 경우 회원에게 사전 통지한 후 아이템의
                외형·기능을 변경할 수 있습니다.
              </span>
              <LegalUnorderedList
                items={[
                  "기술적 한계나 호환성 문제 해결을 위해 필요한 경우",
                  "법령 준수, 권리관계 조정 등 불가피한 사유가 있는 경우",
                  "회원의 이용 경험 개선을 위한 경미한 개선",
                ]}
              />
            </>,
            <>
              회사는 변경 전후 아이템의 가치가 현저히 달라지는 경우, 회원에게
              동등 가치 이상의 보상 (대체 아이템 또는 포인트 환급) 을
              제공합니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제4조 (환불)">
        <LegalSubsection title="4.1 청약철회">
          <LegalOrderedList
            items={[
              <>
                회원은 <strong>포인트 충전일로부터 7일 이내</strong>, 충전된
                포인트 중 <strong>사용하지 않은 부분</strong> 에 대해
                청약철회를 신청할 수 있습니다. (전자상거래법 제17조)
              </>,
              <>
                아이템 구매의 경우, 구매 후 즉시 이용 가능한 디지털 콘텐츠의
                특성상,{" "}
                <strong>
                  사용을 시작한 아이템은 청약철회가 제한
                </strong>{" "}
                될 수 있습니다. (전자상거래법 제17조 제2항 제5호) 단, 회사는
                구매일로부터 7일 이내 미사용 아이템에 대해서는 환불을
                진행합니다.
              </>,
            ]}
          />
        </LegalSubsection>
        <LegalSubsection title="4.2 환불 방식">
          <LegalOrderedList
            items={[
              <>환불은 회원이 결제한 수단으로 동일하게 진행함을 원칙으로 합니다.</>,
              <>
                카드 결제의 경우 결제 취소로 진행되며, 카드사 정책에 따라
                영업일 기준 3~7일 소요될 수 있습니다.
              </>,
            ]}
          />
        </LegalSubsection>
        <LegalSubsection title="4.3 회사 귀책 환불">
          <p>
            회사의 귀책 사유로 서비스를 이용할 수 없게 된 경우, 회사는{" "}
            <strong>미사용 포인트 전액 및 미사용 아이템의 잔여 가치</strong> 를
            환불합니다. 잔여 가치 산정 방식은 제5조 (서비스 종료) 규정을
            따릅니다.
          </p>
        </LegalSubsection>
      </LegalSection>

      <LegalSection title="제5조 (서비스 종료 시 처리)">
        <LegalOrderedList
          items={[
            <>
              회사가 본 서비스의 전부 또는 일부를 종료하고자 하는 경우,{" "}
              <strong>종료일로부터 최소 30일 이전</strong> 에 서비스 내 공지,
              회원 가입 시 등록한 전자우편 등의 방법으로 회원에게 통지합니다.
            </>,
            <>
              <span>
                서비스 종료 시 회사는 다음 각 호의 잔여 가치를 회원에게
                환불합니다.
              </span>
              <LegalUnorderedList
                items={[
                  <>
                    <strong>미사용 포인트</strong>: 충전 금액 전액
                  </>,
                  <>
                    <strong>사용 시작한 아이템</strong>: 종료 통지일 기준 잔여
                    유효기간에 비례한 가치. 단, 자동 갱신으로 인해 유효기간이
                    무기한에 가까운 경우, 회사와 회원이 합리적으로 협의한
                    기준에 따라 산정합니다.
                  </>,
                ]}
              />
            </>,
            <>
              환불 신청은 종료 공지일로부터{" "}
              <strong>최소 6개월간</strong> 접수 받습니다.
            </>,
            <>
              환불은 회원이 결제한 수단으로 진행함을 원칙으로 하되, 카드 결제
              취소가 기술적으로 불가능한 경우 (카드 유효기간 만료 등) 에는
              회원이 지정한 계좌로 송금하는 방식으로 환불합니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="제6조 (미성년자 결제)">
        <LegalOrderedList
          items={[
            <>
              미성년자가 법정대리인의 동의 없이 결제한 경우, 회원 본인 또는
              법정대리인은 결제를 취소할 수 있습니다. (민법 제5조)
            </>,
            <>
              회사는 결제 전 미성년자 여부 확인 및 법정대리인 동의 절차를
              마련합니다.
            </>,
          ]}
        />
      </LegalSection>

      <LegalSection title="부칙">
        <p>
          본 약관은 <strong>2026년 5월 14일</strong> 부터 시행합니다. 본 약관의
          개정이 있을 경우, 회사는 시행 7일 전 (회원에게 불리한 변경의 경우
          30일 전) 까지 공지하며, 회원이 변경된 약관에 동의하지 않을 경우
          서비스 이용을 중단할 수 있습니다.
        </p>
      </LegalSection>
    </LegalPage>
  );
}
