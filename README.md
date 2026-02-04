# Unity Game Framework

> Unity 게임 개발에서 반복적으로 구현되는 시스템들을  
> **프레임워크 단위로 정리한 공통 게임 개발 기반**입니다.
>
> 특정 게임에 종속되지 않으며,  
> 여러 프로젝트에서 재사용·확장하는 것을 목표로 합니다.

---

## 📦 Frameworks

현재 포함된 프레임워크는 다음과 같습니다.

- **Data Parsing Framework**  
  Google Sheet 기반 게임 데이터 파이프라인

- **Pooling Framework**  
  Type 기반 공용 오브젝트 풀링 시스템

- **UI System Framework**  
  우선순위 / 선점 기반 UI 흐름 관리 시스템

- **Audio System Framework**  
  Addressables + Sheet 기반 사운드 재생/관리 시스템

- **Save / Load Framework**  
  Provider 기반 저장/로드 시스템 (AutoFlush / Backup / Restore 지원)

- **Time Framework**  
  UTC 기반 시간 관리, 리셋, 쿨타임, 서버 시간 동기화 시스템

> 프레임워크는 지속적으로 추가될 예정입니다.

---

## 1️⃣ Data Parsing Framework

### 기능
- Google Sheet → ScriptableObject 자동 변환
- Sheet Tab 선택 후 SO 생성
- SO 갱신(Update) 지원
- 런타임 Dictionary 캐싱

---

### 사용 방법

#### 런타임 데이터 접근
```cs
ItemData item = ItemTable.Instance.Get(1001);
```

---

## 2️⃣ Pooling Framework

### 기능
- Type 기반 풀링
- Dictionary + Stack 구조
- Instantiate / Destroy 최소화
- 상태 초기화 훅 제공

---

### 사용 방법

```cs
// 풀에 남아있는 같은 타입 인스턴스를 재사용하거나,
// 없으면 prefab으로 새로 생성해서 반환한다.
MyObject obj = Pool.Get<MyObject>(myObjectPrefab);

// 사용 (위치, 데이터 등은 사용자가 초기화)
obj.transform.position = spawnPosition;
obj.gameObject.SetActive(true);

// 사용이 끝나면 Destroy하지 않고 비활성화 후 풀에 반환한다.
Pool.Return(obj);
```

- **`Get<T>(prefab)`**
  -> 재사용(있으면) / 생성(없으면)  
- **`Return(obj)`**
  -> 비활성화 후 풀에 보관, 다음 요청 시 재사용

---

## (Optional) Pool Settings ScriptableObject
Pooling Framework는 선택적으로
**ScriptableObject 기반 풀 설정을 사용할 수 있습니다.**

|SO|
|-|
|<img width="392" height="249" alt="image" src="https://github.com/user-attachments/assets/2b25c199-f671-493f-9a68-f04054997782" />|

---

### Pool Settings 항목
- Prefab : 풀링 대상 오브젝트
- Prewarm Count : 시작 시 미리 생성할 개수
- Max Count : 풀 최대 개수
- Auto Expand : 최대 개수 초과 시 자동 생성 여부
- Default Parent : 풀링 오브젝트의 기본 부모 Transform

> 단순한 풀링이 필요한 경우에는 설정 없이 사용 가능하며,
> 대량 생성·성능 관리가 필요한 경우에만 PoolingSettings를 사용하면 됩니다.

---

## 3️⃣ UI System Framework

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

---

## 4️⃣ Audio System Framework

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

---

## 5️⃣ Save / Load Framework

### 기능
- **Provider 기반 저장 시스템**
  - `ISaveProvider` 인터페이스로 저장 방식 교체 가능
  - 기본 제공 Provider
    - `PlayerPrefsSaveProvider` (default)
    - `ES3SaveProvider` (optional)
    - `MemorySaveProvider` (테스트용 런타임 임시 저장)
   
- **Domain + Key 기반 저장 구조**
  - `SaveKey`를 통해 `root/domain/key` 형태로 안전하게 키 관리
- **Auto Flush (Dirty 기반 자동 저장)**
  - 변경 발생 시 Dirty 처리 -> 일정 시간 후 자동 Flush
- **Pause/Quit 저장**
  - `OnApplicationPause`, `OnApplicationQuit`에서 Flush 처리
- **백업/복구 지원**
  - `ISaveBackupProvider` 기반
  - `BackupNow()`, `RestoreFromBackup()` 제공
- **Save Meta 자동 관리**
  - `saveVersion`, `createdAtUtc`, `lastSavedAtUtc` 자동 저장/갱신
 
---

### 저장 키 구조
Save Framework는 모든 키를 아래 규칙으로 통합합니다.

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

#### 1) SaveManager 씬 배치
`SaveManager`는 `MonoSingleton<SaveManager>` 기반이라 런타임 자동 생성도 가능하지만,  
초기화 순서 보장 및 Inspector 설정 관리를 위해 Boot 씬에 1회 배치를 권장합니다.  

---

#### 2) Provider 선택 (default : PlayerPrefs)
기본은 PlayerPrefs 저장이며, ES3로 교체 가능합니다.

```cs
#if USE_ES3
    _provider = new ES3SaveProvider(settings);
#else
    _provider = new PlayerPrefsSaveProvider();
#endif
```

#### ✅ ES3 활성화 방법 (Unity 버튼)
* `Project Settings -> Player -> Other Settings -> Scripting Define Symbol`에 **`USE_ES3`** 추가

> ES3 Provider는 대용량/복잡 데이터에 유리하며 파일 기반 백업/복구까지 지원합니다.

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

### SaveManager Inspector 설정

<img width="366" height="519" alt="image" src="https://github.com/user-attachments/assets/f3ebaaf4-4998-47be-94d2-ae0f82e81b4a" />

|항목|설명|
|-|-|
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
Audio System Framework에서 적용 중인 저장 패턴 예시입니다.

```cs
private const string SettingsDomain = "settings";
private const string AudioSettingsKey = "audio";

SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(AudioSettingsKey);

_settings = SaveManager.Instance.LoadOrCreate(key, () => new AudioSettingsData(), saveIfMissing: true
);

SaveManager.Instance.Save(key, _settings);

```

---

## 6️⃣ Time Framework

### 기능
Time Framework는 **게임 전반의 시간 흐름을 통합 관리**하는 시스템입니다.
모든 시간 계산은 **UTC 기준**으로 동작하며, 서버 시간·쿨타임·리셋·오프라인 경과 시간을 안정저긍로 처리합니다.

---

핵심 기능 요약

|기능|설명|
|-|-|
|UTC 기준 시간 제공|`TimeManager.Instance.UtcNow`로 현재 시간 제공|
|서버 시간 동기화|서버 UTC를 받아 로컬 시간 왜곡 없이 보정|
|시간 신뢰도 판단|서버 시간 유효 기간 관리(`Trust Window`)|
|일/주/월 리셋 계산|리셋 시각 기준 키 및 남은 시간 계산|
|쿨타임 시스템|스킬,상점,보상 등에 사용되는 타이머|
|오프라인 경과 시간|앱 종료 후 경과 시간 계산|
|시간 역행(치트) 감지|기기 시간 되돌림 탐지|
|테스트용 Mock 시간|시간 점프 및 리셋 테스트 지원|
|Save Framework 연동|모든 시간 데이터 영구 저장|

---

## 씬 배치
Time Framework는 `MonoSingleton<TimeManager>` 기반입니다.

```text
⚠ SaveManager보다 먼저 초기화되면 안 되므로
Boot 씬에 SaveManager와 함께 배치하는 것을 권장합니다.
```

---

현재 시간 사용
`DataTimeOffset now = TimeManager.Instance.UtcNow;`

---

서버 시간 적용
`TimeManager.Instance.ApplyServerUtc(serverUtc);`
서버 시간이 신뢰 가능할 경우 `PreferServer` 모드에서 자동으로 서버 시간이 사용됩니다.

---

쿨타임 사용

```cs
// 쿨타임 시작
TimeManager.Instance.StartCooldown("skill_A", TimeSpan.FromSeconds(30));

// 사용 가능 여부
bool ready = TimeManager.Instance.IsCooldownReady("skill_A");

// 남은 시간
TimeSpan remain = TimeManager.Instance.GetCooldownremaining("skill_A");

// 강제 초기화
TimeManager.Instance.ClearCooldown("skill_A");
```

---

리셋 키 (Daily / Weekly / Monthly)

```cs
int dailyKey = TimeManager.Instance.GetDailyKey();
int weeklyKey = TimeManager.Instance.GetWeeklyKey();
int monthlyKey = TimeManager.Instance.GetMonthlyKey();
```

이 키는 **보상 중복 지급 방지**에 사용할 수 있습니다.

---

리셋까지 남은 시간

```cs
TimeSpan remain = TimeManager.Instance.GetRemainingToDailyReset();
string text = TimeManager.Instance.GetDailyResetRemainingText();
```

---

오프라인 경과 시간
`TimeSpan offline = TimeManager.Instance.GetOfflineDelta();`

스태미나 회복, 생산 정산 등 여러가지 기능을 구현할 때 활용 가능합니다.

---

시간 역행 감지
bool cheated = TimeManager.Instance.IsCheatDetected;
TimeManager.Instance.ClearCheatFlag();

---

테스트용 Mock 시간

```cs
TimeManager.Instance.EnableMockTime();
TimeManager.Instance.AddMockSeconds(3600); // +1시간

TimeManager.Instance.JumpToNextDailyResetForTest();
TimeManager.Instnace.DisableMockTime();
```

---

저장 구조
Time Framework의 모든 데이터는 Save Framework의 Domain을 사용하여 저장됩니다.

```text
game/time/...
```

저장 항목 예:
* 서버 동기화 시각
* 마지막 접속 시간
* 쿨타임 종료 시각
* Mock 시간 오프셋
* 치트 플래그

---

## TimeManager Inspector 설정

<img width="539" height="302" alt="image" src="https://github.com/user-attachments/assets/565251e6-2ed5-480e-85f7-f6d55c817576" />

|항목|설명|
|-|-|
|Mode|LocalOnly / ServerOnly / PreferServer|
|Daily reset Hour|일일 리셋 기준 UTC 시각|
|Weekly Reset Day|주간 리셋 시작 요일|
|Backward Tolerance Sec|시간 역행 허용 오차|
|Server Trust Window|서버 시간 신뢰 유효 기간|
|Schema Version|타임 저장 데이터 버전|

---

## 실제 활용 예시

```cs
// 스킬 쿨타임
if(TimeManager.Instance.IsCooldownReady("skill_A"))
{
    TimeManager.Instance.StartCooldown("skill_A", TimeSpan.FromSeconds(10));
    UseDash();
}

// 일일 보상 리셋 판단
int todayKey = TimeManager.Instance.GetDailykey();
if(lastRewardKey != todayKey)
{
    GiveDailyReward(); // 일일 보상 주는 보상(미구현)
}
```

---

