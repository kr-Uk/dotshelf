# Metal 디버깅 가이드

## "파란/검은 화면" 체크리스트

### 1단계: MTKView가 렌더링하고 있는가?
- **clear color가 보이는가?** 보이면 → 지오메트리/셰이더 문제. 안 보이면 → 뷰 설정 문제.
- **Renderer가 nil이 아닌가?** `MTKView.delegate`가 weak이므로 strong ref 필요.
- **drawable이 nil인가?** 뷰 크기가 0이면 drawable 생성 안 됨.

### 2단계: SwiftUI 레이아웃 문제
- **MTKView가 윈도우를 채우는가?** `.frame(minWidth:)`는 최소 크기만 지정.
  ```swift
  // 이것만으로는 부족:
  MetalView().frame(minWidth: 800, minHeight: 600)
  
  // 이렇게 해야 전체 채움:
  MetalView().ignoresSafeArea()
  // 또는
  GeometryReader { geo in MetalView() }
  ```
- **윈도우 크기 지정**:
  ```swift
  WindowGroup { ContentView() }
      .defaultSize(width: 900, height: 700)
  ```

### 3단계: 파이프라인 문제
- **pixel format 일치 확인**: MTKView의 colorPixelFormat과 pipeline descriptor의 colorAttachment format 반드시 일치.
- **셰이더 함수 존재 확인**: `makeFunction(name:)`이 nil 반환하면 파이프라인 생성 실패.

### 4단계: 지오메트리가 보이는가?
- **단색 프래그먼트 셰이더로 테스트**:
  ```metal
  fragment float4 fragment_debug(VertexOut in [[stage_in]]) {
      return float4(1.0, 0.0, 0.0, 1.0);  // 빨간색
  }
  ```
  빨간 형상이 보이면 → 지오메트리/투영 OK, 라이팅 문제.
  아무것도 안 보이면 → 투영/버텍스 문제.

- **투영 행렬 검증**: aspect가 0이나 inf가 아닌지 확인.
- **카메라 위치 검증**: 씬이 절두체 안에 있는지 확인.

### 5단계: 라이팅 문제
- **앰비언트만 출력해보기**: `return float4(ambient, 1.0);`
- **노멀 시각화**: `return float4(N * 0.5 + 0.5, 1.0);`
- **그림자값 시각화**: `return float4(shadow, shadow, shadow, 1.0);`

## Metal Validation 활성화

### Xcode
Edit Scheme → Run → Diagnostics → Metal API Validation ✓

### 환경 변수
```bash
METAL_DEVICE_WRAPPER_TYPE=1  # API Validation
MTL_SHADER_VALIDATION=1      # Shader Validation (느림)
MTL_DEBUG_LAYER=1             # Debug Layer
```

## GPU Frame Capture

1. Edit Scheme → Run → Options → GPU Frame Capture = Metal
2. 실행 중 Xcode 디버그바 카메라 아이콘 클릭
3. 모든 draw call, 바인딩된 버퍼/텍스처, 셰이더 변수값 검사 가능
4. 픽셀 클릭 → 해당 픽셀의 프래그먼트 셰이더 단계별 디버깅
