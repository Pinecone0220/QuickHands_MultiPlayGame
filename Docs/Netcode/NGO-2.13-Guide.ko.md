# Netcode for GameObjects 2.13 실무 분석

작성·검증 기준일: 2026-10-06. 대상 프로젝트: Hello Multiplayer World.

## 1. 범위와 근거

이 문서는 NGO 2.13.3의 설치 패키지 `Documentation~`, RPC API 구현, CHANGELOG와 프로젝트 C# 스크립트·설정을 대조한 한국어 분석이다. 공식 문서를 그대로 옮기기보다 동작 원리, 선택 기준, 실수하기 쉬운 부분, 현재 프로젝트에 적용할 사항을 정리한다.

공식 웹 진입점은 [NGO 2.13 매뉴얼](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/index.html)이다. 이 주소는 확인 시 2.13.3을 표시했다. 사용자 링크의 실제 목적지인 [Unity 2020.3 NetCode 소개](https://docs.unity3d.com/kr/2020.3/Manual/com.unity.netcode.html)는 NGO 기준 자료로 혼용하지 않는다.

근거 우선순위:

1. 실제 설치 버전과 그 버전의 공식 문서·API 구현을 대조한다.
2. 같은 버전의 웹 매뉴얼과 변경 기록으로 보완한다.
3. 오래된 튜토리얼은 개념 참고로만 사용한다.

설치된 패키지 경로는 `Library/PackageCache/com.unity.netcode.gameobjects@277a288a1cea`다. 끝의 해시는 재설치 때 바뀔 수 있다. 캐시는 원문 조회용이며 프로젝트 가이드의 영구 저장소가 아니다. [Sources.md](Sources.md)는 공식 목차에서 추출한 링크와 원문 상대 경로를 제공한다.

아래에서 **설계 권고**는 문서로부터 도출한 이 프로젝트의 권장 방식이며 NGO가 강제하는 규칙과 구분한다. 코드 조각은 설명용이고 이번 작업에서 Unity 컴파일·멀티프로세스 실행 검증을 수행하지 않았다.

## 2. 토폴로지와 권한

NGO는 GameObject/MonoBehaviour 기반 네트워킹 계층이다. Transport는 패킷 전달을, NGO는 오브젝트·상태·RPC 동기화를 담당한다. Relay는 인터넷 연결을 중계하는 서비스이며 게임 규칙을 실행하는 권한 서버와 역할이 다르다.

| 개념 | 의미 | 개발상 주의 |
|---|---|---|
| Server | 클라이언트-서버의 중앙 권한 인스턴스 | 게임 결과와 생성·소유권 변경 담당 |
| Client | 서버에 연결된 게임 인스턴스 | 입력·표시와 서버 요청 담당 |
| Host | 한 프로세스가 서버와 클라이언트 역할 수행 | 로컬 RPC 실행과 원격 실행의 차이 존재 |
| Dedicated server | 플레이어 클라이언트 역할 없는 서버 | 로컬 플레이어·카메라·입력이 있다고 가정하면 안 됨 |
| Ownership | NetworkObject에 배정된 소유자 | 서버 권한과 같지 않음 |
| Authority | 어떤 상태/시뮬레이션을 확정할 권한 | 컴포넌트의 authority 설정까지 확인 |

`IsOwner`는 로컬 인스턴스가 오브젝트 소유자인지 판별한다. `IsServer`는 서버 역할 여부다. 호스트는 `IsServer`와 `IsClient`가 모두 참이므로 두 개의 독립된 `if`가 같은 효과를 중복 실행할 수 있다. 다만 서버 처리와 클라이언트 표시를 모두 해야 하는 경우도 있어 무조건 `else if`로 바꾸지 않는다.

**설계 권고:** 현재 프로젝트는 클라이언트가 요청하고 서버가 위치를 결정하는 구조를 유지한다. 체력, 피해, 재화, 아이템 획득도 서버 검증을 통과한 결과만 공유한다. 로컬 입력 권한, 오브젝트 소유권, 상태 쓰기 권한, RPC 호출 권한을 각각 결정한다.

### Distributed Authority(DA)

DA에서는 오브젝트별 시뮬레이션 권한이 클라이언트에 분산된다. 소유자가 해당 오브젝트의 권한을 가지며 session owner가 공통 세션 작업을 담당한다. distributable 오브젝트는 참가·퇴장에 따라 재분배될 수 있다.

- 클라이언트-서버의 `IsServer` 중심 설계를 그대로 이식하면 안 된다.
- ownership permission, session owner, 소유권 변경 중 상태 처리를 별도로 설계한다.
- 여러 소유자의 물리 시뮬레이션이 하나의 중앙 물리 세계와 같다고 가정하지 않는다.
- 공식 문서는 높은 정확도의 예측과 강한 경쟁 무결성이 필요한 게임에 한계를 설명한다.
- Connection approval과 deferred despawn 등 기능의 토폴로지 지원 범위가 다르다.

원문: `terms-concepts/{authority,ownership,client-server,distributed-authority}.md`.

## 3. NetworkManager와 PlayerObject

`NetworkManager`는 Transport, 네트워크 프리팹, PlayerPrefab, tick rate, 씬 관리, 접속 설정의 중심이다. `StartHost`, `StartClient`, `StartServer` 호출 성공과 실제 접속·동기화 완료를 구분한다. 접속은 비동기 과정이므로 버튼 클릭 직후 PlayerObject가 존재한다고 가정하지 않는다.

- 시작 반환값과 연결/실패/종료 이벤트를 UI에 반영한다.
- 연결 시도 중 중복 시작을 막고, 종료 후 재시작 가능한 상태로 정리한다.
- `SpawnManager`, `SceneManager` 같은 하위 시스템은 NetworkManager가 시작된 뒤 접근한다.
- `OnConnectionEvent`는 로컬 접속/해제와 peer 접속/해제를 구분한다. 서버와 클라이언트에서 이벤트 의미가 같지 않으므로 `ClientId`와 `PeerClientIds` 유효 범위를 확인한다.
- 종료 시 콜백과 세션 종속 참조를 정리한다. 종료가 진행 중인데 바로 재접속하지 않는다.

PlayerObject는 해당 클라이언트를 대표하는 특별한 NetworkObject다. 클라이언트가 소유한 모든 오브젝트가 PlayerObject인 것은 아니다. 자동 생성 여부, PlayerPrefab 등록, connection approval의 생성 설정을 함께 확인한다.

**설계 권고:** 입력, 카메라, 오디오 리스너는 로컬 소유 플레이어에서만 활성화한다. 소유권 이전을 지원하면 최초 스폰뿐 아니라 권한 변경 시에도 활성 상태를 갱신한다. 전용 서버 UI는 로컬 PlayerObject에 의존하지 않는다.

원문: `components/core/networkmanager.md`, `components/core/playerobjects.md`, `advanced-topics/connection-events.md`.

## 4. NetworkObject·NetworkBehaviour 수명주기

NetworkObject는 네트워크상 식별 단위이고 NetworkBehaviour는 RPC와 NetworkVariable을 가지는 로직 단위다. `NetworkObjectId`는 세션의 런타임 식별자다. 계정 ID나 영구 저장 키로 사용하지 않는다.

네트워크 프리팹은 루트에 NetworkObject를 두고 NetworkBehaviour들을 동일 오브젝트 또는 자식에 배치한다. 프리팹 내부의 중첩 NetworkObject는 지원하지 않는다. 서버와 클라이언트의 NetworkBehaviour 구성·순서를 일치시킨다. 런타임에 빈 GameObject에 NetworkObject를 붙여 네트워크 프리팹처럼 사용하는 방식은 유효한 hash를 만들지 못하므로 사용하지 않는다.

| 시점 | 적합한 작업 | 주의 |
|---|---|---|
| `Awake` | 로컬 컴포넌트 참조, NetworkList 생성 | 네트워크 역할을 아직 신뢰하지 않음 |
| `OnNetworkPreSpawn` | 스폰 직전 구성 | 역할 관련 속성이 아직 준비되지 않을 수 있음 |
| `OnNetworkSpawn` | 역할별 초기화, 구독, 현재 동기화 상태 적용 | 다른 컴포넌트의 spawn 초기화 순서 주의 |
| `OnNetworkPostSpawn` | 같은 NetworkObject의 다른 Behaviour에 의존하는 초기화 | 다른 모든 오브젝트의 준비를 보장하는 이벤트는 아님 |
| `OnNetworkSessionSynchronized` | 초기 참가 클라이언트의 세션 동기화 이후 처리 | 통합 씬 관리와 문서의 호출 범위를 확인; 서버/호스트에는 같은 방식으로 호출되지 않음 |
| `OnNetworkPreDespawn` | 아직 유효한 네트워크 상태를 필요로 하는 정리 | 상태가 사라지기 전 처리 |
| `OnNetworkDespawn` | 구독 해제, 세션 참조 정리 | 다른 Behaviour가 이미 정리되었을 수 있음 |
| `OnDestroy` | 최종 자원 정리 | 재정의 시 `base.OnDestroy()` 호출 |

일반적인 활성 오브젝트의 순서는 동적 생성에서 `Awake → OnNetworkSpawn → Start`, 씬 배치에서 `Awake → Start → OnNetworkSpawn`이다. 비활성 Behaviour는 예외가 있으므로 `Start`에서만 참조를 준비하지 않는다.

스폰 당시 비활성 자식 GameObject의 NetworkBehaviour는 해당 스폰 수명 동안 포함되지 않을 수 있다. 나중에 활성화해야 하는 네트워크 로직은 GameObject를 유지하고 Behaviour를 비활성화하는 패턴을 검토한다.

원문: `components/core/networkobject.md`, `components/core/networkbehaviour.md`, `components/core/networkbehaviour-synchronize.md`.

## 5. 스폰·디스폰·소유권·가시성

클라이언트-서버의 일반적인 동적 생성 순서는 등록된 프리팹 준비 → 서버의 인스턴스 생성 → 필요한 초기 구성 → `NetworkObject.Spawn()`이다. `Instantiate`만 호출하면 로컬에만 생성된다. 네트워크 변수를 설정할 정확한 시점은 변수 권한과 spawn 수명주기에 맞춘다.

- `Spawn()`은 클라이언트-서버에서 기본적으로 서버 소유로 생성한다.
- `SpawnWithOwnership(clientId)`는 소유자를 지정한다. 서버가 이후 수행할 초기화가 owner 권한을 요구하는지 확인한다.
- `ChangeOwnership`/`RemoveOwnership`은 클라이언트-서버에서 서버가 수행한다.
- `DontDestroyWithOwner`가 필요하다면 연결 해제 후 오브젝트의 새 관리 주체도 함께 정한다.
- `Despawn()`은 기본적으로 삭제를 포함한다. 재사용할 때 `Despawn(false)`와 실제 풀 반환 처리를 구분한다.
- 클라이언트가 네트워크 오브젝트를 임의로 삭제하도록 만들지 않는다.

### 풀링

자주 생성되는 탄환·효과에 유용하다. `INetworkPrefabInstanceHandler`와 prefab handler 등록을 사용해 원격 인스턴스 생성/삭제 경로도 풀과 연결한다. `Despawn(false)` 하나로 모든 peer의 풀이 완성되는 것은 아니다.

**설계 권고:** 재사용 시 속도, 타이머, 체력, 소유자, 이벤트 구독, 시각 효과를 초기화한다. 서버/호스트의 생성 경로와 원격 클라이언트 handler 경로를 분리해 확인하고 이중 반환을 방지한다.

### 네트워크 가시성

Renderer를 끄는 것과 달리 observer에서 제외하면 클라이언트가 해당 네트워크 오브젝트를 가지고 있지 않을 수 있다. `CheckObjectVisibility`는 spawn/신규 연결 시 판단에 쓰이며 매 프레임 거리 검사기로 자동 동작하지 않는다. 동적으로 관심 영역을 바꾸려면 `NetworkShow`/`NetworkHide` 등을 적절히 호출한다.

`SpawnWithObservers = false`는 초기 observer 없이 생성할 때 사용한다. 보이지 않는 오브젝트에 대한 RPC나 참조가 대상 클라이언트에서 해석될 것이라 가정하지 않는다. 다시 보일 때 현재 상태를 복원할 수 있게 설계한다.

원문: `basics/{object-spawning,object-visibility}.md`, `components/core/networkobject-ownership.md`, `advanced-topics/{object-pooling,network-prefab-handler}.md`.

## 6. NetworkVariable과 NetworkList

NetworkVariable은 지속되는 현재 상태다. 기존 참가자의 변경뿐 아니라 늦게 참가한 클라이언트의 초기 상태 동기화를 지원한다. 이벤트 기록 저장소가 아니므로 모든 중간 변경을 하나씩 받는다는 가정으로 만들지 않는다.

| 정보 | 권장 방식 | 이유 |
|---|---|---|
| 체력·문 열림·경기 단계 | NetworkVariable | 늦은 참가자도 현재 값 필요 |
| 인벤토리 슬롯·팀 목록 | NetworkList 또는 명시적 동기화 모델 | 컬렉션 변경 관리 |
| 발사·상호작용 요청 | 서버 대상 RPC | 행위를 요청하고 검증 |
| 일회성 사운드·파티클 | 클라이언트 대상 RPC 또는 로컬 재생 | 과거 이벤트를 새 참가자에게 재생할 필요 없음 |
| 계속 움직이는 위치·회전 | NetworkTransform 또는 명시적 이동 프로토콜 | 보간·전송 빈도 관리 |

클라이언트-서버에서 기본 읽기는 Everyone, 쓰기는 Server다. Owner read는 소유자와 서버가 읽을 수 있다. Owner write는 소유자 전용이므로 서버가 항상 쓸 수 있다고 가정하지 않는다. 읽기 제한은 암호화나 계정 인증을 대신하지 않는다.

초기 값은 클라이언트의 `OnNetworkSpawn` 이전에 동기화된다. 변경 이벤트 구독만으로 초기 표시를 처리하면 늦은 참가자의 UI·위치가 누락될 수 있다. 다음은 현재 위치 예제에 적용할 패턴이다.

```csharp
public override void OnNetworkSpawn()
{
    Position.OnValueChanged += OnPositionChanged;
    ApplyPosition(Position.Value);
}

public override void OnNetworkDespawn()
{
    Position.OnValueChanged -= OnPositionChanged;
}

private void OnPositionChanged(Vector3 previous, Vector3 current)
{
    ApplyPosition(current);
}

private void ApplyPosition(Vector3 value) => transform.position = value;
```

이 패턴은 이벤트 기반 순간 이동 예제용이다. 같은 Transform에 NetworkTransform을 추가한다면 위치를 적용하는 주체를 다시 정해야 한다.

### 컬렉션과 비용

- NetworkList는 공식 예제처럼 `Awake`에서 생성하고 적절한 네트워크 시점에 내용을 초기화한다.
- NetworkVariable 내부 컬렉션을 직접 수정한 경우 `CheckDirtyState()`가 필요하다. 여러 수정 뒤 한 번 검사해 전체·중첩 순회 비용을 줄인다.
- 구조체의 일부 값을 변경할 때 복사본을 수정한 뒤 `.Value`에 다시 대입하는 패턴을 사용한다.
- 지원 컬렉션과 복합 타입의 delta 직렬화 여부는 타입별로 확인한다. 최초 spawn/late join은 전체 상태가 필요하다.
- 네트워크 문자열에는 적절한 `FixedString`을 우선 검토한다. 바이트 용량과 한글 UTF-8 길이를 구분한다.
- 표시 문자열이 정해진 선택지라면 문자열 전체 대신 enum/ID를 동기화한다.

원문: `basics/networkvariable.md`, `basics/custom-networkvariables.md`, `learn/rpcvnetvar.md`.

## 7. RPC: 목적지·호출 권한·검증

RPC 메서드는 NetworkBehaviour 안에 `[Rpc(...)]`로 선언하고 이름을 `Rpc`로 끝낸다. 목적지와 호출 권한은 독립적이다. 프로젝트의 `SubmitPositionRequestServerRpc`는 이름에 ServerRpc가 있어도 `[Rpc]`를 쓰므로 통합 RPC다.

| 목적지 | 사용 의미 |
|---|---|
| `SendTo.Server` | 서버에서 요청 처리; 서버에서 호출하면 로컬 실행 가능 |
| `SendTo.ClientsAndHost` | 호스트 플레이어 포함 클라이언트 표시 처리 |
| `SendTo.NotServer` | 서버 제외; 호스트도 서버라 제외되는 점 주의 |
| `SendTo.Owner` | 오브젝트 소유자 대상으로 전달 |
| `SendTo.Everyone` | observer 범위의 전체 대상; 로컬 실행 가능 |
| `SendTo.SpecifiedInParams` | RpcParams/RpcTarget으로 실행 시 대상 지정 |

2.13.3은 `RpcInvokePermission.Everyone`, `Owner`, `Server`로 발신 권한을 표현한다. 기본값은 Everyone이고 `RequireOwnership`은 deprecated다. `RpcParams`가 실제 타입 이름이다. 공식 설명 일부에 `RpcParameters`라는 표현이 있어도 API를 그대로 추측해서 작성하지 않는다.

```csharp
// 소유 플레이어의 요청에 적합한 선언 예시.
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
private void RequestActionRpc(RpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;
    // 서버에서 senderId의 상태, 쿨다운, 거리, 자원 등을 검증한 뒤 적용.
}
```

이 예시를 현재 Move 경로에 그대로 넣으면 전용 서버가 클라이언트 소유 오브젝트의 RPC를 호출하는 경로와 충돌할 수 있다. **설계 권고:** 서버가 직접 실행할 게임 로직 함수와 클라이언트 요청 RPC를 분리한다. 서버는 내부 함수를, 클라이언트는 권한 제한 RPC를 거쳐 내부 함수를 호출한다.

공유 문처럼 서버 소유 오브젝트를 여러 클라이언트가 조작해야 한다면 Everyone 호출이 적절할 수 있다. 이때 서버가 실제 발신자와 대상 간 거리·상태·권한을 검사한다. 클라이언트가 payload에 넣은 clientId는 신원 근거로 신뢰하지 않고 `rpcParams.Receive.SenderClientId`를 사용한다.

### 전달과 순서

- 기본 reliable은 재전송 비용이 있다. 연결이 끊어져도 영구 보관되는 작업 큐는 아니다.
- 유실되어도 되는 시각 효과는 unreliable을 검토한다. 중요한 게임 상태는 복구 가능한 상태 동기화와 함께 설계한다.
- 여러 오브젝트·여러 발신자의 메시지 전체에 전역 순서가 있다고 가정하지 않는다.
- 로컬 실행되는 중첩 RPC는 원격과 실행 순서가 달라질 수 있다. `DeferLocal`/`LocalDeferMode`는 이 차이를 완화하지만 무한 호출을 종료시키지는 않는다.
- RPC와 NetworkVariable 변경 도착 순서에 의존해 원자적 거래를 만들지 않는다. 하나의 결과 데이터나 명시적 sequence/state 모델을 사용한다.
- `RpcTarget.Single/Group`은 특정 수신자 선택에 쓴다. `RpcTargetUse.Temp`의 캐시 객체를 장기 보관하지 않는다.
- 반복적인 배열 생성·broadcast·프레임당 reliable RPC는 측정 후 줄인다.

원문: `advanced-topics/message-system/{rpc,rpc-params,reliability,rpc-compatibility}.md`; API: `Runtime/Messaging/RpcAttributes.cs`.

## 8. 직렬화와 참조

지원되는 기본형·Unity 값 타입은 내장 직렬화를 사용한다. 사용자 구조체는 `INetworkSerializable`과 `BufferSerializer<T>`로 쓰기/읽기를 함께 정의할 수 있다. RPC에서 되는 타입이 NetworkVariable에서도 같은 경로로 지원된다고 가정하지 않는다.

```csharp
public struct ActionState : INetworkSerializable
{
    public int Sequence;
    public Vector3 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Sequence);
        serializer.SerializeValue(ref Position);
    }
}
```

- 양쪽의 필드 순서와 조건 분기가 같아야 한다. 조건부 데이터는 조건 플래그부터 전송한다.
- 중첩 참조형 데이터는 읽기 전에 필요한 초기화를 수행한다. 전송하지 않은 필드가 자동 초기화될 것이라 기대하지 않는다.
- `INetworkSerializeByMemcpy`는 unmanaged 메모리 복사가 적합한 경우에만 선택한다. 관리 객체 참조를 보내는 우회책이 아니다.
- `FastBufferReader/Writer` 사용 시 길이·경계·자원 수명을 직접 관리한다. 큰 배열과 임의 크기 메시지를 무제한 허용하지 않는다.
- GameObject/NetworkObject 참조 자체는 기본 네트워크 payload가 아니다. `NetworkObjectReference`/`NetworkBehaviourReference`를 사용한다.
- 참조는 이미 spawn된 오브젝트를 전제로 한다. 수신 측에서 `TryGet` 실패를 처리한다. despawn, 미관찰, 도착 시점 차이를 고려한다.
- 데이터 형식 변경은 양쪽 빌드의 호환성과 관련된다. 저장 파일 버전과 네트워크 프로토콜 버전도 구분한다.

원문: `advanced-topics/serialization/`의 overview, INetworkSerializable, memcpy, networkobject-serialization 및 `advanced-topics/custom-serialization.md`.

## 9. 이동·보간·물리

### NetworkTransform

NetworkTransform은 위치·회전·크기 동기화 도구다. 이동 입력 처리, 충돌 판정, 치트 방지를 자동 완성하지 않는다. 클라이언트-서버의 기본은 서버 권한이고 Inspector authority 설정을 확인한다. DA에서는 owner authority로 동작한다.

**설계 권고:** 현재 랜덤 위치 변경 학습 예제와 실시간 캐릭터 이동을 구분한다. 연속 이동을 만들 때는 입력 → 권한 인스턴스의 이동 → 상태 전파 → 비권한 표시의 흐름을 먼저 정한다.

- 서버 권한 Transform을 클라이언트가 직접 움직이면 권한 상태로 덮어써질 수 있다.
- interpolation은 수신 상태를 부드럽게 보여 주는 대신 표시 지연을 추가한다. prediction이나 서버 검증을 대신하지 않는다.
- 위치·회전 threshold를 낮추면 정밀도와 전송량이 함께 증가한다. 게임 단위와 이동 속도에 맞춰 측정한다.
- 필요한 축만 동기화하고, 불필요한 자식 NetworkTransform을 줄인다.
- local/world space 전환과 parenting을 함께 설계한다. 특정 옵션 변경은 full state update와 보간 초기화를 유발할 수 있다.
- `UseUnreliableDeltas`는 손실 허용·보간·복구 비용의 선택이다. 과거 버전의 기본값을 기억으로 적용하지 않는다.
- 2.13 문서는 `SwitchTransformSpaceWhenParented`와 unreliable delta의 동시 사용 제한을 설명한다.
- half float/quaternion 압축은 오차와 대역폭을 교환한다. 2.13.3의 변경 기록을 함께 확인한다.

### Rigidbody

NetworkRigidbody의 authority는 NetworkTransform을 따른다. 자동 kinematic 제어를 사용하는 비권한 인스턴스는 kinematic으로 동작하고 권한 인스턴스가 시뮬레이션한다. 클라이언트에서도 서버와 같은 충돌 callback이 발생한다고 가정하지 않는다.

**설계 권고:** 피해·아이템 획득 등 게임 결과는 권한 측에서 판정한다. 시각 효과와 최종 결과를 분리한다. `Use Rigidbody for Motion`, 자동 kinematic 옵션, FixedUpdate와 tick rate, 보간 설정을 묶어서 테스트한다. owner authority 물리에서는 서로 다른 소유자 간 상호작용을 별도 검증한다.

원문: `components/helper/{networktransform,networkrigidbody}.md`, `advanced-topics/physics.md`.

## 10. 애니메이션·parenting·장착

NetworkAnimator는 상태·전환·설정된 파라미터를 동기화한다. 늦은 참가자는 현재 상태를 받고 과거 trigger 이벤트를 모두 재생하지 않는다. 네트워크 trigger는 `Animator.SetTrigger` 대신 `NetworkAnimator.SetTrigger`를 사용한다.

서버 권한 애니메이션은 서버 결과에 맞지만 소유자의 반응이 늦을 수 있다. owner authority는 즉각 반응하되 다른 참가자의 표시와 게임 판정을 구분해야 한다. 애니메이션 이벤트가 발생했다고 피해를 임의 확정하지 않는다.

**설계 권고:** 이미 이동 상태로 계산할 수 있는 걷기 속도 등을 매 프레임 다시 네트워크로 전송할 필요가 있는지 검토한다. 동기화에서 제외한 파라미터는 모든 표시 인스턴스에서 일관되게 계산한다.

네트워크 parenting은 클라이언트-서버에서 서버가 `TrySetParent`/`TryRemoveParent`로 수행하고 성공 여부를 확인한다. 일반적인 동적 NetworkObject 간 parenting은 양쪽 spawn을 전제로 한다. 씬 배치 계층은 별도 규칙을 원문에서 확인한다.

- `WorldPositionStays`를 장착·해제 시 일관되게 선택한다.
- 비균일 scale 부모/자식 조합은 해제 후 크기 변화까지 테스트한다.
- 참가 직후 플레이어와 씬 오브젝트를 연결하려면 해당 클라이언트의 동기화 완료를 기다린다.
- 프리팹의 금지된 중첩 NetworkObject와, spawn 후 독립 오브젝트의 parenting은 구분한다.

2.13 계열의 AttachableBehaviour는 자식 표시 오브젝트를 AttachableNode에 붙이는 대안이다. ComponentController는 관련 컴포넌트 활성 상태를 관리한다. NetworkObject 전체를 parenting하는 것과 동일한 구조가 아니므로 소유권 변경, 노드 파괴, despawn 시 detach 정책을 확인한다. 물리 장착은 collider, mass, gravity, 분리 속도를 함께 설계한다.

원문: `components/helper/{networkanimator,attachablebehaviour,attachablenode,componentcontroller}.md`, `advanced-topics/networkobject-parenting.md`.

## 11. 씬 전환과 늦은 참가

공유 게임 씬은 통합 씬 관리가 켜진 경우 서버의 `NetworkManager.SceneManager.LoadScene(...)` 등으로 관리한다. 일반 Unity SceneManager만 호출하면 NGO 참가자 동기화 절차와 일치하지 않을 수 있다. 로컬 메뉴·표시 전용 씬은 별도 수명주기로 구분한다.

| 이벤트/결과 | 의미 | 주의 |
|---|---|---|
| SceneEventProgressStatus | 씬 요청이 시작 가능한지 등 | 완료를 뜻하지 않음 |
| LoadComplete | 개별 인스턴스 로드 완료 | 전체 참가자 준비와 다름 |
| LoadEventCompleted | 로드 이벤트 전체 결과 | 완료·시간 초과 클라이언트 목록 확인 |
| SynchronizeComplete | 신규 참가자의 동기화 완료 | 기존 전체 씬 전환 완료와 구분 |
| Unity sceneLoaded | 로컬 Unity 씬 로드 | NGO spawn/sync 작업이 남아 있을 수 있음 |

**설계 권고:** 로딩 화면 → 요청 성공 확인 → 참가자 결과 집계 → 게임 시작의 상태 머신을 둔다. callback 안에서 겹치는 새 씬 이벤트를 즉시 시작하지 않도록 전환 요청을 직렬화한다. 씬이 빌드 목록에 포함되었는지 확인한다.

Single/Additive 선택에 따라 기존 씬과 오브젝트 보존 규칙이 달라진다. in-scene NetworkObject는 동적 프리팹과 동일한 초기화 순서를 기대하지 않는다. Active Scene Synchronization과 Scene Migration Synchronization은 필요한 오브젝트에만 적용한다.

늦은 참가자 검증 항목: 현재 문 상태, 체력, 점수, 위치, 장착 관계, 이미 생성·삭제된 적, 경기 단계. 과거 RPC 재생 없이 현재 상태만으로 화면을 재구성할 수 있어야 한다.

원문: `basics/scenemanagement/{using-networkscenemanager,scene-events,timing-considerations,client-synchronization-mode,inscene-placed-networkobjects}.md`.

## 12. 접속 승인·Relay·세션·재접속

Connection approval은 서버가 참가 가능 여부와 PlayerObject 생성 조건을 결정하는 지점이다. 승인 기능 활성화와 callback 등록을 함께 수행한다. 응답의 `Approved`, `CreatePlayerObject`, `PlayerPrefabHash`, `Pending`, `Reason`을 목적에 맞게 사용하고 비동기 확인이 timeout 안에 끝나도록 한다.

**설계 권고:** 인원 제한, 게임/프로토콜 버전, 이미 종료된 경기 여부를 검사한다. approval payload 자체를 안전한 인증 채널이라고 가정하지 않는다. NGO 문서는 이 raw payload를 자체적으로 암호화·인증하지 않는다고 명시한다. DA에는 같은 approval 기능을 적용하지 않는다.

Relay는 인터넷의 host/client 연결을 중계한다. 로컬 IP로 성공했다고 인터넷 서비스 연동이 끝난 것은 아니다. 서비스 초기화, 인증, allocation/join, Transport 설정은 NGO와 별도 단계다. 서비스 SDK API는 도입 시 [공식 Relay 연동 문서](https://docs.unity.com/ugs/en-us/manual/relay/manual/relay-and-ngo)를 해당 버전으로 확인한다. 현재 프로젝트에 Relay가 연동되었다고 확인한 것은 아니다.

재접속은 새 연결과 상태 복원의 결합이다. NGO clientId는 계정 식별자가 아니다. 서버가 검증한 지속 ID에 상태를 연결하고 보존 기간·중복 로그인·복귀 위치 정책을 정한다. 클라이언트 저장 GUID만으로 계정 인증을 대신하지 않는다.

- 재시도 전 종료 완료와 기존 callback·참조 정리를 확인한다.
- 재시도 횟수, 대기 간격, 취소, 실패 표시를 정의한다.
- Additive 씬이 남아 있으면 재접속 시 중복 로드나 오래된 씬 잔류가 생길 수 있다. 초기 씬 복귀 또는 명시적 정리 정책을 둔다.
- PlayerObject가 새로 생성되는지 기존 상태를 어떻게 복구할지 정한다.
- 클라이언트 재접속 지원이 곧 host migration 구현을 의미하지 않는다. 호스트 종료 정책은 따로 설계한다.

원문: `basics/connection-approval.md`, `advanced-topics/{session-management,reconnecting-mid-game}.md`, `relay/relay.md`.

## 13. 시간·지연·anticipation

NetworkTime은 여러 인스턴스의 시간 관계를 표현한다. LocalTime과 ServerTime은 지연을 고려한 서로 다른 시간이며 임의로 혼용하지 않는다. 서버 기준 이벤트·카운트다운에는 명시적 기준 시각을 공유하고 로컬에서 남은 시간을 계산하는 설계가 유용하다.

render frame, physics fixed step, network tick은 서로 다른 주기다. tick rate를 높이면 더 자주 처리·전송할 수 있지만 지연과 jitter가 자동 해결되지는 않는다. 공식 문서는 물리 사용 시 tick과 FixedUpdate 비율을 맞추는 방식을 권장한다. 프로젝트의 실제 Fixed Timestep도 확인한 뒤 변경한다.

NGO의 client anticipation은 전체 rollback/replay prediction을 완성해 주는 기능이 아니다. `AnticipatedNetworkVariable`/`AnticipatedNetworkTransform`은 미리 표시한 값과 권한 결과를 구분하고 결과가 다르면 보정할 수 있게 한다.

- `StaleDataHandling.Ignore`와 `Reanticipate`는 늦은 권한 데이터 처리 정책이다.
- `OnReanticipate`, `ShouldReanticipate`, smoothing을 기능에 맞게 사용한다.
- 시각적 예상이 서버 승인이나 명중 확정이 되지 않도록 한다.
- 완전한 이동 예측·재시뮬레이션·서버 rewind가 필요하면 별도 입력 기록과 검증 설계가 필요하다.

원문: `advanced-topics/{networktime-ticks,client-anticipation}.md`, `learn/{lagandpacketloss,ticks-and-update-rates,clientside-interpolation,dealing-with-latency}.md`.

## 14. 성능과 확장

병목은 오브젝트 수, observer 수, 메시지 빈도, payload 크기, 직렬화 CPU, GC, 물리 비용의 조합이다. 작은 예제의 LAN 성공만으로 목표 동시접속 수를 판단하지 않는다.

최적화 우선순위 권고:

1. 측정할 플레이어·적·탄환 수와 지연 조건을 정한다.
2. 불필요한 broadcast, 매 프레임 상태/문자열 전송, 반복 로그를 줄인다.
3. 관심 영역과 observer를 조정해 수신 대상을 줄인다.
4. 이동 threshold·tick·압축·보간을 함께 조정한다.
5. 풀링과 메모리 재사용으로 GC를 줄인다.
6. 필요할 때만 custom messages 또는 custom NetworkVariable을 도입한다.

Custom messages는 특정 NetworkObject에 묶이지 않는 named/unnamed 메시지다. 세션 단위 프로토콜에 유용하지만 등록·해제, schema, validation, delivery, late join 처리를 직접 설계한다. RPC보다 무조건 빠르다고 단정하지 않는다.

NetworkUpdateLoop 확장은 처리 stage 순서가 꼭 필요한 경우에 사용한다. 등록 후 해제를 누락하지 않는다. 고급 루프 제어를 도입하기 전에 일반 Behaviour 수명주기로 해결 가능한지 확인한다.

원문: `advanced-topics/message-system/custom-messages.md`, `advanced-topics/network-update-loop-system/`, `learn/ticks-and-update-rates.md`.

## 15. 검증과 문제 해결

아래는 네트워크 기능 변경 시 선택할 검증 목록이다. 문서만 수정하는 작업마다 전부 실행할 필요는 없다.

| 조건 | 핵심 확인 |
|---|---|
| Host + 원격 Client | 호스트 전용 성공·중복 효과 여부 |
| Dedicated server + Clients | 로컬 플레이어 없는 서버의 정상 동작 |
| Client 2명 이상 | 다른 플레이어 권한 침범·발신자 검증 |
| Late join | 기존 상태·위치·씬·장착 관계 복원 |
| Spawn/Despawn 반복 | 풀 재사용·중복 구독·참조 잔류 |
| 지연·jitter·손실 | 보간·유실·reliable backlog·사용자 반응 |
| 씬 전환 중 접속/해제 | timeout·로드 실패·준비 상태 |
| Disconnect/Reconnect | 데이터 복원·씬 중복·재시도 취소 |
| 목표 플랫폼 빌드 | Editor와 IL2CPP/실행 환경 차이 |

**테스트 값 예시:** RTT 100/200ms, 손실 1/5%, 낮거나 가변적인 FPS를 시험할 수 있다. 이는 공식 합격 기준이 아니라 게임 요구사항에 맞게 바꿀 실험 조건이다. simulator의 delay 입력이 편도인지 RTT인지 확인한다.

디버깅 로그에는 프로세스 역할, localClientId, senderId, ownerId, NetworkObjectId, spawn 상태, sequence/tick을 필요한 범위로 남긴다. 매 프레임 대량 로그는 문제를 더 악화시킬 수 있다.

| 증상 | 우선 확인 |
|---|---|
| 호스트만 정상 | 로컬 즉시 RPC·IsServer/IsClient 분기·호스트 포함 target |
| 늦은 참가자의 위치/UI 이상 | OnNetworkSpawn에서 현재 값 적용 여부 |
| RPC가 실행되지 않음 | 연결·spawn·observer·목적지·InvokePermission |
| 위치가 되돌아옴 | authority 불일치·복수 위치 writer |
| 프리팹 생성 실패 | 등록·hash·컴포넌트 구성·버전 일치 |
| 씬 전환 직후 참조 오류 | NGO 완료 이벤트·오브젝트 수명 |
| 클라이언트 충돌 판정 누락 | kinematic·Contact Pairs·authority |
| 트래픽/로그 폭증 | 상호 재귀 RPC·무제한 입력·broadcast |

원문: `tutorials/testing/`, `troubleshooting/`, `basics/logging.md`.

## 16. 현재 프로젝트 분석과 후속 적용

**기록 시점 주의:** 아래는 룰렛 프로토타입 도입 전 이동 예제의 분석이다. 이후 AI/PvP 1:1 게임으로 교체했으며 현재 구조·검증·실행 방법은 [룰렛 프로토타입 문서](../Roulette-Prototype.ko.md)를 참조한다.

다음은 C# 코드와 일부 직렬화 설정의 정적 확인 결과다. 플레이 실행으로 재현한 버그 목록은 아니다. 이번 문서 작업에서 게임 코드를 변경하지 않았다.

### HelloWorldPlayer.cs

- `Position`은 기본 권한의 NetworkVariable<Vector3>다. 서버 RPC가 랜덤 위치를 결정하므로 기본 서버 쓰기 정책과 맞는다.
- `OnNetworkSpawn`은 변경 이벤트만 구독하고 `Position.Value`를 현재 Transform에 적용하지 않는다. 늦게 참가한 클라이언트의 기존 플레이어 표시를 안정적으로 맞추려면 6절 패턴을 적용하고 검증한다.
- `SubmitPositionRequestServerRpc`는 `[Rpc(SendTo.Server)]`이고 발신자 검증이 없다. 기본 권한이 Everyone이므로 소유자만 요청한다는 보장이 없다.
- Move는 UI 외에도 호출 가능한 public 메서드다. 수신 서버에서 권한·요청 빈도 정책을 적용한다.
- 전용 서버의 전체 플레이어 이동 경로가 있으므로 Owner 권한을 추가하기 전에 서버 내부 실행과 요청 RPC를 분리한다.
- 현재는 순간 랜덤 이동이다. 연속 이동을 구현할 때 Position 변수와 NetworkTransform을 동시에 writer로 두지 않는다.

### HelloWorldManager.cs

- Host/Client/Server 버튼과 이동 버튼을 제공한다.
- 이동 처리에서 PlayerObject와 HelloWorldPlayer가 존재한다고 가정한다. 접속·동기화·해제 중 null/미스폰 상태를 처리할 필요가 있다.
- `IsClient`가 참이라는 사실만으로 로컬 PlayerObject가 준비되었다고 가정하지 않는다. 버튼을 플레이어 준비 상태와 연결한다.
- Start 호출의 반환값·비동기 실패·DisconnectReason 등 사용자 피드백을 보완할 수 있다.
- 전용 서버가 연결된 각 플레이어의 `Move()`를 호출한다는 점을 권한 수정 시 함께 고려한다.

### RpcTest.cs

- 원격 소유 클라이언트에서 시작한 요청에 서버가 응답하고, 소유 클라이언트가 다시 요청하는 왕복 반복 구조다.
- 종료 조건과 호출 간격이 없어 오브젝트가 활성인 동안 메시지·로그가 계속 발생할 수 있다.
- 테스트 전용으로 횟수·주기·활성 조건을 제한하고 실게임에서 동작하지 않게 하는 것이 좋다.
- 현재 시작 조건은 호스트 자신의 첫 요청을 막는다. 따라서 현재 코드가 무조건 호스트 stack overflow를 낸다고 단정할 수는 없다. 로컬 왕복 구조를 추가할 때 재귀 실행을 별도로 점검한다.

### 설정

- SampleScene: TickRate 30, ConnectionApproval 0, EnableSceneManagement 1.
- PlayerPrefab 참조가 직렬화되어 있고 `Assets/Prefabs/Player.prefab`이 존재한다.
- Multiplayer Play Mode 2.0.2가 manifest에 있다. 실제 여러 프로세스 테스트는 이번 작업에 포함하지 않았다.
- Unity Transport는 lock 기준 2.7.3이다. NGO package.json의 2.6.0 의존 선언을 설치 버전으로 잘못 기록하지 않는다.

**후속 권장 순서:** 초기 상태 적용 → RPC 호출 구조·검증 정리 → 무한 테스트 RPC 제한 → 연결 UI 준비 상태 → 원격/전용 서버/late join 검증 → 실제 이동 설계.

## 17. 2.13.3에서 특별히 기억할 점

설치 패키지 CHANGELOG의 2.13.3 항목(2026-09-14) 기준:

- half-float 위치 동기화의 이동 거리 누적에 따른 정밀도 저하를 개선했다. unreliable delta 사용 시 full precision 업데이트 빈도가 달라질 수 있다.
- NetworkTransform의 시간 기준, 프레임률에 따른 smoothing 차이, interpolation 설정과 정지 시 jitter 관련 수정이 있다.
- 세션 시작 전 additive 씬을 로드한 경우 unload 추적 문제를 수정했다.

따라서 과거 버전의 보간 문제를 우회하던 코드를 그대로 추가하기 전에 현재 버전에서 재현되는지 확인한다. RPC의 deprecated `RequireOwnership` 대신 `InvokePermission`을 사용한다. API 이름, Inspector 옵션, 기본값은 업데이트마다 다시 확인한다.

문서의 예제에는 과거 명명법·설명상 오타가 섞일 수 있다. 실제 설치 API와 모순되면 근거를 기록하고 실행 검증한다. 패키지 버전을 자동으로 변경하지 않는다.

원문: 패키지 `CHANGELOG.md`, `Runtime/Messaging/RpcAttributes.cs`.

## 18. 덜 자주 쓰는 기능과 원문 탐색

- **Deferred despawn:** DA 전용 tick 기반 despawn 지연이다. 현재 클라이언트-서버 프로젝트에 그대로 적용하지 않는다.
- **SinglePlayerTransport:** 싱글플레이에서도 네트워크 게임 로직을 활용하는 선택지다. 세션 시작·종료와 transport 전환 수명주기를 원문에서 확인한다.
- **Custom scene management:** 자동 동기화를 사용하지 않는 대신 prefab·씬·참가자 동기화를 직접 책임진다. 구체적인 요구가 없으면 통합 관리를 우선한다.
- **Custom NetworkVariable/delta serialization:** 기본 방식의 측정된 한계가 있을 때 검토한다. 초기 전체 동기화와 변경 delta 양쪽을 구현해야 한다.
- **Boss Room 등 샘플:** 권한·세션·풀링 패턴을 참고하되 샘플 의존 패키지와 버전을 프로젝트에 무조건 이식하지 않는다.
- **UNet 마이그레이션:** 기존 UNet 프로젝트에만 해당한다. 현재 NGO 신규 예제에 구형 attribute를 도입하는 근거로 쓰지 않는다.
- **플랫폼:** NGO 2.13 매뉴얼은 Unity 6.0+와 Mono/IL2CPP 지원을 안내한다. 실제 출시 플랫폼·Transport·서비스 제한은 빌드 대상으로 별도 확인한다.

전체 주제별 공식 링크는 [Sources.md](Sources.md)를 사용한다. 매번 전체 원문을 읽지 말고 작업에 직접 관련된 절과 API만 확인한다.

## 19. 현재 프로젝트의 동시 숫자 입력 대결 (2026-10-07 설계 결정)

사용자 요청으로 턴제 룰렛을 각자 리볼버 한 자루를 사용하는 동시 QTE 대결로 바꿨다. NGO API 변경이 아닌 프로젝트 규칙 변경이다. 각 좌석의 무작위 숫자 패턴·진행·단조 증가 Version·벌칙 종료 시각을 서버가 관리하고 하나의 스냅샷에 담는다. 상대 입력 때문에 내 요청이 낡은 것으로 거절되지 않도록 전역 Sequence와 입력 Version을 분리한다. 소유자 RPC가 전달한 숫자와 Version을 검증하고, 서버에 먼저 도착한 최종 유효 입력만 사격을 확정한다. 클라이언트는 입력 강조만 예상 표시하고 다음 동작·피해는 확정 상태를 따른다. AI도 동일 입력 함수를 호출한다. ProtocolVersion=3이며 양쪽 실행 빌드를 함께 갱신해야 한다. 상세 규칙과 검증은 `../Roulette-Prototype.ko.md`를 따른다.

### 19.1 4인 즉사 생존전 확장 (2026-10-07)

현재 설계는 PvP 최대 4명, 한 발 즉사, 최후 생존자 승리다. Connected/Alive/Ready를 분리해 사망한 연결도 다음 경기의 준비 대상에 포함한다. 승인 예약을 포함해 정원을 검사하고 좌석을 동적으로 할당한다. 진행 중 참가를 거절하며 대기/종료 후 빈 자리에 참가하면 전원의 준비를 초기화한다. 이탈은 실제 좌석의 연결·생존 비트만 제거한다. 모든 연결이 준비했고 최소 2명이 있어야 시작한다. AI 준비도 동일 상태를 사용한다. 사격은 5자리 입력 두 번이며 대기 중인 두 번째 패턴은 첫 번째를 완료한 뒤 생성한다. 사망 시각과 좌석별 목표를 스냅샷으로 전달하고 로컬 3D가 쓰러짐을 재구성한다. ProtocolVersion=4. 이는 프로젝트 설계 결정이며 NGO 기본 규칙이 아니다.

### 19.2 닉네임 동기화

접속 payload의 닉네임을 서버에서 UTF-8 길이/형식 검사 후 정리한다. 성공적으로 등록한 사람의 순번을 방 단위로 증가시키고 빈 닉네임은 `player{순번}`으로 확정한다. 좌석 재사용과 이름 순번을 분리한다. 이름 네 개는 `FixedString64Bytes`로 스냅샷에 포함한다. 늦은 참가자의 초기 상태에도 이름이 포함되고 재대전에서는 보존된다. 화면의 머리 위 표시는 서버 이름과 로컬 머리 위치에서 파생한다. 인증/고유 계정명 기능은 아니다. ProtocolVersion=5.

### 19.3 행동별 6자리 단일 세트 (2026-10-07 최신 결정)

19.1의 사격 2세트 규칙을 대체한다. 집기·장전·조준·사격 모두 1~5 숫자 6개를 한 세트 입력하며, 사격의 여섯 번째 정답에서 서버가 즉시 사격을 판정한다. FireBatch 필드를 제거했고 UI도 대기창 없이 현재 세트만 표시한다. AI는 같은 입력 함수를 사용하며 권한·발신자·Version 검증은 유지한다. 직렬화 형식 변경으로 ProtocolVersion=6이며 모든 참가자가 최신 빌드를 사용해야 한다.

### 19.4 조준 완료 시 무작위 대상 (19.17로 대체)

조준 6자리 입력 완료로 Fire 단계에 진입할 때 서버가 자신을 제외한 생존 상대 중 하나를 무작위로 선택한다. 그 전 Target=-1이며 총 겨냥/붉은 경고도 시작하지 않는다. 상대 사망·이탈 시 Fire 단계의 생존자만 다시 무작위 대상을 지정한다. 수동 대상 선택 UI와 RPC를 제거했다. AI도 같은 경로를 사용한다. 상단 HUD를 제거하고 경기 입력창은 행동 이름과 숫자만 표시한다. RPC 구성 변경으로 ProtocolVersion=7.

### 19.5 킬로그 (2026-10-08)

킬로그는 서버 Publish에서 새 사격 확정 시 각 플레이어의 Reliable `[Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]`로 전달한다. NetworkVariable의 모든 중간 상태를 받을 것이라고 가정해 기록을 만드는 대신 순간 사건을 RPC로 알린다. 경기 번호/사격 번호와 확정된 FixedString 닉네임을 보내고 수신 화면에서 중복 제거·6초 표시·재경기 초기화를 처리한다. 서버/AI 모두 동일하며 서버 자체에 소유 화면이 없는 경우 로컬 UI를 요구하지 않는다. ProtocolVersion=8.

### 19.6 AI 상대 수 선택 (2026-10-08)

AI전은 사람 좌석 0과 AI 좌석 1~3을 사용한다. 메뉴에서 상대 1~3명을 선택하고 연결/생존/준비 비트마스크와 기존 4인 스냅샷으로 표현한다. 각 봇은 서버에서 독립적인 입력 스케줄을 갖고 동일한 Input/사격 검증을 거친다. 사격 후 전체 정지 처리는 19.9에서 제거했다. 탈락한 봇도 재경기 준비를 자동 완료하며 사람 준비가 필요하다. AI는 로컬 표시 인물이며 NetworkObject를 별도 스폰하지 않는다. AI전 원격 접속 거절 및 PvP 2~4인 규칙은 유지한다. 당시 네트워크 형식/API 변경은 없었으며 ProtocolVersion=8.

### 19.7 로컬 UDP 포트 충돌 처리 (2026-10-08)

실제 Editor 로그의 `address is already in use`와 UDP 7777의 Unity 프로세스 점유를 확인했다. IPv4 Any UDP exclusive bind로 요청 포트의 사용 가능 여부를 사전 확인하고, 충돌/예약이면 근처 32개 후보와 OS 임시 포트 순으로 선택한다. 확인용 소켓은 즉시 폐기하고 UnityTransport에 실제 포트를 설정한다. 사전 확인과 실제 bind 사이에 다른 프로세스가 점유하는 경쟁 가능성은 남으므로 StartHost 실패/ShutdownInProgress 검사를 유지한다. 기존 점유 프로세스를 종료하지 않는다. UI에 실제 포트를 표시하고 클라이언트는 해당 포트를 명시해 접속한다. 원문 API는 설치 패키지 UnityTransport.SetConnectionData/ServerBindAndListen 기준이며 ProtocolVersion=8을 유지한다.

### 19.8 자신의 사망 중앙 알림 (2026-10-08)

Reliable 처치 RPC에 피해 좌석을 추가했다. 서버가 확정한 처치자 이름과 피해 좌석으로 자신의 사망만 판별하므로 닉네임 중복에도 안전하다. 사망 알림은 중앙에 4초 표시하되 Finished 상태의 최후 생존자 승리 알림이 우선한다. 승리 발표가 끝난 경기의 늦은 처치 RPC는 사망 알림을 재생하지 않는다. RPC 시그니처 변경으로 ProtocolVersion=9.

호스트와 모든 원격 클라이언트는 각자의 소유 플레이어 좌석을 기준으로 판별한다. Unity Windows 빌드와 실제 호스트·원격 클라이언트 2개의 3인 대결에서 원격 참가자의 사망 알림, 생존자 알림 제외, 승리 우선 전환, 만료 및 재경기 초기화를 확인했다.

### 19.9 사격 후 참가자별 입력 유지 (2026-10-08)

사격 후 생존자가 둘 이상이면 Racing을 유지하며 사격자만 즉시 Pickup으로 전환한다. 다른 생존자의 행동·진행·Version·벌칙과 AI 입력 일정은 유지한다. 사망한 목표를 겨누던 Fire 참가자만 서버에서 즉시 생존 상대를 다시 지정하며 입력 진행은 보존한다. 사망 및 경기 종료 입력 차단, 서버 판정과 소유자 RPC 검증은 유지한다. AfterShot은 마지막 생존자 확정 후 준비 화면 전환에만 사용한다.

좌석별 FiredAt/FiredTarget을 스냅샷에 포함해 연속 사격의 반동·섬광이 전체 경기 단계나 가장 최근 사격에 종속되지 않도록 했다. 피격·쓰러짐은 기존 좌석별 DiedAt으로 재생한다. 서버의 ShotConfirmed 이벤트마다 즉시 Publish하므로 여러 AI가 같은 틱에 사격해도 각 처치 RPC를 누락하지 않는다. 지속 상태를 순간 사건 기록으로 취급하지 않는다. 직렬화 변경으로 ProtocolVersion=10이며 패키지 버전은 유지한다.

Unity Windows 빌드 및 실제 호스트·원격 클라이언트 3개의 4인 대결에서 생존 원격 참가자들의 사격 직후 입력 진행, 탈락자 입력 차단, 처치 로그 3건, 승리·전원 준비 재경기를 확인했다. 규칙 검사에서는 생존자 Version/진행/벌칙 유지, 0.01초 간격 연속 사격과 AI 같은 틱 사격 3건의 개별 이벤트를 확인했다.

### 19.10 단발 총기 선택·누적 피해 (2026-10-08)

각 좌석은 준비 전 단발 데린저/원배럴 소드오프를 선택한다. 소유자 전용 SelectWeaponRpc에서 실제 SenderClientId를 검사하고 서버가 발신자 좌석·경기 번호·유효 enum·Idle·미준비를 검증한다. 선택은 좌석 상태의 Weapon으로 유지하며 새 스폰/재경기 초기 상태에도 포함한다. 새 입장자의 기본 무기는 데린저이며 재경기는 이전 선택을 유지한다. AI 기본 무기는 데린저이고 사람은 AI전에서도 선택할 수 있다.

데린저는 기존 4행동·각 6자리·직격 100 피해를 유지한다. 소드오프는 Pickup→OpenBarrel→Load→CloseBarrel→Aim→Fire의 6행동·각 4자리다. 서버가 최대 체력 100에 직격 50, 주 목표의 물리적 양옆 좌석에 각각 25를 누적 적용한다. 사격자 자신·빈 자리·사망자는 스플래시에서 제외한다. 모든 피해를 계산한 뒤 AliveMask/승리를 확정해 한 발의 여러 사망을 원자적으로 처리한다. 재경기는 연결된 전원의 체력을 100으로 복원한다.

무기·체력·HitAt은 좌석 스냅샷으로 직렬화한다. 비치명적 피격은 HitAt 연출만 갱신하며 해당 생존자의 입력 Version/진행/벌칙을 바꾸지 않는다. 사망은 DiedAt과 생존 비트로 처리하고 기존 얼굴 추적·관전·중앙 알림을 유지한다. 표시 모델은 로컬 3D로 재구성하며 서버가 권한을 갖는 판정과 분리한다. 두 총 모두 단일 배럴/한 발 삽입이며 별도 NetworkObject는 추가하지 않았다.

각 확정 사격의 LastKilledMask를 순회해 실제 사망자마다 Reliable 소유자 RPC를 보낸다. RPC에 판정 당시 무기를 포함해 이후 무기 변경과 무관하게 킬로그 이미지를 확정한다. 수신 중복 제거 기준은 경기/사격/피해 좌석이며 같은 사격 번호의 여러 처치를 허용한다. 비치명적 사격은 킬로그를 만들지 않는다. 직렬화 및 RPC 변경으로 ProtocolVersion=11, 패키지는 유지한다.

Unity Windows 빌드, 피해·물리적 이웃·무기 단계·준비 잠금 등 규칙 506개 검사, 기존 연속 입력 검사, 실제 호스트+원격 3명의 4인 대결 및 AI 3명 대결을 통과했다. 원격 선택/초기 상태, 진행 중 변경 거절, 피격 후 입력 유지, 한 발 3명 사망의 개별 킬로그·승리 우선, 데린저 즉사/이미지, 전원 준비 재경기 체력/무기 보존을 확인했다.

### 19.11 데린저 행동 비용 조정 (2026-10-08)

19.10의 데린저 4행동을 8행동으로 대체한다. 단발 데린저 제조사 조사와 설계 근거는 `../Weapons/Derringer-Balance.ko.md`를 따른다. Pickup→UnlockBarrel→OpenBarrel→Load→CloseBarrel→CockHammer→Aim→Fire이며 각 6자리, 총 48개다. 소드오프는 기존 6행동·각 4자리·총 24개다. 피해량은 유지하며 데린저 한 발과 소드오프 직격 두 발의 기본 처치 입력 비용을 같게 맞춘다. 추가 전역 지연 없이 기존 좌석별 Version/진행/벌칙 검증을 사용한다.

UnlockBarrel/CockHammer enum을 기존 값 뒤에 추가했으며 새 필드나 RPC는 필요하지 않다. 서버의 NextAction으로 진행하고 AI도 같은 경로를 사용한다. Target은 Aim 완료 뒤에만 생성하므로 잠금 해제·장전·격철 단계에서 겨냥이나 경고가 켜지지 않는다. 로컬 모델은 선택한 무기와 행동/진행에서 잠금 레버·배럴·격철 자세를 파생한다. 지속 상태에 있으므로 초기 수신에서도 현재 자세를 복원하고 새 NetworkObject를 만들지 않는다. 구버전 행동 규칙의 혼용을 막기 위해 ProtocolVersion=12로 변경했다.

Unity Windows 빌드와 무기 규칙 538개 검사 및 연속 입력 검사를 통과했다. 실제 호스트+원격 3명의 4인 대결에서 데린저 8단계/즉사/킬로그/재경기와 소드오프의 기존 피해·다중 킬로그·생존자 입력 유지를 확인했다. AI 3명 대결의 입력·승리·체력 및 선택 보존 재경기도 통과했다.

### 19.12 최대 6인·원형 테이블 (2026-10-08)

이 절이 이전 4인 인원 제한과 좌석 배치를 대체한다. PvP는 2~6명, AI전은 사람 1명과 AI 1~5명이며 모두 연결된 전원이 준비해야 시작한다. 서버의 승인 예약까지 포함한 상한, 빈 좌석 배정, 독립 입력/AI 배열, 이름과 사망 통지를 6개 좌석으로 확장했다. 기존 소유자 RPC 권한과 실제 발신자·경기 번호·준비·생존·Version 검증을 유지한다. 경기 중 신규 참가는 거절하며 빈자리 재참가자는 새 PlayerObject와 새 기본 무기를 받고 입장 순번은 재사용하지 않는다. 탈락자도 준비 대상이고 AI는 자동 준비한다.

DuelSnapshot은 값 타입의 Seat0~Seat5 및 Name0~Name5를 고정 순서로 직렬화하고 동등성 비교에도 모두 포함한다. 지속 상태를 새 스폰에 즉시 적용하는 흐름을 유지하며 원격 참가자의 마지막 두 좌석도 같은 전체 상태를 받는다. 이 데이터 형식 변경으로 ProtocolVersion=13이다. 구버전과 혼용하지 않으며 NGO/Transport 버전은 변경하지 않았다. 호스트가 떠나면 기존처럼 방이 종료되고 전용 서버 전환이나 호스트 이전을 추가하지 않았다.

테이블은 지름 7.5의 원형이고 좌석은 반지름 4.2, 60도 간격이다. 최초 6인 구현의 물리적 이웃은 `(seat + 5) % 6`, `(seat + 1) % 6`이었으나 끝/처음의 스플래시 연결은 아래 19.13의 사용자 수정으로 제거했다. 스플래시는 빈자리·탈락자를 건너뛰지 않으며 사격자 본인을 제외한다. 한 발의 모든 피해 뒤에 승패를 확정한다. 테이블·총기·인물·닉네임·카메라는 서버 스냅샷에서 파생한 로컬 표현으로 추가 NetworkObject나 Transform RPC 없이 표시한다. 6개 좌석의 기본/관전 카메라가 방 안에 있도록 공간을 확장했다.

Unity Windows 빌드, 스냅샷 동등성/좌석 기하 검사와 규칙 9,495개 검사를 통과했다. 실제 호스트+원격 5명에서 6인 초기 상태·7번째 접속 거절·빈자리 재참가·5/6번 이름/무기/사격·순환 스플래시·다른 생존자의 입력 보존·사망 통지/승리 우선·탈락자 포함 전원 준비 재경기를 확인했다. 여섯 시점 모두 다섯 상대가 화면에 들어오는 것을 검사했다. 별도 AI 5명 대결도 경기 종료와 전원 준비/체력 복원을 통과했다.

### 19.13 스플래시 1번/6번 연결 제거 (2026-10-08)

원형 공간 배치는 유지하되 스플래시 이웃은 유효 좌석의 번호 차이가 1일 때만 인정한다. 화면 기준 1번 목표는 2번에만, 6번 목표는 5번에만 번진다. 2~5번 목표는 이전/다음 번호에 적용한다. 빈자리·탈락자·사격자 제외와 피해 누적/동시 승패 판정은 유지한다. 이 규칙은 서버의 공통 WeaponRules/RouletteRules 경로에서 AI와 PvP에 동일하게 적용하고 결과 체력·생존 상태를 기존 스냅샷으로 전파한다. RPC 권한·발신자 검사·스폰/초기 상태 구조는 변경하지 않았다. 참가자 빌드의 규칙 혼용을 막기 위해 ProtocolVersion=14로 변경했다.

Unity Windows 빌드와 규칙 9,495개 검사를 통과했다. 모든 참가 좌석 조합·사격자·목표의 끝 번호 미연결 및 내부 양옆 피해, 사격자/빈자리 제외, 다중 사망의 원자적 승리, AI 각 인원/난이도와 전원 준비 재경기를 확인했다.

### 19.14 데린저 오입력 패턴 재생성 (2026-10-12)

데린저에서 잘못된 1~5 숫자를 입력하면 서버가 현재 행동의 Progress를 0으로 초기화하고 Code의 6개 자리를 각각 이전 숫자를 제외한 4개 숫자 중에서 다시 뽑는다. Action/Target은 유지하며 사격을 발생시키지 않는다. 기존 Mistakes 증가·Version 증가·0.45초 벌칙과 발신자/생존/숫자 범위 검증을 유지한다. 다른 좌석의 진행/Version/패턴·AI 일정은 수정하지 않는다. AI도 같은 Input 경로를 사용하고 소드오프는 기존 패턴으로 재시도한다.

새 Code는 기존 서버 쓰기 NetworkVariable 스냅샷에 포함해 전파한다. 클라이언트는 오입력의 로컬 표시 이후 새 스냅샷을 기다리고, 이전 패턴의 숫자를 잠시 노출하거나 입력하지 않는다. 새 패턴을 받으면 첫 숫자만 보이고 이후 칸은 빈칸이다. 오래된 Version의 RPC는 새 패턴에서도 거절된다. 새 RPC/직렬화 필드는 없으며 spawn/late join 상태 흐름은 유지한다. 규칙 혼용 방지를 위해 ProtocolVersion=15로 변경했다.

Unity Windows 빌드 및 규칙 13,552개 검사를 통과했다. 실제 호스트/원격 클라이언트 두 프로세스에서 8행동 각각의 재생성·새 패턴 동기화·기존 조준 유지·숫자 표시/빈칸·0.45초 벌칙·오래된 Version의 RPC 거절을 확인했다. 공통 규칙 검사에는 다른 좌석 상태 보존, 소드오프 패턴 유지와 AI 각 인원/난이도의 경기·재경기도 포함했다.

### 19.15 경기 통계와 지속 결과 화면 (2026-10-12)

서버의 DuelDirector.Input이 승인한 정답/오입력만 집계하며 정답 수·현재/최장 연속 정답을 좌석 상태에 보관한다. 범위·Version·벌칙·생존 검사에서 거절된 요청은 집계하지 않는다. 정답 연속은 행동 전환에도 이어지고 오입력에서 끊긴다. 성공한 Shoot 이후 LastKilledMask의 실제 사망자 수만 사격자의 Kills에 더한다. 한 발 여러 사망도 모두 포함하며 이탈에는 킬을 주지 않는다. TryStart에서 경기 통계를 초기화하고 새 Join도 이전 좌석의 통계를 재사용하지 않는다.

시작 당시 참가 비트마스크를 별도로 유지하며 종료 시 HelloWorldManager.Publish가 DuelRoundResults 값 타입을 한 번 고정한다. 이름·오입력·킬·정답·최장 연속·승자·경기 시간을 6개 고정 필드로 캡처해 DuelSnapshot에 포함한다. 이탈/늦은 참가/자리 재사용/무기 변경/준비 상태 변화에도 고정된 결과는 바뀌지 않고 다음 Round 시작에서 지운다. 기존 서버 쓰기 NetworkVariable의 초기 상태에 포함하므로 늦은 참가자도 동일한 결과를 받는다. 경기 중 참가 거절, 발신자/소유자 검증 및 PlayerObject 수명주기는 유지한다. 통계와 결과의 직렬화/동등성 필드를 함께 확장했으며 ProtocolVersion=16이다.

클라이언트는 승리 문구를 중앙에서 상단으로 이동시킨 뒤 결과표를 표시하고 다음 경기까지 둘 다 유지한다. 뒤늦게 들어오면 이동 없이 곧바로 표시한다. 결과표 아래 기존 소유자 준비 버튼과 무기 선택을 재배치하며 Idle/준비 상태 검증은 서버에서 유지한다. 입력 정확도는 정답/(정답+오입력)이고 입력이 없으면 수치 대신 `—`를 표시한다. 개인 사망 알림의 4초 표시 및 승리 우선은 유지한다.

Unity Windows 빌드, 규칙 13,906개 검사 및 결과/통계 스냅샷 직렬화 왕복 검사를 통과했다. 실제 호스트+원격 5명에서 개인 통계 동기화·4초 이후 패널 유지·준비 위치·무기 변경/재입장의 고정 결과·늦은 참가자의 즉시 표시·전원 준비 재경기 초기화를 확인했다. AI 5명 경기 및 결과 화면에서도 같은 통계와 준비/초기화를 확인했다.

### 19.16 실제 기록 하이라이트 (2026-10-12)

결과표의 최장 연속 열을 제거하고 승리→하이라이트→결과표 순서로 표시한다. 승리와 하이라이트는 각각 중앙 0.8초 유지/상단 0.6초 이동이며 하이라이트는 승리 아래에 자리 잡는다. 세 패널은 다음 경기까지 유지하고 늦은 참가자는 이동 없이 바로 표시한다. 무기 선택은 항상 펼쳐진 상태다.

서버는 Damage에서 남은 체력 이하의 실제 감소량을 합산하고, 성공한 Shoot에서만 사격 수·최대 한 발 처치 수를 집계한다. 죽은 대상·빈자리·사격자에게 스플래시 피해를 중복 집계하지 않고 과잉 피해도 제외한다. 기존 RPC 발신자/좌석/생존/Version 검증을 유지하며 클라이언트가 피해나 하이라이트 문구를 제출하는 RPC는 없다.

종료 캡처에 실제 피해·사격·한 발 다중 처치·잔여 체력·실제 사망 여부를 포함하고, 서버가 해당 경기에서 성립하는 문구 하나를 FixedString512Bytes로 고정한다. 피해를 주고도 처치 없음, 다중 처치, 적은 체력으로 승리, 입력 없이 사망, 완벽 입력 승리, 반복 오입력 중 해당 사례를 사용하며 없으면 실제 종료 시간을 사용한다. 실제 없는 칼이나 발생하지 않은 사건은 만들지 않는다. 문구와 기록은 기존 서버 쓰기 스냅샷으로 동기화해 이탈/재입장/준비 중에도 유지하고 다음 경기에서 초기화한다. 새 필드의 직렬화와 동등성 비교를 함께 추가했으며 ProtocolVersion=17이다. NGO 패키지와 스폰/초기 상태 수명주기는 변경하지 않았다.

Unity Windows 빌드, 규칙 13,908개 검사, 사실 조건별 하이라이트 및 스냅샷 직렬화 왕복 검사를 통과했다. 실제 6인 PvP와 AI 5명 경기에서 동일 문구 수신·순차 연출·유지·4개 결과 열·항상 표시되는 무기 선택·늦은 참가·전원 준비 재경기 초기화를 확인했다.

### 19.17 수동 1인칭 사격과 고정 체력 피해 (2026-10-12 최신 결정)

기존 숫자 조준/사격과 무작위 대상 규칙(19.4/19.11)을 대체한다. 데린저는 장전 6행동×6자리, 소드오프는 4행동×4자리 이후 Aim(장전 완료)에서 마우스로 조준/발사한다. 최대 체력 100, 머리/몸통/다리 고정 피해는 데린저 100/50/35·소드오프 75/50/35다. 소드오프 이웃 피해는 25이고 번호 1/6 비연결·사격자 제외 규칙을 유지한다. 빗나간 발도 소모하며 사격자만 즉시 집기로 돌아간다. 다른 참가자의 숫자 Version/진행/벌칙은 변하지 않는다.

Owner 권한 LookRpc는 Unreliable, 클라이언트 전송 20Hz이며 서버가 동일 ADS 상태에서 최대 30Hz로 제한한다(전환 최소 간격 0.01초). 발신자·현재 경기·생존·숫자 범위 아닌 유한한 각도/피치 ±65도를 검증한다. 각도는 서버 쓰기 스냅샷에 포함하며 Numeric Version을 바꾸지 않는다. ADS가 0.35초간 갱신되지 않으면 경고를 해제한다. AI는 서버에서 동일한 Look/Fire 경로를 사용한다.

Owner 권한 Reliable FireRpc는 경기 번호·해당 좌석 Version·장전 완료·생존·각도·서버 시간 기준 발사 시각(-0.3~+0.05초)을 검증한다. 발사에 포함된 각도는 Look 패킷 손실/순서 변경과 독립적으로 적용한다. 서버가 고정된 자기 눈 위치에서 공유 시간 기반 흔들림과 서버 난수 탄 퍼짐을 더하고, 머리 구체·몸통/다리 좌석 공간 상자와 원형 테이블·바닥·벽·의자 가림을 직접 계산한다. 클라이언트가 대상/명중 부위/피해를 제출하는 경로는 없다. 시각 바운드는 흔들림 표시를 맞추기 위한 것이며 움직이는 몸체의 과거 상태 되감기는 제공하지 않는다. 현재 착석 모델은 정적 판정 영역이며 팔/피격 연출은 표현용이다.

조준 방향·ADS 시작 시각·마지막 명중 부위를 INetworkSerializable 및 동등성 비교에 함께 추가했다. ProtocolVersion=18, 패키지 변경 없음. 지속 상태는 기존 서버 쓰기 NetworkVariable로 늦은 참가 시 초기 복원하며 경기 중 참가 거절·PlayerObject 스폰/디스폰·준비 수명주기는 유지한다. 총/몸/의자는 로컬 절차 모델이며 네트워크 Transform writer를 추가하지 않는다. 조준/사격은 서버 게임 로직, 카메라/검은 가장자리/붉은 경고는 각 소유 클라이언트의 표현으로 분리해 전용 서버에 카메라를 요구하지 않는다.

검증: 현재 규칙 544개 검사, 최신 Unity Windows 빌드, 조준/명중 부위 및 결과 스냅샷 직렬화 왕복/동등성 통과. 실제 그래픽 호스트+원격 5명에서 소유자 마우스 이동/우클릭 홀드·해제/좌클릭 한 발 발사, 각 좌석 FPS 시점과 두 무기 아이언사이트·줌, 고정 피해/스플래시·빗나감, 경고 감소, 다른 사람 사격 중 숫자 진행 유지, 원격 사망·얼굴 카메라/관전, 결과/재경기를 확인했다. AI 5명 실제 경기·자동 준비·재경기도 통과했다. 인터넷 고지연/패킷 손실 및 악성 호스트 방어는 미검증이다. 몸체는 절차 생성 프로토타입이며 움직임에 따른 판정 영역 변화는 제공하지 않는다.

### 19.18 소유자 사망 내역과 장전 시점 안정화 (2026-10-12)

장전 단계에서 DuelFirstPerson.Rotation은 사용자 마우스 각도만 적용하고 Aim(마우스 사격)에서만 조준 흔들림을 더한다. 크로스헤어 UI/스타일을 제거했다.

RouletteRules는 각 사격에서 좌석별 실제 체력 감소량을 별도로 기록한다. HelloWorldManager.Publish는 새 사격 번호당 한 번만 해당 좌석의 서버 내역에 피격 당시 공격자 이름·총기·실제 직격 부위 또는 스플래시·피해량·치명타 여부를 추가한다. 빗나감/거절/중복 Publish는 내역을 추가하지 않는다. 사망 순간 전체 기록을 소유 플레이어의 서버 쓰기/Owner 읽기 NetworkVariable<DuelDamageReport>로 전달한다. 현재 최대 체력 100·최소 피해 25·회복 없음에 따라 최대 4개 고정 필드를 사용한다. 최소 피해/회복 규칙을 변경할 때는 기록 용량도 함께 재설계해야 한다.

클라이언트는 자기 사망 시각과 내역의 경기 번호가 모두 일치할 때만 왼쪽 패널을 표시한다. 결과표는 화면 가로 중앙에 유지하며 사망 내역이 보이면 좌우 여백을 대칭으로 적용하고 왼쪽 내역의 폭을 조절해 겹침을 방지한다. RPC와 변수의 도착 순서를 전제로 하지 않는다. 해당 객체의 초기 상태도 매 프레임 읽으므로 이벤트만 구독해 초기 값이 누락되는 문제를 피한다. AI 사람 좌석도 같은 경로이며 전용 서버는 카메라 없이 내역을 확정한다. 새 경기에서 서버 내역/소유자 변수 모두 초기화하고 새로운 객체/좌석 참가자는 지난 소유자의 내역을 재사용하지 않는다. 공격자 이탈/이름 변경은 이미 기록한 FixedString 이름을 수정하지 않는다. NGO 2.13.3 유지, PlayerObject 데이터 필드 추가로 ProtocolVersion=19다.

검증: 규칙 552개 검사, Unity Windows 빌드, 4건 피해 기록 직렬화 왕복/동등성/피격 당시 이름 고정을 통과했다. 실제 호스트+원격 5명에서 숫자 단계 시점 안정화와 크로스헤어 제거, 원격 소유자별 부위/총기/공격자/실제 피해 전체 내역, 혼합 공격자와 치명적 스플래시, 결과표 비겹침과 다음 경기 기록 초기화를 확인했다.
AI 5명 실제 경기에서도 사람의 사망 내역·결과·자동 준비·재경기를 확인했다.

### 19.19 희귀한 실제 기록을 우선하는 하이라이트 (2026-10-12 설계 결정)

DuelHighlights.Select가 고정된 DuelRoundResults의 기존 필드에서 후보를 만들고 희귀도 우선순위로 한 사건을 고른다. 최대 체력(현재 100) 미만 무킬 피해, 3발 미만 피해 0, 일반적인 무실수와 체력 25 같은 흔한 사례를 박제 후보로 삼지 않는다. 최대 체력(현재 100) 이상 무킬 누적 피해·탈락자의 3발 이상 피해 0·한 발 다중 처치·적은 피해로 여러 킬·체력 10 이하 우승 등 강한 사실을 우선하고, 오입력/입력 없음/완벽 입력/빠른 경기 등에는 횟수·정확도·인원·실제 처치 조건을 함께 둔다. 사건이 없으면 인원/총 사격/실제 시간만 표시한다. 이는 게임 설계 조건이며 NGO API 사실과 구분한다.

새로운 클라이언트 제출 값이나 직렬화 필드는 없다. 서버의 종료 캡처 시 한 번 선정한 FixedString512Bytes를 기존 스냅샷에 고정하며 이탈·늦은 참가·준비 중에도 다시 선정하지 않는다. 표현 변형도 서버가 경기/좌석 번호로 결정하고 저장하므로 클라이언트별로 다른 문구를 만들지 않는다. 권한/RPC 발신자 검증/스폰 수명주기와 ProtocolVersion=19는 유지한다.
검증: 실제 결과/스냅샷 자료형을 사용하는 37개 임계값·실제 사실·후보 우선순위·직렬화 왕복/종료 문구 고정 검사 및 Unity Windows 빌드 통과. 결과 UI 가로 중앙/사망 내역 비겹침을 960·1200·1600 너비에서 렌더 확인했다.
사용자 정정: 무킬 누적 피해의 기준은 고정 150이 아닌 RouletteRules.MaxHealth(현재 100)다. 99 제외·100 포함과 양쪽 문구 및 직렬화/기존 희귀도 검증까지 40개 검사를 Unity Editor에서 통과했다.

### 19.20 공유 총기 초읽기 모드 (2026-10-12 설계 결정)

기존 총격전과 병행하는 DuelVariant.Countdown을 추가했다. 서버가 각 초읽기의 목표7~15초, 공통 시작시각, 숨김 시각2.4~2.9초를 고른다. 지속 상태는 기존 NetworkVariable<DuelSnapshot>으로 전달하며 서버 내부의 정지 기록은 Counting 동안 전부 가린다. 정지 요청만으로 공개 Sequence를 증가시키지 않아 다른 사람의 Look 갱신에 정지 횟수가 새어 나오지 않게 한다.

소유자 전용 StopClockRpc는 경기/초읽기 번호와 NGO ServerTime 표본만 제출한다. 서버가 실제 SenderClientId와 OwnerClientId, 현재 연결된 객체의 좌석·생존·단계·중복·시각 범위를 검증한다. 표본은 수신시각-0.3~+0.05초 이내만 허용하며 기록을 소수점 두 자리로 반올림한다. 이는 프로토타입 지연 보정이며 클라이언트 자동화/악성 호스트에 대한 완전한 부정행위 방지가 아니다. 승인 응답은 해당 객체 Owner에게 reliable RPC로 보내고 클라이언트는 경기/초읽기 번호가 일치하는 응답만 적용한다.

서버가 절대 오차와 동점 d6 재굴림으로 모든 순위를 확정해 한 스냅샷으로 공개한다. 1등만 Aim 단계로 전환하며 4초/한 발 제한과 꼴찌 생존을 서버가 검사한다. FireRpc는 기존 서버 부위/가림/흔들림/퍼짐을 적용하되 꼴찌 외의 피격을 빗나감으로 취급한다. 사격 기회마다 Version이 달라 과거 요청을 재사용할 수 없다. 상대 이탈은 참가 명단에서 제거하며 1등/꼴찌 이탈은 진행 중인 사격 기회를 취소한다. AI 판정도 같은 규칙 클래스에 들어간다.

테이블의 총기 모델은 각 클라이언트가 하나만 표시하고 사격 기회 동안 1등의 기존 총기 리그로 표현한다. 추가 NetworkObject나 NetworkTransform을 만들지 않는다. 초기/늦은 참가 상태는 기존 PlayerObject의 서버 쓰기 변수로 복원하고 경기 중 참가 거절·스폰/디스폰·전원 준비 정책은 공통이다. 전용 서버의 시간/순위/사격 판정은 카메라/UI를 요구하지 않는다. 모드/초읽기 필드 및 결과 통계 변경으로 ProtocolVersion=20이며 NGO2.13.3은 유지한다. 상세 게임 시간 제한과 결과 표시는 Docs/Roulette-Prototype.ko.md 참조.

검증: 초읽기 규칙1,100개, 기존 총격전552개, 실제 Unity 스냅샷/동등성/통계 직렬화 및 하이라이트40개 검사와 Windows 빌드 통과. 그래픽 호스트+원격5명과 별도 AI5명에서 비공개 정지 기록·동점 주사위·공유 총1자루·1등의 마우스 사격/빗나감/비치명타 반복·원격 소유자 사망 내역·최종 결과 통계·탈락자 포함 전원 준비 재경기를 통과했다. 전용 서버 실기동 및 인터넷 고지연은 별도 검증이 필요하다.

### 19.21 파티 모드 선택과 초읽기 마감 자동 탈락 (2026-10-12 설계 정정)

상위 모드는 DuelVariant.Party이며 초읽기는 PartyMiniGame.Countdown이다. PartyGames는 구현된 미니게임만 열거하며 현재 후보는 하나다. 서버가 매 회 선택한 게임과 ClockPhase.Selecting의 시작 시각을 기존 스냅샷에 넣는다. 클라이언트는 공통 서버 시간으로 이름 릴을 감속시키고 중앙에 정지시키며, 서버만 선택 완료 후 초읽기를 시작한다. 화면 연출이나 카메라가 서버 진행을 좌우하지 않는다. 향후 후보 추가 시 해당 게임의 진행 로직도 함께 구현한다.

초읽기 마감은 목표+2초로 변경했다. UI는 그 시각 이후 입력을 막으며 서버는 마감 이전의 시간 표본에 대한 기존0.3초 수신 여유만 유지한다. 마감 처리에서 미정지 마스크를 확정한 뒤 RouletteRules.Eliminate로 한 번에 모두 HP0/생존 마스크 제거를 적용하고 마지막에 승패를 판정한다. 따라서 전원 미정지는 Winner=-1/AliveMask=0으로 종료하며 배열의 마지막 플레이어를 승자로 만들지 않는다. 단독 정지 생존자는 사격 없이 승리하고 두 명 이상 정지한 경우에만 그들의 1등/꼴찌에게 기존 사격 기회를 적용한다.

자동 탈락은 연결/PlayerObject/좌석을 유지한다. Shot 번호·총격 피해·킬·킬로그를 만들지 않으며 DiedAt 및 DuelDeathCause.ClockTimeout을 서버 쓰기 스냅샷으로 전달한다. 소유자 UI는 이 사인을 보고 사망 메시지/왼쪽 상세 패널을 표시하고, 이전 피격 내역이 있으면 기존 Owner-read DeathReport에 담아 보존한다. 빈 피격 내역도 시간 초과 사인을 표시할 수 있다. 쓰러짐/얼굴 카메라/관전은 기존 DiedAt 연출을 이용한다.

전원 탈락도 서버가 종료 결과와 실제 하이라이트를 고정하며 모든 클라이언트가 동일한 결과/전원 준비 재경기를 제공한다. 초읽기 순위 발표에는 현재 자동 탈락한 좌석을 잠시 남기되 사격 대상과 다음 선택의 생존 명단에서는 제외한다. 소유자 RPC의 권한·발신자·Round/Iteration/시간 검증과 늦은 참가 복원/진행 중 참가 거절 정책은 유지한다. 선택 게임·탈락 마스크·개인 사인 필드를 추가해 ProtocolVersion=21로 올렸고 패키지는 NGO2.13.3을 유지한다.

검증: 규칙1,157개 및 기존 총격전552개 검사, 실제 스냅샷의 선택/탈락 마스크/사인 직렬화와 동등성, 전원 탈락 결과, 하이라이트40개 검사 및 Unity Windows 빌드 통과. 그래픽 호스트+원격5명에서 호스트의 모드 선택/미니게임 릴/여러 원격 좌석의 동시 마감 탈락/정지 생존자끼리의 사격/단독 생존 승리/전원 탈락 결과/탈락자 포함 전원 준비 재경기를 확인했다. AI5명 실제 경기에서도 미정지 사람의 자동 탈락/자동 준비/재경기를 확인했다. 인터넷 고지연·전용 서버 실기동은 별도 검증 대상이다.

### 19.22 단일 파티 모드와 대난투 임시 사망/재참가 (2026-10-12)

상위 DuelVariant 분기를 제거했다. 모든 방은 파티 모드이며 서버는 첫 경기부터 PartyGames의 초읽기/대난투 중 하나를 무작위로 고르고 공통 Selecting 시각을 전달한다. 선택 연출이 끝나기 전에는 둘 다 진행하지 않는다. QuickHands/QuickResults는 미니게임 단계이며 독립 게임 모드가 아니다. 해당 단계에서만 숫자 입력/개인 총기의 자유 조준을 허용하고 초읽기에서는 기존 정지/1등→꼴찌 한 발을 유지한다. 서로의 단계 요청을 받을 수 없으며 전환 때 행동 Version을 바꾼다.

PartyAliveMask는 파티의 실제 생존 명단이고 AliveMask는 현재 미니게임의 살아 있는 몸이다. 대난투는 마지막 한 명까지 사격하되 첫 사망자만 영구 탈락시킨다. 같은 첫 사격에 여러 명이 사망하면 서버가 d6 재굴림으로 꼴찌 한 명을 확정한다. 생존 순위는 서버의 실제 LastKilledMask에서 기록한다. 대난투 마지막 사격 직후 Game의 미니 종료를 파티 종료와 구분하므로 임시 AliveMask=1에서 최종 결과를 캡처하지 않는다. 파티 생존자가2명 이상이면 미니 요약을3초 유지한 후 꼴찌를 제외한 참가자를 체력100으로 되살리고 다시 선택한다. 파티 생존자가1명/0명일 때만 최종 종료/전원 준비 정책을 적용한다. 초읽기의 사망/시간 초과와 연결 이탈은 실제 파티 명단에서도 제거한다.

새 생명마다 개인 Life를 증가시키고 DiedAt/HitAt/사인/부위 연출·행동 입력·마우스 캡처를 초기화한다. 선택 무기는 별도로 보관해 초읽기의 공유 데린저가 덮어쓰지 않는다. 죽은 캐릭터의 NetworkObject를 재생성하지 않고 기존 PlayerObject/서버 쓰기 스냅샷을 사용한다. 입력/조준 요청은 생존·단계·소유자·Version 검증을 유지하며 전용 서버 판정에는 UI가 필요 없다.

피격 내역은 파티 전체 통계와 분리해 각 Life에 최대4건을 보관한다(HP100, 생명 안에서 회복 없음, 최소 피해25). 서버는 부활한 Life의 보고와 내부 누적 내역을 비우고 소유자 읽기 변수에 Round/Life를 함께 넣는다. 클라이언트는 현재 Life와 맞는 내역만 표시한다. 킬 RPC에도 피해자의 Life를 넣어 부활 뒤 늦게 도착한 이전 사망 이벤트가 자신의 새 사망 메시지를 만들지 않게 한다. RPC가 변수보다 먼저 올 수 있으므로 중앙 사망 문구는 단순 Alive 여부로 지우지 않고 Life 변경 시에만 지운다. 파티 통계의 실제 킬/피해/입력/초읽기 성적은 부활해도 유지한다. 동일 인물의 재처치가 있으므로 하이라이트에서는 누적 처치를 n킬로 표현한다.

클라이언트-서버 권한/RPC 발신자/진행 중 참가 거절/늦은 참가 복원/종료 준비 정책은 유지한다. 단발 총기 모델은 초읽기/선택 중 한 자루, 대난투에서는 참가자별 한 자루로 표현한다. NPC도 같은 서버 단계/순위/임시 사망/복귀 규칙을 사용한다. PartyAliveMask·Life·선택 무기·보고 Life와 RPC 형식 변경으로 ProtocolVersion=22이며 NGO2.13.3은 유지한다.

규칙 검증: 초읽기1,157개, 총격/파티 반복923개, 파티 통합191개(32개 초기 시드에서 첫 선택 양쪽 모두 가능·선택 전 행동 거절·순위/동점·미니 종료 후 한 명만 영구 제외·체력100/Life 부활·전환 전 요청 거절·혼합 성적·전원 준비), 실제 Unity 직렬화/동등성/피격 Life 및 하이라이트40개 검사와 Windows 빌드 통과. 실제 호스트+원격5명에서 대난투→초읽기→대난투→초읽기, 중간 미니 승리/파티 계속, 새 Life/원격 시점 복귀·피격 내역 초기화, 혼합 결과 통계와 전원 준비 재경기를 확인했다.
AI5명 실제 혼합 경기에서도 초읽기 총격 사망/시간 초과가 파티 생존 명단에서 즉시 제거되어 이후 대난투 종료 부활에 포함되지 않는지 확인했고, 최종 파티 생존자1명/자동 준비/재경기를 통과했다. 새 AI 방의 첫 선택 릴에서 두 후보가 함께 표시되고 초읽기를 선택해 시작하는 화면도 렌더 확인했다.

### 19.23 서버 투사체와 호스트 사격시간 설정 (2026-10-12)

프로젝트 설계: 유한 속도(40m/s, 최대20m) 투사체는 서버의 DuelProjectileVolley에서 틱별 이동 구간을 검사한다. 클라이언트 물리 callback을 피해 근거로 사용하지 않는다. FireRpc의 Owner 호출 권한, 실제 발신자/연결 객체/Round/Version/생존/단계/각도/시각 검증은 유지한다. 서버가 산탄 난수 시드와 실제 총구 좌표/방향을 정하며 클라이언트가 명중 대상/피해/시드를 제출할 수 없다. 소드오프37개 산탄은 제한된 원뿔 안에 있고 실제 충돌 후보 중 화면 중심에 가까운 한 명에게75, 이후 번호 이웃25를 적용한다. 한 탄환의 부위별 피해는 데린저만 사용한다. 같은 틱의 투사체 충돌을 모두 수집한 뒤 피해와 승패를 확정해 맞교환을 허용한다. 발사 후 사망한 공격자의 이미 날아가는 탄은 해당 미니게임이 진행 중이고 연결/Life가 유효하면 유지한다. 종료/전환/재경기/공격자 연결 이탈 시 오래된 비행을 적용하지 않는다.

사격 상태에 발사 총구 좌표/방향/ADS/산탄 범위/난수 시드/원래 생존 마스크를 서버 쓰기 NetworkVariable 스냅샷으로 추가했다. 클라이언트는 시각 탄환만 풀링하고 피해 권한이 없다. 별도 NetworkObject/NetworkTransform/NetworkRigidbody를 생성하지 않아 전용 서버 판정에는 그래픽/카메라가 필요 없다. 늦은 참가 시 과거 발사를 재생하지 않으며 새 Life/경기마다 표시를 초기화한다. 통계/킬/피격 내역은 발사와 충돌을 구분하고 서버 충돌 확정 이벤트마다 기존 reliable 소유자 보고 경로로 전달한다.

로비 사격시간 설정은 서버의 ShotTimeLimit(0=무제한, 1~60초, 기본10초)으로 동기화한다. 호스트의 로컬 관리 진입점만 변경하며 NetworkManager.IsHost, 유효 director, Idle, 경기 미진행, 유한 값/범위를 검사한다. 원격 클라이언트는 관리 RPC를 갖지 않는다. 변경 시 전원 준비를 해제하며 시작 후 변경은 거절한다. 대난투 Aim 진입과 초읽기 Shooting 진입부터 각각 같은 제한을 적용한다. 서버는 만료 후 발사를 거절하고 대난투 재장전/초읽기 다음 게임을 수행한다. 마감 안에 발사한 탄환을 타이머가 취소하지 않으며 초읽기는 비행 중 전환을 보류한다. 설정/탄환 필드 추가로 ProtocolVersion=23이며 NGO2.13.3/Unity6000.3.18f1을 유지한다.
이미 발사된 탄환이 남아 있으면 승패 확정을 보류하며, 혼자 남은 사람의 새 사격은 거절한다. 살아 있는 투사체가 모두 충돌/범위 만료된 뒤 승패를 확정한다. 짧은 제한시간에서 AI는 장전 숫자 속도를 유지하고 조준 진입/사격 대기만 남은 제한 안으로 줄여, 1초 설정에서 재장전만 반복하지 않도록 한다.
검증: 규칙3,125개·실제 Unity 직렬화/동등성·Windows 빌드, 그래픽 호스트+원격5명의 설정 권한/동기화/기한/투사체 피해/소유자 사망 기록/부활, AI5명 혼합 경기/재경기, 두 탄환의 실제 렌더 픽셀 차이 검사를 통과했다. 서버 전용 프로세스 및 인터넷 고지연/손실 실기동은 별도 검증 대상이다.

### 19.24 미니게임 사이 대기와 대난투 준비 카운트 (2026-10-13)

프로젝트 설계: 대난투 미니 결과5초, 초읽기 사격 종료 뒤3초를 서버 시각으로 유지한다. 룰렛 선택 완료 후 QuickCountdown을 별도 단계로 두고, 기존 ClockState.StartedAt에3초 뒤의 공통 시작 시각을 넣는다. 클라이언트는 NGO ServerTime으로3→2→1을 표시하며 서버의 단계 전환이 입력 시작을 확정한다. 표시 프레임이나 지연으로 게임 시작을 앞당기지 않는다. 실제 QuickHands 진입에만 새 행동 Version/StartedAt/AI 일정을 배정한다. Input/Fire는 기존 QuickHandsActive/CanClockShoot 검증으로 준비 단계 요청을 거절한다. 숫자 입력창/AI 진행/사격 제한시간은 카운트 종료 전에 시작하지 않는다. 파티 종료 뒤 전원 준비 및 연결 이탈/스폰 수명 정책은 유지한다. 기존 서버 쓰기 스냅샷에 새 단계와 시작 시각을 함께 전달하며 추가 RPC/NetworkObject는 없다. 직렬화 기본 형식은 같지만 미지원 단계 해석을 막기 위해 ProtocolVersion=24로 올렸고 NGO2.13.3/Unity6000.3.18f1은 유지한다.
검증: 규칙3,135개/새 카운트 단계와 공통 시작 시각의 실제 NGO 직렬화·동등성/Windows 빌드, 호스트+원격5명에서 동일3→2→1·미리 보낸 숫자 요청 거절·시작 Version 갱신·미니 결과5초 유지/다음 선택을 통과했다. AI 카운트 전 행동 차단/종료 후 진행도 규칙 검증을 통과했다.

### 19.25 공통 조준 흔들림 재조정 (2026-10-13)

프로젝트 밸런스 변경: 기본 진폭에 소드오프1.75배, 데린저3.5배를 적용한다. 소드오프는 변경 전 데린저와 동일하고 데린저는 기존 대비2배다. 수평/수직 및 ADS/일반에 함께 적용하며 빈도/진입 안정화/줌/탄 퍼짐은 유지한다. 로컬 카메라·상대 총기 자세·서버 조준 경고·서버 발사 방향은 모두 기존 공통 Sway 함수를 사용하고 서버 시각/발사 시각 검증도 유지한다. RPC 권한/발신자/생존/단계/Version 검증과 스폰/참가 정책에는 변화가 없다. 패킷 형식은 같지만 서로 다른 흔들림으로 클라이언트의 화면과 서버 명중 방향이 어긋나지 않게 ProtocolVersion=25로 올렸다. 패키지는 유지한다. 기존 총격/AI1,766개·초읽기1,173개·파티198개(총3,137개) 규칙 검사를 통과했다.

### 19.26 아이템 준비 단계 (2026-10-13 프로젝트 설계)

ItemPreparation은 서버만 시작/종료하며 기존 서버 쓰기 ClockState에 단계·PhaseAt·StartedAt 종료 시각을 담는다. 대난투는 선택 뒤5초 준비→3초 카운트, 초읽기는 순위 발표 뒤5초 준비→사격이다. 클라이언트는 NGO ServerTime으로 남은 시간만 표시한다. Input/Fire의 기존 단계·생존·Version 검증으로 이 구간 요청을 거절하고 AI 일정은 실제 행동 시작에 배정한다. 초읽기 사격자의 Aim/StartedAt 및 제한시간도 준비 종료 시 배정한다. 마우스 해제/복구는 로컬 표현이며 결과에 권한이 없다. 새로운 RPC/NetworkObject/인벤토리는 추가하지 않았다.

초읽기 준비 도중 사격자/대상이 연결을 끊으면 기존 순위/사격 단계와 같이 해당 사격 기회를 취소한다. 늦은 참가 복원은 스냅샷의 현재 단계/종료 시각으로 가능하고 진행 중 참가 거절 정책은 유지한다. 호스트 여부와 무관하게 서버가 전환한다. 전용 서버 실기동은 별도 검증 대상이다. 새 단계를 모르는 빌드의 접속을 막기 위해 ProtocolVersion=26으로 올렸다. 패키지 버전은 유지하며 아이템 종류/지급/사용 로직은 아직 미구현이다.
검증: 규칙3,169개·실제 NGO 준비 단계/종료 시각 직렬화와 동등성·Unity Windows 빌드 통과. 호스트+원격5명의 동일5초/조작 해제·복구/초기 요청 거절/뒤의 카운트 및 사격 제한시간, AI5명의 대난투↔초읽기 반복/최종 결과/전원 준비 재경기를 확인했다. 실제 아이템 사용/보상은 구현 범위가 아니다.

### 19.27 시선 기반 머리·상체 표시 (2026-10-13 프로젝트 설계)

기존 소유자 전용 unreliable LookRpc의 발신자/경기/생존/유효 각도/빈도 검증을 거친 AimYaw/AimPitch 서버 쓰기 스냅샷으로 머리와 상체를 표시한다. 추가 RPC/자식 NetworkTransform 없이 각 화면의 RouletteTable이 좌석 상대 각도를 제한·보간한다. 자기 모델만 현재 로컬 카메라 기본 각도를 사용해 응답 지연을 줄인다. AI도 같은 서버 각도를 사용한다. 시각 회전은 서버 피해/발사 방향/부위 판정을 바꾸지 않으며 상체 아래 다리와 의자는 고정된다. 피격/사망 표시를 합성하고 얼굴 사망 카메라는 기존 Head Transform을 따른다.

기존 현재 스냅샷 적용·진행 중 참가 거절·스폰/디스폰·서버 권한 정책은 유지하며 호스트에만 적용되는 분기는 없다. 동일 필드의 표시 변경이므로 패키지/ProtocolVersion=26을 유지한다.
검증: 실제 NGO 타입 검사/Unity Windows 빌드 및 호스트+원격5명에서6개 좌석의 시선 스냅샷/머리 방향/상하 및0↔360도/다리 고정/행동 입력 독립성/총격·사망·부활을 확인했다.

### 19.28 시작 아이템 방 설정 (2026-10-13 프로젝트 설계)

호스트가 Stage.Idle/비경기 상태에서만 RouletteRules.SetStartWithItems를 호출한다. 실제 변경 시 ReadyMask를 해제하고 기존 서버 쓰기 DuelSnapshot.StartWithItems를 갱신한다. 읽기 공개/서버 쓰기 설정으로 현재 참가자와 새 참가자에게 같은 옵션을 제공하며 스폰 전 표시/클라이언트의 변경/경기 중 변경을 막는다. 기본값false, 같은 방의 재경기에 유지한다. 추가 RPC/NetworkObject 없이 bool의 From/직렬화/동등성 경로를 모두 반영했다. 패킷 필드 추가로 ProtocolVersion=27이다. 현재 실제 아이템/인벤토리는 없으며 설정 변경으로 아이템이 지급되는 것은 아니다.
검증: 규칙1,770개 및 실제 NGO bool 직렬화/동등성/Unity Windows 빌드, 호스트+원격 클라이언트의 토글/늦은 참가 초기 상태/비호스트 변경 거절/준비 해제/경기 중 잠금/단계 제목 확인을 통과했다. 실제 인벤토리 지급은 미구현이다.

### 19.29 명중 확인과 공통 흔들림/선택 시간 (2026-10-13)

서버 투사체 결과의 LastDamage>0인 경우, 해당 사격자 PlayerObject의 소유자에게 서버 호출 권한의 reliable ReportHitRpc(round,life,shot)를 한 번 보낸다. 호스트의 소유자도 같은 수신 함수를 사용하고 원격 소유자는 자신의 화면에만0.24초 확인을 표시한다. Round/Life/샷 중복을 검사하고 사망/재접속/부활/이전 경기 표시를 막는다. AI 사격자에게 인간 PlayerObject가 없으면 확인 RPC를 보내지 않는다. 늦게 참가한 사용자에게 이전 명중 이벤트를 재생하지 않으며 스폰/디스폰 정책은 유지한다. 피해 판정/기록의 서버 권한을 변경하지 않는다.

공통 Sway에서 ADS 및 홀드 경과 시간의 진폭 감소/안정화를 제거했다. 기본1.8도와 총기별1.75/3.5배를 카메라/무기/서버 판정에 동일 적용한다. 선택은 서버5.8초(감속4.8초+정지1초), 클라이언트는 ServerTime으로 화면과 로컬 클릭/완료음을 계산한다. 소리는 입력이나 결과를 결정하지 않고 유실된 중간 항목을 누적 재생하지 않는다. 새 RPC와 서로 다른 발사/선택 해석의 혼용을 막기 위해 ProtocolVersion=28이다. 패키지는 유지한다.

검증: 규칙3,184개/Unity 빌드/호스트와 원격 클라이언트에서 두 총기의 명중 확인·빗나감/타인 명중 무표시·fade·이전 Life 거절·룰렛 두 효과음 재생/공통5.8초 전환을 확인했다. 전용 서버 실기동과 인터넷 지연은 별도 검증 대상이다.

### 19.30 리볼버 약실과 부분 장전 (2026-10-13 프로젝트 설계)

DuelWeapon.Revolver 값0은 기존 데린저를 대체한다. DuelActionState.LoadedMask/SpentMask/NextChamber/ShootingStartedAt을 서버 쓰기 스냅샷의 From/직렬화/동등성에 포함했다. Ammo는6개 live 비트 수로 계산한다. 기존 소유자 숫자 요청의 생존/단계/Version/벌칙 검증 뒤3자리 완료에만 빈 약실 한 개를 채운다. 이미 완료된 약실은 현재 패턴 오입력으로 지우지 않는다. 서버 발사 요청은 Ammo>0을 추가 검증하고 live 한 비트를 탄피 비트로 옮긴다. 사격 Version을 갱신해 중복 요청이 두 발을 소비하지 못하게 하고 기존 비행 중 투사체/시각/발신자/경기 검증을 유지한다.

기존 소유자 LookRpc의 ADS는 대난투 리볼버 준비 중에도 허용한다. 빈 총은 조준하지만 위협 대상이나 피해를 만들지 않는다. 부분 장전도 ADS로 사격하고, 남은 탄이 있으면 추가 숫자 단계 없이 다음 발을 쏜다. 슬롯 데이터로 각 화면에서 실린더/게이트/탄/탄피를 표현하며 GameObject 생성/추가 NetworkObject는 없다. ShootingStartedAt은 부분 장전 첫 ADS/전체 장전 완료에서 시작하고 각 발사 뒤 재배정해 방 제한시간 우회를 막는다. 만료 시 재장전이며 파티 전환/부활/재경기의 탄 상태를 초기화한다. 초읽기 사격자는 한 발을 서버가 제공하며 기존 꼴찌에게 한 번의 사격만 허용한다. 진행 중 참가 거절/초기 스냅샷/스폰·디스폰/호스트와 서버 권한 구분은 유지한다. 프로토콜29, 패키지는 유지한다.

검증:3,329개 규칙/실제 NGO 약실/탄피/커서/시간 직렬화와 동등성/Unity 빌드, 호스트+원격 클라이언트의 Input System 마우스 경로/빈 총/부분 장전/6발 무입력 연속 사격/약실 복제/원격 피해·승패를 확인했다. AI1~5명·3난이도 완주/사격 제한은 규칙 시뮬레이션을 통과했다. 전용 서버 실기동/인터넷 고지연은 별도 검증 대상이다.

### 19.31. 목숨3개·회복 연출·직전 미니게임 제외 (2026-10-13, 프로토콜30)

프로젝트 설계 결정이며 NGO 기본 규칙이 아니다. 서버가 LivesRemaining(최초3)을 사망/초읽기 시간 초과마다1씩 차감한다. PartyAliveMask는 연결된 잔여 목숨 참가자, AliveMask는 현재 미니게임의 생존자다. DiedAt으로 중복 차감을 막고 목숨0만 파티 마스크에서 제외한다. 대난투 꼴찌만 영구 제외하던 정책을 폐기했다. 전원 현재 사망/상호 처치도 목숨이 남으면 계속하고 전원 목숨0은 무승부다.

결과5초/사격 후3초 구간에 서버 ReviveAt을 배정한다. 클라이언트는 이 시각으로1.3초 동안 양손 테이블 지지·상체 일어나기·자기 얼굴 카메라 복귀를 렌더한다. 복귀 전에는 여전히 AliveMask에서 제외되어 조작/RPC가 거절된다. 복귀 시 새 Life/Version을 발급하고 피격 보고를 비우며 HP100으로 회복한다. 대난투는 잔여 목숨 모두, 초읽기는 죽은 잔여 목숨만 회복한다. 유일한 잔여 목숨 참가자가 쓰러져 있으면 회복 뒤 최종 승리를 확정한다. 이탈로 결과 단계가 바뀌면 회복 시각도 앞당겨 새 종료 시각 전에 일어난다.

지속 목숨/회복 시각은 서버 쓰기 DuelSnapshot에 담고 직렬화·동등성에 포함한다. 기존 소유자 사망 RPC에는 서버가 확정한 잔여 목숨을 함께 보내 State 갱신 순서 차이에도 올바른 문구를 표시한다. 목숨0 사망/최종 승리 덮어쓰기와 Round/Life 검증은 유지한다. 준비 대상은 영구 탈락자를 포함한 연결된 전원이며 AI는 자동 준비한다. 새 파티는 체력100/목숨3/통계/미니게임 이력을 초기화한다.

PartyGames.Choose는 직전에 실제 시작한 미니게임만 제외한다. 선택만 했다 취소된 게임은 이력에 넣지 않는다. 현재 두 종류는 첫 무작위 선택 이후 번갈아 나온다. 새 종류를 추가하면 직전 외 모든 후보를 균등 추첨한다. 패키지 버전은 바꾸지 않는다.

19.31 검증: 규칙3,853개(총격/초읽기/목숨 통합), 실제NGO 스냅샷 직렬화·동등성, 하이라이트40개, Unity Windows 개발 빌드 통과. 별도 호스트45개/원격 클라이언트44개 실행 검사로 잔여 목숨/회복 시각/자기 안내/아이콘·양손 접점·피격 보고 비우기·미니게임 번갈아 선택·영구 탈락·최종 승리·전원 준비 재경기를 확인했다. 전용 서버/인터넷 고지연 실기동은 미검증이다.

### 19.32. 초읽기 순위 공개6초·본인 행 강조 (2026-10-13)

서버 DuelClockContest.RevealDuration을6초로 늘렸다. 기존 ClockState.PhaseAt/Ranking 상태를 그대로 사용하며 단계 전환 권한은 서버에 있다. 클라이언트는 소유 플레이어의 Seat를 비교해 자기 기록(시간 초과 포함)을 금색 배경/굵은 글씨/`나` 표시로 강조한다. 새 RPC/스냅샷 필드는 없으므로 프로토콜30을 유지한다. 닉네임의 서식 태그가 다른 행의 강조에 영향을 주지 않도록 행 Label.enableRichText를 끈다.

초읽기 결과 강조 검증: 기존 규칙3,853개, 실제NGO 직렬화/하이라이트 검사 및 Unity Windows 개발 빌드 통과. 호스트6개/원격 클라이언트7개 실행 검사로6행 중 본인 한 행만 강조, 시간 초과 본인 기록 강조, 이전3초 이후에도 결과 유지,6초 뒤 아이템 단계 전환을 확인했다. 검사 소스는 `Tools/UnityQA/ClockRankingRuntimeQA.cs`다.

### 19.33. 사격 역할 안내·초읽기 자유 대상 (2026-10-13)

프로젝트 설계 결정: 기존 ClockState.Phase/First와 소유자 Seat로 각 클라이언트에서 역할별 준비/사격 안내를 표시한다. 새 이벤트/스냅샷 필드 없이 지속 상태로 표시하므로 도착 순서에 의존하지 않으며 단계 이탈 시 숨긴다. 안내에 맞춰 초읽기의 Last 전용 조준 경고/피해 제한을 없앴다. 서버는 여전히 First/현재 생존/한 발/Version/시각/사격 제한시간/실제 투사체를 검증한다. Last 이탈은 다른 상대가 남으면 기회를 취소하지 않고 First 이탈/상대 없음은 취소한다. AI는 생존 상대를 고른다. 프로토콜30/패키지 버전 유지.

사격 안내 검증: 규칙3,853개, Unity 실제 직렬화/하이라이트 검사 및 Windows 개발 빌드 통과. 호스트10개/원격8개 실행 검사로 역할별 결과/아이템/사격 안내, 준비 중 및 비사격자 발사 거절, 원격1등이 꼴찌가 아닌2등에게 실제 피해/목숨 차감, 발사 뒤 안내 숨김을 확인했다. 실행 소스는 `Tools/UnityQA/ShootingNoticeRuntimeQA.cs`다.

### 19.34. 신호등·욕심의 대가: 서버 시각/개인 기록 (2026-10-13, 프로토콜31)

프로젝트 설계 결정이다. PartyMiniGame에Signal/Greed를 등록하며 기존 Counting/Ranking/ItemPreparation/Shooting/Returning 흐름을 재사용한다. ClockEntry에Points/Failure, ClockState에Signal/Points/SignalAt을 추가하고 모든 필드를 명시적으로 직렬화·동등성 비교한다. 별도 NetworkTransform이나 클라이언트 피해 판정은 추가하지 않는다.

서버가 미래 진짜 신호/가짜 일정/폭발 시각을 생성한다. 욕심의 대가 점수는 공통 시작 시각과 초당40점 공식으로 계산한다. PublicState는 Counting 동안 개인 기록을 숨기고 현재 신호/발생 시각/현재 공통 점수만 공개한다. 입력은 기존 소유자→서버 StopClockRpc로 경기/미니게임 번호·좌석·생존·중복·유한 시각·과거0.3초~미래0.05초 범위를 검증한다. 클라이언트 점수를 받지 않고 서버가 제출 시각의 점수를 계산한다. 확정 기록/실패 사유는 서버→소유자 reliable 응답에 함께 담아 상태/RPC 도착 순서에 의존하지 않는다.

신호등은 진짜 신호 이전 입력/미입력을 실패로 두고 반응 시간이 작은 성공자가 먼저다. 욕심의 대가는 폭발 이전 점수 확정만 성공이며 미확정자는0점이다. 폭발 전 표본의 도착에0.3초 여유를 주고0.9초 폭발 표시 뒤 공개 결과로 간다. 실패만으로 Eliminate/목숨 차감을 호출하지 않는다. 전원 실패는 사격 없이 다음 게임이다. 성공자는 기존 한 발/Version/시간 제한/서버 투사체 판정을 통해 자유 상대 사격을 한다. 기존 목숨/복귀·전원 준비·호스트/전용 서버 정책을 유지한다.

AI는 서버 내부 입력 함수를 사용하고 난이도별 반응/실수/욕심 성향을 적용한다. 욕심의 대가에서는 미래 폭발 시각을 이용해 최적 확정하지 않는다. 두 새 게임의 기록을 초읽기 오차/승수에 합산하지 않는다. 스냅샷과 응답 RPC 형식이 바뀌어 프로토콜31로 올렸고 NGO/Unity/Transport 버전은 유지한다.

19.34 검증: 규칙4,515개(새 게임662개 포함), 실제NGO 직렬화·동등성/하이라이트40개/Unity Windows 개발 빌드 통과. 호스트25개·원격24개·추가 원격22개 실행 검사에서6인 개인 기록 비공개·실제 입력/소유자 확정·실패/점수/주사위·사격 권한·기존 피해/목숨/복귀·직전 제외·통계 분리를 확인했다. 신호/점수/폭발/결과 렌더도 확인했다. 클라이언트의 버퍼링된 ServerTime이 SignalAt보다 이르면 초록 표시/효과음을 기다려 보이는 신호와 제출 시간 판정을 맞춘다. 인터넷 고지연/패킷 손실과 전용 서버 실기동은 이번 검증에 포함하지 않았다.

### 19.35. 욕심의 대가 점수/진행 간격 (2026-10-13)

프로젝트 설계 결정: 3초 중앙 카운트→초당40점의 일정한 누적→결과8초→기존 아이템/사격 단계다. GreedScoreAt(startedAt,sample)은 경과시간×40의 정수 내림이며 시작 전은0이다. 클라이언트는 버퍼링된 NetworkManager.ServerTime과 공통 StartedAt으로 매 프레임 표시하고, 서버는 검증한 제출 시각으로 같은 공식을 적용한다. 개인 확정 후에는 소유자 RPC로 받은 확정 점수만 보여주며 폭발 상태 수신 시 점수 진행을 중단한다. 카운트 동안 입력을 거절하고 서버가8초 결과 전환을 결정한다. 미래 폭발 시각/다른 사람의 개인 기록/기존 표본 검증은 유지한다. 스냅샷/RPC 필드와 패키지 버전은 변경하지 않아 프로토콜31을 유지한다.

검증: 규칙2,670개, Unity 직렬화/동등성/하이라이트와 Windows 개발 빌드, 별도 호스트/원격2명90개 실행 검사 통과. 매 프레임 점수 증가/동일 공식 확정,3초 입력 차단/시작 연출과8초 순위 유지, 기존 비공개/폭발/사격/목숨 흐름을 확인했다.

### 19.36. 직접 선택·아이템·감전 (2026-10-13, 프로토콜32)

프로젝트 설계 결정: ManualMiniGame은 서버 쓰기 방 설정이다. 로비에서 NetworkManager.IsHost/Idle/비경기 검증 후 바꾸며 ReadyMask를 지운다. 방장 로컬 ChooseMiniGame은 현재 Selecting/경기/iteration/유효 카탈로그 항목을 확인하고 다음 준비 단계로 간다. 자동 Selecting 시간 전환을 막고 직전 제외를 호출하지 않는다. 원격 관리 RPC를 만들지 않으며 전용 서버는 호스트 전용 직접 선택 옵션을 켜지 않는 기본 자동 방으로 동작한다.

StartWithItems가 켜진 파티 시작에 HasRecovery/HasTaser를 지급한다. UseItemRpc는 Owner 권한과 발신자 OwnerClientId를 검사하고 manager가 서버/스폰/경기 번호/연결 플레이어를 확인한다. Director는 살아 있는 좌석, 현재 ItemPreparation, iteration/종료 시각, 소지품/쿨다운/사격자 자격을 검증한다. 회복/소비/테이저 활성화/재사용 시점은 원자적으로 같은 서버 상태에 적용한다. 잘못된 enum/중복/지난 경기·턴/만료·사격 중 요청은 거절한다.

TaserArmed를 Shooting 진입 때 한 발짜리 Taser 무기/새 Version으로 장착한다. 기존 발사 RPC의 권한/각도/시간 표본/탄약/Version/서버 투사체 검증을 재사용한다. 비사격자라도 이번 턴에 활성화한 테이저만 발사 가능하다. 균일5 피해 뒤 생존자에게 Stunned/StunnedAt을 적용한다. 클라이언트 Look/Fire는 감전 중 거절하고 서버 내부 TickShock만 불수의 격발을 허용한다. 별도 외부 RPC에 불수의 발사 플래그를 노출하지 않는다. 랜덤 격발은 실제 ammo를 소비하며 1등 총기의 발사로 정상 턴이 종료된다. 테이저 타격은 단독으로 사격 종료를 만들지 않는다. 이미 발사한 탄환은 처리하되 사격 단계가 끝난 뒤 남은 탄환/활성화/감전은 다음 턴에 적용하지 않는다. 사격자 이탈/사망과 게임 종료도 정리 경로에 포함한다.

ManualMiniGame과 소지품/쿨다운/효과 시각·감전 상태는 DuelSnapshot에 명시적으로 직렬화하고 Equals에도 넣었다. 화면 효과는 서버 시각/현재 상태에서 파생하므로 새 상태 수신만으로 복구하며 과거 연출을 반복하지 않는다. 재경기 초기화/복귀 Life별 피격 보고 정책을 유지한다. DuelDamageReport는32개의 고정 엔트리로 최소5 피해와 최대50 회복을 포함한 한 생명 전체 피해를 소유자 읽기 상태로 보존한다. 킬 RPC는 물리 접촉 부위를 추가해 각 화면의 무기 이미지 옆에 표시한다. 데이터/API 페이로드가 바뀌어 프로토콜32이며 NGO2.13.3/Unity6000.3.18f1/Transport2.7.3은 유지한다.

### 19.37. 반복 선택·발사 무기 보존 (2026-10-14, 프로토콜33)

직접 선택 UI는 경기/미니게임 식별자가 바뀐 Selecting에서 이전 포인터 캡처를 해제하고 새 버튼을 구성한다. 버튼 표시와 클릭 가능성은 로컬 표현이며 서버의 현재 Selecting/iteration/카탈로그/방장 검증을 대체하지 않는다. 방장 전용 시작 아이템 정책은 서버 TryStart에서만 좌석0에 적용하며 원격 요청으로 소지품을 생성하지 않는다. 기존 UseItem의 소유권/소지품/단계/쿨다운 검증은 유지한다. 회복은 소비 후 숨기고 활성 테이저만 노란 테두리로 확인한다.

DuelActionState.FiredWeapon은 실제 발사 시 서버가 저장하고 현재 Weapon/SelectedWeapon과 구분한다. 효과 만료로 Weapon이 복구된 최종 스냅샷만 받아도 전기 효과음/투사체 색/발사 표현을 재구성할 수 있다. NetworkSerialize와 Equals 모두에 포함하며 프로토콜33으로 구분한다. 클라이언트는 좌석별 ShotsFired 증가와 최근 FiredAt을 확인해 방전음을 한 번 재생하고 새 경기/과거 상태는 재생하지 않는다. 순간 NetworkVariable 중간값을 반드시 받는다고 가정하지 않는다.

직접 선택의 실제 반복 실패 원인은 선택을 룰렛 종료 시각으로 역산한 뒤 부동소수점 경과시간을 다시 비교한 것이었다. 이제 호스트의 검증된 선택은 명시적인 즉시 시작 경로를 사용하며 시간 역산/만료 비교를 거치지 않는다. 240개의 서로 다른 서버 시각/네 미니게임 선택 회귀 검사로 즉시 전환을 확인했다.

최종 검증: 규칙4,841개(직접 선택/아이템320개 포함), NGO 스냅샷/피격 내역 직렬화·동등성/하이라이트 및 Unity Windows 개발 빌드 통과. 별도 호스트42개·원격41개·원격 관전자37개(총120개)로 세 판 연속 같은 미니게임 직접 선택, 방장만 시작 지급, 회복/감전/랜덤 격발/쿨다운/효과 종료, 테이저 킬 이미지·헤드샷 표시·최종 사인 내역을 확인했다. 원격 아이템 RPC 검증은 시작 지급 정책 확인 후 서버 시험 코드로만 추가 소지품을 부여해 진행했다. 실제 방장 AI전4개 렌더 검사에서 노란 테두리/선택 밝기, 회색 비활성 회복제, 이미지·이름만 표시를 확인했다. 사격자에게 이미 발사된 탄환이 있으면 테이저로 사망해도 탄환 처리가 끝난 뒤 사격/아이템 효과를 종료하는 교차 처치 검사도 통과했다. 최종 실행본과 프로젝트 스크립트/USS 해시가 같다.

최종 렌더에서 아이콘은 리소스 복제 대신 기존 코드 아이콘 생성 방식으로 표시하며 노란 선택 테두리와 회색 비활성 앰플의 차이/이미지 표시를 확인했다.

### 19.38. 개발용 아이템 무한 사용·준비 유지 (2026-10-15, 프로토콜34)

프로젝트 설계: UnlimitedItems 기본false를 서버 쓰기 DuelSnapshot에 직렬화하고 Equals/초기 동기화에 포함한다. 방장 로컬 관리 함수만 IsHost/Idle/비경기를 검증해 변경하며 원격 관리 RPC는 없다. 사격시간/시작 지급/직접 선택/무한 사용 설정 변경은 ReadyMask를 유지한다. 경기 중 변경은 계속 거절한다. 현재 기본 사격시간은10초다.

소지품 지급 정책은 별개로 유지한다. 무한 사용은 소지한 회복제를 소비하지 않고 테이저의 재사용 iteration 제한을 생략한다. 회복50/체력100 상한, 살아 있는 소유자/현재 경기·iteration/아이템 사용 단계/만료 시각, 테이저 비사격자/대난투 금지/턴당 한 번 활성화와 한 발 제한은 그대로다. 종료 시 활성화/감전은 정리하며 재경기에도 방 설정을 유지한다. UI는 동기화된 플래그로 쿨다운을 무시하며 활성 테이저 노란 테두리를 유지한다. 새 스냅샷 필드 때문에 프로토콜34이며 설치 패키지는 NGO2.13.3/Unity6000.3.18f1/Transport2.7.3을 유지한다.

검증: 규칙4,896개(아이템370개 포함), 실제 NGO 스냅샷 직렬화/동등성 및 Windows 개발 빌드를 통과했다. 호스트9개·원격15개 실행 검사에서 늦은 참가 설정 수신, 방장 전용 제어/지급, 네 방 설정 변경 후 원격 준비 유지, 원격 소유자 RPC의 두 번 연속 회복/인벤 유지, 두 미니게임 연속 테이저 활성화/쿨다운0/노란 테두리, 턴 종료 효과 정리를 확인했다. 원격 아이템 동작은 방장 전용 지급 확인 후 서버 시험 코드로 원격에게 소지품을 부여해 검증했다.


### 19.39 사격 후 재장전 순환 (2026-10-15, 프로토콜35)

프로젝트 설계 변경: 리볼버는 발사 후 CloseBarrel(탄피 빼기), 소드오프는 OpenBarrel로 전환하며 Pickup을 반복하지 않는다. 리볼버는 LoadedMask를 유지하고 발사한 비트만 SpentMask에 옮긴다. 탄피 빼기 완료는 SpentMask만 비우며 빈 약실만 채운다. 남은 실탄은 숫자 입력 없이 연속 사격할 수 있다. 소드오프는 OpenBarrel 완료 때 탄피를 자동 배출한다. 클라이언트는 동일 마스크/진행으로 배출을 표현하며 피해/잔탄 권한은 없다.

소유자 RPC의 발신자·생존·경기·단계·Version·시간과 비행 중 투사체 검증은 유지한다. 사격 때 새 Version/행동 패턴을 발급하므로 이전 행동의 중복 입력/발사 요청은 새 탄을 소비하지 못한다. 다른 좌석의 입력/벌칙/AI 일정은 바꾸지 않는다. 공유 사격의 한 발 제한과 사격시간 초과 정책은 유지한다. 초기 스냅샷/진행 중 참가 거절/스폰 수명/전용 서버의 카메라 비의존 구조도 유지한다. 추가 동기화 필드나 RPC 없이 구버전 동작 혼용을 막기 위해 ProtocolVersion=35이며 설치 NGO/Unity/Transport 버전은 유지한다.

검증: 규칙5,288개 및 Unity 스냅샷/동등성·하이라이트/Windows 개발 빌드 통과. 실제 호스트8개/원격11개 소유자 RPC 검사에서 잔여 실탄 보존, 탄피만 배출, 두 약실 보충, 소드오프 자동 배출/반복 장전 및 상대 약실 상태 복제를 확인했다. 전용 서버/인터넷 고지연 실기동은 별도 검증 대상이다.


### 19.40 총기 음향·개발자 모드·테이블 소품 (2026-10-15, 프로토콜36)

DuelWeaponAudio는 공개된 서버 확정 Action/Progress/LoadedMask/SpentMask/ShotsFired/FiredWeapon을 관찰해 각 좌석의 로컬3D AudioSource로 재생한다. 초기 수신·이전 Version·오래된 발사·같은 스냅샷은 과거 소리를 다시 만들지 않는다. 총소리/조작음/감전 루프/유리 파손 소스를 분리하며 빈자리 정리가 유리 소리를 끊지 않게 한다. 새 RPC/오디오 권한이나 Transform writer는 없다. 전용 서버의 Null 그래픽에서는 음향 재생을 생략한다.

DeveloperMode는 호스트 로컬 관리 플래그이며 원격 관리 RPC가 없다. 호스트만 F11로 창을 켜며 모든 설정 진입점도 이 플래그를 검사한다. 끄면 서버 설정 네 개를 기본값으로 강제 복원하고 준비 비트를 유지한다. 경기 중 개별 설정 변경 거절은 유지하고 끄기만 허용한다. Selecting에서 수동 선택을 끄면 새 iteration/정상 직전 제외 추첨으로 자동 룰렛을 다시 시작한다. 실제 적용 값은 기존 서버 쓰기 스냅샷으로 원격에 전달한다.

DuelTabletop이 파손/잔 채워짐/음주 완료 비트와 여섯 좌석의 활동/서버 시작 시각/병·잔 파손 시각을 관리한다. 로비/선택/비총격 미니게임에서만 활동을 예약한다. 시선/키 입력은 활동을 취소하거나 게임 결과를 바꾸지 않는다. 파손 후 동작 제한과 한 잔 제한도 서버에서 결정한다. 동작 중단은 총격/아이템 단계 전에 이루어지므로 실제 사격 때 소품은 고정된 테이블 위치에 있다. 클라이언트는 메시/고정 팔 IK/주입 줄기/잔/파편만 표시하고 피해 콜라이더나 물체 파손 요청 RPC를 만들지 않는다.

DuelProjectileVolley의 서버 이동 거리 검사에 소품 접촉을 포함했다. 실제로 도달한 물체만 한 번 깨며 탄환을 정지시키고, 플레이어와 별개의 충돌 결과를 사용해 피해/킬/히트마커를 만들지 않는다. 산탄은 각 탄의 실제 접촉으로 물체를 깨고 나머지 탄은 계속 검사한다. FiredBottleMask/FiredGlassMask로 발사 당시 장애물 상태를 고정해 원격 탄환 끝점도 복원한다. 새 Tabletop 및 발사 필드를 From/직렬화/동등성에 함께 포함했다. 기존 소유자/발신자/생존/Round/Version/사격 시각/투사체 수명 검증을 유지한다. 초기/늦은 참가도 전체 상태를 받으며 경기 중 참가 거절/스폰 수명/서버의 카메라 비의존 판정은 그대로다. 형식 변경으로 ProtocolVersion=36이며 패키지는 유지한다.

최종 검증: 규칙5,367개(유리 접촉/한 잔 제한/개발자 기본값 및 수동 선택 취소 포함), 실제 Unity 음향258개, 소품/손 접촉·시선/파손 렌더4,944개, 스냅샷 직렬화/동등성·하이라이트40개와 Windows 개발 빌드를 통과했다. 호스트20개/원격20개 실행 검사에서 실제 F11 키의 호스트 전용 제어, 기본값 복원/준비 유지, 음주 상태 전파, 탄환 병·잔 파손, 미니게임 간 파손 유지, 수동 선택 중 F11 종료→직전 제외 자동 룰렛 복귀를 확인했다. 전용 서버·인터넷 고지연 실기동은 이번 검증에 포함하지 않았다.

### 19.41 앉은 배치·자기 모델 숨김·시선 합성 (2026-10-15, 프로토콜37)

프로젝트 설계: 의자/캐릭터는0.15m 안쪽, 앉은 상체/골반/의자 높이는0.25m 아래로 조정했다. DuelGunplay의 공통 SeatRadius/SeatedDrop을 모델·1인칭 Eye·서버 머리/몸통/다리·의자 가림에 함께 적용한다. 발은 바닥에 두고 테이블 소품/놓인 총의 월드 좌표는 유지한다. 피해 권한/발신자/Version·Life 검증은 유지하며, 낮아진 다리도 테이블 가림을 적용한다. 서로 다른 배치의 클라이언트 접속을 막기 위해 ProtocolVersion=37을 사용한다. 패킷 필드·패키지 버전은 바꾸지 않는다.

애니메이션은 기존 공개 AimYaw/AimPitch로 재현한다. 머리는 좌석 기준 시선 방향으로 합성해 상체 전경/롤로 목의 회전축이 기울지 않게 하고, 턱 지지/술 동작 중 몸통 회전은 줄인다. 대기 턱 지지15도/따르기12도와 고정 팔 길이·골반 연결을 유지한다. 추가 RPC/NetworkTransform은 없다.

로컬 표시: 연결된 자기 좌석의 생존/복귀 중 모델 전체와 들고 있는 술 소품은 Renderer.forceRenderingOff로 숨긴다. 게임 상태나 GameObject 활성 상태를 바꾸지 않으므로 호스트/원격/늦은 참가자가 각자 자기 모델만 숨긴다. 무기 전용 손은 별도 렌더로 유지한다. 관전/소유 좌석 변경 시 숨김을 해제하며 사망 얼굴 카메라는 기존대로 따른다. 전용 서버는 렌더 상태에 의존하지 않고 같은 공통 좌표에서 투사체를 판정한다.

검증: 규칙5,342개와 실제 Unity 스냅샷 직렬화/동등성·하이라이트40개, Windows 개발 빌드를 통과했다. 캐릭터3,120개·소품/가시성6,305개 검사로 팔/턱 접촉·목 회전·로컬 숨김/관전 복원을 확인했다. 실제 호스트25개/원격25개 검사에서 각 클라이언트의 자기 모델 숨김/타인 표시, 기존 LookRpc로 전달된 시선 방향, 같은 눈높이 및 서버 탄환/유리/피해를 확인했다. 전용 서버와 인터넷 고지연 실기동은 미포함이다.

### 19.42 미니게임 결과3초 통일 (2026-10-15)

프로젝트 진행 시간 변경: Countdown/Signal/Greed의 Ranking과 QuickHands의 QuickResults를 공통 MiniResultsDuration=3초로 통일한다. 서버가 PhaseAt 기준으로 전환하며 UI는 기존 공개 단계/시각을 사용한다. 이전 결과6/8/5초 설계보다 이 값이 우선한다. 아이템 사용5초/사격 후 복귀3초/최종 전원 준비 대기는 유지한다. 새로운 네트워크 필드/RPC 없이 기존 권한·초기 상태·수명주기와 프로토콜37을 유지한다. 결과 종료 경계 시각, 후속 아이템/사격과 목숨 복귀를 기존 규칙 검사로 확인했다.

### 19.43 뭐였더라?: 공개/개인 상태와 입력 검증 (2026-10-16, 프로토콜38)

프로젝트 설계: PartyMiniGame.Memory와 ClockPhase.Memory를 추가하고 ArrowMemoryState로 세부 Showing/Input/Results/Finished 단계, 공통 시각, 길이/후보/이번 라운드 탈락/순위/d6를 복제한다. 공개3초/입력10초/결과3초이며 길이3부터 라운드마다1씩 늘린다. 최다 오류 한 명만 미니 탈락하고, 오류>0 동점이 생겨 d6를 굴릴 때는 해당 라운드에서 오류 오름차순→동률d6 내림차순으로 전체 최종 순위를 바로 확정한다. 미니 탈락은 Game.AliveMask/HP/Lives에 영향을 주지 않고 최종1등만 기존 아이템/사격 경로로 이어진다.

DuelArrowMemory는 서버의 원본 정답/부분 기록을 보관한다. DuelClockContest.PublicState는 Input 정답seed와 개별 진행/오류/확정을 지우며, 공개 단계가 바뀔 때만 공개 Sequence를 갱신해 입력 빈도도 불필요하게 노출하지 않는다. InputMemoryRpc는 Owner 발신 권한과 실제 SenderClientId를 확인하고, 서버는 경기Round/Iteration/세부Round/생존·연결/단계/인덱스/방향0~3/마감/자동잠금을 검증한다. 개인 진행 응답은 Server→Owner RPC이며 클라이언트 오류/정답 판정을 신뢰하지 않는다. 늦은 참가 초기 표시/진행 중 참가 거절/디스폰 수명 정책은 유지하고 자식 NetworkObject는 추가하지 않는다.

Clock.Memory의 모든 필드를 NetworkSerialize와 Equals에 포함하고 프로토콜38로 서로 다른 패킷 해석을 차단한다. 개인 ACK는 파티Round/Iteration/세부Round가 현재 화면과 같을 때만 적용한다. AI도 서버의 같은 입력 검증 경로를 사용하며 전용 서버에서 UI나 소유자 로컬 상태 없이 판정할 수 있다.

검증: 기억 상태 모든 필드 직렬화/Equals와 새 규칙3,672개·기존 규칙5,344개, Unity Windows 개발 빌드 통과. 실제 호스트29개/원격30개 검사에서 소유자별 방향키/개인ACK·정답/부분 기록 소거·미니 탈락 HP/목숨 보존·결과3초/아이템5초/한 발·다음 수동 선택을 확인했다. 전용 서버/인터넷 고지연 실기동은 미포함이다.

## 2026-10-20 프로젝트 설계: 방 채팅
Owner→Server SendChatRpc는 실제 발신자를 확인하며 서버가 연결/스폰/좌석 닉네임·본문120텍스트요소/480UTF8바이트·0.6초 간격을 검증한다. Server→Owner ReceiveChatRpc로 각 접속자에게 reliable 전달하고 순서/중복은 서버 sequence로 정리한다. 최근100개를 보관하고 늦은 참가자는 스폰 전송 이후 다음 프레임에 서버 기록을 받는다. 전용 서버의 판정/중계에 로컬 플레이어나 UI는 필요하지 않다. 클라이언트 UI는 main 제외 전 단계에서 Enter로 열고 숫자/방향/정지/조준/사격 입력과 분리하며, 닫는 클릭은 소비한다. 프로토콜39이며 상세는 Docs/Chat.ko.md.
채팅 검증: Unity 로컬 입력/UI25개와 별도 Windows 호스트11개/원격10개/늦은 참가10개, 총56개 검사 및 개발 빌드 통과. 루프백 실기동에서 서버 닉네임/기록/순서/전송 제한과 아이템/실제 Aim 중 채팅을 확인했다. 인터넷 고지연/전용 서버 실기동은 미포함.

### 19.44 시작 아이템 선택 (2026-10-20, 프로토콜40)

프로젝트 설계: DuelLoadout의 유효 아이템 비트마스크로 서로 다른2개를 선택한다. 각 소유자의 SelectLoadoutItemRpc는 Owner 호출 권한과 실제 발신자를 확인하고, 서버 manager가 연결/스폰/경기Round를 검증한다. Director가 유효 좌석/아이템·대기 단계·미준비 상태를 검사해 선택을 변경한다. Ready와 TryStart에서도 선택 완성을 검증하므로 UI 우회로 시작할 수 없다. 전용 서버 판정에는 로컬 UI가 필요하지 않다.

SelectedItems를 각 좌석의 서버 스냅샷/NetworkSerialize/Equals에 포함해 원격/늦은 참가도 현재 선택을 표시한다. 시작 지급은 서버 TryStart 한 곳에서 연결된 전원에게 적용한다. 미니게임 결과에는 지급하지 않고 소비/쿨다운은 유지한다. 최종 재경기는 기존 선택을 유지해 재선택 가능하며 새 파티 시작 시 소지품/쿨다운만 새로 구성한다. 새 참가/좌석 재사용은 선택0, AI는 두 종류 자동 선택이다. 방장 전용 StartWithItems 설정을 제거했고 F11 기본값 복원은 선택/준비에 영향을 주지 않는다. 이전 패킷과 호환되지 않아 ProtocolVersion40을 사용한다.

검증(2026-10-20): 선택/준비/전원 지급/소비·쿨다운 유지/다음 미니게임 무지급/새 파티 재지급/좌석 재사용/AI6인/직렬화·동등성39개와 실제 루프백 호스트14개·원격13개·늦은 참가13개, 총79개를 통과했다. 원격 소유자의 선택 RPC, 미선택 준비 거절, 준비 후 변경 거절, 늦은 참가 초기 선택, 최종 결과의 재선택과 재경기 지급 및 로비/결과 UI 렌더를 확인했다. Unity Windows 개발 빌드 통과. 전용 서버/인터넷 고지연 실기동은 포함하지 않았다. 재현 도구는 Tools/UnityQA/LoadoutRuntimeQA.cs이며, 과거 StartWithItems 옵션을 검사하는 이전 도구를 대체한다.

## 2026-10-21 첫 게임 전 아이템 선택으로 정정 (현재 규칙, 프로토콜41)

각 참가자는 방에 들어와 첫 게임을 시작하기 전 로비에서만 서로 다른 아이템2개를 직접 고른다. 현재 두 종류여도 사람의 선택은 빈 상태로 시작하며 두 종류를 자동 선택하지 않는다(AI만 자동 선택). 준비 전에는 선택을 바꿀 수 있고 첫 파티 시작 시 LoadoutLocked로 확정한다. 미니게임 사이와 기존 참가자의 최종 결과 화면에는 아이템 선택창을 표시하지 않는다. 최종 재경기는 최초 조합을 그대로 사용해 준비만 받는다. 매 파티 시작 지급/쿨다운 초기화와 미니게임 사이 소비/쿨다운 유지·결과 무지급 정책은 유지한다.

서버 SelectItem도 확정 이후의 변경을 거절한다. LoadoutLocked를 좌석 스냅샷 직렬화/동등성에 포함해 원격/늦은 참가의 표시를 맞춘다. 새 입장/재사용 좌석은 선택과 확정을 초기화해 새 참가자 본인의 첫 게임 전 선택을 허용한다. 소유자 RPC·발신자/경기/단계/연결/준비 검사와 F11 선택 보존은 유지하며 프로토콜41이다. 이 규칙이 이전 최종 결과 재선택 설명을 대체한다.

검증(2026-10-21): 첫 시작 확정/결과 변경 거절/재경기 기존 선택 유지/신규 좌석 초기화/스냅샷 직렬화·동등성 등 규칙41개와 실제 Windows 호스트15개·일반 참가자14개·늦은 참가자14개, 총84개 검사를 통과했다. 첫 로비에는 직접 선택이 보이고 미니게임/최종 결과에는 숨겨지며 재선택 RPC도 거절하는 것을 확인했다. Windows 개발 빌드와 최종 결과 렌더 검토를 완료했다. 전용 서버/인터넷 고지연 실기동은 미포함이다.

### 2026-10-21 로컬 시점·확정 입력 변경 (프로토콜41 유지)

DuelFirstPerson은 아이템 사용/채팅/감전/사망 얼굴 카메라·복귀의 입력 보호를 유지하고 로비·최종 결과·관전의 개요 카메라에도 로컬 회전을 적용한다. 개요 화면은 자기 모델과 자유 커서를 유지하고 세계 수직축 기준으로 회전해 수평선이 기울지 않게 한다. Racing 진입 시 커서를 재확보하며 방장 직접 선택 화면은 시점 이동과 UI 커서를 함께 허용한다. 초읽기/신호등/욕심의 대가 확정은 Space만 받고 마우스 확정과 클릭 버튼을 제거한다. 서버의 Look/StopClock/Fire 검증과 패킷은 바꾸지 않는다.

검증(2026-10-21): Windows 실행본71개 검사와 개발 빌드를 통과했다. 로비/방장 직접 선택/초읽기·신호등·욕심의 대가의 진행·순위·복귀/최종 결과 시점 이동, 아이템 사용·채팅 중 시점 고정, 개요 카메라의 수평선 유지, 커서가 필요한 화면의 UI 커서 보존을 확인했다. 세 미니게임의 월드/안내 클릭 무입력·Space 확정, 실제 우클릭 ADS/좌클릭 발사·탄약 소비도 확인했다. 로컬 입력 검사이며 원격 네트워크/전용 서버 재검증은 포함하지 않았다. 재현 도구는 Tools/UnityQA/MouseLookRuntimeQA.cs다.

### 2026-10-21 팔면체 주사위 확정 (프로토콜43)

DuelDiceContest는 서버의 순서 있는 동점 그룹을 보관하고 ClockState.Dice에는 현재 굴림 round, 대상/미확정·확정 마스크, 서버 시작/마감 시각, 좌석별1~8 눈/굴림 수만 공개한다. 실제 눈은 서버의 검증된 Stop/10초 자동 Stop에서만 선택한다. 회전은 로컬3D 렌더이며 렌더 프레임마다 RPC/스냅샷을 보내지 않는다. 모든 확정 후1.2초 비교하고 같은 눈의 하위 그룹만 새 round로 돌린다. 원래 점수/오입력/사망 배치 순서를 유지하며 이탈자를 제거한다.

StopDiceRpc는 InvokePermission.Owner/SendTo.Server이며 서버가 SenderClientId==OwnerClientId와 등록/스폰/연결, 경기/iteration/굴림 round, 현재 Dice 단계/대상/기확정/마감을 검증한다. 클라이언트의 숫자/순위/시간 표본은 받지 않는다. 사망한 대난투 동점자도 연결돼 있으면 자기 굴림만 확정할 수 있다. AI는 서버 함수로 자동 정지한다. 스냅샷/Equals/직렬화에 Dice를 함께 넣어 최신 상태만으로 표시가 복원되며 패키지 버전은 유지하고 프로토콜43으로 올린다. 최종 대난투 판정이 진행 중일 때 최종 승리를 먼저 발행하지 않는다.

### 2026-10-21 테이저 피격·전체 선택 설정 (프로토콜42)

AutoSelectItems는 방장 로컬 개발 설정이며 Host/DeveloperMode/Idle/비경기 검증 후 연결 좌석의 선택 마스크를 모두 채운다. 이후 Join에도 적용한다. 스냅샷 bool의 From/Serialize/Equals를 함께 갱신해 늦은 참가자가 같은 설정과 선택을 받는다. 준비 비트를 지우지 않고 F11 종료는 옵션만false로 복원한다. 지급은 기존 파티 시작 서버 경로를 사용한다.

UseItem의 서버 재사용 마감은 사용 iteration+3으로 두어 다음 두 미니게임을 막는다. 무한 아이템은 검사만 우회하며 일반 사용 권한/단계/생존/소지품/대난투 금지는 유지한다. Stunned에서 파생하는3인칭 전기 효과는 로컬 표시로만 추가한다. 실제 LastHitMask의 테이저 명중도 ReportKillRpc에 보내되 서버가 결정한 fatal로 실제 처치와 구분한다. RPC는 계속 Server 발신/Owner 수신이며 비치명 명중은 사망 안내/킬 통계를 만들지 않는다. 스냅샷/RPC 형식 변경으로 프로토콜42다.

### 2026-10-21 최종 결과 시점 고정 (프로토콜41 유지)

DuelPhase.Finished에서 DuelFirstPerson의 회전/정조준 입력을 차단하고 RouletteTable은 고정3인칭 결과 카메라와 자기 모델을 표시한다. 최종 피격/사망 얼굴 카메라가 이를 덮어쓰지 않는다. 커서와 기존 준비/취소·채팅은 유지하며 전원 준비 후 재경기 Countdown부터1인칭으로 돌아간다. 최초 로비/미니게임 순위는1인칭을 유지한다. 기존 결과 자유 시점 설명을 대체한다. 서버/스냅샷/RPC 변경 없이 호스트/원격/늦은 참가 Windows 실행본111개 검사와 빌드를 통과했다.

## 2026-10-21 준비 취소

로비/최종 결과에서 다른 참가자를 기다리는 동안 준비 버튼을 다시 누르면 본인만 준비를 취소한다. 준비 상태에도 버튼을 활성화하고 문구를 준비 취소로 표시한다. 서버 Director.Ready가 Idle에서 기존 준비 비트를 확인해 SetReady 또는 CancelReady를 적용하고 최신 스냅샷을 발행한다. 다른 참가자의 준비·선택·설정은 유지한다. 취소 후 무기 선택과 첫 시작 전 아이템 선택을 다시 편집할 수 있다. 이미 첫 게임에 확정한 아이템 조합은 최종 결과의 준비 취소로 풀리지 않는다. 새 준비도 기존 전원 준비/아이템2개 조건을 따른다. 전원 준비로 게임 시작 카운트다운에 진입한 뒤에는 취소하지 않는다. 소유자 RPC/발신자·연결·스폰·경기 번호 검증을 유지하고 패킷/프로토콜41은 바꾸지 않는다.

검증(2026-10-21): 규칙47개와 실제 Windows 호스트28개·원격 참가자29개·늦은 참가자24개, 총128개 검사를 통과했다. 호스트/원격의 준비→취소→재준비, 상대 준비 유지·초기 선택 편집 복원·아이템 미완성 준비 거절, 최종 결과 준비 취소와 기존 아이템 확정 유지, 잘못된 발신자/이전 경기 요청 거절을 확인했다. Windows 개발 빌드 통과. 전용 서버/인터넷 고지연 실기동은 미포함이다.

### 2026-10-21 초읽기 핑 진단

게임 코드를 바꾸지 않고 Windows1:1 UDP 인공 지연 실험을 수행했다. 고정 RTT32/166ms에서는 숨김 전후 속도가 정상이었으나, 지연 급변 시 NGO 서버 시각을 쓰는 숨겨진 타이머가 약±0.300초 움직였다. 고정 RTT334ms 조건에서는 정지 요청이 거절되고 시간 초과로 목숨이 차감됐다. 현행 sample<now-.3 검증과 측정 요청 시각 차이357.9ms가 일치한다. 호스트 본인은 안정적이었고 실제 사용자 인터넷 회선/6인 부하는 최종 통계에 포함하지 않았다. 상세 원자료와 한계는 Docs/QA/Clock-Ping-2026-10-21.md를 참고한다. 진단만 수행했으며 판정/패킷/프로토콜41은 유지했다.

### 2026-10-21 전원 실패 동점의 사격자 (프로토콜43 유지)

프로젝트 설계: DuelClockContest는 실패 동점도 주사위 확정 순위의 첫 생존 참가자를 First로 정한다. 신호등 전원 실패/욕심의 대가 전원 폭발에서도 서버가 기존 결과→아이템→한 발 사격 경로를 사용한다. 초읽기 시간 초과는 기존대로 정렬/주사위 후보에서 제외되므로 전원 시간 초과 시 First=-1, 목숨 차감 후 사격을 생략한다. RPC 권한·nonce·접속/생존/단계 검증과 공개 상태 직렬화는 바꾸지 않는다.

### 2026-10-21 뭐였더라?의 우승자 전용 주사위 (프로토콜43 유지)

프로젝트 설계: 기억 게임은 하위 동점을 별도 순위로 나누지 않는다. 다른 오입력 기록에서 최다 오류 참가자들을 탈락시키고 남은 인원은 기존 라운드/길이를 계속한다. 남은 전원이 같은 양수 오류일 때만 DuelDiceContest.Begin의 서버 전용 firstPlaceOnly 옵션을 사용한다. 최고 눈만 동점이면 재굴림하고 하위 눈 동점은 생략한다. 결과의 Rank는 우승1/탈락2 표시이며2는 세부 순위가 아니다. 다른 미니게임은 기존 전체 순위 주사위를 유지한다. 옵션은 서버 내부 판정으로만 사용하므로 새 공개 필드/RPC는 없고 기존 PendingMask/nonce/마감·권한 검증을 유지한다.

### 2026-10-21 공개 주사위 표시 (프로토콜43 유지)

프로젝트 설계: 각 화면은 기존 Clock.Dice의 참가자/미확정/확정 마스크·눈·서버 시각으로 타인 머리 위 회전/확정 주사위를 렌더한다. 회전은 서버 단계 시작으로부터의 경과 시각을 사용하고, 눈은 서버가 확정한 값만 표시한다. 새 스폰/네트워크 필드/RPC는 없으며 원격·관전자도 같은 공개 상태를 읽는다. 미니 결과3초 뒤나 새 경기/연결 종료 때 숨기고 화면 밖 좌석 렌더 카메라는 끈다. 외부 UI/숫자·마우스/채팅 입력 권한은 바꾸지 않는다.

### 2026-10-21 좌석 기준 시선 검증 (프로토콜43 유지)

프로젝트 설계: DuelGunplay.LookYawLimit=85를 마우스 원본 입력과 서버 Look/자발적 Fire에서 공유한다. 서버는 허용 범위 밖 yaw를 경계값이나 적 방향으로 보정하지 않고 거절한다. 검증 뒤 기존 Sway/Spread/유한 속도 탄환 판정을 그대로 적용하므로 실제 발사 방향은85도를 넘을 수 있다. 소유자/Round/Version/생존/사격 권한 검증과 직렬화는 유지한다. 서버 내부 involuntary 강제 격발은 기존 감전 경로만 사용하며 클라이언트 RPC는 이 예외를 제출하지 못한다.

### 2026-10-21 욕심의 대가 가속·비공개 폭발 확률 (프로토콜43 유지)

프로젝트 설계: 점수는 공통 StartedAt 기반30초 cubic 함수, 최대150,000이다. 서버 전용 Greed 기준값은50~150,000 균등 정수이며 실제 폭발은 점수 진행에 따른 누적확률을 역추첨해 미리 결정한다. 같은 시드/표본은 Tick 빈도와 무관하게 같은 폭발 시각을 만들며 기준값/시각은 PublicState/스냅샷에서 숨긴다. 기존 StopClock 소유자/경기/iteration/시각·중복/생존 검증과 도착 여유0.3초를 유지한다. Greed 주사위도 firstPlaceOnly를 사용해 최고 점수 그룹의 승자 한 명만 결정한다. 우승 후보 이탈로 다음 그룹이 승격되면 새 PendingMask를 ParticipantsMask에도 포함해 소유자/관전자 표시를 복구한다. 클라이언트는 점수/위험도/폭발/사격자를 결정하지 않으며 새로운 네트워크 필드는 없다.

## 2026-10-21 사격자 명중음 (프로토콜44)
서버가 실제 피해를 확인한 기존 ReportHitRpc(SendTo.Owner, InvokePermission.Server)에 headshot bool을 추가했다. 서버가 LastWeapon==Revolver 및 실제 LastHitPart==Head로 결정하고 클라이언트의 스냅샷/RPC 도착 순서에 의존하지 않는다. 소유자 화면은 기존 Round/Life/shot 검사와 중복 억제를 통과한 히트마커와 함께 일반/머리 소리를 한 번 재생한다. 소드오프의 여러 피해는 한 번이며 고정 피해 총기의 머리 접촉은 일반음이다. 빗나감·소품 파손·다른 사격자에게는 재생하지 않는다. 늦은 참가자에게 과거 소리를 복원하지 않는다. 피해/투사체/스폰/준비/게임 규칙은 그대로며 전용 서버는 오디오를 실행하지 않고 소유자 RPC만 보낸다. NGO2.13.3/Unity6000.3.18f1/Transport2.7.3 유지. RPC 형식 변경으로 프로토콜44다.

## 2026-10-22 준비 완료 연습 사격·피격 연출 (현재 규칙, 프로토콜45)
Stage Idle이며 본 경기가 진행 중이 아닐 때, 연결된 ReadyMask의 플레이어만 준비 연습에 참가한다. 준비하면 각 소유자의 화면은1인칭 조작/선택 무기 직접 사격으로 전환한다. 준비 취소 버튼은 화면 아래 중앙에 남긴다. 최종 결과의3인칭 고정·승리/결과 패널은 아직 준비하지 않은 소유자에게 유지하고, 준비한 소유자는 연습 화면을 본다. 원래 최초 로비의1인칭 카메라는 유지한다. 모두 준비하면 기존 시작 카운트다운으로 넘어가며 연습 탄환/피격/탄약 상태를 정리한다.

연습은 선택한 리볼버/소드오프에 장전 입력/탄 소모/사격 시간 제한이 없다. 서버는 기존 Owner Fire/Look 요청에서 경기 번호·버전·시선 범위·시각 표본을 검증한다. 발사 간격은 리볼버0.16초/소드오프0.30초 이상이고 비행 묶음은 최대32개다. 기존 서버 유한 속도 탄환/산탄 퍼짐/테이블·의자·소품 가림을 쓰며 발사 당시와 피격 당시 모두 준비한 대상에게만 명중 연출을 적용한다. 소드오프 이웃 연출도 기존 비순환 좌석 규칙을 따른다. 소품은 연습 중 가림만 제공하고 파손하지 않는다. 준비한 캐릭터는 음주 동작을 중단한다.

연습은 실제 HP/목숨/킬/피해/경기 사격 수/최종 기록/아이템을 소비하지 않는다. 연습 화면은 체력100/목숨3개를 보여주며 실제 지난 경기 기록은 보존한다. PracticeShotsFired와 PracticeSession을 스냅샷/직렬화/동등성에 추가하고 실제 ShotsFired와 분리한다. 화면·무기음의 ShotSerial은 표시용 합계다. 준비 취소/새 참가/이탈로 준비가 해제되면 세션과 버전을 갱신해 지연 요청·탄환·확인 응답을 무효화한다. 늦은 참가자는 기존 방 정책에 따라 전원 준비를 해제한다.

명중은 서버의 연습 탄환 접촉 뒤 소유자 전용 ReportPracticeHitRpc(SendTo.Owner/InvokePermission.Server, Round/Session/shot/headshot)로 알려준다. 수신자는 자기 플레이어/현재 준비/경기/세션/중복을 검사하고 기존 히트마커·명중음을 재생한다. 다른 사람의 명중음이나 킬로그는 만들지 않는다. 서버는 카메라/오디오를 참조하지 않으며 피해 결정 권한은 그대로 서버에 있다. NGO2.13.3/Unity6000.3.18f1/Transport2.7.3 유지, 새 필드/RPC 형식으로 프로토콜45다.

조준 경고 가장자리는 주황색이며 정조준 중0.05배 규칙을 유지한다. 실제 사격 피격(연습 포함) 시에는 별도 넓은 주황 가장자리가0.04초 안에 나타나 안쪽으로 번지고0.7초 안에 사라진다. 피격은 서버 HitAt 기준이며 실제 피격 연출에는 정조준 경고 감쇠를 적용하지 않는다. 검은 ADS 가장자리/줌과 평소 크로스헤어 없음은 유지한다.

## 2026-10-22 BAS·조준/지향사격 (현재 규칙, 프로토콜46)

선택 무기에 BAS(값3)를 추가한다. 기존 리볼버0/소드오프1/테이저2는 유지하며 선택 목록은 명시적인 Revolver/SawedOff/BAS 배열이다. 로비·최종 결과·로컬 무기 HUD·메인 무기 정보·킬로그에 이름과 저격총 아이콘을 제공한다. 준비 연습에서도 선택 가능하며 무한 탄약/비피해 규칙은 그대로다. 다른 미니게임의 공유 한 발 리볼버 규칙은 유지한다.

BAS는 스코프/올리브색 개머리판/긴 얇은 총열/오른쪽 볼트·탄 배출구를 갖춘 로우폴리 단발 볼트액션이다. 대난투 처음은 집기→볼트 열기→한 발 장전→볼트 닫기(각4자리, 총16자리), 이후는 볼트 열기(자동 탄피 배출)→한 발 장전→볼트 닫기(총12자리)다. 탄을 배출구에 직접 넣는 모션과 오른쪽으로 나오는 탄피를 표시한다. 몸통/다리는0피해이며 피격 플래시·히트마커·명중음도 없다. 실제 서버 투사체 헤드샷은100피해/현재 미니게임 사망 한 번/목숨2개 차감(0하한)이다. 킬은1회만 기록하고 실제 남은 HP만 피해 통계에 더한다. 잔여 목숨이 있으면 기존 복귀 규칙을 따른다.

BAS ADS는3배율 원형 조준경/검은 외곽/스코프 내부 레티클이다. 자기 총기는 ADS 중 숨겨 시야를 막지 않고 타인 모델은 보인다. 평소/다른 총기의 크로스헤어는 없다. BAS 흔들림은 기본1.8도×5=9도(리볼버6.3도보다 큼), 수직0.7배로 ADS/지향사격이 같다. 지향사격 BAS는35~70도 범위의 중심을 제외한 극심한 무작위 편차, 리볼버는10도 원형 편차다.

모든 총기는 ADS 시 무작위 탄퍼짐0이다. 흔들리는 실제 카메라 조준 방향을 정확히 따라가며 대상 추적/명중 보장/가림 무시는 없다. 서버 ADS 탄환은 눈 앞0.08m의 시선 레이에서 시작해 평행 총구 오차를 없앤다. 렌더 총구는 별도로 기존 정상 무기 위치를 유지하고 트레이서는 그 총구에서 서버 레이 종점으로 이어진다. 소드오프 ADS37발은 같은 조준 방향이며, 지향사격은12도 무작위 산탄/최대6m로 더 가까운 상대만 실제 접촉할 수 있다(ADS20m). 실제 접촉자 중 중앙에 가까운 한 명75와 번호 이웃25를 한 번만 적용한다. 서버와 클라이언트 탄환의 범위가 같다.

발신자/소유권/좌석각도/Version/시간/잔탄 검증과 서버 피해 권한은 유지한다. 새 네트워크 필드/RPC는 없으나 무기 ID와 행동·탄도 의미 변경으로 프로토콜46을 사용한다. 설치 버전 NGO2.13.3/Unity6000.3.18f1/Transport2.7.3은 유지한다. BAS 발사762x54r Single MP3, 볼트Semi 22LR Rack MP3, 장전Pump Shell Load MP3를 사용하고 소드오프 장전도 같은 Pump Shell Load로 교체한다. 원본과 재생성 경로는 Docs/Audio/Sources.md를 참조한다.

BAS 보충: 긴 총을 위한 1인칭 준비 자세는 시야 아래에 옆으로 들어 볼트/배출구와 한 발 삽입을 보여준다. 비조준 탄퍼짐은 발사 방향에만 적용하며 총구의 시작점을 무작위로 옮기거나 렌더 총을 그 각도로 꺾지 않는다. 준비 연습 BAS의 연속 격발 간격은0.8초이며 탄약/HP/목숨은 소모하지 않는다.

## 2026-10-22 BAS 레버액션 외형·탄피 배출 (현재 규칙)
BAS는 사용자 참조 이미지에 맞춘 레버액션으로 정정한다. 오른쪽 볼트 손잡이 대신 아래쪽 큰 레버 고리, 은색 리시버, 어두운 목재 개머리판/앞손잡이, 총열 아래 튜브를 갖춘 로우폴리 형태다. 기존 스코프/피해/탄퍼짐/목숨2개/단발 규칙은 유지한다. 행동 이름은 집기→레버 열기→한 발 장전→레버 닫기(각4자리), 재사격은 레버 열기부터3행동이다. 오른손은 레버 고리를 움직이고 왼손은 앞손잡이를 받치며, 한 발 삽입 때만 왼손이 배출구로 간다. 지정했던 Semi 22LR Rack 녹음(BasBolt 리소스 키)을 레버 열기/닫기에 그대로 사용한다.

실제 사격 후 레버 열기 완료로 서버 SpentMask가 비워지면, 각 화면에서 별도 빈 탄피가 오른쪽 배출구에서 튀어나와 회전/포물선으로0.85초간 이동한다. 탄두 없는 빈 탄피이며 충돌/피해 권한은 없다. 입력 진행률에 탄피가 매달리거나 새 실탄과 같이 움직이지 않는다. 좌석당1개를 재사용하고 첫 빈 총 준비/동일 스냅샷/늦은 초기 상태에는 과거 배출을 재생하지 않는다. 경기/생명/연습 세션 변경 및 무기 해제 시 정리한다. 서버 행동/잔탄/RPC/직렬화는 바꾸지 않는 표시 수정이므로 프로토콜46을 유지한다. 이전 볼트액션 명칭/외형/모션 설명은 이 항목으로 대체한다.

## 2026-10-22 BAS 매 발사 후 필수 레버 입력 (프로토콜47)
BAS는 발사한 순간 소유자 ADS/스코프를 즉시 해제한다. 탄이 있어도 Action이 Aim으로 복귀하기 전에는 서버가 ADS/Fire를 거절한다. 본 경기의 레버 조작→한 발 장전→레버 닫기(각4자리)는 유지한다. 준비 연습에서도 BAS는 발사마다4자리 레버 조작을 완료해야 다시 조준/발사한다. 연습용 탄은 무제한이지만 이 기계 조작은 생략하지 않는다. 다른 총기의 준비 연습은 기존 규칙을 유지한다.

준비 연습에서만 ForSeat의 BAS OpenBarrel/SpentMask를 보존한다. Owner Input 요청은 서버의 현재 연습 참가/무기/단계/Version/자리/마감·오입력 대기를 검사한다. 레버 완료 후 Aim/새 Version으로 전환하며 연습 입력은 실제 Mistakes/CorrectInputs/경기 결과를 바꾸지 않는다. 준비 취소/재준비·새 경기에는 이전 레버 상태를 초기화한다. 연습 입력창에는 레버 조작 이름과4개 숫자만 표시하고 준비 취소 버튼은 유지한다. 기존 배출구 탄피/조작음도 연습 입력 완료에 적용한다. 클라이언트는 발사 요청 직후 pending 상태에서 ADS 재진입을 막고, 서버 상태를 받은 뒤에도 실제 액션 완료를 기다린다.

네트워크 필드/RPC 서명은 같지만 이전 클라이언트는 연습 레버 입력을 지원하지 않으므로 프로토콜47로 올린다. 설치 NGO2.13.3/Unity6000.3.18f1은 유지한다. 이번 작업에서 모든 총기의 흔들림 수치·패턴·ADS 탄퍼짐은 변경하지 않는다. 느린 호흡/고정 반동 패턴은 제안 단계다.

## 2026-10-22 Alt 조작·LAN 방 목록·발사 시점 추종 (현재 규칙, 프로토콜48)

- 방 입장 후에는 왼쪽/오른쪽 Alt를 누르는 동안만 커서와 UI 마우스 조작을 허용한다. 놓으면 즉시 시점 조작으로 돌아가며 해제 프레임에는 사격/행동 입력을 막아 클릭이 새지 않게 한다. 메인화면은 일반 커서를 유지한다. 아이템 선택/사용 시간에도 자동으로 커서를 풀지 않으며 시점 회전은 허용한다. 채팅은 Enter로 바로 입력하되 마우스 조작/기록 스크롤은 Alt를 사용한다. 최종 결과/완전 탈락의 고정3인칭은 유지한다.
- PvP 메뉴는 방 제목(최대40자)·선택 비밀번호(최대32자)·방 만들기/방 목록보기다. 같은 Wi-Fi/LAN에서 UDP로 방을 검색하고 선택한 방의 주소/게임 포트로 자동 참가한다. 인터넷 로비/Relay는 사용하지 않는다. 검색 포트47770~47785와 자동 게임 포트 선택으로 같은 PC의 여러 방도 지원한다. 목록에는 인원/최대6명·비밀번호 여부·대기/진행/가득 참을 표시한다. 검색 주기는1.5초, 응답이 끊긴 방은6초 후 제거한다.
- 서버 연결 승인에서 방 ID·비밀번호 proof·닉네임·경기 단계·정원을 검증한 뒤 좌석/플레이어를 생성한다. 검색에는 비밀번호/proof를 보내지 않으며 응답 nonce/프로토콜/길이/주소를 검사한다. 접속 proof는 방 ID와 비밀번호의 SHA256이며 암호화 통신/계정 인증을 제공하는 방식은 아니다. 잘못된 비밀번호/진행 중 참가 거절은 좌석을 차지하지 않는다. 방 종료/실패/오브젝트 파괴 시 검색 소켓을 정리한다.
- 격발/총구 섬광 중에도 렌더 총기는 현재 시선과 현재 ADS 위치를 따른다. 이미 발사한 탄환의 원점/방향/난수 시드와 서버 피격 판정은 발사 당시 값으로 유지한다. 카메라 회전이 날아가는 탄을 꺾지 않는다. 무기 흔들림/피해/행동 규칙은 변경하지 않는다.
- NGO2.13.3/Unity6000.3.18f1/Transport2.7.3 유지. 참가 payload/검색 호환성을 위해 프로토콜48을 사용한다. 검증은 `Docs/QA/LAN-Rooms-Alt-2026-10-22.md` 참조.

## 2026-10-22 소드오프 정조준 제거·ADS 전용 흔들림 (프로토콜49)

- 소드오프는 우클릭 정조준/줌/검은 ADS 테두리를 사용하지 않는다. 일반 시점에서37개 산탄을 매 발사 다른 서버 난수 시드로 반각6도 원뿔에 발사하며 최대20m까지 판정한다. 이전 ADS0도·20m/지향12도·6m 구분을 대체한다. 실제 피격자 중 화면 중심에 가까운1명에게75, 번호 이웃25/사격자 제외/1번과6번 비연결은 유지한다. 빗나감과 가림은 피해가 없다.
- 무기 흔들림은 정조준 중에만 적용한다. 지향사격은 카메라/총기/서버 발사 방향에 무기 흔들림을 더하지 않고 기존 리볼버10도/BAS35~70도 지향 탄퍼짐은 유지한다. ADS 리볼버6.3도/BAS9도·수직0.7배 진폭과 ADS 무작위 퍼짐0은 유지한다. 소드오프는 흔들림 없이 고정 산탄 범위를 사용한다. 감전/피격/반동 등 별도 효과는 그대로다.
- WeaponRules.SupportsAds를 로컬 입력과 서버 Look/Fire/준비 연습에서 공통 적용한다. 소드오프에 ADS=true를 제출해도 Aiming/FiredAds/발사 원점/퍼짐/사거리에 이득이 없다. AI의 소드오프는 Aiming=true를 기다리지 않고 목표 확보 후 사격하며 매 발사 뒤 목표를 새로 정한다. 권한/Version/생존/잔탄/시간 검증은 유지한다.
- 서버 탄도 의미가 달라져 프로토콜49로 갱신한다. 스냅샷/RPC 필드와 NGO2.13.3/Unity6000.3.18f1/Transport2.7.3은 유지한다.
- 박제 문구는 기존 DuelHighlights의 사실 기반 문장 규칙이 최종 결과에 연결되어 있다. 무킬 누적 피해100 이상/한 발 다중 처치/체력10 이하 최종 승리 등을 고르고, 일반50피해는 후보가 아니다. 특별한 사건이 없으면 경기 시간 문장을 표시한다. 미니게임 결과에는 별도 박제 문구 표시가 없으며 이번 조준 변경에서 추가하지 않는다.

## 2026-10-22 리볼버 지향 탄퍼짐·저격소총 명칭 (프로토콜50)

리볼버 지향사격도 기존 BAS와 동일한 중심 제외35~70도 범위를 사용한다. 최대 반경70도와 `70*sqrt(0.25+0.75*u)` 반경/무작위 방위각을 공유하며 준비 연습과 본 경기의 서버 Fire 모두 DuelGunplay.PrecisionSpreadRadius를 호출한다. 정조준의 무작위 퍼짐0·ADS 전용 기존 흔들림/피해/장전/잔탄/실제 투사체 판정은 유지한다. 소드오프6도/20m도 유지한다.

공개 무기 이름 BAS는 저격소총으로 바꾼다. 무기 선택·메인 정보·체력 위 선택 무기 툴팁·킬/피격 로그는 WeaponRules.Name을 통해 같은 이름을 사용한다. 내부 enum DuelWeapon.BAS(값3), 모델/오디오 리소스 키/파일명은 그대로 두어 참조와 직렬화를 유지한다. 서버 탄도 의미가 달라 프로토콜50으로 갱신하며 스냅샷/RPC 필드·발신자/권한/생존/Version/잔탄/시각 검증은 유지한다. Unity6000.3.18f1/NGO2.13.3/Transport2.7.3은 변경하지 않는다.

### 먹물팩 추가 (2026-10-22, 프로토콜51)

DuelInkState의 소지/장착/Version/투척 일련번호/원점·방향/투척·충돌·피격 시각을 좌석 스냅샷의 명시적 직렬화와 동등성에 포함한다. 소유자 전용 ThrowInkRpc와 서버 발신자 매핑/라운드·미니게임·Life·Version·단계·재고·시각·시선 검증을 사용한다. 서버의 고정 간격 포물선 구간 검사만 피격을 확정하며 클라이언트는 공통 시각으로 파우치와5초+0.5초 먹물을 재현한다. 투척체별 NetworkObject/클라이언트 충돌 보고는 없다. 늦은 상태 적용에도 스냅샷 시각을 사용하고, 진행 중 신규 참가 금지 정책은 유지한다. 사망/재경기/미니게임 전환/연결 해제에서 장착·비행 수명을 정리한다. 아이템3종 중2개 선택(개발 옵션 전체3개), 무한 소비 예외는 서버가 결정한다. 상세 규칙은 Roulette-Prototype의 먹물팩 절을 따른다.

### 대난투 인벤토리 (2026-10-22, 프로젝트 프로토콜52)

아이템 사용 시간의 한 칸 예약과 실제 장착/발사·투척/소비를 분리한다. QuickItem/QuickEquipped/QuickSpent/QuickItemVersion은 좌석 스냅샷의 직렬화와 동등성에 포함한다. 총기의 기계적 상태는 서버에 보관해 전환 시 복원하며 체력/통계 등 생존 상태는 과거 값으로 되돌리지 않는다. 소유자 SwitchQuickItemRpc는 발신자 매핑과 Round/Iteration/Life/Revision·단계/생존/감전/선택·소모 검증을 통과해야 한다. 테이저 발사와 먹물 투척은 기존 서버 투사체/효과 시각 흐름을 사용한다. 예약 아이템의 미사용 소비/쿨타임은 사망/결과/퇴장에도 서버에서 멱등 처리한다. 추가 NetworkObject/클라이언트 충돌 권한은 없으며 진행 중 신규 입장 제한은 유지한다.

### 로비 준비 유지 (2026-10-26, 프로젝트 프로토콜52 유지)

ReadyMask는 서버가 관리하며 참가/퇴장 시 해당 좌석 비트만 초기화한다. 설정 변경·개발 설정 해제는 준비를 바꾸지 않는다. 연결된 모든 플레이어에게 같은 스냅샷을 배포하고 새 참가자의 초기 동기화에서도 기존 준비 상태를 보인다. 연습 사격은 기존 참가자의 PracticeSession/기계적 상태를 유지하면서 로스터 변경 시 서버의 이전 비행 목록만 비워 재사용 좌석 피격을 막는다. 소유자의 명시적 준비 취소·경기 시작/종료의 기존 초기화는 유지한다. 미준비자가 나가 남은 전원이 준비된 경우 서버가 기존 TryStart를 실행한다. RPC/직렬화 필드와 권한은 변경하지 않으므로 프로토콜52를 유지한다.

### 머리 시선 표현 (2026-10-26, 프로젝트 프로토콜52 유지)

미준비 로비·시작 카운트에서도 기존 소유자 전용 비신뢰 LookRpc와 AimYaw/AimPitch 스냅샷을 재사용한다. 서버가 좌석 연결/단계/각도/호출 주기를 확인하고 해당 단계에는 ADS/사격 권한을 부여하지 않는다. 머리의 감쇠 보간/범위 제한/소폭 roll은 클라이언트 표현이며 카메라 원본 각도와 서버 탄도·피격에 반영하지 않는다. 몸통 시선 회전은 제거하고 머리 월드 방향을 좌석 기준으로 조합해 기대 자세/고개 움직임을 분리한다. 스냅샷/RPC 필드와 권한은 그대로이므로 프로토콜52를 유지한다.

### 먹물팩 표시·음향 (2026-10-26, 프로토콜52 유지)
기존 확정 InkedAt과 Clock.PhaseAt/StageStartedAt으로 화면 및 얼굴 자국의 자연 종료와 사격 종료0.5초 페이드를 계산한다. 클라이언트의 DuelInkFade는 첫 종료 시각을 유지해 Dice에서 QuickResults로 넘어가거나 최종 결과가 추가되어도 같은 자국을 재활성화하지 않는다. 효과 종료로 서버 피해/권한/아이템 상태를 바꾸지 않는다. 투척/적중음도 기존 Serial/ThrownAt/ImpactAt과 소유자 좌석으로 로컬 재생하며 초기 과거 상태·중복·새 Life/새 라운드 재생을 억제한다. 추가 RPC/필드/NetworkObject는 없다.

## 2026-10-26 총알 아이템 (프로토콜53)
로비에서 총알을 고르면 파티 시작/최종 재경기에2개를 지급한다. 아이템 사용5초 동안 대난투를 제외한 미니게임의 살아 있는 사격자만 사용할 수 있다. 한 턴에1개만 사용하고 즉시1개를 소비해 그 사격 턴의 발사 기회를1회 추가한다(기본1발→2발). 미니게임 사이 수량은 유지하고 새 선택마다 추가 기회/사용 표시는 초기화한다. 사용 후 실제로 쏘지 못해도 환불하지 않는다. 무한 아이템 설정은 수량만 보존하며 한 턴1회 제한은 유지한다.
기존 서버 투사체가 첫 발을 처리한 뒤 추가 한 발을 자동 준비한다. 숫자 재장전은 대난투 전용이며, 대난투에서는 이 아이템을 쓸 수 없다. 추가 발사는 최초 사격 제한시간을 늘리지 않는다. 한 명만 남거나 사격자가 탈락하면 기존 종료 규칙을 따른다. AI도 자신이 사격자일 때 사용한다. UI는 총알 두 발 썸네일·이름·남은 개수·활성화 노란 테두리를 표시하며0개는 숨긴다. 메인 설명에는 발사1회 추가/2개 지급/소모성을 표시한다.
기존 소유자 전용 UseItemRpc를 재사용하고 발신자/좌석/생존/Iteration/준비 마감/사격자/수량/동일 턴 사용을 서버에서 검증한다. BulletItems/BulletUsed를 스냅샷 직렬화·동등성에 추가하고 개발용 전체 아이템 선택도4종에 맞춘다. 클라이언트가 추가 횟수나 수량을 결정하지 않는다. 중도 경기 입장은 기존처럼 거절하며 새 접속에는 현 스냅샷을 전달한다. 프로토콜53으로 이전 빌드 접속을 분리한다.

## 2026-10-26 노루페이스·테이저 자유 대상 (프로토콜54)
노루페이스는 두꺼운 로우폴리 패딩이며 소유한 생존자는 사격자를 포함해 아이템 사용5초에 착용한다. 이번 사격 턴의 테이저 피해5와 감전을 모두 막고, 일반 총격 피해는 막지 않는다. 사격 종료/사망/퇴장 때 보호와 착용 외형을 해제한다. 사용 미니게임N 이후 N+1/N+2는 사용 불가이며 N+3부터 재사용한다. 소비하지 않고 소지품에 남고, 무한 아이템은 쿨다운을 면제하되 중복 활성화는 막는다. 대난투에서는 회복처럼 즉시 쓰는 아이템으로 기존1개 사용 슬롯을 차지한다. 다른 아이템과 같이 고르는 종류 수는 기존2개다.
서버 HasNoruFace/NoruFaceActive/NoruFaceReadyIteration을 스냅샷/동등성에 추가하고 기존 소유자 전용 UseItemRpc의 발신자/좌석/생존/Iteration/준비 마감과 소지/쿨다운/중복 사용을 검증한다. 면역은 실제 서버 투사체 처리에서 피해/감전/히트마커/처치·감전 로그 전에 적용하며 테이저 탄은 그대로 소비된다. 신규 지급/재경기에 선택 상태로 소유·쿨다운을 초기화한다. 개발용 전체 선택은5종을 포함하고 네트워크 프로토콜54로 구버전을 분리한다.
테이저를 든 비사격자는 사격자 외의 생존자에게도 자유롭게 사격할 수 있고 AI도 자유 대상을 고른다. 비사격자 피격은 부위 무관5 피해·시점 잠금·감전 표시만 적용하며 무작위 에임/자동 격발은 하지 않는다. 사격자 또는 대난투 참가자의 기존 무작위 격발은 유지한다. 사격 종료 시 서버가 Stunned/StunnedAt와 보호를 즉시 정리하고 클라이언트도 실제 Shooting/QuickHands 밖에서는 감전 화면·시점 잠금을 표시하지 않는다. 전기 접촉 루프/제삼자 아크도 기존 combat 조건으로 종료한다.
패딩은 실제 상체/팔 본을 따르는 표시 전용 몸통/네 소매 메시이며 자기1인칭에서는 본인 모델과 함께 숨긴다. 다른 유저/관전에는 보이고 서버 피격 크기/팔 길이/콜라이더를 바꾸지 않는다. 썸네일은 지퍼와 가로 누빔이 있는 패딩이다. 사용 노란 테두리와 비활성 쿨다운을 구분하고 n게임 뒤 사용가능 형식을 재사용한다. 메인 정보/호버 설명도 갱신한다.

## 2026-10-26 봇 총기·아이템 다양화 (프로토콜54 유지)
AI 좌석은 로비부터 리볼버/소드오프/저격소총을 순환 배정한다. 봇3명 이상이면 세 종류를 모두 고르며, 봇마다 서로 다른 아이템2개 조합을 사용한다. 첫5봇은 다섯 아이템 종류를 모두 포함한다. 최종 재경기마다 무기는 다음 종류, 아이템은10개 가능한 두 종류 조합 중 다음 조합으로 바뀐다. 봇1명도 재경기를 거치면 세 총기/다섯 아이템을 모두 사용한다. 미니게임 사이에는 총기/선택 조합을 바꾸거나 소모품을 재지급하지 않는다. 사람의 선택/준비 상태와 PvP 선택은 건드리지 않는다. 개발용 전체 아이템 선택은 유지한다.
봇은 체력50 이하에서 앰플을 우선하고, 사격자일 때 총알, 상대 테이저가 준비돼 있을 때 노루페이스를 사용한다. 대난투의 한 아이템 제한에서 테이저/먹물/패딩 후보 순서를 봇·미니게임별로 바꾸어 특정 아이템만 계속 고르지 않는다. 선택한 테이저/먹물을 장비·발사/투척한 뒤 원래 총기 준비를 재개한다. 무한 아이템에서도 AI는 같은 장비를 반복 사용하며 총기 진행이 멈추지 않도록 테이저/먹물을 이번 미니게임에 한 번씩 사용한다. 일반 사격 턴에는 준비 중 먹물을 꺼내 두고 포물선/총구 위치를 고려해 실제 서버 투사체를 발사한다. 소모/쿨다운/권한/목숨과 대난투 총알 금지는 기존 서버 규칙으로 검증한다.
저격소총은 피해가 있는 머리를 겨냥하며, 소드오프는 정조준 없이 발사한다. 봇은 공개된 흔들림/총구 위치를 따라 조준을 보정하고 기존 난이도별 오차/반응·숫자 입력 속도/오입력 확률을 유지한다. 행동 숫자를 생략하거나 탄을 무한 지급하지 않는다. 실패한 발사는 재시도하며 기존 Version/잔탄/살아 있음/시간 제한을 통과해야 한다. 추가 AI 선택/사용 기록은 서버 내부 배열이며 기존 SelectedWeapon/SelectedItems/행동 스냅샷으로 현재 및 새 참가자에게 표시한다. RPC/네트워크 필드/프로토콜54는 변경하지 않는다.

## 2026-10-26 Q/E 생존자 1인칭 관전 (현재 규칙)

목숨0 완전 탈락자는 기존 쓰러짐/감각 회복/3인칭 전환(사망 뒤2.75초)을 끝낸 뒤 Q(이전)/E(다음)로 기본 고정 관전 시점과 현재 살아 있는 연결 참가자의1인칭 시점을 순환한다. 일시 사망/복귀 중인 참가자는 대상에서 제외하고, 관전 대상 사망/연결 해제 시 기본 시점으로 돌아간다. 채팅/Alt UI 조작 중 Q/E는 전환하지 않는다. 최종 결과에서는 기존 고정3인칭을 유지하고 새 파티/방 이탈 때 선택을 초기화한다.

DuelSpectator는 공개 스냅샷의 시선/ADS/무기/체력/감전/먹물 상태만 읽는다. 관전 대상 머리/목만 로컬 렌더에서 숨기고 몸과 무기를 표시하며 실제 좌석 눈 위치/총기별 줌·조준경·공통 흔들림을 따른다. 화면 아래에 관전 대상과 Q/E 안내를 표시한다. 소유자의 사망 내역/조작 권한/크로스헤어 사망 숨김 정책은 유지한다. 관전 대상의 입력·사격 RPC를 발신하지 않으며 개인 미니게임 정답/입력 화면은 복제하지 않는다. 새 네트워크 필드/RPC/프로토콜 변경 없이54를 유지한다. 과거 목숨0 관전 고정 규칙은 기본 시점과 최종 결과에만 적용한다.

## 2026-10-26 로비에서 봇 추가 (현재 규칙)

호스트에게 로비/최종 대기 화면 왼쪽 위에 봇 추가 버튼을 표시한다(Alt 홀드로 클릭). 클릭마다 빈 좌석 하나에 봇이 들어오며 사람+봇 최대6명이다. AI전과 LAN PvP 방 모두 가능하고 경기 진행 중에는 추가할 수 없다. 접속 승인 후 스폰을 기다리는 사람이 있을 때도 추가를 막아 자리를 빼앗거나 먼저 시작하지 않도록 한다. 다른 사람의 준비/무기/아이템 선택은 유지하고 봇은 두 아이템과 총기를 고른 뒤 자동 준비한다. 전원이 준비돼 있다면 기존 시작 규칙으로 시작한다.

서버 전용 DuelDirector.BotMask/IsBot이 사람과 봇을 구분한다. PvP의 사람 좌석을 AI가 조작하지 않으며 추가된 봇은5개 미니게임·주사위·사격·아이템 사용·최종 재준비에 기존3난이도 AI 로직으로 참여한다. 봇 퇴장 시 식별 비트를 지워 같은 자리를 재사용한 사람에게 AI 상태가 남지 않게 한다. 방 목록/승인은 봇 좌석과 아직 스폰하지 않은 사람의 예약을 포함해 정원을 계산한다. 클라이언트에는 기존 연결/준비/무기/아이템/AI 닉네임 상태만 동기화하며 새 RPC/직렬화 필드는 필요하지 않다. 프로토콜54 유지.

## 2026-10-26 방 코드 직접 생성·참가 (프로토콜55)

메인에서 방 코드(영문·숫자4~12자, 대소문자 구분 없음)를 직접 정해 방 만들기를 누른다. 기존 제목/선택 비밀번호/방 목록은 유지하며 같은 코드와 비밀번호로 코드로 참가할 수 있다. 로비 왼쪽 위와 대기 패널, 방 목록에 코드를 표시한다. 검색 범위는 기존 같은 Wi-Fi/LAN이며 인터넷 Relay 서비스는 추가하지 않는다.

생성 전2초 LAN 조회로 이미 사용 중인 코드를 거절하고, 코드 참가도2초 조회 후 실제 응답 주소/포트로 연결한다. 같은 코드가 여러 방에 있으면 임의 연결하지 않고 목록 선택을 안내한다. 요청 중 중복 생성/다른 Begin을 막고 Leave로 취소할 수 있다. 사용자 코드는 검색용이고 기존 무작위32자리 roomId/방별 비밀번호 proof 및 서버 참가 승인/정원 검증은 유지한다. 코드가 아닌 roomId가 실제 세션을 구분한다. 퇴장/실패/AI전 전환 때 코드/검색 상태를 지운다. LAN 광고에 code를 추가하고 구버전 혼합을 막기 위해 프로토콜55로 올린다. NGO/Transport 버전 및 경기 스냅샷/RPC는 바꾸지 않는다. 검증은 Docs/QA/Room-Codes-2026-10-26.md 참조.

## 2026-10-26 테이저 최대2초·1등 전용 주사위 (현재 규칙)

테이저 감전은 마지막 피격의 StunnedAt부터 최대2초다. 서버 TickShock가 만료/사망/사격 종료 때 감전과 예약된 무작위 격발을 해제하며 사격이 먼저 끝나면2초 전이라도 끝난다. 재피격은 새2초를 시작한다. 피해5/면역 패딩/아이템 소비·2게임 쿨타임은 유지한다. 소유자·타인·관전 화면의 시점 잠금/흔들림/전기 아크/전기음은 같은 IsStunned(ServerTime) 만료 판정을 사용해 오래된 스냅샷 때문에2초를 넘겨 표시하지 않는다. 추가 네트워크 필드 없이 프로토콜55 유지.

초읽기·신호등·욕심의 대가·뭐였더라?·대난투 모두 이미1등이 정해지면 하위권 무승부를 위한 주사위는 생략한다. 최고 기록 후보들만 굴리고, 최고 눈이 같은 사람들만 다시 굴린다. 대난투 생존1등이 있으면 같은 사격에서 사망한 하위 그룹은 굴리지 않는다. 1등 후보의 연결 해제로 한 명만 남아도 즉시 확정한다. 초읽기 전원 시간 초과의 목숨 차감/사격자 없음과 뭐였더라? 전원 정답 시 다음 라운드는 유지한다.
