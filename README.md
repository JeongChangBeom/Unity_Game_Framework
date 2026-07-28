# Unity Game Framework

> Unity 게임 개발에서 반복적으로 구현되는 시스템들을  
> **프레임워크 단위로 정리한 공통 게임 개발 기반**입니다.
>
> 특정 게임에 종속되지 않으며,  
> 여러 프로젝트에서 재사용·확장하는 것을 목표로 합니다.

---

## 📦 Frameworks

현재 포함된 프레임워크는 다음과 같습니다.

- **Core**  
  모든 매니저가 상속하는 공용 베이스(MonoSingleton)

- **Data Parsing**  
  Google Sheet 기반 게임 데이터 파이프라인

- **Pooling**  
  Type 기반 공용 오브젝트 풀링 시스템

- **UI System**  
  우선순위 / 선점 기반 UI 흐름 관리 시스템

- **Audio System**  
  Addressables + Sheet 기반 사운드 재생/관리 시스템

- **Save / Load**  
  Provider 기반 저장/로드 시스템 (AutoFlush / Backup / Restore 지원)

- **Time System**  
  UTC 기반 시간 관리, 리셋, 쿨타임, 서버 시간 동기화 시스템

> 프레임워크는 지속적으로 추가될 예정입니다.

---

## 🔗 목차
- [0️⃣ Core](#core)
- [1️⃣ Data Parsing](#data)
- [2️⃣ Pooling](#pooling)
- [3️⃣ UI System](#ui)
- [4️⃣ Audio System](#audio)
- [5️⃣ Save / Load](#saveload)
- [6️⃣ Time System](#time)

---

## 📥 패키지 다운로드
필요한 패키지만 골라서 설치할 수 있습니다. Unity Package Manager → `+` → `Add package from git URL...`에 아래 주소를 붙여넣으세요.

|패키지|설명|설치 주소|
|-|-|-|
|Core|공용 싱글톤 베이스|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.core`|
|Save / Load|Provider 기반 저장/로드|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.saveload`|
|Pooling|프리팹 기반 오브젝트 풀링|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.pooling`|
|Data Parsing|Google Sheet 데이터 파이프라인|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.data`|
|UI System|패키지화 예정|-|
|Audio System|패키지화 예정|-|
|Time System|UTC 기반 시간/쿨타임/리셋 관리|`https://github.com/JeongChangBeom/Unity_Game_Framework.git?path=/Packages/com.changbeom.gameframework.time`|

> Save / Load는 Core에 의존하므로 Core도 함께 설치해야 합니다.

---

<details id="core">
<summary><h2>0️⃣ Core</h2></summary>


### 기능
- 공용 싱글톤 베이스 `MonoSingleton<T>` 제공
- **씬 배치 불필요** — 처음 `.Instance`에 접근하는 순간 자동 생성
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
<summary><h2>1️⃣ Data Parsing</h2></summary>

### 기능
- Google Sheet → ScriptableObject 자동 변환 (TSV 다운로드 후 파싱)
- Sheet Tab 선택 후 C# 테이블 스크립트 + .asset 자동 생성
- 기존 테이블 갱신(Update)/삭제 지원
- `DataManager`가 타입별로 로드 결과를 캐싱

---

### 외부 패키지
Editor 툴이 시트 다운로드 대기에 **Unity 공식 Editor Coroutines 패키지**(`com.unity.editorcoroutines`)를 사용합니다. 그 외 런타임 코드는 전부 Unity API(`UnityEngine.Networking.UnityWebRequest`, `Resources`)만 사용합니다.

---

### 사용 방법

#### 1) 시트 임포트 (Editor 전용)
`Tools/DataTable/DataTable Importer` 메뉴에서:
1. Sheet URL, API Key 입력 후 **시트 불러오기**
2. 원하는 탭 선택 후 **선택 시트 생성** — `{ScriptFolder}/{TabName}.cs`와 `Resources/GeneratedTables/{TabName}.asset`이 만들어집니다
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

1. **타입 이름 오타** — `enum:ElementTyp`처럼 존재하지 않는 타입을 적으면, "선택 시트 생성" 시점에 **에러로 즉시 차단**되고 해당 탭 전체가 생성되지 않습니다. 같은 이름의 enum이 여러 개(다른 네임스페이스) 있어도 모호하다고 차단됩니다.
2. **셀 값 오타** — 타입은 맞는데 셀 값이 `"Fier"`처럼 실제 enum 멤버와 안 맞으면, 조용히 기본값으로 넘어가지 않고 **Console에 에러 로그**(테이블명·행 번호·잘못된 값·enum 타입명 포함)를 남기고 해당 필드만 기본값으로 처리합니다. 다른 컬럼 값은 그대로 유지됩니다.

> 즉 오타가 있으면 "선택 시트 생성/갱신" 직후 Console에서 반드시 확인할 수 있습니다 — 조용히 잘못된 데이터가 들어가는 경우는 없습니다.

---

#### 2) 런타임 데이터 접근
```cs
Item.Data item = DataManager.Instance.GetTable<Item>().Get(1001);
```

- **`DataManager.Instance.GetTable<T>()`**
  -> 처음 호출 시 `Resources/GeneratedTables/{T의 타입명}`에서 로드 후 캐싱, 이후 호출은 캐시 반환
- **`table.Get(rowKey)`**
  -> 해당 rowKey 행이 없으면 `null` 반환

---

### 테스트 방법
`Assets/00.Scripts/Tests/DataTester.cs`를 아무 GameObject에 붙이고 Play하면, RowKey를 입력하고 Item/Monster/Quest/Sound 각 테이블에서 조회한 결과를 버튼으로 확인할 수 있습니다.

</details>

---

<details id="pooling">
<summary><h2>2️⃣ Pooling</h2></summary>

### 기능
- 프리팹(GameObject) 단위 풀링 — 프리팹별로 독립된 Pool 관리
- Dictionary + Queue 구조로 재사용 인스턴스 관리
- Instantiate / Destroy 최소화 (Spawn/Despawn만 반복)
- `IPoolable.OnSpawn/OnDespawn` 상태 초기화 훅 제공 (자식 오브젝트 포함 자동 호출)
- 씬 배치 불필요 — 처음 사용하는 순간 자동 생성

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
* 항목: Prefab / Prewarm Count / Max Count(0 = 무제한) / Auto Expand
* 재사용 대기 중인(비활성) 인스턴스는 `PoolManager` 하위 `[PoolRoot]/[Pool] <프리팹 이름>`에 자동으로 정리됩니다. Spawn 시 부모를 직접 넘기지 않으면 씬 루트로 배치됩니다.

> 단순한 풀링이 필요한 경우에는 설정 없이 `Spawn`/`Despawn`만 사용해도 됩니다. 설정은 대량 생성·성능 관리가 필요할 때만 추가하면 됩니다. 스폰 시 부모가 필요하면 `Spawn(prefab, position, rotation, parent)`처럼 그때그때 넘겨주세요.

---

### 테스트 방법
`Assets/00.Scripts/Tests/PoolingTester.cs`를 아무 GameObject에 붙이고 Play하면:
- Spawn/Despawn 버튼으로 재사용 여부를 인스턴스 ID로 직접 확인할 수 있습니다 (Despawn 후 다시 Spawn하면 같은 ID가 재사용됨).
- Hierarchy 창에서 `[PoolRoot]` 하위에 풀이 쌓이는 것도 함께 확인 가능합니다.

</details>

---

<details id="ui">
<summary><h2>3️⃣ UI System</h2></summary>

### 기능
- 단일 팝업 표시 (Single Active Popup)
- 우선순위 처리 (Low / Normal / High / Critical)
- 선점 / 대기 / 교체 정책
- Suspend / Resume 흐름
- 닫힘 연출 대응 (비동기 Close)
- Model 입력 차단
- Pooling 연계

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

#### 정책 지정
```cs
UIManager.Instance.RequestPopup(
    popupPrefab,
    EPopupPriority.High,
    policy: EPopupPolicy.ReplaceCurrent
);
```

---

#### 팝업 닫기
```cs
UIManager.Instance.CloseTopPopup();
```

</details>

---

<details id="audio">
<summary><h2>4️⃣ Audio System</h2></summary>

### 기능
- Sound Sheet 기반 사운드 관리
  * Channel(BGM/SFX/UI/Voice), Volume, Loop, MaxConcurrent 등을 Sheet에서 관리
- ESound 자동 생성
  * Sheet의 `FileName`을 기반으로 `ESound` enum 자동 생성
- Addressables 자동 등록
  * Audio 폴더 스캔 후 Addressables 그룹에 자동 등록
  * Addressables address = fileName 규칙 강제
- 사운드 재생 통합 API
  * `SoundManager.Instance.PlaySound(ESound.xxx)` 형태로 단순 사용
- BGM 크로스페이드
- 동시 재생 제한
  * 사운드별 MaxConcurrent 설정 지원

---

### 사용 방법

#### 1) 사운드 파일 추가
오디오 파일을 아래 주소 폴더에 추가

`Assets/Audio/`

예)
* Assets/Audio/SFX_Test.wav
* Assets/Audio/BGM_Test.mp3

---

#### 2) Google Sheet(Sound 탭)에 Row 추가
`FileName`은 확장자 제외 파일명과 반드시 동일해야 합니다.

예)
|Google Sheet|
|-|
|<img width="521" height="104" alt="image" src="https://github.com/user-attachments/assets/3908d0a7-2312-4e49-9d7b-13bbabb55319" />|

---

#### 3) ESound 생성
SoundSO가 준비되면 `FileName`을 기반으로 enum을 자동 생성합니다.

Unity Editor에서 아래 버튼을 누릅니다.

`Framework/Audio/Generate/ESound From SoundTable`

* 동작:
  * `SoundSO`를 읽어 `FileName` 목록 수집
  * `ESound.cs`를 자동 생성
* 생성 위치:
  * `Assets/Scripts/Audio/ESound.cs`

이제 런타임에서 다음처럼 바로 사용 가능합니다.

`SoundManager.Instance.PlaySound(ESound.UI_Click);`

---

#### 4) SoundDatabase 빌드 + Addressables 자동 등록
SoundDatabaseSO를 갱신하고 AudioClip을 Addressables에 자동 등록합니다.

`Framework/Audio/Build Sound Database From Sheet + Folder`

* 입력:
  * `SoundSO` (시트 파싱 결과)
  * `Assets/Audio/` 폴더의 AudioClip들
* 출력:
  * `SoundDatabaseSO` entries 자동 갱신
  * Addressables 그룹에 자동 등록 + address 통일(fileName)

>런타임에서 자동 로드되도록 `SoundDatabaseSO`는 아래 위치를 사용합니다.

```text
Assets/Resources/SoundDatabaseSO.asset
또는 Assets/Resources/Audio/SoundDatabaseSO.asset
```

---

#### 5) 런타임 사용

```cs
// SFX/UI/Voice
SoundManager.Instance.PlaySound(ESound.UI_Click);
SoundManager.Instance.PlaySound(ESound.SFX_Merge);

// BGM
SoundManager.Instance.PlaySound(ESound.BGM_Main);

// BGM 정지
SoundManager.Instance.StopBgm();

// 모든 원샷 정지
SoundManager.Instance.StopAllOneShots();

// 마스터 볼륨 설정
SoundManager.Instance.SetMasterVolume(0.0f);

// 채널별 볼륨 설정
SoundManager.Instance.SetChannelVolume(EAudioChannel.BGM, 0.6f);
SoundManager.Instance.SetChannelVolume(EAudioChannel.SFX, 1.0f);
SoundManager.Instance.SetChannelVolume(EAudioChannel.UI, 0.8f);
SoundManager.Instance.SetChannelVolume(EAudioChannel.Voice, 1.0f);
```

</details>

---

<details id="saveload">
<summary><h2>5️⃣ Save / Load</h2></summary>

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
- `JsonFileSaveProvider`, `PlayerPrefsSaveProvider`, `MemorySaveProvider`는 **`UnityEngine.JsonUtility`만 사용합니다. 외부 패키지 불필요.**
- `ES3SaveProvider`만 예외적으로 **Easy Save 3** (Unity Asset Store 유료 에셋) 설치가 필요합니다. 설치 안 하면 자동으로 JsonFile로 대체됩니다.

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
SaveManager.Instance.Flush();



// Backup / Restore
bool hasBackup = SaveManager.Instance.HasBackup();

SaveManager.Instance.BackupNow();
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
<summary><h2>6️⃣ Time System</h2></summary>

### 기능
게임 전반의 시간 흐름을 UTC 기준으로 통합 관리합니다.

- **서버/로컬 시간 소스 전환** — `Mode`(LocalOnly / ServerOnly / PreferServer)를 설정으로 선택. 게임마다 서버 동기화가 필요한지 다르기 때문에 하나로 고정하지 않음
- **모노토닉 클럭 기반 서버 신뢰도 판단** — 서버 동기화는 기기 시계가 아니라 `Stopwatch` 기반 모노토닉 클럭에 앵커링되어, 동기화 후 기기 시계를 바꿔도 흔들리지 않음. 신뢰 유효기간(Trust Window) 만료 시 자동으로 로컬로 대체
- **일/주/월 리셋 키 + 남은 시간 계산**
- **쿨타임** — 개별 조회 + 전체 목록 조회
- **오프라인 경과 시간**
- **시간 역행(치트) 감지** — 마지막 접속 시각보다 뒤로 가면 감지, 허용 오차 내의 사소한 뒤로 감(NTP 보정 등)은 미신뢰 소스에 한해 허용
- **리셋 크로싱 이벤트** — 게임을 켜놓은 채로 일/주/월 리셋 시각을 넘기면 `OnDailyReset`/`OnWeeklyReset`/`OnMonthlyReset` 발생
- **서버 재동기화 필요 신호** — `IsServerTrustExpiringSoon(초)`으로 신뢰 만료가 임박했는지 확인 가능 (실제 재동기화 네트워크 호출은 프로젝트마다 다르므로 신호만 제공)
- **이벤트 기간 유틸(`TimeRangeUtc`)** — 시작~종료 UTC 구간의 진행 여부/남은 시간 계산 (기간 한정 이벤트 등에 사용)
- **스키마 버전 체크** — 저장된 버전과 현재 버전이 다르면 감지 후 로그
- **테스트용 Mock 시간** — 시간 점프, 리셋 시점으로 바로 이동
- **Save / Load 연동** — 모든 시간 데이터 영구 저장

---

### 외부 패키지
없음. `System.Diagnostics.Stopwatch`(모노토닉 클럭)와 Unity API만 사용합니다.

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

> 서버 신뢰는 **앱을 껐다 켜는 것만으로는 풀리지 않습니다** (OS 부팅 이후 누적 시간 기준 클럭 사용). 다만 **기기를 재부팅하면 항상 풀리고** 다음 `ApplyServerUtc` 전까지 로컬 시간으로 대체됩니다 — 재부팅 후에는 경과 시간을 검증할 방법이 없어 안전하게 미신뢰 처리하는 의도된 동작입니다. `ServerOnly`/`PreferServer`를 쓴다면 앱 시작 시점에 항상 서버 동기화를 한 번 시도하는 걸 권장합니다.

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

