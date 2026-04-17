# Metal-Swift 메모리 정렬

## 타입별 크기/정렬

| Metal Type | Size | Align | Swift Type | Size | Stride | Align |
|-----------|------|-------|-----------|------|--------|-------|
| float | 4 | 4 | Float | 4 | 4 | 4 |
| float2 | 8 | 8 | SIMD2<Float> | 8 | 8 | 8 |
| float3 | **16** | **16** | SIMD3<Float> | 12 | **16** | **16** |
| packed_float3 | **12** | **4** | - | - | - | - |
| float4 | 16 | 16 | SIMD4<Float> | 16 | 16 | 16 |
| float4x4 | 64 | 16 | float4x4 | 64 | 64 | 16 |
| int | 4 | 4 | Int32 | 4 | 4 | 4 |

## SIMD3 함정

SIMD3<Float>는 size=12이지만 alignment=16. 구조체 안에서 다음 필드는 16바이트 경계에서 시작.

### 나쁜 예 (Metal float3 사용)
```metal
struct Bad {
    float3 position;  // offset 0, size 16 (Metal float3는 16바이트!)
    float value;      // offset 16
};
// Total: 20 → padded to 32
```
```swift
struct Bad {
    var position: SIMD3<Float>  // offset 0, size 12
    var value: Float            // offset 12 (alignment 4이므로 12에 들어감!)
}
// Total: 16. Metal과 불일치!
```

### 좋은 예 (packed_float3 + 명시적 패딩)
```metal
struct Good {
    packed_float3 position;  // offset 0, size 12, align 4
    float _pad0;             // offset 12
    packed_float3 color;     // offset 16
    float intensity;         // offset 28
};
// Total: 32
```
```swift
struct Good {
    var position: SIMD3<Float>  // offset 0, size 12
    var _pad0: Float = 0        // offset 12
    var color: SIMD3<Float>     // offset 16 (alignment 16, 16은 정렬됨)
    var intensity: Float        // offset 28
}
// Total: 32. Metal과 일치!
```

## Vertex 구조체와 VertexDescriptor

Vertex attribute는 [[stage_in]]으로 읽히므로 VertexDescriptor의 offset이 실제 구조체 offset과 일치해야 함.

```swift
struct Vertex {
    var position: SIMD3<Float>  // offset 0
    var normal: SIMD3<Float>    // offset 16
    var uv: SIMD2<Float>        // offset 32
    var tangent: SIMD3<Float>   // offset **48** (NOT 40! alignment 16 패딩)
}
// stride = 64

// 안전한 offset 계산:
vertexDescriptor.attributes[3].offset = MemoryLayout<Vertex>.offset(of: \Vertex.tangent)!
// 또는 하드코딩: 48
```

## 검증 방법

```swift
print("Vertex stride:", MemoryLayout<Vertex>.stride)  // 64
print("tangent offset:", MemoryLayout<Vertex>.offset(of: \Vertex.tangent)!)  // 48
print("Uniforms stride:", MemoryLayout<Uniforms>.stride)  // 272
```
