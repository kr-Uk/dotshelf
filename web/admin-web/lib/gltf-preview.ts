// 본체 .glb 를 받아 wasm meshoptimizer 로 저폴리화한 preview.glb 를 만든다.
// gltfpack CLI 와 같은 알고리즘이지만 브라우저에서 돌아가서 BE 서버에 부담이 없다.
// 처리 시간은 운영자 PC 에서 1MB 본체 기준 1~3초 정도, 결과는 보통 30%대 크기.

import { WebIO } from "@gltf-transform/core";
import { dedup, simplify, weld } from "@gltf-transform/functions";
import { MeshoptSimplifier } from "meshoptimizer";

export interface GeneratePreviewOptions {
  /** 보존할 폴리곤 비율 (0~1). 0.3 = 30% 만 남김. */
  ratio?: number;
  /** 단순화 허용 오차 (메시 bounding box 대비). 클수록 더 거칠어짐. */
  error?: number;
}

export async function generatePreviewGlb(
  input: Uint8Array,
  options: GeneratePreviewOptions = {},
): Promise<Uint8Array> {
  const ratio = options.ratio ?? 0.3;
  const error = options.error ?? 0.01;

  await MeshoptSimplifier.ready;
  const io = new WebIO();
  const doc = await io.readBinary(input);
  await doc.transform(
    dedup(),
    weld(),
    simplify({ simplifier: MeshoptSimplifier, ratio, error }),
  );
  return io.writeBinary(doc);
}
