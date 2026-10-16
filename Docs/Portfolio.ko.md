### 프로젝트 간단 설명

---

**3D 원형 테이블에서 미니게임과 총격전을 반복하며 마지막 생존자를 가리는 멀티플레이 파티 게임입니다.**

Unity와 Netcode for GameObjects를 사용해 AI 상대 1~5명과의 플레이, 2~6인 PvP를 구현했습니다. 참가자는 체력 100과 목숨 3개로 시작하며, 미니게임 결과에 따라 사격 기회를 얻거나 동시에 총격전을 벌입니다.

현재 초읽기, 퀵핸즈, 신호등, 욕심의 대가, 뭐였더라?까지 5종의 미니게임을 제공합니다. 숫자 입력 속도, 반응 속도, 기억력, 위험을 감수하는 판단과 직접 조준하는 실력을 결합했습니다.

### 주 시도들

---

**1. 서버가 검증하는 멀티플레이 입력 처리**

클라이언트가 행동 결과를 직접 변경하지 않고 입력을 서버에 요청하도록 구성했습니다. RPC 호출 권한과 실제 발신자를 확인한 뒤, 서버에서 생존 여부·현재 단계·행동 버전·입력 가능 시간을 추가로 검사합니다.

```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
private void InputDigitRpc(int digit, int version, RpcParams rpcParams = default)
{
    if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
    HelloWorldManager.Instance?.RequestInput(OwnerClientId, digit, version);
}
```

`HelloWorldPlayer.cs`의 실제 코드입니다. 입력 요청의 Version을 서버의 현재 행동과 비교해 이전 행동에 대한 중복·지연 요청이 적용되지 않도록 했습니다. 사격과 미니게임 입력도 서버에서 규칙을 검증합니다.

**2. 무기별 행동과 부분 장전을 상태로 관리**

퀵핸즈의 준비 과정을 집기·총기 조작·장전·조준 상태로 나눴습니다. 리볼버는 한 발씩 최대 6발을 넣고, 일부만 장전한 상태에서도 사격할 수 있습니다. 사격 후에는 남은 실탄을 보존하고 빈 약실만 다시 채웁니다.

아래는 `DuelDirector.cs`에서 장전 행동 완료를 처리하는 부분입니다.

```csharp
if (state.Action == DuelAction.Load)
{
    int capacity = WeaponRules.Capacity(state.Weapon), mask = (1 << capacity) - 1;
    int chamber = WeaponRules.Chamber(
        mask & ~(actions[seat].LoadedMask | actions[seat].SpentMask),
        state.NextChamber);

    if (chamber >= 0)
    {
        actions[seat].LoadedMask |= 1 << chamber;
        actions[seat].SpentMask &= ~(1 << chamber);
    }

    if (state.Weapon == DuelWeapon.Revolver && actions[seat].Ammo < capacity)
        next = DuelAction.Load;
}
```

약실마다 실탄과 탄피를 비트마스크로 관리해 부분 장전·잔탄 연속 사격·탄피 배출을 구분했습니다. 소드오프는 배럴 열기 → 장전 → 배럴 닫기를 반복하는 별도 흐름을 사용합니다.

**3. 미니게임의 공개 정보와 개인 정보를 구분**

방향을 기억하는 ‘뭐였더라?’에서는 서버가 정답과 오입력 수를 관리합니다. 입력 단계의 공개 스냅샷에서는 정답 시드와 개인 진행을 숨기고, 입력 접수 여부와 완료 개수만 해당 플레이어에게 별도로 전달합니다.

```csharp
public ArrowMemoryState PublicState()
{
    var s = State;
    if (s.Phase == ArrowMemoryPhase.Input || s.Phase == ArrowMemoryPhase.None)
        s.AnswerSeed = 0;

    if (s.Phase == ArrowMemoryPhase.Input || s.Phase == ArrowMemoryPhase.Showing)
        for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            s.Set(i, new ArrowMemoryEntry { Errors = -1, Rank = s.ForSeat(i).Rank });

    return s;
}
```

`DuelArrowMemory.cs`의 실제 코드입니다. 화면에서 글자만 가리는 방식에 더해 입력 단계에 배포하는 데이터도 구분했습니다. 또한 길이가 늘어나는 방향열은 화면에 보이는 UI 셀을 재사용하고, 공개 중에는 자동으로, 입력 중에는 현재 칸을 따라 가로로 이동하도록 구현했습니다.

**4. 직접 조준과 실제 투사체 판정**

서버가 탄환의 이동 구간을 검사해 피격·가림·부위별 피해를 결정합니다. 소드오프는 매 발사마다 산탄을 무작위로 분산하고, 실제로 명중했을 때 직격 대상과 좌우 좌석의 주변 피해를 처리합니다.

조준 흔들림, 무기별 피해, 잔탄, 사격 제한시간을 함께 적용해 입력을 빨리 끝내는 것뿐 아니라 조준과 사격 타이밍도 중요하게 만들었습니다.

**5. 게임 상태에 맞춘 캐릭터 연출과 피드백**

로우폴리 캐릭터의 시선·총기 조작·피격·쓰러짐·복귀와 대기 애니메이션을 구현했습니다. 팔 길이를 고정한 채 손과 팔꿈치 위치를 계산하고, 로비에서는 본인 모델을 보여주되 실제 1인칭 시점에서는 본인 몸의 애니메이션이 시야를 가리지 않도록 처리했습니다.

행동 완료 효과음, 히트마커, 킬로그, 부위 아이콘, 사망 피해 내역, 경기 결과와 실제 기록을 바탕으로 선정하는 하이라이트를 연결했습니다.

### 예산 및 리소스

---

- **개발 환경:** Unity 6000.3.18f1, C#.
- **주요 패키지:** Netcode for GameObjects 2.13.3, Unity Transport 2.7.3, URP, Input System, UI Toolkit.
- **그래픽:** 코드로 생성한 로우폴리 캐릭터·무기·테이블 소품과 절차적으로 생성한 UI 아이콘.
- **사운드:** FreeWeaponSFX, 제공받은 로비 입퇴장 음원, 출처를 기록한 공개 녹음 리소스. 적용 내역은 `Docs/Audio/Sources.md`에 관리했습니다.
- **개발 보조:** Codex를 활용한 구현·수정·검증 보조.
- **비용:** 실제 지출 내역은 별도 집계가 필요합니다. 구매 비용이나 전체 예산을 확인 없이 0원으로 표기하지 않았습니다.

### 관련 멤버

---

- **프로젝트 담당자:** [이름 기입]
- **담당 영역:** 게임 기획, 미니게임·무기·아이템 규칙 설계, 개발 요구사항 전달, 플레이 테스트 및 수정 피드백.
- **코드 구현·수정 및 검증 보조:** Codex 활용.

※ 참여 인원과 개인별 담당 범위는 실제 작업 내역에 맞춰 확정해 기입합니다.

### 성과

---

- **Codex를 활용해 게임 아이디어를 플레이 가능한 3D 파티 게임 프로토타입으로 구체화했습니다.**
- 미니게임, 무기, 아이템의 규칙과 예외 상황을 정리하고 개발 요구사항으로 전달하는 경험을 쌓았습니다.
- 실제 플레이와 화면을 확인하며 조작 방식, UI, 캐릭터 자세, 사운드와 연출에 대한 수정 피드백을 반복했습니다.
- AI가 만든 결과를 기획 의도와 비교하고, 원하는 방향으로 조정하는 개발 과정을 경험했습니다.

코드 구현과 수정에는 Codex를 활용했으며, 본인은 기획과 요구사항 구체화, 플레이 확인 및 개선 방향 제시에 참여했습니다.
