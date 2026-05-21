---
name: unity-on-device-ai
description: Use this skill whenever the dotshelf user wants to add an AI/ML feature that runs on the user's device — book recommendations from a local model, "books similar to this one", text classification (book genre/sentiment from a review), image classification (cover style match), simple TTS/STT, or any feature where you'd otherwise call a cloud LLM but could run a small model locally for latency, offline, or privacy. Trigger when the user says "추천", "비슷한 책 찾기", "리뷰 감정 분석", "오프라인에서도", "ONNX", "Sentis", "Inference Engine", "단말 추론", or asks "should this be a backend API or run on the phone". The skill enforces the Unity Inference Engine package (com.unity.ai.inference, formerly Sentis) workflow — ONNX import, worker scheduling, quantization for mobile, backend selection (CPU vs GPUCompute), and clean integration with the dotshelf clean-architecture (model lives in Infrastructure, UseCase calls inference, Domain doesn't know).
---

# unity-on-device-ai

dotshelf에 *단말에서 돌아가는 AI 기능*을 추가할 때 따르는 스킬. Unity의 **Inference Engine** (구 Sentis, 패키지 `com.unity.ai.inference`) 으로 ONNX 모델을 가져와 모바일·데스크톱에서 직접 추론한다. 클라우드 호출 대신 **저지연, 오프라인 가능, 프라이버시 보존** 을 얻는다.

## 왜 단말 추론을 고려하는가

- **책 추천 / 유사 책** 같은 기능에 클라우드 LLM을 매번 부르면 비용·지연·오프라인 실패. 작은 임베딩 모델(MiniLM 등)로 단말에서 코사인 유사도 계산하면 즉답.
- **사용자 독후감 감정 분석** 같은 텍스트 처리는 사용자 데이터를 서버로 보내지 않는 게 더 깔끔 (개인정보).
- **이미지 분류** (책 표지 카테고리 자동 태깅) 도 가벼운 ResNet/MobileNet 양자화 모델로 가능.
- 단점: 모델 사이즈가 빌드에 들어감 (수십 MB), 첫 로드 비용, 일부 기기에서 GPU 호환 이슈, NPU 미활용.

매뉴얼 인용:
> *"Inference Engine is a neural network inference library for Unity. It lets you import trained neural network models into Unity and run them in real-time with your target device's compute resources, such as central processing unit (CPU) or graphics processing unit (GPU)."*

> *"Currently, Inference Engine doesn't use NPU or hardware acceleration to run the inferences."*

→ NPU 미사용은 중요한 한계. iPhone Neural Engine, Snapdragon Hexagon 같은 가속기는 *현재* 활용 안 됨. 모델은 CPU 또는 GPU shader로 실행.

## 언제 발동하나

- "추천", "비슷한", "유사도", "분류", "감정", "태깅" 같은 단어가 나올 때
- "백엔드 API 만들까 / 단말에서 처리할까" 결정 순간
- ONNX / Sentis / Inference Engine 키워드가 직접 등장
- 외부 LLM API 호출이 들어가려는데 단순 분류·임베딩으로 대체 가능한 경우

먼저 **단말 vs 클라우드 결정 트리**를 돌리고, 단말이 맞으면 통합 절차로 진행.

## 단말 vs 클라우드 결정 트리

| 질문 | 단말 (Inference Engine) | 클라우드 |
|---|---|---|
| 모델 크기가 50MB 이하인가? | ✅ | ❌ |
| 응답 지연이 100ms 이내여야 하는가? | ✅ | ❌ |
| 오프라인에서도 동작해야 하는가? | ✅ | ❌ |
| 사용자 데이터 (독후감, 메시지) 를 서버에 보내고 싶지 않은가? | ✅ | ❌ |
| 작업이 단순 분류/임베딩/유사도/회귀인가? | ✅ | ❌ |
| 정밀도가 SOTA LLM 수준이어야 하는가? | ❌ | ✅ |
| 모델을 자주 갱신·실험해야 하는가? | ❌ (재빌드 필요) | ✅ |
| 추론 비용이 빌드 사이즈/메모리 예산에 맞는가? | ✅ | (해당 없음) |

→ 추천 / 유사 책 / 감정 분석 / 카테고리 태깅 → **단말**. 자유 형식 응답 / RAG 기반 답변 / 새 모델 자주 교체 → **클라우드**.

## Inference Engine 워크플로 (Unity 매뉴얼 인용)

### 1. 패키지 설치

```
Window → Package Manager → Add package by name: com.unity.ai.inference
```

(이전 이름 `com.unity.sentis`는 단종 → `com.unity.ai.inference`로 통합. 6.x 버전 2.0+).

### 2. ONNX 모델 임포트

매뉴얼:
> *"Inference Engine supports most models in Open Neural Network Exchange (ONNX) format with an opset version between 7 and 15."*

- `.onnx` 파일을 `Assets/AI/Models/` 같은 곳에 드롭
- 자동으로 `ModelAsset` 으로 임포트됨
- Hugging Face / ONNX Model Zoo / 자체 학습한 PyTorch → ONNX export

### 3. 모델 로드 + Worker 생성

```csharp
using Unity.InferenceEngine;
using UnityEngine;

[SerializeField] ModelAsset _modelAsset;
Worker _worker;

void Awake()
{
    var runtime = ModelLoader.Load(_modelAsset);
    _worker = new Worker(runtime, BackendType.GPUCompute);
}

void OnDestroy()
{
    _worker?.Dispose();
}
```

매뉴얼 인용:
> *"a worker is the inference engine. You create a worker to break down the model into executable tasks, run the tasks on the GPU or CPU, and retrieve the result."*

### 4. Backend 선택

| Backend | 권장 상황 | 비고 |
|---|---|---|
| `BackendType.CPU` | 작은 모델, 텍스트 임베딩, GPU 사용 중 | 안정성 ↑, 호환성 ↑ |
| `BackendType.GPUCompute` | 큰 모델, 행렬 연산 무거움, 그래픽과 같은 GPU 자원 사용 | 매뉴얼: *"keep all the processing on the GPU by selecting the GPUCompute backend"* |
| `BackendType.GPUPixel` | GPUCompute 미지원 기기 fallback | 일부 모바일 구형 |

→ 모바일 책 추천 (작은 임베딩 모델) 은 **CPU 권장**. 게임 그래픽이 GPU 고정 사용 중이라 추론까지 GPU로 보내면 frame budget 깎임.

### 5. 추론 실행

```csharp
public async UniTask<float[]> Embed(string text)
{
    using var input = TokenizeToTensor(text);  // Tensor<int> or Tensor<float>
    _worker.Schedule(input);
    using var output = _worker.PeekOutput() as Tensor<float>;
    return await output.ReadbackAndCloneAsync();  // GPU → CPU 비동기 readback
}
```

매뉴얼 인용:
> *"Sentis schedules the model layers on the given backend. Execution is asynchronous, so after this is called, tensor operations may still be pending."*

→ `Schedule`은 비동기. 즉시 결과 안 나옴. **GPU readback** 은 추가 지연 — 게임 frame을 막지 않으려면 `await` 또는 코루틴.

### 6. 양자화 (모바일 필수)

매뉴얼:
> *"Quantization compresses your model by storing weights in lower precision formats (Float16 or Uint8), reducing file size and memory usage with minimal accuracy loss."*

| 양자화 | 파일 크기 | 정확도 | 권장 |
|---|---|---|---|
| Float32 (원본) | 100% | 100% | 데스크톱 / 정확도 critical |
| Float16 | 50% | ~99% | 모바일 기본 권장 |
| Uint8 | 25% | ~95% | 매우 작은 모델, 일부 모델 정확도 큰 손실 가능 |

ONNX 임포트 시 양자화 옵션 또는 Inference Engine API로 후처리.

## 이 프로젝트와의 연결

### 클린 아키텍처 적용

```
Domain        — Book, Review (POCO, AI 모름)
              ↓
Application   — IBookRecommender 인터페이스 (UseCase가 의존)
                RecommendSimilarBooksUseCase
              ↓
Infrastructure — OnDeviceBookRecommender : IBookRecommender
                  (Inference Engine Worker 보유, 임베딩 계산)
              ↓
Features      — 추천 결과를 UI에 표시 (DataBinding으로 ViewModel에)
              + Composition Root에서 OnDeviceBookRecommender 등록
```

→ Domain/Application은 Inference Engine 의존 0. Infrastructure에서만 패키지 사용. `unity-layer-check`가 가드.

### 권장 첫 모델 (작은 시작)

**책 추천 — 텍스트 임베딩**:
- 모델: `sentence-transformers/all-MiniLM-L6-v2` (~22 MB Float32, ~11 MB Float16)
- 입력: 책 제목 + 짧은 설명 → 384차원 벡터
- 사용처: 책 N개의 임베딩을 미리 계산해 두고, 사용자가 본 책의 임베딩과 코사인 유사도 → top K
- 단말 추론 비용: 모바일 mid 등급 ~50ms / 책

**책 표지 카테고리 분류**:
- 모델: MobileNetV3 small ImageNet (양자화 시 ~5 MB)
- 입력: 224×224 RGB 표지 → 1000 클래스 → 카테고리 매핑
- 비용: 모바일 mid 등급 ~30ms

**감정 분석 (선택)**:
- 모델: distilBERT 한국어 finetuned (양자화 ~30 MB)
- 입력: 독후감 텍스트 → positive/negative/neutral
- 비용: 텍스트 길이에 비례, ~100~300ms

### 빌드/메모리 예산 영향

`unity-perf-budget`과 협업:
- 모델 파일은 빌드 사이즈에 직접 추가됨 — Float16 임베딩 모델 ~11MB, 표지 분류 ~5MB 합쳐도 작음
- 런타임 메모리 — Worker 인스턴스가 모델 가중치를 메모리에 보관. Float16 ~11MB
- GPU backend 선택 시 frame budget — 추론 중 다른 그래픽 작업과 경쟁

### 첫 모델 자산 위치

```
Assets/AI/
├── Models/
│   ├── book-embed-minilm-fp16.onnx   (또는 .sentis 변환 후)
│   └── cover-cat-mnv3-int8.onnx
└── Tokenizers/
    └── minilm-vocab.txt
```

**Resources/ 에는 두지 않는다** — 시작 시 인덱싱 비용. `Assets/AI/` 직접 참조 또는 Addressables.

## Tensor / Texture 변환 패턴

이미지 분류:
```csharp
public async UniTask<int> ClassifyCover(Texture2D texture)
{
    using var input = TextureConverter.ToTensor(texture, 224, 224, 3);
    _worker.Schedule(input);
    using var logits = _worker.PeekOutput() as Tensor<float>;
    var arr = await logits.ReadbackAndCloneAsync();
    return ArgMax(arr);
}
```

텍스트 (직접 토크나이저 필요):
```csharp
public async UniTask<float[]> EmbedText(string text)
{
    var ids = _tokenizer.Encode(text);  // int[] (예: [101, 7592, ..., 102])
    using var input = new Tensor<int>(new TensorShape(1, ids.Length), ids);
    _worker.Schedule(input);
    using var output = _worker.PeekOutput() as Tensor<float>;
    return await output.ReadbackAndCloneAsync();
}
```

→ 한국어 토크나이저는 Hugging Face 모델 카드 + 토크나이저 vocab을 단말에 같이 포함. C# 포팅이 별도 작업이 될 수 있음 — 흔한 함정.

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| ONNX import 실패 | opset 16+ 또는 미지원 operator | matching opset (7~15)으로 export, 또는 `ai.onnx.opset_version` 다운그레이드 |
| 모바일에서 GPU backend로 NRE | 일부 구형 Adreno/Mali에서 compute shader 미지원 | runtime check 후 `BackendType.CPU` fallback |
| 추론 결과가 학습 시점과 다름 | 입력 정규화 / 토큰화 / 이미지 채널 순서 (BGR vs RGB) 불일치 | 모델 카드의 preprocessing 정확히 재현 |
| 첫 추론이 매우 느림 (1초+) | Worker 초기화 + shader compile + JIT | Awake에서 *warmup* 추론 한 번 (더미 입력) |
| 메모리가 빌드 사이즈만큼 커짐 | 양자화 안 함 / Float32 그대로 | Float16 또는 Uint8 양자화 |
| readback 으로 frame stall | 동기 readback 또는 GPU 작업 큐 정체 | `ReadbackAndCloneAsync` (비동기) 사용, 결과 사용 시점까지 기다리지 않게 설계 |
| Worker.Dispose 안 함 → 메모리 누수 | C# struct/IDisposable 패턴 | `OnDestroy`에서 Dispose. `using` block 활용 |
| Tensor를 Dispose 안 함 | 마찬가지 IDisposable | 모든 input/output Tensor에 `using var` |
| Domain에 Inference Engine using이 들어감 | 레이어 위반 | Infrastructure에 격리, `unity-layer-check`로 가드 |
| 모델 갱신 시 빌드 다시 | Resources/Addressables 그룹 분리 안 됨 | Addressables Group을 모델 전용으로 분리, 원격 갱신 가능 (단 모바일 정책 주의) |

## 출력 형식

AI 기능 도입 시 응답 한 블록:

```
## On-device AI integration
- 결정: 단말 추론 (이유: <50ms 응답, 오프라인, 사용자 데이터 비전송)
- 모델: sentence-transformers/all-MiniLM-L6-v2 (Float16, 11MB)
- 위치: Assets/AI/Models/book-embed-fp16.onnx
- 패키지: com.unity.ai.inference (2.x)
- 백엔드: CPU (게임 GPU 자원 보존)
- 클린 아키텍처:
  - Application: IBookRecommender 인터페이스
  - Infrastructure: OnDeviceBookRecommender (Worker 보유)
  - Domain: 변경 없음
- 빌드 예산: +11MB (perf-budget 보고에 반영)
- 양자화: Float16 적용 — Float32 대비 정확도 99% 유지
```

## 협업 — 다른 스킬과의 경계

- **`unity-layer-check`** — Inference Engine 사용을 Infrastructure에 격리 강제
- **`unity-perf-budget`** — 모델 파일 빌드 사이즈 / 런타임 메모리 / 추론 ms 예산 차감 보고
- **`unity-fundamentals/references/asset-pipeline.md`** — 모델 자산 관리 (직접 참조 vs Addressables)
- **`unity-fundamentals/references/build-il2cpp.md`** — Inference Engine은 IL2CPP 호환, 단 토크나이저 라이브러리는 link.xml 필요할 수 있음
- **`unity-ai-handoff`** — 모델 학습/변환은 외부 (Python/Hugging Face), 통합 코드는 이 스킬, 자산 생성 (예: 데이터셋 만들기) 은 Unity AI Generators

## 출처

- Unity Manual / Inference Engine — [Inference Engine overview](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.2/manual/index.html)
- Unity Manual / Inference Engine — [Understand the Sentis workflow](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/understand-sentis-workflow.html)
- Unity Manual / Inference Engine — [Quantize a Model](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.2/manual/quantize-a-model.html)
- Unity Manual / Inference Engine — [How Inference Engine runs a model](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.2/manual/how-inference-engine-runs-a-model.html)
- Unity API Reference — [Unity.InferenceEngine namespace](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.2/api/Unity.InferenceEngine.html)
- Unity Discussions — [Bringing Runtime AI to Unity 6](https://discussions.unity.com/t/bringing-runtime-ai-to-unity-6/1529799)
- Meta for Developers — [Unity Inference Engine (Quest)](https://developers.meta.com/horizon/documentation/unity/unity-ai-unity-inference-engine/)
- Christian Mills — [Getting Started With Deep Learning in Unity](https://christianjmills.com/posts/deep-learning-unity-intro/)
- Hugging Face — [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) (권장 첫 임베딩 모델)
- ONNX — [opset version compatibility](https://onnx.ai/onnx/operators/)
