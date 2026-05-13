# Fantasy / Space / Spooky 테마팩 구현

- **브랜치**: `feat/fe/theme`
- **범위**: `origin/dev..HEAD` (커밋 1개 기준 작성)
- **기간**: 2026-05-13
- **관련 커밋**: `f357a486` `[UNITY] Feat: fantasy space spooky theme packs`

## TL;DR

Fantasy, Space, Spooky 3개 Unity 룸 테마를 DeepSea 테마와 같은 구조로 추가했다.
각 테마는 `RoomThemeAsset`, material 세트, Resources catalog entry, GLB 모델, 벽지/바닥 텍스처를 함께 가진다.
기존 벽지만 바뀌던 구조는 천장까지 같은 wallpaper material을 적용하도록 확장했다.
Play Mode에서 배치한 테마 소품을 `RoomTheme_*.asset`으로 다시 저장할 수 있도록 Editor capture 메뉴도 확장했다.

## 무엇을 했나

### 1. 신규 테마 3종 추가

- 사용자 관점 효과: 꾸미기 화면에서 Fantasy, Space, Spooky 테마를 선택하면 해당 분위기의 방 구성과 소품 배치가 적용된다.
- 시스템 관점 효과: DeepSea와 동일하게 `RoomThemeAsset` + catalog entry + Unity asset bundle 후보 파일 구조로 관리된다.
- 주요 파일:
  - `frontend/Assets/App/Composition/Configs/RoomThemes/RoomTheme_Fantasy.asset`
  - `frontend/Assets/App/Composition/Configs/RoomThemes/RoomTheme_Space.asset`
  - `frontend/Assets/App/Composition/Configs/RoomThemes/RoomTheme_Spooky.asset`
  - `frontend/Assets/Resources/RoomThemeCatalog.asset`

### 2. 테마별 material / texture / model 자산 정리

- Fantasy, Space, Spooky 각각 `Wall`, `Floor`, `Bookshelf`, `Furniture`, `Accent`, `Dashboard` material을 분리했다.
- GLB 모델은 `frontend/Assets/Arts/Models` 아래에 배치하고 Unity `.meta`까지 함께 커밋했다.
- 벽지와 바닥 이미지는 `frontend/Assets/Arts/Textures/Environment/Wallpaper`, `Floor` 아래로 분리했다.
- GLB/PNG는 `.gitattributes`의 LFS 패턴을 따라 pointer로 커밋된다.

### 3. 벽지 적용 범위를 천장까지 확장

- 사용자 관점 효과: DeepSea, Fantasy, Space, Spooky 모두 벽지 material이 벽뿐 아니라 천장에도 적용된다.
- 시스템 관점 효과: `RoomGameObjectPatterns`에 ceiling 판별을 추가하고, `RoomThemeApplier`와 `RoomEditModeController`에서 wallpaper surface를 벽 + 천장으로 처리한다.
- 주요 파일:
  - `frontend/Assets/Scripts/RoomTheme/RoomGameObjectPatterns.cs`
  - `frontend/Assets/Scripts/RoomTheme/RoomThemeApplier.cs`
  - `frontend/Assets/Scripts/RoomTheme/WallTilingNormalizer.cs`
  - `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`

### 4. 테마 배치 캡처 Editor utility 확장

- DeepSea 전용이던 capture 흐름을 Fantasy, Space, Spooky에도 사용할 수 있게 메뉴를 추가했다.
- prefab 참조가 깨진 경우 `assetKey`로 다시 로드해서 scene object 이름을 계산하도록 보강했다.
- Spooky에서 `spooky_character` prefab 참조 fileID가 깨졌을 때 반복 경고가 뜨던 문제를 이 흐름으로 방어했다.

## 왜 했나

DeepSea 테마를 만든 뒤 같은 패턴으로 추가 테마를 확장해야 했다.
단순히 모델 파일만 넣으면 사용자가 테마를 선택했을 때 방 전체가 의도한 모습으로 재현되지 않는다.
테마는 material, 기본 catalog entry, floor/wall decoration placement, editor capture workflow가 함께 맞아야 실제로 선택 가능한 완성형 프리셋이 된다.

또 Play Mode에서 소품 위치를 눈으로 맞춘 뒤 asset으로 저장하는 작업 흐름이 반복되었기 때문에, 테마별 capture 메뉴가 필요했다.
이 메뉴가 없으면 scene에서 조정한 값이 Play 종료 시 날아가고, YAML을 직접 수정해야 해서 실수 가능성이 커진다.

## 어떻게 구현했나

### RoomThemeAsset 중심의 프리셋 구조

각 테마는 `RoomThemeAsset`에 기본 material, accent color, default entry, floor/wall decoration spec을 가진다.
테마 선택 시 runtime은 이 asset을 기준으로 방 material을 바꾸고, catalog asset id를 통해 책상/소품을 적용한다.

핵심 포인트:
- 테마 루트: `frontend/Assets/App/Composition/Configs/RoomThemes`
- material 루트: `frontend/Assets/App/Composition/Materials/Themes/{ThemeName}`
- 사용자 선택 목록: `frontend/Assets/Resources/RoomThemeCatalog.asset`
- 소품 목록: `frontend/Assets/Resources/EditableDecorationCatalog.asset`
- 책상/벽지 catalog: `DeskCatalog.asset`, `WallpaperCatalog.asset`

### 벽지와 천장 적용의 책임 분리

천장을 단순히 별도 material slot으로 두지 않고, wallpaper surface의 일부로 취급했다.
그래서 테마가 `wallMaterial` 하나만 제공해도 벽과 천장이 같은 룩으로 맞춰진다.

```csharp
public static bool IsWallpaperSurfaceName(string name)
{
    return IsWallName(name) || IsCeilingName(name);
}
```

이 방식은 테마마다 `ceilingMaterial` 필드를 새로 추가하지 않아도 되며, 기존 DeepSea/GoodVibes 계열의 wall material 흐름을 유지한다.

### Capture utility의 assetKey fallback

Unity YAML에서 prefab fileID가 잘못 들어가면 `objectReferenceValue`가 null이 된다.
Spooky capture 경고는 이 케이스였고, `assetKey`에 실제 모델 경로가 남아 있었기 때문에 복구 경로를 둘 수 있었다.

```csharp
var assetKey = spec.FindPropertyRelative("assetKey")?.stringValue;
prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetKey);
if (prefab != null)
{
    SetObject(prefabProp, prefab);
    return prefab.name;
}
```

핵심은 scene object 이름을 prefab reference 하나에만 의존하지 않는 것이다.
이렇게 하면 잘못된 prefab 참조가 들어가도 다음 capture 시 asset이 스스로 정상 참조로 회복될 수 있다.

## 배운 것

- **Unity 테마는 asset 파일 하나가 아니라 catalog 묶음이다**: `RoomThemeAsset`만 만들어도 선택 목록, 책상 skin, 벽지, decoration catalog가 맞지 않으면 실제 테마 적용이 완성되지 않는다.
- **Play Mode 배치는 저장되지 않는다**: scene에서 조정한 runtime transform을 ScriptableObject에 다시 써주는 Editor utility가 있어야 디자이너 조정값을 유지할 수 있다.
- **Unity prefab 참조는 guid만으로 충분하지 않다**: 같은 guid라도 fileID가 틀리면 Unity가 null reference로 읽을 수 있다. 경로 기반 fallback을 둔 이유다.
- **천장 적용은 domain naming이 중요하다**: `Wall`과 `Ceiling`을 별도 surface로만 보면 중복 로직이 생기고, `WallpaperSurface`로 묶으면 사용자 기대에 맞게 동작한다.
- **LFS asset은 diff stat이 작게 보인다**: GLB/PNG는 실제 파일 크기가 커도 git diff에는 pointer 3줄로 보인다. push 전 `git lfs status`로 LFS 추적 여부를 확인해야 한다.

## 더 공부할 것

- [ ] Unity `ScriptableObject`를 runtime 저장소처럼 사용할 때 undo/backup 정책은 어떻게 잡는 게 좋은가?
- [ ] `Resources` catalog가 늘어날 때 Addressables/S3 기반 catalog로 전환하는 경계는 어디가 적절한가?
- [ ] Unity GLB import의 fileID가 모델마다 달라지는 기준은 무엇이고, YAML 수동 편집 시 어떤 참조가 안전한가?
- [ ] 테마별 wall/floor tiling 값을 material에 둘지, 별도 theme config 필드로 분리할지 기준을 정리한다.

## 검증

- `dotnet build frontend/Assembly-CSharp-Editor.csproj --no-restore` PASS
- `git diff --cached --check` PASS
- `git lfs status`로 GLB/PNG LFS staging 확인

## 참고

- 관련 커밋: `f357a486`
- 비교 기준: DeepSea 테마 구현 패턴
