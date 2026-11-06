# 코드 살펴보기

아래 링크는 구현을 읽기 위한 진입점입니다. 개인별 기여도나 실제 Scene 연결 상태를 나타내지 않습니다.

| 시스템 | 주요 코드 | 살펴볼 내용 |
|---|---|---|
| 플레이어 이동 | [Move_Hankyul](<../Source/Assets/1. Scripts/Player/Hankyul/Move_Hankyul.cs>), [Move_Yunseo](<../Source/Assets/1. Scripts/Player/Yunseo/Move_Yunseo.cs>) | 이동·시점·계단/접지 보정과 입력 허용 상태 |
| 전투 | [Hankyul_Battle](<../Source/Assets/1. Scripts/Player/Hankyul/Hankyul_Battle.cs>), [Hankyul_Fire](<../Source/Assets/1. Scripts/Player/Hankyul/Hankyul_Fire.cs>), [Yunseo_Battle](<../Source/Assets/1. Scripts/Player/Yunseo/Yunseo_Battle.cs>) | 무기 상태, 발사·재장전, 캐릭터별 전투 |
| EMP | [EMPThrow](<../Source/Assets/1. Scripts/Player/Hankyul/EMP/EMPThrow.cs>), [EMPStunReceiver](<../Source/Assets/1. Scripts/Player/Hankyul/EMP/EMPStunReceiver.cs>) | 투척, 효과 적용과 상태 복원 |
| 상호작용 | [PlayerInteractor](<../Source/Assets/1. Scripts/Player/PlayerInteractor.cs>), [PlayerInteractionArbiter](<../Source/Assets/1. Scripts/PlayerInteractionArbiter.cs>) | 즉시/홀드 대상 선택과 입력 중재 |
| Modal 제어 | [PlayerModalControlContext](<../Source/Assets/1. Scripts/PlayerModalControlContext.cs>), [CursorModalBroker](<../Source/Assets/1. Scripts/CursorModalBroker.cs>) | UI 사용 중 조작·커서 상태 소유권 |
| 인벤토리/QuickSlot | [PlayerInventory](<../Source/Assets/1. Scripts/Player/Inventory/PlayerInventory.cs>), [QuickSlotManager](<../Source/Assets/1. Scripts/Player/Inventory/QuickSlotManager.cs>) | 아이템 보관, 사용·교체, UI 연결 |
| 파츠 제작/장착 | [EquipmentManager](<../Source/Assets/1. Scripts/PartsSystem/EquipmentManager.cs>), [PartCraftingUI](<../Source/Assets/1. Scripts/PartsSystem/PartCraftingUI.cs>), [CraftingController](<../Source/Assets/Scripts/CraftingController.cs>) | 제작 트랜잭션, 장착 상태, 능력 반영 |
| 전력 퍼즐 | [PuzzleManager](<../Source/Assets/1. Scripts/Puzzle/PuzzleManager.cs>), [PuzzleOpener](<../Source/Assets/1. Scripts/Puzzle/PuzzleOpener.cs>) | 퍼즐 진입/종료와 플레이어 제어 잠금 |
| 회로 퍼즐 | [CircuitPuzzleManager_Final](<../Source/Assets/1. Scripts/Puzzle/Puzzle2/CircuitPuzzleManager_Final.cs>), [Puzzle2DDAController_Final](<../Source/Assets/1. Scripts/Puzzle/Puzzle2/Puzzle2DDAController_Final.cs>) | 성공/실패·Restart, 난이도 관련 구현 |
| 코드 해독 퍼즐 | [CodebreakerGameManager_Final](<../Source/Assets/1. Scripts/Puzzle/Puzzle3/CodebreakerGameManager_Final.cs>) | 해독 퍼즐 상태와 로그/UI |
| 퍼즐 복귀 흐름 | [PuzzleSceneReturnBootstrap](<../Source/Assets/1. Scripts/PuzzleSceneReturnBootstrap.cs>), [PuzzleSceneFlowState](<../Source/Assets/1. Scripts/PuzzleSceneFlowState.cs>) | 퍼즐 Scene 결과와 이전 구역 복귀 계약 |
| AI/경보 | [Robots](<../Source/Assets/1. Scripts/Robots/Robots.cs>), [Drones](<../Source/Assets/1. Scripts/Drones/Drones.cs>), [AlertLevelManager](<../Source/Assets/1. Scripts/Managers/AlertLevelManager.cs>) | 적 상태, 탐지/전투, 경보 이벤트 |
| 멀티플레이 | [MultiplayerManager](<../Source/Assets/1. Scripts/MultiplayerScript/MultiplayerManager.cs>), [NetworkPlayerSetup](<../Source/Assets/1. Scripts/MultiplayerScript/NetworkPlayerSetup.cs>), [NetworkPlayerSpawner](<../Source/Assets/1. Scripts/MultiplayerScript/NetworkPlayerSpawner.cs>) | 세션/로비, 플레이어 스폰, 로컬 표시/소유권 |
| Scene 상태 보존 | [PlayerStateSessionStore](<../Source/Assets/1. Scripts/PlayerStateSessionStore.cs>), [PlayerStatePersistenceBridge](<../Source/Assets/1. Scripts/PlayerStatePersistenceBridge.cs>) | Scene 전환 시 플레이어 상태 캡처/복원 |
| HUD/내비게이션 | [AetherModernHUD](<../Source/Assets/1. Scripts/UI/AetherModernHUD.cs>), [LocalPlayerUIBinding](<../Source/Assets/1. Scripts/LocalPlayerUIBinding.cs>), [CompassScroller](<../Source/Assets/1. Scripts/CompassScroller.cs>) | 로컬 플레이어 HUD와 나침반 바인딩 |
| 대화/엔딩 | [DialogueController](<../Source/Assets/1. Scripts(upload)/Dialogue/DialogueController.cs>), [EndingDecisionManager](<../Source/Assets/1. Scripts(upload)/Managers/EndingDecisionManager.cs>) | 대사 진행·선택·제어 잠금 관련 보존 코드 |
| 시네마틱 | [AetherBrainPulse](<../Source/Assets/1. Scripts/AetherBrainPulse.cs>), [SubtitleUI](<../Source/Assets/00. Cinema(upload)/01. Scripts/SubtitleUI.cs>) | 연출용 pulse와 자막 표시 |

폴더별 보조/호환 클래스는 해당 진입점에서 함께 살펴볼 수 있습니다. `.meta`는 GUID 보존용이며 코드 설명 문서는 아닙니다.
