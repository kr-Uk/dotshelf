# 2026-05-04 feat/be/payment

## 한 줄 요약

토스페이먼츠 기준으로 만들었던 결제 흐름을 제거하고, KG이니시스 웹표준결제 흐름에 맞춰 페이지 충전 주문 생성, 이니시스 승인 콜백 처리, 지갑 충전 반영, 결제 상태 조회 API를 구현했다.

## 왜 바꿨나

초기 결제 구현은 토스페이먼츠의 `paymentKey` 승인 모델을 전제로 했다. 하지만 실제 도입 방향이 KG이니시스로 바뀌면서 결제 구조도 달라져야 했다.

KG이니시스 웹표준결제는 서버가 결제 주문과 서명 값을 만들고, 클라이언트 또는 서버 렌더링 HTML이 이니시스 결제창을 호출한다. 결제가 끝나면 이니시스가 `returnUrl`로 인증 결과를 보내고, 서버는 전달받은 `authUrl`에 다시 승인 요청을 보내 최종 승인 결과를 확인해야 한다.

그래서 단순히 provider 이름만 바꾸는 것이 아니라, 주문 상태, 인증 결과, 승인 실패, DB 반영 실패 시 망취소까지 포함하는 별도 결제 주문 흐름이 필요했다.

## 무엇을 바꿨나

### 1. 결제 주문 저장 구조 추가

`payment_orders` 테이블을 추가해서 외부 결제 한 건의 상태를 추적한다.

- `order_id`: 이니시스에 전달하는 주문 번호
- `provider`: 현재는 `INICIS`
- `status`: `READY`, `AUTHENTICATED`, `APPROVED`, `FAILED`
- `amount_krw`, `pages_amount`: 금액과 충전 페이지 수
- `provider_transaction_id`: 이니시스 `tid`
- `auth_token`, `auth_url`, `net_cancel_url`: 이니시스 인증 후 승인과 망취소에 필요한 값
- `failure_code`, `failure_message`: 실패 원인 기록

`page_transactions` 테이블도 추가해서 실제 지갑 잔액을 변경한 이력을 남긴다. 결제가 승인되어 페이지가 충전되면 `INICIS_PAYMENT` 타입의 거래가 저장된다.

### 2. KG이니시스 전용 설정과 서명 생성

`InicisPaymentProperties`, `InicisSignatureGenerator`, `InicisPaymentFormFactory`를 추가했다.

주문 생성 시 서버는 다음 값을 만든다.

- `mid`: KG이니시스 상점 ID
- `oid`: DotShelf 주문 번호
- `price`: 결제 금액
- `timestamp`: 서명 기준 시각
- `signature`: 결제창 요청 검증용 서명
- `verification`: 서버 검증용 서명
- `mKey`: signKey 해시
- `returnUrl`, `closeUrl`: 이니시스 콜백 URL

서명과 form 값을 서버에서 만들면 클라이언트는 결제창 호출에 필요한 값을 그대로 받아 사용할 수 있다. Unity에서 바로 쓰기 어렵다면 서버가 제공하는 HTML 엔드포인트를 브라우저로 열어도 된다.

### 3. 결제 API 흐름 변경

새로 쓰는 API 흐름은 다음과 같다.

1. `POST /v1/payments/page-charge/orders`
   - 사용자가 충전할 페이지 수와 결제 금액을 보내면 결제 주문을 만든다.
   - 응답으로 `paymentPageUrl`과 `inicisForm`을 내려준다.

2. `GET /v1/payments/inicis/request/{orderId}`
   - 서버가 이니시스 결제창 호출용 HTML을 렌더링한다.
   - Unity Desktop은 이 URL을 외부 브라우저로 열 수 있다.

3. `POST /v1/payments/inicis/return`
   - 이니시스 인증 결과를 form-urlencoded로 받는다.
   - 서버가 `authUrl`로 승인 요청을 보내 최종 승인 결과를 확인한다.
   - 승인 성공이면 지갑 페이지를 충전하고 거래 이력을 저장한다.

4. `GET /v1/payments/orders/{orderId}`
   - 클라이언트가 결제 완료 여부를 polling할 수 있다.

기존 직접 충전 API `POST /v1/payments/page-charge`는 더 이상 결제 승인 없이 지갑을 올리지 않도록 막았다.

### 4. 승인 후 DB 반영과 중복 처리

결제 승인 이후 지갑 반영은 `PaymentOrderCompletionService`가 담당한다.

- 이미 `APPROVED`인 주문이면 같은 결과를 반환해서 중복 콜백을 안전하게 처리한다.
- 주문 금액과 이니시스 승인 금액이 다르면 실패 처리한다.
- 지갑 row는 비관적 락으로 조회해서 동시 충전 시 잔액 경합을 줄인다.
- 승인 후 DB 반영이 실패하면 `netCancelUrl`로 망취소를 시도한다.

결제는 외부 PG와 내부 DB가 함께 움직이므로, "외부 승인 성공인데 내부 반영 실패" 케이스를 반드시 고려해야 한다.

### 5. 보안 가드

이니시스가 돌려주는 `authUrl`, `netCancelUrl`은 서버가 다시 호출하는 URL이다. 외부 입력 URL을 그대로 호출하면 SSRF 위험이 있으므로, 허용 host를 제한했다.

현재 허용 대상은 이니시스 표준결제 도메인 계열이다.

- `stgstdpay.inicis.com`
- `fcstdpay.inicis.com`
- `ksstdpay.inicis.com`

또한 결제 주문의 소유자와 현재 사용자를 확인해서 다른 사용자의 주문 상태를 조회하지 못하게 했다.

## 테스트한 내용

단위 테스트를 추가해 핵심 정책을 검증했다.

- 주문 생성 시 금액과 페이지 수가 맞으면 `READY` 주문이 생성된다.
- 금액이 정책과 다르면 결제 주문 생성이 실패한다.
- 기존 직접 충전 API는 비활성화된다.
- 이니시스 승인 성공 시 지갑 잔액과 거래 이력이 저장된다.
- 같은 주문 완료 처리가 다시 들어와도 중복 충전되지 않는다.
- 승인 금액이 주문 금액과 다르면 실패 처리된다.

로컬 검증 결과:

- `./gradlew.bat test` 통과
- `./gradlew.bat bootJar` 통과
- Docker 데몬이 실행 중이 아니어서 컨테이너 기반 smoke test는 수행하지 못했다.

## 배운 점

### PG 결제는 "주문 생성"과 "지갑 반영"을 분리해야 한다

결제창을 띄우는 순간 돈이 충전되는 것이 아니다. 내부 주문은 `READY`로 만들고, PG 승인 결과를 확인한 뒤에만 내부 재화를 지급해야 한다.

### returnUrl 콜백은 최종 승인 자체가 아니다

KG이니시스 웹표준결제에서는 `returnUrl`로 받은 `authToken`과 `authUrl`을 이용해 서버가 다시 승인 요청을 보내야 한다. 콜백을 받았다는 사실만으로 충전하면 안 된다.

### 멱등성이 중요하다

PG 콜백은 사용자의 새로고침, 네트워크 재시도, PG 재전송으로 중복 호출될 수 있다. 이미 승인된 주문은 다시 지갑을 올리지 않고 기존 승인 결과를 반환해야 한다.

### 망취소는 실패 복구의 마지막 방어선이다

외부 결제 승인은 성공했지만 내부 DB 반영이 실패하면 사용자 돈은 빠져나가고 페이지는 충전되지 않는 상태가 된다. 이 경우 `netCancelUrl`로 취소를 시도해야 운영 사고 범위를 줄일 수 있다.

## 앞으로 확인할 것

- 실제 KG이니시스 MID와 signKey가 dev/prod 환경변수에 정확히 들어갔는지 확인
- `returnUrl`, `closeUrl`이 이니시스 콘솔과 서버 배포 도메인에 맞는지 확인
- Unity Desktop에서 `paymentPageUrl`을 열고 `GET /v1/payments/orders/{orderId}` polling으로 완료 상태를 받는 흐름 연결
- 결제 취소와 환불 API는 별도 도메인 정책을 정한 뒤 추가
- 운영에서 결제 금액 정책이 바뀔 경우 `pagesAmount`와 `amountKrw` 검증 정책을 설정화할지 검토
