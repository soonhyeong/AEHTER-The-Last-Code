# 원본 개발 환경과 의존성

## 원본 환경

- Unity Editor: **6000.2.12f1**
- C#: Unity의 MonoBehaviour/ScriptableObject/Editor 및 .NET API 사용
- 목표 플랫폼: Windows PC

다음 버전은 원본 `Packages/manifest.json`에 기록된 값을 참고한 것입니다. 이 저장소에서 패키지를 설치하거나 실행 검증한 결과는 아닙니다.

| 패키지 | 원본 선언 버전 | 관련 코드 |
|---|---|---|
| com.unity.inputsystem | 1.14.2 | PlayerInput, InputValue, Keyboard/Mouse/Gamepad |
| com.unity.netcode.gameobjects | 2.7.0 | NetworkObject/NetworkBehaviour, ownership, Scene 전환 |
| com.unity.services.multiplayer | 1.1.8 | 세션 생성/참가 및 Unity Services 연동 |
| com.unity.animation.rigging | 1.3.1 | 캐릭터 리깅·전투 표시 |
| com.unity.ai.navigation | 2.0.12 | NavMesh와 AI/편집기 설정 |
| com.unity.cinemachine | 2.10.4 | 카메라 연동 코드의 원본 설치 기준 |
| com.unity.timeline | 1.8.9 | 연출·Timeline 관련 API |
| com.unity.ugui | 2.0.0 | Unity UI와 TextMesh Pro 관련 UI |
| com.unity.render-pipelines.high-definition | 17.2.0 | 원본 프로젝트의 렌더 파이프라인 선언 |

Unity Services Authentication/Core 등은 원본 패키지의 전이 의존성입니다. 패키지 구현, SDK 바이너리, 계정 자격 증명과 서비스 설정은 이 저장소에 포함하지 않습니다. Unity 패키지는 해당 배포처와 라이선스에 따라 별도로 취득해야 합니다.

## 이 코드만으로 재현할 수 없는 항목

- Scene/Prefab의 component 연결, ScriptableObject·아이템·퍼즐 데이터
- 캐릭터 모델/애니메이션, Animator Controller, 무기/이펙트와 물리 설정
- InputActionAsset, UI 이미지/폰트, 음원, 영상
- 빌드 Scene 목록, 네트워크 Prefab 등록 및 온라인 서비스 설정
- 외부 리소스와 함께 동작하는 직렬화 reference 및 게임 기획 설정

위 항목을 임의로 복구하거나 placeholder로 치환한 실행 프로젝트를 만들지 않았습니다. 코드 구문 검사는 Unity 전체 컴파일, 실제 입력, 멀티플레이 동기화 또는 게임 완성도의 증명이 아닙니다.
