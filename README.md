# Unity Game Framework

> Unity 게임 개발에서 반복적으로 구현되는 시스템들을  
> **프레임워크 단위로 정리한 공통 게임 개발 기반**입니다.
>
> 특정 게임에 종속되지 않으며,  
> 여러 프로젝트에서 재사용·확장하는 것을 목표로 합니다.

---

## 🛠 개발 환경
- **Unity**: 6000.0.68f1 (Unity 6 LTS)
- **에디터 / 도구**: Visual Studio Code, Claude Code
- **버전 관리**: Git / GitHub
- **패키지 배포**: UPM(Unity Package Manager), Git URL 기반 패키지별 개별 설치

---

## 📦 Frameworks

현재 포함된 프레임워크는 다음과 같습니다.

- **[Core](#core)**  
  모든 매니저가 상속하는 공용 베이스(MonoSingleton) + `BootPriority` 기반 초기화 순서 보장

- **[Data Parsing](#data)**  
  Google Sheet 기반 게임 데이터 파이프라인

- **[Pooling](#pooling)**  
  프리팹 기반 공용 오브젝트 풀링 시스템

- **[UI System](#ui)**  
  우선순위 / 선점 기반 팝업 흐름 관리 + 토스트 / HUD·Overlay 레이어

- **[Sound System](#sound)**  
  Addressables + Sheet 기반 사운드 재생/관리 시스템

- **[Save / Load](#saveload)**  
  Provider 기반 저장/로드 시스템 (AutoFlush / Backup / Restore 지원)

- **[Time System](#time)**  
  UTC 기반 시간 관리, 리셋, 쿨타임, 서버 시간 동기화 시스템

- **[Scene Loading](#sceneloading)**  
  비동기 씬 전환 + 로딩 화면 관리 시스템

- **[Input](#input)**  
  Unity Input System 기반 액션 입력 + 리바인딩 관리 시스템

> 프레임워크는 지속적으로 추가될 예정입니다.

---

## 📥 패키지 다운로드
필요한 패키지만 골라서 설치할 수 있습니다. Unity Package Manager → `+` → `Add package from git URL...`에 아래 주소를 붙여넣으세요.

|패키지|설명|설치 주소|
|-|-|-|
|Core|공용 싱글톤 베이스|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.core`|
|Data Parsing|Google Sheet 데이터 파이프라인|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.data`|
|Pooling|프리팹 기반 오브젝트 풀링|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.pooling`|
|UI System|우선순위/선점 기반 팝업 + 토스트|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.ui`|
|Sound System|Addressables + Sheet 기반 사운드 재생|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.sound`|
|Save / Load|Provider 기반 저장/로드|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.saveload`|
|Time System|UTC 기반 시간/쿨타임/리셋 관리|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.time`|
|Scene Loading|비동기 씬 전환 + 로딩 화면|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.sceneloading`|
|Input|Unity Input System 기반 액션 입력/리바인딩|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.input`|

> * Save / Load는 Core에 의존하므로 Core도 함께 설치해야 합니다.
> * Data Parsing은 Core에 의존하므로 Core도 함께 설치해야 합니다(`DataManager`가 `MonoSingleton<T>` 사용). 다만 테이블 로드 자체는 관례 경로(`Resources/GeneratedTables/{타입명}`)와 리플렉션 기반이라, 이름/구조만 맞으면 Data Parsing으로 생성하지 않고 직접 만든 SO도 그대로 동작합니다.
> * Sound System은 Core, Save / Load, Unity Addressables에 의존하므로 함께 설치해야 합니다.
>   * 사운드 데이터(`SoundTable`)를 직접 만드는 건 번거로우니, Data Parsing으로 시트에서 생성하는 걸 권장합니다.
> * UI System은 Core, Pooling에 의존하므로 함께 설치해야 합니다.
> * Scene Loading은 Core, UI System, Unity Addressables에 의존하므로 함께 설치해야 합니다.
> * Input은 Core, Save / Load, UI System, Unity Input System에 의존하므로 함께 설치해야 합니다.

---

<details id="core">
<summary><h2>0. Core</h2></summary>


### 기능
- 공용 싱글톤 베이스 `MonoSingleton<T>` 제공
- **씬 배치 불필요** - 처음 `.Instance`에 접근하는 순간 자동 생성
- `[BootPriority(int)]`로 매니저 간 초기화 순서를 코드로 직접 선언 가능
- Domain Reload 비활성화(Enter Play Mode Settings) 환경에서도 안전하게 동작
- 초기화(`OnInitialize`)는 정확히 1회만 보장

---

### 사용 방법

```cs
using GameFramework.Core;

public class MyManager : MonoSingleton<MyManager>
{
    protected override void OnInitialize()
    {
        // 씬에 아무것도 배치하지 않아도, 처음 MyManager.Instance에 접근하는
        // 순간 자동 생성되며 정확히 1회 호출됩니다.
    }
}
```

```cs
MyManager.Instance.DoSomething();
```

- 씬에 배치할 필요가 없습니다. 배치하지 않으면 자동 생성되고, Console에 안내 로그가 남습니다.
- 다른 매니저보다 먼저/나중에 초기화돼야 하는 매니저는 클래스에 `[BootPriority(int)]`를 붙이면, 씬이 로드되기도 전에 숫자가 작은 것부터 순서대로 초기화됩니다.
  ```cs
  [BootPriority(-100)]
  public class SaveManager : MonoSingleton<SaveManager> { ... }
  ```
  `[BootPriority]`가 없는 매니저는 기존처럼 누군가 처음 `.Instance`를 호출하는 시점에 초기화됩니다.
- 씬 전환 시 파괴되지 않아야 하면 기본적으로 유지됩니다. 유지되지 않아야 하는 매니저는 `protected override bool ShouldPersistAcrossScenes => false;`로 오버라이드하세요.

---

### 테스트 방법
`Assets/00.Scripts/Tests/BootOrderTester.cs`를 아무 GameObject에 붙이고 Play하면:
- `[BootPriority(-100)]`, `[BootPriority(-50)]`가 붙은 더미 매니저 2개가 씬 로드 전에 이미 자동 초기화되어 순서대로 로그에 기록되어 있는 걸 확인할 수 있습니다.
- attribute가 없는 매니저는 버튼을 눌러 `.Instance`를 직접 건드리기 전까지는 기록되지 않는 것도 함께 확인할 수 있습니다.

</details>

---

<details id="data">
<summary><h2>1. Data Parsing</h2></summary>

### 기능
- Google Sheet → ScriptableObject 자동 변환 (TSV 다운로드 후 파싱)
- Sheet Tab 선택 후 C# 테이블 스크립트 + .asset 자동 생성
- 기존 테이블 갱신(Update)/삭제 지원
- `DataManager`가 타입별로 로드 결과를 캐싱
- 생성되는 클래스 이름에 `Table` 접미사 자동 부여 (탭 이름이 `{TabName}`이면 클래스는 `{TabName}Table`) - 나중에 같은 이름의 게임플레이 클래스를 만들어도 충돌하지 않도록 방지

---

### 외부 패키지
Editor 툴이 시트 다운로드 대기에 **Unity 공식 Editor Coroutines 패키지**(`com.unity.editorcoroutines`)를 사용합니다.

---

### 사용 방법

#### 1) 시트 임포트 (Editor 전용)
`Game Framework/Data Parsing/DataTable Importer` 메뉴에서:
1. Sheet URL, API Key 입력 후 **시트 불러오기**
2. 원하는 탭 선택 후 **선택 시트 생성** - `{ScriptFolder}/{TabName}Table.cs`와 `Resources/GeneratedTables/{TabName}Table.asset`이 만들어집니다 (탭 이름이 이미 `Table`로 끝나면 중복으로 붙지 않습니다)
3. 시트 내용이 바뀌면 **선택 시트 갱신**, 탭을 지우려면 **선택 시트 삭제**

> 시트 형식: 1행=컬럼명, 3행=타입, 4행부터 데이터. 1열은 항상 `RowKey`(int)로 취급됩니다. 컬럼명이 `~`로 시작하면 무시됩니다.

지원 타입(3행에 그대로 적으면 됨):

|타입|C# 타입|비고|
|-|-|-|
|`int`|int||
|`long`|long||
|`float`|float||
|`double`|double||
|`string`|string||
|`bool`|bool|`1`/`0`/`true`/`false` 모두 인식|
|`int[]`, `long[]`, `float[]`, `double[]`, `string[]`, `bool[]`|배열|셀 안에서 콤마(`,`)로 구분. 예: `1,2,3`|
|`enum:EnumTypeName`|해당 enum 타입|먼저 C# 코드에 `public enum EnumTypeName { ... }`을 정의해야 함. 배열은 `enum:EnumTypeName[]`|

---

### enum 컬럼과 시트 작성 실수 방지
enum은 시트에 문자열로 적은 값(`"Fire"` 등)을 실제 C# enum으로 변환하는데, 실수를 두 단계에서 확실하게 잡습니다.

1. **타입 이름 오타** - `enum:ElementTyp`처럼 존재하지 않는 타입을 적으면, "선택 시트 생성" 시점에 **에러로 즉시 차단**되고 해당 탭 전체가 생성되지 않습니다. 같은 이름의 enum이 여러 개(다른 네임스페이스) 있어도 모호하다고 차단됩니다.
2. **셀 값 오타** - 타입은 맞는데 셀 값이 `"Fier"`처럼 실제 enum 멤버와 안 맞으면, 조용히 기본값으로 넘어가지 않고 **Console에 에러 로그**(테이블명·행 번호·잘못된 값·enum 타입명 포함)를 남기고 해당 필드만 기본값으로 처리합니다. 다른 컬럼 값은 그대로 유지됩니다.

> 즉 오타가 있으면 "선택 시트 생성/갱신" 직후 Console에서 반드시 확인할 수 있습니다 - 조용히 잘못된 데이터가 들어가는 경우는 없습니다.

---

#### 2) 런타임 데이터 접근
```cs
{TabName}Table.Data item = DataManager.Instance.GetTable<{TabName}Table>().Get(1001);
```

- **`DataManager.Instance.GetTable<T>()`**
  -> 처음 호출 시 `Resources/GeneratedTables/{T의 타입명}`에서 로드 후 캐싱, 이후 호출은 캐시 반환
- **`table.Get(rowKey)`**
  -> 해당 rowKey 행이 없으면 `null` 반환

---

### 테스트 방법
`Assets/00.Scripts/Tests/DataTester.cs`를 아무 GameObject에 붙이고 Play하면, RowKey를 입력하고 각 테이블에서 조회한 결과를 버튼으로 확인할 수 있습니다.

</details>

---

<details id="pooling">
<summary><h2>2. Pooling</h2></summary>

### 기능
- 프리팹(GameObject) 단위 풀링 - 프리팹별로 독립된 Pool 관리
- Dictionary + Queue 구조로 재사용 인스턴스 관리
- Instantiate / Destroy 최소화 (Spawn/Despawn만 반복)
- `IPoolable.OnSpawn/OnDespawn` 상태 초기화 훅 제공 (자식 오브젝트 포함 자동 호출)
- 씬 배치 불필요 - 처음 사용하는 순간 자동 생성
- Pool Settings에 등록한 Key로 프리팹 참조 없이 바로 Spawn 가능
- `EPoolKey` 자동 생성 - Key 오타를 컴파일 타임 에러로 잡음 (Sound System의 `ESound`와 동일한 방식)
- `Assets/02.Prefabs/Pooling/`에 프리팹을 넣기만 하면 Pool Settings 등록 + `EPoolKey` 재생성까지 전부 자동 (버튼 조작 불필요)

---

### 사용 방법

```cs
// 풀에 남아있는 인스턴스를 재사용하거나, 없으면 prefab으로 새로 생성해서 반환한다.
GameObject obj = PoolManager.Instance.Spawn(myPrefab, spawnPosition, Quaternion.identity);

// 컴포넌트로 바로 받고 싶다면 제네릭 오버로드 사용
MyComponent comp = PoolManager.Instance.Spawn(myPrefabComponent, spawnPosition, Quaternion.identity);

// 사용이 끝나면 Destroy하지 않고 비활성화 후 풀에 반환한다.
PoolManager.Instance.Despawn(obj);
```

- **`Spawn(prefab, position, rotation, parent = null)`**
  -> 재사용(있으면) / 생성(없으면)
- **`Spawn<T>(prefab, position, rotation, parent = null)`**
  -> 위와 동일하되 컴포넌트 타입으로 바로 반환
- **`Despawn(instance)`**
  -> 비활성화 후 풀에 보관, 다음 요청 시 재사용

---

### Pool Settings (선택, 씬 배치 불필요)
대량 생성이 예상되는 프리팹은 시작 시 미리 생성(Prewarm)해두는 설정 에셋을 만들 수 있습니다.

* `Assets/Create/Game Framework/Pooling/Pool Settings`로 에셋 생성
* 반드시 `Assets/Resources/GameFramework/PoolSettings.asset` 경로에 저장 (관례 경로로 자동 로드됨)
* 항목: Key(선택) / Prefab / Prewarm Count / Max Count(0 = 무제한) / Auto Expand
* 재사용 대기 중인(비활성) 인스턴스는 `PoolManager` 하위 `[PoolRoot]/[Pool] <프리팹 이름>`에 자동으로 정리됩니다. Spawn 시 부모를 직접 넘기지 않으면 씬 루트로 배치됩니다.

> 단순한 풀링이 필요한 경우에는 설정 없이 `Spawn`/`Despawn`만 사용해도 됩니다. 설정은 대량 생성·성능 관리가 필요할 때만 추가하면 됩니다. 스폰 시 부모가 필요하면 `Spawn(prefab, position, rotation, parent)`처럼 그때그때 넘겨주세요.

#### Key로 바로 스폰하기
Pool Settings의 Entry마다 `Key`를 적어두면, 프리팹 참조를 스포너 스크립트에 다시 연결하지 않고도 Key만으로 스폰할 수 있습니다. `Key`는 영문/숫자/밑줄만 가능하고 숫자로 시작할 수 없습니다 (아래 `EPoolKey` enum의 멤버 이름으로 그대로 쓰이기 때문).

**0) `Assets/02.Prefabs/Pooling/` 폴더에 넣기만 하면 자동 등록**

`Assets/02.Prefabs/Pooling/` 폴더에 프리팹을 넣으면, Unity가 임포트하는 순간 자동으로:
1. Pool Settings에 새 Entry로 등록 (Key = 프리팹 이름, Prewarm = 1, Max = 1, AutoExpand = true)
2. `EPoolKey`도 곧바로 재생성

버튼을 누를 필요가 없습니다. 이미 등록된 Entry는 절대 건드리지 않으므로, 나중에 Inspector에서 Prewarm/Max 값을 직접 조정해도 다음 동기화 때 덮어써지지 않습니다. Pool Settings 에셋 자체를 Inspector에서 손으로 고치고 저장해도(Key 변경 등) 마찬가지로 `EPoolKey`가 자동으로 다시 생성됩니다.

이 폴더 기능이 생기기 전부터 있던 프리팹처럼, 자동 감지를 못 받은 경우에는 아래 메뉴로 한 번에 몰아서 등록할 수 있습니다.

`Game Framework/Pooling/Sync Pool Settings From Folder`

**1) `EPoolKey` 직접 생성 (위 자동 동기화 없이 수동으로 하고 싶을 때)**

Pool Settings에 Key들을 다 적은 뒤, Unity Editor에서 아래 메뉴를 누릅니다.

`Game Framework/Pooling/Generate EPoolKey From Pool Settings`

* 동작: Pool Settings의 모든 `Key`를 모아 `EPoolKey.cs`를 자동 생성 (유효하지 않은 Key/중복 Key는 Console에 에러·경고를 남기고 건너뜀)
* 생성 위치: `Packages/com.changbeom.gameframework.pooling/Runtime/EPoolKey.cs`

```cs
// PoolSettings에 Key="Orc"로 등록해둔 프리팹을 스폰. 오타는 컴파일 에러로 즉시 드러남
GameObject orc = PoolManager.Instance.Spawn(EPoolKey.Orc, spawnPosition, Quaternion.identity);

// 컴포넌트 타입으로 바로 받고 싶으면 명시적으로 타입 인자를 지정
Orc orcComp = PoolManager.Instance.Spawn<Orc>(EPoolKey.Orc, spawnPosition, Quaternion.identity);
```

**2) 문자열로 바로 스폰하기 (EPoolKey 생성 전이거나, 동적으로 Key를 다뤄야 할 때)**

```cs
GameObject orc = PoolManager.Instance.Spawn("Orc", spawnPosition, Quaternion.identity);
Orc orcComp = PoolManager.Instance.Spawn<Orc>("Orc", spawnPosition, Quaternion.identity);
```

- **`Spawn(key, position, rotation, parent = null)`** (`EPoolKey` 또는 `string` 오버로드)
  -> Pool Settings에 등록된 Key로 프리팹을 찾아 스폰. 등록 안 된 Key(또는 `EPoolKey.None`)면 Console에 에러를 남기고 `null` 반환
- **`TryGetPrefab(key, out prefab)`**
  -> Key에 연결된 프리팹만 필요할 때 (Spawn 없이 참조만 얻고 싶을 때) 사용
- Despawn은 스폰된 인스턴스 하나하나를 반환하는 동작이라 Key가 필요 없습니다. Spawn이 돌려준 인스턴스를 그대로 `Despawn(instance)`에 넘기면 됩니다.

---

### 테스트 방법
`Assets/00.Scripts/Tests/PoolingTester.cs`는 `EPoolKey.Test` 키로 바로 스폰하는 예시입니다. 미리 `PoolSettings.asset`에 프리팹을 Key=`Test`로 등록하고 `Generate EPoolKey From Pool Settings` 메뉴로 `EPoolKey`를 생성해둬야 동작합니다.

아무 GameObject에 붙이고 Play하면:
- Spawn/Despawn 버튼으로 재사용 여부를 인스턴스 ID로 직접 확인할 수 있습니다 (Despawn 후 다시 Spawn하면 같은 ID가 재사용됨).
- Hierarchy 창에서 `[PoolRoot]` 하위에 풀이 쌓이는 것도 함께 확인 가능합니다.

</details>

---

<details id="ui">
<summary><h2>3. UI System</h2></summary>

### 기능
- 단일 팝업 표시 (Single Active Popup)
- 우선순위 처리 (Low / Normal / High / Critical)
- 선점 / 대기 / 교체 정책
- Suspend / Resume 흐름
- Open / Resume / Suspend / Close, Toast Show / Hide 전 구간에 애니메이션 훅 제공 (기본 스케일 연출 포함, 원하는 연출 코드로 교체 가능)
- Modal 입력 차단
- Pooling 패키지(`PoolManager`) 연계 (자체 풀 없음)
- Pool Settings에 등록한 `EPoolKey`로 팝업 프리팹 참조 없이 바로 `RequestPopup` 가능
- 팝업 결과 콜백 - 확인/취소처럼 "어떻게 닫혔는지" 결과값을 호출한 쪽이 받을 수 있음
- 전체 닫기(`CloseAll`) - 씬 전환 시 대기열까지 한번에 정리
- 비모달 토스트(Toast) - 모달 팝업과 별개로 여러 개 동시 표시 가능, 자동 사라짐
- HUD / Overlay 레이어 - 상시 표시 UI, 전체화면 연출용 레이어를 별도로 제공
- 뒤로가기 / Escape 키로 최상단 팝업 닫기 (팝업별로 끄기 가능)
- Settings ScriptableObject 기반 설정 (씬 배치 불필요)

---

### 사용 방법

#### 팝업 열기
```cs
UIManager.Instance.RequestPopup(
    popupPrefab,
    EPopupPriority.High
);
```

---

#### Key로 팝업 열기
팝업 프리팹도 Pooling의 `PoolSettings`에 Key로 등록해두면(`Assets/02.Prefabs/Pooling/`에 넣기만 해도 자동 등록), 프리팹 참조 없이 Key만으로 열 수 있습니다.

```cs
UIManager.Instance.RequestPopup(EPoolKey.ConfirmPopup, EPopupPriority.High);

// 결과 콜백 버전도 동일하게 지원
UIManager.Instance.RequestPopup<bool>(EPoolKey.ConfirmPopup, EPopupPriority.High,
    result => Debug.Log($"확인 결과: {result}"));
```

> 등록된 프리팹에 `UIPopupBase`가 없거나 Key가 없으면 Console에 에러를 남기고 아무 일도 일어나지 않습니다.

---

#### 정책 지정
```cs
UIManager.Instance.RequestPopup(
    popupPrefab,
    EPopupPriority.High,
    policy: EPopupPolicy.ReplaceCurrent
);
```

---

#### 결과 콜백 (확인/취소 다이얼로그)
팝업 쪽에서 `CloseSelf(result)`로 닫으면, 호출한 쪽이 결과를 받을 수 있습니다.

```cs
// 호출하는 쪽
UIManager.Instance.RequestPopup<bool>(
    confirmPopupPrefab,
    EPopupPriority.High,
    result => Debug.Log($"확인 결과: {result}")
);

// 팝업 내부 (예: Yes 버튼)
CloseSelf(true);

// 팝업 내부 (예: No 버튼)
CloseSelf(false);
```

---

#### 팝업 닫기
```cs
UIManager.Instance.CloseTopPopup();

// 특정 팝업을 결과값과 함께 닫기
UIManager.Instance.ClosePopup(popupInstance, result: true);

// 씬 전환 전: 대기열까지 전부 정리
UIManager.Instance.CloseAll();
```

---

#### 토스트 (비모달 알림)
```cs
UIManager.Instance.ShowToast(toastPrefab, "아이템 획득!");

// 기본 노출 시간(UIManagerSettings.DefaultToastDuration) 대신 직접 지정
UIManager.Instance.ShowToast(toastPrefab, "아이템 획득!", duration: 3f);
```

---

#### HUD / Overlay 레이어
상시 표시되는 HUD나 로딩 화면 등 전체화면 연출을 팝업/토스트와 겹치지 않는 별도 레이어에 붙일 수 있습니다.

```cs
Instantiate(hudPrefab, UIManager.Instance.HudRoot);
Instantiate(loadingScreenPrefab, UIManager.Instance.OverlayRoot);
```

---

#### 상태 조회
```cs
if (UIManager.Instance.IsAnyPopupOpen) { /* 조작 비활성화 등 */ }
```

---

#### 뒤로가기 / Escape로 닫히길 원하지 않는 팝업
```cs
public class MandatoryConfirmPopup : UIPopupBase
{
    public override bool CloseableByBackButton => false;
}
```

---

#### 애니메이션 커스터마이징
`UIPopupBase`(Open/Resume/Suspend/Close)와 `UIToastBase`(Show/Hide)는 각 동작마다 `PlayXAnimation()` 훅과 스케일 기반 기본 연출을 제공합니다. 원하는 연출로 바꾸려면 해당 메서드를 override해서 애니메이션 코드(코루틴, 자체 트윈 등 원하는 방식)를 작성하고, 끝났을 때 대응하는 `CompleteX()`(`CompleteOpen`/`CompleteResume`/`CompleteSuspend`/`CompleteClose`/`CompleteShow`/`CompleteHide`)를 반드시 호출하면 됩니다. override하지 않은 동작은 기본 스케일 연출(0↔1)이 그대로 재생되며, 각 클래스의 `_animationDuration`(Inspector에서 조정 가능한 float) 필드로 코드 수정 없이 기본 연출 속도만 바꿀 수도 있습니다.

---

### UI Manager Settings 만들기 (씬 배치 불필요)
`Assets/Create/Game Framework/UI System/UI Manager Settings`로 에셋을 만들고 아래 경로에 저장합니다.

`Assets/Resources/GameFramework/UIManagerSettings.asset`

설정 가능한 항목:
* Canvas 참조 해상도 / Match Width-or-Height
* 모달 블로커 색상
* 토스트 기본 노출 시간
* 뒤로가기/Escape로 팝업 닫기 사용 여부

---

### 테스트 방법
`Assets/00.Scripts/Tests/UITester.cs`를 빈 GameObject에 붙이고 `_popupA`/`_popupB`에 `UIPopup_TestA`/`UIPopup_TestB` 프리팹을 연결하면 팝업/결과 콜백/토스트/전체 닫기를 확인할 수 있는 OnGUI 버튼이 표시됩니다.

</details>

---

<details id="sound">
<summary><h2>4. Sound System</h2></summary>

### 기능
- Sound Sheet 기반 사운드 관리
  * Channel(BGM/SFX/UI/Voice), Volume, Loop, MaxConcurrent 등을 Sheet에서 관리
- ESound 자동 생성
  * Sheet의 `FileName`을 기반으로 `ESound` enum 자동 생성
- Addressables 자동 등록
  * Sound 폴더 스캔 후 Addressables 그룹에 자동 등록
  * Addressables address = fileName 규칙 강제
- Sound 데이터는 Data Parsing이 생성하는 `SoundTable` 하나로 통일 (별도의 데이터베이스 에셋 없음) - `SoundTable`이 갱신될 때마다 ESound 재생성 + Addressables 등록까지 버튼 없이 자동 동기화
- 사운드 재생 통합 API
  * `SoundManager.Instance.PlaySound(ESound.xxx)` 형태로 단순 사용
  * Channel이 BGM이면 자동으로 크로스페이드 전환, 그 외에는 원샷 재생
- BGM 크로스페이드
- 동시 재생 제한
  * 사운드별 MaxConcurrent 설정 지원
- 개별 사운드 정지
  * `StopSound(ESound id)`로 현재 재생 중인 특정 사운드(BGM/원샷 불문)만 골라서 정지
  * `StopAll()`로 BGM + 모든 원샷을 한 번에 전부 정지
- Voice 재생 시 BGM 자동 더킹(Ducking)
  * Voice 채널 사운드가 재생되는 동안 BGM 볼륨을 자동으로 낮추고, 끝나면 원복
- 사운드 프리로드(Preload)
  * 부팅 시 지정한 사운드 목록을 미리 로드해 첫 재생 지연 제거
- 볼륨은 Master × Channel × Sound별 기본 볼륨의 곱으로 계산되며, Master/Channel 볼륨은 SaveManager를 통해 영구 저장
- Settings ScriptableObject 기반 설정 (씬 배치 불필요)

---

### 외부 패키지
사운드 클립 스트리밍/로딩에 **Unity 공식 Addressables 패키지**(`com.unity.addressables`)를 사용합니다. 비동기 처리는 Unity 6 네이티브 `Awaitable`을 사용합니다.

---

### 사용 방법

#### 1) 사운드 파일 추가
오디오 파일을 아래 폴더에 추가

`Assets/03.Sound/`

예)
* Assets/03.Sound/SFX_Test.wav
* Assets/03.Sound/BGM_Test.wav

---

#### 2) Google Sheet(Sound 탭)에 Row 추가
`FileName`은 확장자 제외 파일명과 반드시 동일해야 합니다.

예)
|Google Sheet|
|-|
|<img width="521" height="104" alt="image" src="https://github.com/user-attachments/assets/3908d0a7-2312-4e49-9d7b-13bbabb55319" />|

---

#### 3) SoundTable 생성 (Data Parsing) → ESound·Addressables 자동 처리
Data Parsing의 `Game Framework/Data Parsing/DataTable Importer`에서 Sound 탭을 다른 테이블(Item/Monster/Quest 등)과 완전히 똑같은 방식으로 "선택 시트 생성"/"선택 시트 갱신"합니다. Sound만을 위한 별도 절차나 예외는 없습니다 - `SoundTable.cs`/`SoundTable.asset`이 그대로 생성됩니다.

`SoundTable`이 (재)생성되는 순간 **버튼 조작 없이 자동으로**:
1. `ESound.cs` 재생성 (`FileName` 기반)
2. `Assets/03.Sound/`의 AudioClip을 Addressables `Sound` 그룹에 자동 등록 (address = fileName)

`ESound`는 `SoundPlayer`/`SoundManager`가 직접 참조하는 프로젝트 전용 타입이라 패키지 어셈블리 내부(`Packages/com.changbeom.gameframework.sound/Runtime/ESound.cs`)에 생성됩니다. 패키지가 처음 설치되면 `None`만 있는 placeholder 상태이며, `SoundTable`을 처음 생성하는 순간 실제 사운드 id들로 덮어써집니다.

`SoundManager`는 프로젝트가 생성한 `SoundTable` 타입을 직접 참조할 수 없기 때문에(패키지는 프로젝트를 참조할 수 없음), 부팅 시 `SoundTable`을 1회 리플렉션으로 읽어 Channel/Volume/MaxConcurrent/Loop를 자체 Dictionary로 캐싱합니다 - 별도의 데이터베이스 에셋은 없습니다.

수동으로 다시 실행하고 싶을 때를 위한 메뉴도 남아 있습니다: `Game Framework/Sound System/Generate ESound + Register Addressables`.

이제 런타임에서 다음처럼 바로 사용 가능합니다.

`SoundManager.Instance.PlaySound(ESound.UI_Click);`

---

#### 4) Sound Manager Settings 만들기 (씬 배치 불필요)
`Assets/Create/Game Framework/Sound System/Sound Manager Settings`로 에셋을 만들고 아래 경로에 저장합니다.

`Assets/Resources/GameFramework/SoundManagerSettings.asset`

설정 가능한 항목:
* Sound Table Resource Path (기본: `GeneratedTables/SoundTable`) - 시트 탭 이름을 다르게 지어서 생성된 클래스명이 다르면 여기를 맞춰주세요
* Initial/Max Pool Size
* 채널별 AudioMixerGroup (선택)
* BGM Crossfade Seconds
* Duck Bgm On Voice / Ducked Bgm Volume Scale / Duck Fade Seconds
* Preload Sounds (부팅 시 미리 로드할 `ESound` 목록)

---

#### 5) 런타임 사용

```cs
// SFX/UI/Voice - 원샷 재생
SoundManager.Instance.PlaySound(ESound.UI_Click);
SoundManager.Instance.PlaySound(ESound.SFX_Merge);

// Voice 재생 시 SoundManagerSettings.DuckBgmOnVoice가 켜져 있으면 BGM이 자동으로 낮아졌다 복구됩니다.
SoundManager.Instance.PlaySound(ESound.Voice_Greeting);

// BGM - 자동 크로스페이드 전환
SoundManager.Instance.PlaySound(ESound.BGM_Main);

// BGM 정지
SoundManager.Instance.StopBgm();

// 특정 사운드만 정지 (BGM/원샷 불문, 현재 재생 중인 모든 인스턴스)
SoundManager.Instance.StopSound(ESound.SFX_Merge);

// 모든 원샷 정지
SoundManager.Instance.StopAllOneShots();

// 지금 나는 소리 전부 정지 (BGM + 모든 원샷)
SoundManager.Instance.StopAll();

// 마스터 볼륨 설정 (자동 저장)
SoundManager.Instance.SetMasterVolume(0.0f);

// 채널별 볼륨 설정 (자동 저장)
SoundManager.Instance.SetChannelVolume(ESoundChannel.BGM, 0.6f);
SoundManager.Instance.SetChannelVolume(ESoundChannel.SFX, 1.0f);
SoundManager.Instance.SetChannelVolume(ESoundChannel.UI, 0.8f);
SoundManager.Instance.SetChannelVolume(ESoundChannel.Voice, 1.0f);
```

---

### 테스트 방법
`Assets/00.Scripts/Tests/SoundTester.cs`를 빈 GameObject에 붙이면 재생/정지/볼륨/더킹 확인용 OnGUI 버튼이 표시됩니다.

</details>

---

<details id="saveload">
<summary><h2>5. Save / Load</h2></summary>

### 기능
- **Provider 기반 저장 시스템**
  - `ISaveProvider` 인터페이스로 저장 방식 교체 가능
  - 기본 제공 Provider
    - `JsonFileSaveProvider` (default, 원자적 쓰기 + 자동 백업)
    - `PlayerPrefsSaveProvider` (가장 단순, 백업 미지원)
    - `ES3SaveProvider` (optional, `USE_ES3` 필요)
    - `MemorySaveProvider` (테스트용 런타임 임시 저장)
   
- **Domain + Key 기반 저장 구조**
  - `SaveKey`를 통해 `root/domain/key` 형태로 안전하게 키 관리
- **Auto Flush (Dirty 기반 자동 저장)**
  - 변경 발생 시 Dirty 처리 -> 일정 시간 후 자동 Flush
  - `Flush()`/`BackupNow()`는 실제 쓰기 성공 여부를 `bool`로 반환. 실패(false)면 Dirty 상태가 유지되어 다음 자동 Flush에서 재시도됨
- **Pause/Quit 저장**
  - `OnApplicationPause`, `OnApplicationQuit`에서 Flush 처리
- **크래시/강제종료 대응**
  - 파일 저장 시 임시 파일 교체 방식의 원자적 쓰기 (쓰는 도중 종료돼도 기존 파일 보존)
  - 저장 성공 시마다 이전 버전을 `.bak`으로 자동 보관
- **백업/복구 지원**
  - `ISaveBackupProvider` 기반 (파일 기반 Provider만 지원)
  - `BackupNow()`, `RestoreFromBackup()` 제공
- **Save Meta 자동 관리**
  - `saveVersion`, `createdAtUtc`, `lastSavedAtUtc` 자동 저장/갱신

---

### 외부 패키지
`ES3SaveProvider`를 사용하려면 **Easy Save 3** (Unity Asset Store 유료 에셋) 설치가 필요합니다. 설치 안 하면 자동으로 JsonFile로 대체됩니다.

---

### 저장 키 구조
Save / Load는 모든 키를 아래 규칙으로 통합합니다.

```text
{RootKey}/{Domain}/{Key}
```

예)

```text
game/settings/audio
game/inventory/slots
game/meta/saveVersion
```

* RootKey 기본값 : `game`
* Domain은 저장 데이터를 큰 범주로 묶는 용도 (settings, inventory, quest 등)

---

### 사용 방법

#### 1) 설정 에셋 만들기 (씬 배치 불필요)
`SaveManager`는 씬에 배치할 필요가 없습니다. 대신 프로젝트에 설정 에셋을 하나 만듭니다.

* `Assets/Create/Game Framework/Save Load/Save Manager Settings`로 에셋 생성
* 반드시 `Assets/Resources/GameFramework/SaveManagerSettings.asset` 경로에 저장 (관례 경로로 자동 로드됨)
* 에셋이 없으면 기본값으로 동작하며 Console에 경고가 남습니다

---

#### 2) Storage Mode 선택 (SaveManagerSettings 에셋, default : JsonFile)
코드 수정 없이 `SaveManagerSettings` 에셋의 `Storage Mode` 드롭다운으로 저장 방식을 교체합니다.

|Storage Mode|설명|
|-|-|
|**JsonFile (default)**|`Application.persistentDataPath`에 JSON 파일 1개로 저장. 원자적 쓰기 + 자동 백업(.bak) 지원|
|PlayerPrefs|가장 단순한 저장 (키마다 JSON 문자열로 저장). 백업/복구는 지원하지 않음|
|Es3|Easy Save 3 사용. `USE_ES3` 미정의 시 자동으로 JsonFile로 대체되고 경고 로그 출력|
|Memory|디스크에 저장하지 않음 (테스트/에디터 전용)|

#### ✅ ES3 활성화 방법 (Unity 버튼)
* Easy Save 3 asset 설치
* `Project Settings -> Player -> Other Settings -> Scripting Define Symbol`에 **`USE_ES3`** 추가
* Storage Mode를 `Es3`로 변경

> JsonFile이 기본값인 이유: 강제 종료/크래시 상황에서도 원자적 쓰기와 자동 백업으로 데이터 손상을 막아주기 때문입니다. PlayerPrefs는 가장 간단하지만 이 안전장치가 없어, 설정값처럼 손실돼도 괜찮은 가벼운 데이터에 적합합니다.

---

#### 3) 저장 (Save)

```cs
SaveKey key = SaveManager.Instance.Domain("settings").Join("audio");
SaveManager.Instance.Save(key, audioSettingsData);
```

* `Save()` 호출 시 Dirty 처리됨
* AutoFlush Enabled면 자동으로 Flush됨
* Pause/Quit 시점에도 Flush됨

---

#### 4) 로드 (LoadOrCreate)
저장 데이터가 없으면 기본값 생성 후 저장까지 자동으로 처리합니다.

```cs
SaveKey key = SaveManager.Instance.Domain("settings").Join("audio");
AudioSettingsData settings = SaveManager.Instance.LoadOrCreate(key,() => new AudioSettingsData(), saveIfMissing: true);
```

---

### 런타임 API 정리

```cs
// Domain / Key
SaveKey domain = SaveManager.Instance.Domain("inventory");
SaveKey key = domain.Join("slots");



// Save / Load
SaveManager.Instance.Save(key, value);

bool ok = SaveManager.Instance.TryLoad(key, out MyData loaded);

MyData data = SaveManager.Instance.LoadOrCreate(key, () => new MyData(), saveIfMissing: true);



// Delete / HasKey
bool exists = SaveManager.Instance.HasKey(key);
SaveManager.Instance.Delete(key);



// Flush
bool flushed = SaveManager.Instance.Flush();



// Backup / Restore
bool hasBackup = SaveManager.Instance.HasBackup();

bool backedUp = SaveManager.Instance.BackupNow();
SaveManager.Instance.RestoreFromBackup();
```

---

### SaveManagerSettings 항목
`Assets/Resources/GameFramework/SaveManagerSettings.asset`의 Inspector에서 아래 항목을 설정합니다.

|항목|설명|
|-|-|
|Storage Mode|JsonFile / PlayerPrefs / Es3 / Memory 중 저장 방식 선택 (기본: JsonFile)|
|Save File Name|JsonFile/Es3 모드에서 사용할 파일명 (기본: `save.json`)|
|Current Version|세이브 버전. 버전 변경 시 `meta/saveVersion` 갱신|
|Root Key|모든 저장 키의 root prefix (기본: `game`)|
|Auto Flush Enabled|Dirty 상태에서 자동 저장 사용 여부|
|Auto Flush Interval Seconds|AutoFlush 주기(초)|
|Backup On Pause|백그라운드 진입 시 백업 수행|
|Backup On Quit|종료 시 백업 수행|
|Auto Restore On Init|초기화 시 백업 자동 복구 로직 활성화|

---

### Save Meta 구조
프레임워크가 자동으로 관리하는 메타 정보:

```text
meta/saveVersion
meta/createdAtUtc
meta/lastSavedAtUtc
```

실제 저장 키는 다음처럼 들어갑니다.

```text
game/meta/saveVersion
game/meta/createdAtUtc
game/meta/lastSavedAtUtc
```

---

### 실제 사용 예시
Audio System에서 적용 중인 저장 패턴 예시입니다.

```cs
private const string SettingsDomain = "settings";
private const string AudioSettingsKey = "audio";

SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(AudioSettingsKey);

_settings = SaveManager.Instance.LoadOrCreate(key, () => new AudioSettingsData(), saveIfMissing: true
);

SaveManager.Instance.Save(key, _settings);

```

---

### 테스트 방법
`Assets/00.Scripts/Tests/SaveLoadTester.cs`를 아무 GameObject에 붙이고 Play하면 버튼으로 아래 기능을 전부 확인할 수 있습니다.
- Save / TryLoad / LoadOrCreate / HasKey / Delete
- Flush (강제 즉시 저장)
- HasBackup / BackupNow / RestoreFromBackup (JsonFile, Es3 모드에서만 동작 확인 가능)

> `SaveManagerSettings` 에셋을 아직 안 만들었다면 기본값(JsonFile)으로 동작하며 Console에 경고가 남습니다.

</details>

---

<details id="time">
<summary><h2>6. Time System</h2></summary>

### 기능
게임 전반의 시간 흐름을 UTC 기준으로 통합 관리합니다.

- **서버/로컬 시간 소스 전환** - `Mode`(LocalOnly / ServerOnly / PreferServer)를 설정으로 선택. 게임마다 서버 동기화가 필요한지 다르기 때문에 하나로 고정하지 않음
- **모노토닉 클럭 기반 서버 신뢰도 판단** - 서버 동기화는 기기 시계가 아니라 `Stopwatch` 기반 모노토닉 클럭에 앵커링되어, 동기화 후 기기 시계를 바꿔도 흔들리지 않음. 신뢰 유효기간(Trust Window) 만료 시 자동으로 로컬로 대체
- **일/주/월 리셋 키 + 남은 시간 계산**
- **쿨타임** - 개별 조회 + 전체 목록 조회
- **오프라인 경과 시간**
- **시간 역행(치트) 감지** - 마지막 접속 시각보다 뒤로 가면 감지, 허용 오차 내의 사소한 뒤로 감(NTP 보정 등)은 미신뢰 소스에 한해 허용
- **리셋 크로싱 이벤트** - 게임을 켜놓은 채로 일/주/월 리셋 시각을 넘기면 `OnDailyReset`/`OnWeeklyReset`/`OnMonthlyReset` 발생
- **서버 재동기화 필요 신호** - `IsServerTrustExpiringSoon(초)`으로 신뢰 만료가 임박했는지 확인 가능 (실제 재동기화 네트워크 호출은 프로젝트마다 다르므로 신호만 제공)
- **이벤트 기간 유틸(`TimeRangeUtc`)** - 시작~종료 UTC 구간의 진행 여부/남은 시간 계산 (기간 한정 이벤트 등에 사용)
- **스키마 버전 체크** - 저장된 버전과 현재 버전이 다르면 감지 후 로그
- **테스트용 Mock 시간** - 시간 점프, 리셋 시점으로 바로 이동
- **Save / Load 연동** - 모든 시간 데이터 영구 저장

---

### 사용 방법

#### 1) 설정 에셋 만들기 (씬 배치 불필요)
* `Assets/Create/Game Framework/Time System/Time Manager Settings`로 에셋 생성
* 반드시 `Assets/Resources/GameFramework/TimeManagerSettings.asset` 경로에 저장
* 에셋이 없으면 기본값(PreferServer 등)으로 동작하며 Console에 경고가 남습니다

---

#### 2) 현재 시간 사용
```cs
DateTimeOffset now = TimeManager.Instance.UtcNow;
bool trusted = TimeManager.Instance.IsTrusted;
```

---

#### 3) 서버 시간 동기화
```cs
TimeManager.Instance.ApplyServerUtc(serverUtc);

// 신뢰 만료가 임박했으면(60초 이내) 재동기화 트리거
if (TimeManager.Instance.IsServerTrustExpiringSoon(60))
{
    // 프로젝트의 서버 시간 API를 호출해서 다시 ApplyServerUtc(...)
}

TimeManager.Instance.ClearServerSync();
```
`Mode`가 `PreferServer`면 신뢰 가능한 동안 자동으로 서버 시간이 쓰이고, 신뢰가 만료되면 자동으로 로컬 시간으로 대체됩니다. `LocalOnly`/`ServerOnly`로 고정할 수도 있습니다.

> 서버 신뢰는 **앱을 껐다 켜는 것만으로는 풀리지 않습니다** (OS 부팅 이후 누적 시간 기준 클럭 사용). 다만 **기기를 재부팅하면 항상 풀리고** 다음 `ApplyServerUtc` 전까지 로컬 시간으로 대체됩니다 - 재부팅 후에는 경과 시간을 검증할 방법이 없어 안전하게 미신뢰 처리하는 의도된 동작입니다. `ServerOnly`/`PreferServer`를 쓴다면 앱 시작 시점에 항상 서버 동기화를 한 번 시도하는 걸 권장합니다.

---

#### 4) 쿨타임
```cs
TimeManager.Instance.StartCooldown("skill_A", TimeSpan.FromSeconds(30));

bool ready = TimeManager.Instance.IsCooldownReady("skill_A");
TimeSpan remain = TimeManager.Instance.GetCooldownRemaining("skill_A");
TimeManager.Instance.ClearCooldown("skill_A");

// 현재 진행 중인 쿨다운 전체 목록 (UI 표시용)
IReadOnlyDictionary<string, TimeSpan> all = TimeManager.Instance.GetAllCooldownsRemaining();
```

---

#### 5) 리셋 키 / 남은 시간 / 리셋 이벤트
```cs
int dailyKey = TimeManager.Instance.GetDailyKey();
int weeklyKey = TimeManager.Instance.GetWeeklyKey();
int monthlyKey = TimeManager.Instance.GetMonthlyKey();

TimeSpan remain = TimeManager.Instance.GetRemainingToDailyReset();
string text = TimeManager.Instance.GetDailyResetRemainingText();

TimeManager.Instance.OnDailyReset += () => { /* 게임을 켜놓은 채로 자정을 넘긴 순간 호출됨 */ };
```
리셋 키는 **보상 중복 지급 방지**에 사용할 수 있습니다.

---

#### 6) 오프라인 경과 시간 / 치트 감지
```cs
TimeSpan offline = TimeManager.Instance.GetOfflineDelta();

bool cheated = TimeManager.Instance.IsCheatDetected;
TimeManager.Instance.ClearCheatFlag();
```

---

#### 7) 테스트용 Mock 시간
```cs
TimeManager.Instance.EnableMockTime();
TimeManager.Instance.AddMockSeconds(3600); // +1시간
TimeManager.Instance.JumpToNextDailyResetForTest();
TimeManager.Instance.DisableMockTime();
```

---

#### 8) 이벤트 기간 (TimeRangeUtc)
```cs
TimeRangeUtc eventPeriod = new TimeRangeUtc(eventStartUtc, eventEndUtc);

bool isActive = eventPeriod.IsActive(TimeManager.Instance.UtcNow);
TimeSpan remain = eventPeriod.Remaining(TimeManager.Instance.UtcNow);
```

---

### TimeManagerSettings 항목
`Assets/Resources/GameFramework/TimeManagerSettings.asset`의 Inspector에서 설정합니다.

|항목|설명|
|-|-|
|Mode|LocalOnly / ServerOnly / PreferServer|
|Daily Reset Hour|일일 리셋 기준 UTC 시각|
|Weekly Reset Day|주간 리셋 시작 요일|
|Backward Tolerance Seconds|시간 역행 허용 오차(초)|
|Server Trust Window Seconds|서버 시간 신뢰 유효 기간(초)|
|Schema Version|Time 저장 데이터 버전|

---

### 저장 구조
Time System의 모든 데이터는 Save / Load의 Domain을 사용하여 저장됩니다.

```text
game/time/...
```

저장 항목: 서버 동기화 시각, 마지막 접속 시간, 쿨타임 목록, Mock 시간 오프셋, 치트 플래그, 스키마 버전

---

### 테스트 방법
`Assets/00.Scripts/Tests/TimeTester.cs`를 아무 GameObject에 붙이고 Play하면 버튼으로 스냅샷/쿨타임/서버 동기화/Mock 시간/리셋 키/오프라인 경과/치트 플래그를 전부 확인할 수 있고, 리셋 이벤트가 발생하면 로그에 자동으로 찍힙니다.

</details>

---

<details id="sceneloading">
<summary><h2>7. Scene Loading</h2></summary>

### 기능
- `UnityEngine.SceneManagement` 기반 비동기 씬 전환 (`LoadSceneMode.Single`)
- Addressables로 등록된 씬도 동일한 파이프라인으로 로드 (`LoadSceneFromAddressableAsync`)
- 씬 로드와 나란히 진행되는 추가 비동기 작업(에셋 프리로드 등)의 진행률을 가중 합산해 하나의 `Progress`로 보여주는 `SceneLoadStep`
- 로드 실패 시 자동 재시도(횟수/간격 설정 가능) + 전부 실패하면 이동할 폴백 씬 지정 가능
- 씬 이름 오타를 컴파일 타임 에러로 잡는 `ESceneKey` 자동 생성 (Build Settings의 씬 목록 기준, Pooling의 `EPoolKey`와 동일한 방식)
- `Assets/01.Scenes/`에 씬을 넣기만 하면 Build Settings + Addressables 등록, `ESceneKey` 재생성까지 전부 자동
- 최소 로딩 화면 노출시간 보장 - 빠른 로드에서 화면이 깜빡이고 바로 사라지는 것 방지
- 진행률(`Progress`, `OnProgressChanged`)과 로드 시작/완료/실패 이벤트(`OnSceneLoadStarted`/`OnSceneLoadCompleted`/`OnSceneLoadFailed`) 제공
- 예외가 나도(사용자 코드의 `ISceneEntryPoint`/`ISceneExitPoint` 구현 포함) `IsLoading`과 로딩 화면이 항상 정상 상태로 복구됨 - 한 번 실패했다고 이후 씬 전환이 영구히 막히지 않음
- 씬 전환 전 `UIManager.CloseAll()` 자동 호출로 팝업/토스트 대기열 정리
- `ISceneEntryPoint`/`ISceneExitPoint` - 등록 절차 없이 씬에 배치하기만 하면 씬 진입/퇴장 시점에 자동 호출되는 훅
- `UIManager.OverlayRoot` 위에 표시되는 내장 기본 로딩 화면 (검은 배경 + 퍼센트 텍스트), 필요하면 직접 만든 프리팹으로 교체 가능
- 씬 배치 불필요 - 처음 사용하는 순간 자동 생성
- 이미 로딩 중일 때 추가 요청은 대기열에 넣지 않고 경고 로그와 함께 무시 (동시 전환 방지)

---

### 외부 패키지
Addressables로 등록한 씬을 로드하는 기능(`LoadSceneFromAddressableAsync`)에 **Unity 공식 Addressables 패키지**(`com.unity.addressables`)를 사용합니다. Build Settings 기반 로드(`LoadSceneAsync`)만 쓴다면 이 의존성은 그냥 딸려오는 상태로 두면 되고, 별도 설정 없이도 동작합니다.

---

### 사용 방법

```cs
// 생성된 ESceneKey로 씬 전환 (오타는 컴파일 에러로 즉시 드러남)
_ = SceneLoadingManager.Instance.LoadSceneAsync(ESceneKey.GameScene);

// ESceneKey 생성 전이거나 동적으로 씬 이름을 다뤄야 할 때는 문자열도 가능
_ = SceneLoadingManager.Instance.LoadSceneAsync("GameScene");

// 진행률 구독
SceneLoadingManager.Instance.OnProgressChanged += p => progressBar.value = p;

// 상태 조회
bool isLoading = SceneLoadingManager.Instance.IsLoading;
```

- **`LoadSceneAsync(sceneName)`** (`ESceneKey` 또는 `string` 오버로드)
  -> 로딩 화면 표시 -> 씬 비동기 로드 -> 최소 노출시간 대기 -> 씬 활성화 -> 로딩 화면 숨김 순으로 진행. 이미 로딩 중이면 경고 로그만 남기고 무시
- **`LoadSceneFromAddressableAsync(address)`**
  -> Addressables 주소로 같은 파이프라인을 그대로 사용. 자세한 내용은 아래 "Addressables로 씬 로딩하기" 참고
- **`LoadSceneAsync(sceneName, extraSteps)` / `LoadSceneFromAddressableAsync(address, extraSteps)`**
  -> `extraSteps`(`IReadOnlyList<SceneLoadStep>`)로 전달한 추가 작업을 씬 로드와 동시에 진행하고 진행률을 가중 합산. 자세한 내용은 아래 "가중 합산 progress" 참고
- **`IsLoading` / `Progress` / `CurrentSceneName`**
  -> 현재 로딩 상태 조회
- **`OnSceneLoadStarted` / `OnProgressChanged` / `OnSceneLoadCompleted` / `OnSceneLoadFailed`**
  -> 각각 로드 시작, 진행률 변경(0~1), 로드 성공 완료, 최종 실패(재시도/폴백까지 전부 소진된 뒤) 시점에 발행
- **`OnSceneLoadAttemptFailed` / `OnSceneLoadRetrying` / `OnSceneLoadFallback`**
  -> 재시도/폴백 관련 이벤트. 자세한 내용은 아래 "로드 실패 시 자동 재시도 / 폴백" 참고

#### `ESceneKey` 생성하기
Build Settings에 씬을 등록한 뒤, Unity Editor에서 아래 메뉴를 누릅니다.

`Game Framework/Scene Loading/Generate ESceneKey From Build Settings`

* 동작: Build Settings에 등록된(활성화된) 모든 씬 이름을 모아 `ESceneKey.cs`를 자동 생성 (유효하지 않은 이름/중복 이름은 Console에 에러를 남기고 건너뜀)
* 생성 위치: `Packages/com.changbeom.gameframework.sceneloading/Runtime/ESceneKey.cs`
* 기존 멤버의 선언 순서(=정수 값)는 재생성해도 보존되고, 새로 등록한 씬만 맨 뒤에 추가됩니다.

---

### 씬 폴더 자동 등록
`Assets/01.Scenes/`에 씬을 새로 추가하기만 하면 별도 조작 없이 자동으로:
1. Build Settings에 등록 (이미 등록되어 있는데 비활성화 상태라면 다시 활성화 - 이 폴더에 있는 씬은 항상 빌드에 포함되는 게 원칙)
2. Addressables "Scene" 그룹에 등록되고, 주소가 씬 파일 이름으로 설정됨 (이름이 나중에 바뀌어도 주소가 계속 따라감)
3. Build Settings가 바뀌었으면 `ESceneKey`도 곧바로 재생성됨

이미 폴더에 있었지만 한 번도 (재)임포트된 적이 없어서 자동 감지를 못한 씬이 있다면, `Game Framework/Scene Loading/Sync Scenes From Folder` 메뉴로 한 번에 정리할 수 있습니다. Pooling 패키지의 폴더 기반 자동 등록과 동일한 방식입니다.

---

### Addressables로 씬 로딩하기
Build Settings 대신 Addressables로 등록한 씬을 로드하려면 `LoadSceneFromAddressableAsync`를 씁니다. 로딩 화면/최소 노출시간/`ISceneEntryPoint`/`ISceneExitPoint`/재시도/폴백까지 `LoadSceneAsync`와 완전히 동일한 파이프라인을 공유합니다.

```cs
_ = SceneLoadingManager.Instance.LoadSceneFromAddressableAsync("DungeonScene");
```

* Addressables 그룹에 씬을 등록하고, 위 예시의 `"DungeonScene"` 자리에 그 주소(Address)를 넣으면 됩니다.
* 씬을 새로 성공적으로 활성화할 때마다(Addressables든 Build Settings든) 직전에 로드했던 Addressables 씬 핸들을 자동으로 언로드하고, 앱 종료 시에도 남은 핸들을 정리합니다 - 직접 `Addressables.Release`를 호출할 필요가 없습니다.
* 이 경로는 `com.unity.addressables` 패키지 의존성이 필요하며, 이 패키지를 설치하면 함께 설치됩니다.

---

### 가중 합산 progress
씬 로드와 나란히 진행하고 싶은 다른 비동기 작업(다음 씬에 필요한 에셋 프리로드 등)이 있다면, `SceneLoadStep`으로 만들어 `extraSteps`로 넘기세요. 씬 로드 자체(가중치 1)와 각 단계가 전부 동시에 시작되고, 다음 식으로 가중 평균한 값이 `Progress`/`OnProgressChanged`에 반영됩니다.

```
Progress = Σ(단계 진행률 × 단계 가중치) / Σ(단계 가중치)
```

```cs
SceneLoadStep preloadStep = new SceneLoadStep(
    label: "PlayerSkinPreload",
    weight: 1f,
    runAsync: async (reportProgress, cancellationToken) =>
    {
        // 0~1 진행률을 reportProgress로 보고하며 원하는 작업 수행
        AudioClip clip = await LoadPlayerSkinAsync(reportProgress, cancellationToken);
    });

_ = SceneLoadingManager.Instance.LoadSceneAsync("GameScene", new[] { preloadStep });
```

* **`Weight`**: 씬 로드 자신의 가중치는 항상 `1`로 고정입니다. 예를 들어 단계 하나에 `Weight = 2`를 주면 씬 로드보다 2배 비중으로 반영됩니다.
* **`Critical`** (기본값 `true`): 이 단계가 실패하면 씬 로드 전체가 실패로 취급되어 재시도/폴백 대상이 됩니다. `false`면 경고 로그만 남기고 씬 로드는 그대로 계속 진행됩니다.
* 단계들은 씬 로드와 **동시에** 진행됩니다(직렬이 아님) - 프리로드가 씬 로드 시간과 겹쳐서 전체 대기 시간이 줄어드는 게 목적입니다.
* `ISceneEntryPoint`/`ISceneExitPoint` 훅과 마찬가지로, 단계 하나가 `SceneLoadingManagerSettings`의 `Load Step Timeout Seconds`(기본 30초) 안에 끝나지 않으면 타임아웃 처리됩니다 - 다만 훅과 달리 "성공한 걸로 치고 넘어가는" 게 아니라 그 단계의 실패로 처리되어 `Critical` 값에 따라 재시도/폴백 대상이 됩니다(사용자 코드의 버그로 프리로드가 영원히 안 끝나서 씬 전환 전체가 멈추는 것을 방지).
* `new SceneLoadStep(...)` 생성자를 거치지 않은 `default(SceneLoadStep)`을 실수로 넘기면(`RunAsync`가 null) `Critical` 값과 무관하게 항상 실패로 처리됩니다.

---

### 로드 실패 시 자동 재시도 / 폴백
`SceneLoadingManagerSettings`의 `Max Retry Count`를 1 이상으로 설정하면, 로드가 실패해도 바로 포기하지 않고 `Retry Delay Seconds` 간격으로 자동 재시도합니다. 재시도까지 전부 실패했을 때 `Fallback Scene Name`이 설정되어 있으면 그 씬(Build Settings 기준, 항상)으로 자동 전환합니다 - 원래 요청이 Addressables였어도 폴백은 네트워크에 의존하지 않는 Build Settings 경로만 사용합니다. 폴백 씬 로드 자체가 실패할 경우의 재시도 횟수는 `Fallback Max Retry Count`로 따로 설정합니다 (기본 0=1번만 시도, 폴백의 폴백은 없음 - 무한 루프 방지).

```cs
SceneLoadingManager.Instance.OnSceneLoadAttemptFailed += (sceneOrAddress, attempt, maxAttempts) =>
    Debug.Log($"{sceneOrAddress} 로드 시도 {attempt}/{maxAttempts} 실패");

SceneLoadingManager.Instance.OnSceneLoadRetrying += (sceneOrAddress, nextAttempt, maxAttempts) =>
    Debug.Log($"{sceneOrAddress} 재시도 {nextAttempt}/{maxAttempts} 시작");

SceneLoadingManager.Instance.OnSceneLoadFallback += (original, fallback) =>
    Debug.Log($"{original} 재시도 소진 - {fallback}으로 전환");
```

* `OnSceneLoadFailed`는 재시도/폴백까지 전부 소진된 뒤 **최종 실패**했을 때만 발행됩니다. `Max Retry Count`가 기본값 0이면 시도가 1번뿐이라 기존과 동일하게 그 1번의 실패에 바로 발행됩니다.
* `OnSceneLoadAttemptFailed`는 개별 시도가 실패할 때마다(재시도 예정인 경우 포함) 발행됩니다.
* 재시도/폴백 전체가 끝날 때까지 `IsLoading`은 계속 `true`로 유지되므로, 그 사이 다른 `LoadSceneAsync` 호출은 기존과 동일하게 무시됩니다.
* `Fallback Scene Name`이 방금 실패한 요청과 같은 이름이면(자기 자신을 폴백으로 지정한 경우) 무한 루프를 막기 위해 폴백을 건너뛰고 경고 로그만 남긴 뒤 `OnSceneLoadFailed`로 끝납니다.

> **주의**: 씬이 이미 활성화된 뒤(`ISceneEntryPoint` 실행 도중) 실패하면, 이전 씬은 이미 언로드된 상태라 되돌릴 방법이 없습니다. `MaxRetryCount`/`FallbackSceneName`을 켜두면 다음 시도가 이 깨진 씬을 "이전 씬"으로 취급해서 자동으로 정리하지만, 둘 다 기본값(꺼짐)이면 아무도 정리하지 않아 이 깨진 씬이 계속 활성 씬으로 남습니다(Console에 눈에 띄는 에러 로그는 남습니다). `ISceneEntryPoint`에서 예외가 날 가능성이 있다면 `FallbackSceneName` 설정을 권장합니다.

---

### Scene Loading Manager Settings (선택, 씬 배치 불필요)
* `Assets/Create/Game Framework/Scene Loading/Scene Loading Manager Settings`로 에셋 생성
* 반드시 `Assets/Resources/GameFramework/SceneLoadingManagerSettings.asset` 경로에 저장 (관례 경로로 자동 로드됨)
* 항목:

|필드|설명|기본값|
|---|---|---|
|Minimum Loading Screen Duration|로딩 화면 최소 노출시간(초)|0.5|
|Fade Duration|로딩 화면 페이드 인/아웃 시간(초)|0.25|
|Loading Screen Prefab Override|커스텀 로딩 화면 프리팹 (비워두면 내장 기본 화면 사용)|없음|
|Scene Operation Timeout Seconds|씬 오퍼레이션 자신(Build Settings/Addressables 공통)이 이 시간 안에 준비되지 않으면 실패 처리. 실패한 로드를 정리(언로드)하는 시간, 활성화 성공 후 이전 씬들을 언로드하는 마무리 단계에도 재사용 (0 이하면 무한 대기)|60|
|Loading Screen Timeout Seconds|로딩 화면의 RequestShow/RequestHide 콜백이 이 시간 안에 안 불리면 성공한 걸로 치고 건너뜀 (0 이하면 무한 대기)|10|
|Entry Exit Point Timeout Seconds|`ISceneEntryPoint`/`ISceneExitPoint` 훅 하나가 이 시간 안에 끝나지 않으면 건너뛰고 진행 (0 이하면 무한 대기)|10|
|Load Step Timeout Seconds|`SceneLoadStep` 하나가 이 시간 안에 끝나지 않으면 실패 처리 (0 이하면 무한 대기)|30|
|Max Retry Count|로드 실패 시 자동 재시도 횟수 (0이면 재시도 없음)|0|
|Retry Delay Seconds|재시도 사이 대기 시간(초)|1|
|Fallback Scene Name|모든 재시도 소진 시 이동할 Build Settings 씬 (비어있으면 폴백 없음)|없음|
|Fallback Max Retry Count|폴백 씬 로드 자체의 재시도 횟수|0|

* 에셋이 없으면 Console에 경고가 남고 기본값으로 동작합니다.

---

### 커스텀 로딩 화면 만들기
내장 기본 화면(검은 배경 + 퍼센트 텍스트) 대신 직접 만든 연출을 쓰려면 `SceneLoadingScreenBase`를 상속하세요.

```cs
public class MyLoadingScreen : SceneLoadingScreenBase
{
    protected override void PlayShow()
    {
        // 원하는 연출 재생 후 반드시 CompleteShow() 호출
    }

    protected override void PlayHide()
    {
        // 원하는 연출 재생 후 반드시 CompleteHide() 호출
    }

    public override void SetProgress(float progress01)
    {
        // 프로그레스바 등에 반영
    }
}
```

만든 컴포넌트를 프리팹으로 만들어 `SceneLoadingManagerSettings`의 `Loading Screen Prefab Override`에 연결하면 됩니다.

> `PlayShow()`/`PlayHide()`에서 `CompleteShow()`/`CompleteHide()`를 호출하지 않는 버그가 있어도, `Loading Screen Timeout Seconds`(기본 10초)가 지나면 자동으로 다음 단계로 넘어가 `IsLoading`이 영구 고착되지 않도록 방지합니다. 그래도 실제로는 호출하는 게 정상 동작입니다.

---

### 씬 진입/퇴장 훅 (`ISceneEntryPoint` / `ISceneExitPoint`)
씬이 켜질 때/떠날 때 그 씬만의 초기화·정리 로직이 필요하면 이 두 인터페이스를 구현하세요. 별도의 등록 절차가 없습니다 - 씬 안의 아무 GameObject에 구현체를 붙여두기만 하면, `SceneLoadingManager`가 씬 전환 시점에 자동으로 찾아서 호출합니다.

```cs
public class GameSceneBootstrap : MonoBehaviour, ISceneEntryPoint
{
    public async Awaitable OnSceneEnterAsync()
    {
        // 플레이어 스폰, 씬 전용 데이터 로드 등. 끝날 때까지 로딩 화면이 유지됩니다.
    }
}
```

- **`ISceneEntryPoint.OnSceneEnterAsync()`**
  -> 새 씬이 활성화된 직후 호출됩니다. 로딩 화면은 모든 `ISceneEntryPoint`가 끝날 때까지 유지되므로, "씬이 실제로 준비된 뒤에" 화면이 사라져야 하는 작업(플레이어 스폰 등)에 적합합니다. 한 씬에 여러 개가 있으면 `Order`(기본값 0, 작을수록 먼저)순으로 순서대로 실행됩니다.
- **`ISceneExitPoint.OnSceneExitAsync()`**
  -> 다음 씬 로드가 시작되어 로딩 화면이 이미 화면을 덮은 뒤, 지금 씬이 실제로 전환되기 전에 호출됩니다. 여기서 하는 정리 작업은 사용자에게 보이지 않습니다.
- 비활성화(`SetActive(false)`)된 오브젝트에 붙은 훅은 호출되지 않습니다. 나중에 쓰려고 꺼둔 오브젝트가 있다면 그 위에는 이 인터페이스를 두지 마세요.

> 주의: `OnSceneEnterAsync()`/`OnSceneExitAsync()` 안에서 `SceneLoadingManager.Instance.LoadSceneAsync(...)`를 다시 호출하지 마세요. 그 시점엔 이미 `IsLoading`이 true라 경고 로그와 함께 조용히 무시됩니다. 다른 씬으로 리다이렉트해야 한다면, 현재 로드가 완전히 끝난 뒤(`OnSceneLoadCompleted` 등에서) 호출하세요.

---

### 테스트 방법
`Assets/00.Scripts/Tests/SceneLoadingTester.cs`를 아무 GameObject에 붙이고 Play하면 다음 10개 버튼을 제공합니다.
1. 문자열로 씬 전환
2. `ESceneKey`(`None` 아닌 정상 키)로 씬 전환
3. `ESceneKey.None`으로 전환 시도해 에러 로그 확인
4. 존재하지 않는 씬 이름으로 로드해 에러 로그 확인 (재시도/폴백을 확인하려면 `SceneLoadingManagerSettings`에서 `Max Retry Count`/`Retry Delay Seconds`/`Fallback Scene Name`을 먼저 설정)
5. 연속으로 두 번 빠르게 호출해 중복 요청 방지(`IsLoading` 가드) 확인
6. Addressables 주소로 씬 전환
7. 존재하지 않는 Addressables 주소로 로드해 에러 로그 확인
8. 가중치가 다른 가짜 프리로드 단계 2개와 함께 씬 전환 (progress bar 속도 변화로 가중 합산 확인)
9. 같은 테스트 + 한 단계가 실패(`Critical=false`) - 씬 로드는 성공해야 함
10. 같은 테스트 + 한 단계가 실패(`Critical=true` 기본값) - 씬 로드도 실패해야 함

상단에는 `IsLoading`/`Progress`/`CurrentSceneName`과 `OnProgressChanged` 발행 횟수가 실시간으로 표시되고, 로드 시작/완료/실패/재시도/폴백 이벤트는 전부 로그에 자동으로 찍힙니다. 실제 씬 전환을 확인하려면 Build Settings에 씬이 최소 2개 등록되어 있어야 합니다. 6~7번 버튼을 테스트하려면 Unity 에디터에서 대상 씬을 Addressable로 표시하고 주소를 인스펙터의 `_addressableSceneAddress` 필드에 맞게 지정해야 합니다. `ESceneKey`를 생성하지 않아도(즉 `None`만 있는 상태여도) 컴파일에 문제가 없도록, 2번 버튼을 제외한 나머지는 문자열 오버로드를 사용하고 3번 버튼은 항상 존재하는 `ESceneKey.None`만 사용합니다.

</details>

---

<details id="input">
<summary><h2>8. Input</h2></summary>

### 기능
- Unity 공식 새 Input System 패키지(`com.unity.inputsystem`) 기반 - 레거시 `UnityEngine.Input`은 사용하지 않음
- 액션 정의는 시트가 아니라 Unity `.inputactions` 에디터 에셋(`GameFrameworkInputActions`)으로 직접 관리 - Unity 내장 "Generate C# Class" 기능으로 코드 생성
- PC(키보드/마우스, 게임패드 선택적)와 모바일(Android/iOS 터치)을 함께 고려 - 특정 장르를 가정하는 액션을 프레임워크가 강제하지 않음
- 기본 제공 액션맵 2개
  - `Gameplay`: `Move`(Vector2, WASD/왼쪽 스틱), `Interact`(Button, E 키/터치/게임패드 버튼) - 프로젝트에 맞게 지우고 새로 정의하는 걸 전제로 한 최소 예시
  - `UI`: `Cancel`(Button, Esc 키/게임패드 버튼) 하나만 - PC의 Esc 키와 Android 하드웨어 뒤로가기는 Unity 내부적으로 같은 키 이벤트로 들어와서 바인딩 하나로 두 플랫폼을 함께 처리. Navigate/Submit 등 UGUI 내비게이션은 `InputSystemUIInputModule`을 쓰는 소비 프로젝트 쪽 몫으로 남겨둠(프레임워크가 강제하지 않음)
- UIManager 연동 - 모달 팝업이 열려 있으면 `Gameplay` 맵을 자동으로 비활성화(`UI` 맵은 항상 유지), `Cancel` 입력이 들어오면 `UIManager.CloseTopPopup()`을 자동 호출(팝업별 `CloseableByBackButton = false` opt-out 존중)
- 인터랙티브 리바인딩 - 진행 중 취소, 제외 컨트롤 경로, 취소 전용 경로 설정 가능
- 리바인딩 결과는 SaveManager를 통해 영구 저장/자동 복원
- 기기 변경(연결/해제) 이벤트 제공
- 로컬 멀티플레이(여러 기기-플레이어 페어링)는 지원하지 않음 - 단일 로컬 플레이어 전용
- 씬 배치 불필요 - 처음 사용하는 순간 자동 생성

---

### 외부 패키지
**Unity 공식 Input System 패키지**(`com.unity.inputsystem`)를 사용합니다. Player Settings의 **Active Input Handling**은 **Both**를 권장합니다 - `UIManager`의 레거시 Esc 키 처리(`Input.GetKeyDown`)가 계속 동작하려면 새 Input System만 단독으로 켜면 안 됩니다.

---

### 사용 방법

#### 액션 읽기
```cs
Vector2 move = InputManager.Instance.Actions.Gameplay.Move.ReadValue<Vector2>();

InputManager.Instance.Actions.Gameplay.Interact.performed += ctx =>
{
    // 상호작용 처리
};
```

- **`InputManager.Instance.Actions`**
  -> 생성된 `GameFrameworkInputActions` 래퍼(`Gameplay`/`UI` 맵)에 직접 접근
- **`IsGameplayInputEnabled`**
  -> 현재 `Gameplay` 맵이 켜져 있는지(모달 팝업이 열려 있으면 자동으로 꺼짐) 조회

---

#### 리바인딩
```cs
InputAction interact = InputManager.Instance.Actions.Gameplay.Interact;

InputManager.Instance.StartRebind(interact, bindingIndex: 0, onComplete: path =>
{
    Debug.Log($"새 바인딩: {path}");
});

// 진행 중인 리바인딩 취소
InputManager.Instance.CancelActiveRebind();

// 바인딩 하나 초기화
InputManager.Instance.ResetBinding(interact, bindingIndex: 0);

// 전체 초기화
InputManager.Instance.ResetAllBindings();
```

- **`StartRebind(action, bindingIndex, onComplete = null)`**
  -> 인터랙티브 리바인딩 시작. 완료되면 자동으로 `SaveBindings()`까지 호출됨. 이미 진행 중인 리바인딩이 있으면 먼저 취소하고 새로 시작
- **`OnRebindStarted` / `OnRebindCompleted` / `OnRebindCanceled`**
  -> 리바인딩 UI 갱신용 이벤트
- 리바인딩 제외 경로(`RebindExcludePaths`)와 취소 경로(`RebindCancelPath`)는 `InputManagerSettings`에서 설정

---

#### 저장 / 복원
```cs
InputManager.Instance.SaveBindings();
InputManager.Instance.LoadBindings();
```
`OnInitialize()` 시점에 자동으로 `LoadBindings()`가 호출되므로, 보통 직접 호출할 일은 리바인딩/초기화 이후의 명시적 저장 정도입니다. SaveManager의 `Domain("settings").Join("input")` 키로 저장됩니다.

---

#### 기기 변경 감지
```cs
InputManager.Instance.OnDeviceChange += (device, change) =>
{
    if (change == InputDeviceChange.Disconnected) { /* 컨트롤러 연결 해제 안내 등 */ }
};
```

---

#### 모바일 터치 UI
가상 조이스틱/버튼 같은 화면 터치 컨트롤이 필요하면 Unity Input System의 On-Screen Controls 컴포넌트(`OnScreenStick`/`OnScreenButton`)를 그대로 사용하면 됩니다 - 같은 `GameFrameworkInputActions`에 그대로 값이 들어오므로 이 패키지에서 별도 작업이 필요 없습니다.

---

### Input Manager Settings (선택, 씬 배치 불필요)
* `Assets/Create/Game Framework/Input/Input Manager Settings`로 에셋 생성
* 반드시 `Assets/Resources/GameFramework/InputManagerSettings.asset` 경로에 저장 (관례 경로로 자동 로드됨)

|필드|설명|기본값|
|---|---|---|
|Rebind Exclude Paths|인터랙티브 리바인딩 중 무시할 컨트롤 경로|`<Mouse>/position`, `<Mouse>/delta`|
|Rebind Cancel Path|리바인딩 도중 이 경로를 누르면 취소|`<Keyboard>/escape`|

* 에셋이 없으면 Console에 경고가 남고 기본값으로 동작합니다.

---

### 테스트 방법
`Assets/00.Scripts/Tests/InputTester.cs`를 아무 GameObject에 붙이고 Play하면 다음을 OnGUI 버튼/라벨로 확인할 수 있습니다.
- `Move`/`Interact` 실시간 값과 `IsGameplayInputEnabled` 상태
- 테스트 팝업 열기 버튼 - 열려 있는 동안 `Gameplay` 입력이 자동으로 막히고, `Cancel`(Esc)로 닫히는지 확인
- `Interact` 리바인딩 시작/취소/초기화 버튼과 현재 바인딩 경로 표시
- 저장/재로드 버튼 - Play 모드를 재시작해도 리바인딩이 유지되는지 확인
- 기기 연결/해제 로그

</details>

---

