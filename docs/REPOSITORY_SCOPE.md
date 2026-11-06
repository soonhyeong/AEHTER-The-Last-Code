# 저장소 안내

## 목적

외부 에셋 원본을 공개하지 않고 프로젝트의 게임 로직을 검토할 수 있도록 구성한 코드 열람용 제출본입니다. 원본 Unity 프로젝트에서 파일을 삭제하거나 이동하지 않고 별도로 추출했습니다.

```text
README.md                  게임 소개
NOTICE.md                  공개 범위와 권리 안내
Source/Assets/             원래 경로를 보존한 프로젝트 코드와 해당 .meta
docs/CODE_MAP.md           시스템별 주요 코드 진입점
docs/DEPENDENCIES.md       원본 환경과 필요한 의존성
docs/SOURCE_MANIFEST.json  원본/제출본 경로·해시·인코딩 기록
docs/UPLOAD_GUIDE.md       저장소 생성 및 업로드 안내
```

## 포함한 코드

- `Source/Assets/1. Scripts/`: 현재 게임 구현. 단순 Hello 확인용 스크립트 2개는 제외했습니다.
- `Source/Assets/1. Scripts(upload)/`: 원본 AETHER의 대화·엔딩·호환 API 등 남아 있는 기능 코드입니다. `upload`라는 이름만으로 미사용 백업으로 분류하지 않았습니다.
- `Source/Assets/00. Cinema(upload)/`: 자체 시네마틱/자막/선택/캐릭터 연출 코드입니다. Cinema 폴더의 외부 에셋 원본과 데모는 포함하지 않았습니다.
- `Source/Assets/Scripts/`: 제작·인벤토리·퍼즐 관련 보조 구현입니다. 테스트 아이템 주입용 `TestInventoryInitializer.cs`는 제외했습니다.
- `Source/Assets/Editor/`: 프로젝트 전용 편집기 도구입니다.
- `Source/Assets/11.another aseets/Scropts/`: 주변 외부 에셋과 분리한 프로젝트 로딩/Scene 이동 코드 2개입니다.

번호 `00~12` 폴더를 사용자 작업 범위의 우선 근거로 삼았으나, `DownloadAssets` 등 외부 제품 위치를 통째로 복사하지 않았습니다. 번호 밖의 자체 보조 코드도 검토해 포함했습니다. 이 출처 선별이 모든 코드의 작성자·라이선스를 법적으로 증명하는 것은 아닙니다. 공동 코드의 공개 합의는 제출자가 확인해야 합니다.

## 실행 가능한 Unity 프로젝트가 아닙니다

이 저장소에는 Scene, Prefab, 모델, 텍스처, 음원, 폰트, InputActionAsset, ScriptableObject 데이터, PackageCache, Unity 설정 전체가 없습니다. 해당 데이터와 직렬화 참조는 원본 프로젝트에만 있습니다.

따라서 `.meta`가 함께 있다고 해서 원래 Scene과 Prefab이 복구되거나 모든 스크립트가 곧바로 실행되는 것은 아닙니다. 직접 Unity 프로젝트로 열거나, 외부 타입이 누락된 채 파일을 복사하면 컴파일·참조 오류가 발생할 수 있습니다. `Source/Assets`는 원본 경로를 설명하기 위한 배치입니다.

Editor 도구에는 `InitializeOnLoad`, Scene 설정/저장 같은 편집 작업이 포함될 수 있습니다. 코드를 읽는 것과 별개로, 검토 없이 실제 Unity 프로젝트의 Editor 폴더에 복사·실행하지 마세요.

원본에는 호환/이전 구현과 보조 코드가 함께 남아 있을 수 있습니다. 포함된 모든 클래스가 현재 운영 Scene에 연결되거나 같은 완성도로 검증됐다고 주장하지 않습니다. 이 제출본을 만드는 과정에서 Gameplay 리팩터링이나 API 변경은 하지 않았습니다.

## 소스와 인코딩

공개본의 C#은 한글 가독성을 위해 UTF-8(BOM 없음)로 통일했습니다. 원본 CP949/UTF-8 BOM 파일은 원본에서 수정하지 않았습니다. 변환 시 디코딩된 코드 텍스트와 줄바꿈은 그대로 보존하고 원본/제출본 해시를 manifest에 기록했습니다.

`.cs.meta`는 원본 byte 그대로 보존했습니다. 폴더 meta, 원본 Scene/Prefab, 외부 에셋 meta 전체를 복사한 것은 아닙니다.

## 개인 담당 문서와 실행 빌드

개인 기여도는 사용자가 별도로 작성할 예정이므로 임의의 기여 비율·담당자·성과를 추가하지 않았습니다. 실행 빌드와 플레이 영상은 실제 준비가 완료되면 별도로 연결할 수 있습니다. 이 저장소를 준비하면서 Play Mode, Host-Client 테스트 또는 빌드를 실행하지 않았습니다.
