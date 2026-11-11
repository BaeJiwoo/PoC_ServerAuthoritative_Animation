# Bob 서버 권위 애니메이션 POC 시작 가이드

이 가이드는 현재 로컬 프로젝트에서 서버와 Unity 클라이언트를 실행하고, Bob의 걷기 애니메이션과 히트박스를 확인하는 방법을 설명합니다.

## 1. 준비 사항

| 항목 | 현재 프로젝트 기준 |
| --- | --- |
| 프로젝트 루트 | `/home/jiwoo-bae/Projects/ServerAuthoritative_Animation` |
| 서버 | .NET 8 SDK |
| Unity 버전 | `6000.3.23f1` |
| 열어야 할 Unity 프로젝트 | 프로젝트 루트 아래의 `UnityClient` 폴더 |
| 기본 서버 주소 | `http://127.0.0.1:5000` |
| 클라이언트 접속 주소 | `ws://127.0.0.1:5000/ws` |

Bob 모델과 걷기 FBX는 `StickManFight`에서 이 프로젝트로 복사되어 있습니다. 서버용 애니메이션 JSON과 표시용 프리팹도 이미 생성되어 있으므로 **처음 실행할 때 다시 베이크할 필요는 없습니다.**

원본 `StickManFight` 프로젝트를 함께 실행할 필요도 없습니다.

클라이언트는 서버 스냅샷을 약 100ms 버퍼링하고 위치·크기는 Lerp, 회전은 Slerp로 매 화면 프레임 보간합니다.
HUD의 `Interpolation: 100 ms | Lerp / Slerp`로 적용 여부를 확인할 수 있습니다.
이동 시작·방향 전환·정지가 약 100ms 늦게 표시되는 대신 20Hz 스냅샷 사이의 움직임이 부드러워집니다.
수신이 끊기면 마지막 포즈에서 멈춥니다. 화면 히트박스도 같은 시각으로 보간하며 실제 서버 판정 데이터는 바뀌지 않습니다.

## 2. 서버 실행

터미널을 열고 다음 명령을 실행합니다.

```bash
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation

dotnet run --project src/ServerAuthoritativeAnimation.Server \
  --urls http://127.0.0.1:5000
```

처음 실행하면 필요한 .NET 패키지를 복원하고 서버를 빌드합니다. 다음과 같은 로그가 나오면 준비된 상태입니다.

```text
Loaded Bob_Rigged/WalkForward: 43 transforms, 1s, skeleton ...
Now listening on: http://127.0.0.1:5000
```

서버 터미널은 실행 상태로 둡니다. 별도 터미널에서 다음 명령으로 응답을 확인할 수 있습니다.

```bash
curl http://127.0.0.1:5000/
```

응답에는 `animation`, `skeletonId`, `boneCount`, `tickRate` 등이 표시됩니다. HTTP 루트 주소는 상태 확인용이고, Unity는 `/ws`에 WebSocket으로 연결합니다.

## 3. Unity 클라이언트 실행

### Unity Hub에서 열기

1. Unity Hub에서 다음 폴더를 프로젝트로 추가하고 Unity `6000.3.23f1`로 엽니다.

   ```text
   /home/jiwoo-bae/Projects/ServerAuthoritative_Animation/UnityClient
   ```

2. 에셋 임포트와 스크립트 컴파일이 끝날 때까지 기다립니다.
3. 상단 메뉴에서 **POC → Run Server-Authoritative Demo**를 선택합니다.
4. Game 뷰를 클릭하고 WASD를 누릅니다.

이 메뉴는 빈 씬을 새로 만들고 자동으로 Play Mode에 진입합니다. 별도로 씬을 구성할 필요가 없습니다. 이미 열린 빈 씬에서 Play 버튼을 눌러도 실행할 수 있습니다.

### 터미널에서 바로 열기

같은 프로젝트가 다른 Unity 에디터에서 열려 있지 않을 때 사용합니다.

```bash
/home/jiwoo-bae/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -projectPath /home/jiwoo-bae/Projects/ServerAuthoritative_Animation/UnityClient \
  -executeMethod ServerAuthoritativeAnimation.Client.Editor.PocLauncher.Run \
  -logFile /tmp/bob-client.log
```

에디터가 열리고 자동으로 Play Mode에 진입합니다. Unity Console 또는 `/tmp/bob-client.log`에서 다음 연결 로그를 확인합니다.

```text
Connected to ws://127.0.0.1:5000/ws
```

## 4. 화면과 조작 확인

화면에는 흰색 Bob 캐릭터, 바닥, 루트 마커, 전방 표시선과 컬러 와이어 히트박스가 표시됩니다.

| 입력 | 기대 동작 |
| --- | --- |
| W | 월드 `+Z`를 바라보며 걷기 |
| S | 월드 `-Z`로 몸을 돌린 뒤 같은 전진 걷기 동작으로 이동 |
| A | 월드 `-X`를 바라보며 걷기 |
| D | 월드 `+X`를 바라보며 걷기 |
| WA / WD / SA / SD | 대각선 방향을 바라보며 이동; 이동 속도는 직선과 동일 |
| 모든 이동 키 해제 | 이동·위상 진행 정지, 기준 서기 포즈 표시 |

입력 방향은 **카메라 기준이 아닌 월드 좌표 기준**입니다. 서버는 방향 전환을 즉시 적용하며 클라이언트 화면은 스냅샷 사이의 회전을 보간합니다.

히트박스는 머리·몸통과 양쪽 상완·전완·허벅지·정강이, 총 10개입니다. 팔꿈치와 무릎이 굽혀지면 각각의 박스도 해당 관절을 따라 움직여야 합니다.

화면 왼쪽 위에서 다음 상태를 확인할 수 있습니다.

- `Moving`: 이동 중인지 여부
- `Walk phase`: 걷기 루프 위상; 이동 중에 진행하고 키를 놓으면 멈춤
- `Bob | 43 server transforms`: 서버 본 포즈가 모델에 적용되는 상태

현재 걷기는 `StickManFight`의 `Bob_Walk_Forward.fbx`를 그대로 사용합니다. 팔을 앞으로 든 자세도 원본 클립에 포함된 동작입니다.

## 5. 종료

1. Unity의 Play 버튼을 다시 눌러 Play Mode를 종료합니다.
2. 에디터도 닫으려면 Unity 창을 종료합니다.
3. 서버를 실행한 터미널에서 `Ctrl+C`를 누릅니다.

Unity만 닫아서는 서버가 종료되지 않습니다. 서버 로그에 `Application is shutting down...`이 표시되는지 확인합니다.

## 6. 애니메이션 데이터 다시 만들기

FBX 또는 임포트 설정을 변경했을 때만 진행합니다.

### 현재 Bob 걷기 재추출

1. Unity Play Mode와 서버를 종료합니다.
2. Unity 메뉴에서 **POC → Bake Bob WalkForward for Server**를 선택합니다.
3. Console에서 `BAKED_ANIMATION` 로그를 확인합니다.
4. 2절의 `dotnet run` 명령으로 서버를 다시 실행합니다.
5. Unity에서 데모를 다시 시작합니다.

베이크는 다음 파일을 갱신합니다.

| 파일 | 용도 |
| --- | --- |
| `assets/animations/BobWalkForward.json` | 서버가 재생하는 전체 본 애니메이션 |
| `assets/animations/BobWalkForward.reference.json` | Unity와 서버의 본 계산 결과를 비교하는 테스트 기준 |
| `UnityClient/Assets/Resources/ServerCharacter.prefab` | 서버 포즈를 적용할 Bob 표시용 프리팹 |
| `UnityClient/Assets/Resources/BobWhite.mat` | 표시용 흰색 머티리얼; 없으면 생성 |

서버 JSON과 프리팹은 서로 맞는 한 쌍입니다. 서버 빌드 시 JSON이 실행 폴더로 복사되므로, 베이크 직후에는 `--no-build` 없이 서버를 실행합니다.

### 다른 Mixamo 걷기 클립 사용

1. 다운로드한 걷기 FBX를 `UnityClient/Assets` 아래로 가져옵니다.
2. Bob 모델과 호환되는 Humanoid 아바타를 설정합니다. Mixamo 본 계층이 유지되어야 하며 `Optimize Game Objects`는 해제합니다.
3. 이동은 서버가 담당하므로 In Place 걷기 루프를 사용합니다.
4. **POC → Bake Imported Walk Clip...**을 엽니다.
5. `Character model`에 Bob 모델을, `WalkForward clip`에 FBX 내부의 AnimationClip을 지정합니다.
6. **Bake full skeleton at 60 Hz**를 누릅니다.
7. 서버를 다시 빌드·실행하고 Unity 데모를 시작합니다.

추출기는 현재 Mixamo 본 이름으로 히트박스를 연결합니다. 다른 이름 체계의 리그는 매핑 수정이 필요하며, 비균일 본 스케일은 지원하지 않습니다. 다른 클립을 베이크해도 출력 파일명은 `BobWalkForward.json`으로 유지됩니다.

### 베이크 CLI

같은 프로젝트의 Unity 에디터를 닫은 상태에서 실행합니다.

```bash
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation

/home/jiwoo-bae/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD/UnityClient" \
  -executeMethod ServerAuthoritativeAnimation.Client.Editor.ServerAnimationBaker.BakeBob \
  -quit -logFile /tmp/bob-bake.log
```

## 7. 문제 해결

| 증상 | 확인 및 해결 |
| --- | --- |
| W를 눌러도 움직이지 않음 | Play Mode인지 확인하고 Game 뷰를 클릭합니다. 서버 연결 로그도 확인합니다. |
| `WebSocket unavailable` 또는 연결 재시도 | 서버가 실행 중인지, 서버 포트와 클라이언트 `/ws` 주소가 맞는지 확인합니다. |
| `Rig mismatch: bake + restart server` | Bob을 다시 베이크하고, `--no-build` 없이 서버를 재실행합니다. Unity Play Mode도 다시 시작합니다. |
| 서버에서 `BobWalkForward.json`을 찾지 못함 | 6절의 베이크를 실행한 뒤 서버를 다시 빌드·실행합니다. |
| 서버 포트가 이미 사용 중 | 기존 서버 터미널에서 `Ctrl+C`로 종료하거나 아래 절차로 포트를 변경합니다. |
| Unity에서 프로젝트가 이미 열려 있다고 나옴 | 기존 에디터를 사용하거나 종료한 뒤 CLI/테스트를 실행합니다. |
| `POC` 메뉴가 나타나지 않음 | `UnityClient`를 열었는지, 임포트·컴파일이 끝났는지, Console에 컴파일 오류가 있는지 확인합니다. |
| FBX 베이크에서 본/Avatar 관련 오류 | Bob의 Humanoid Avatar 연결과 Mixamo 본 이름, `Optimize Game Objects` 해제 여부를 확인합니다. |
| 키 해제 후 팔다리가 서기 포즈로 돌아옴 | 정상 동작입니다. 마지막 걷기 포즈가 아닌 정적 기준 포즈를 표시합니다. |

포트를 바꾸려면 서버와 클라이언트 주소를 함께 변경합니다. 예를 들어 `5001`을 사용할 경우:

```bash
# 서버 터미널
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation
dotnet run --project src/ServerAuthoritativeAnimation.Server \
  --urls http://127.0.0.1:5001
```

```bash
# 별도 터미널: 기존 Unity 에디터를 닫은 뒤 실행
/home/jiwoo-bae/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -projectPath /home/jiwoo-bae/Projects/ServerAuthoritative_Animation/UnityClient \
  -executeMethod ServerAuthoritativeAnimation.Client.Editor.PocLauncher.Run \
  --server-url ws://127.0.0.1:5001/ws
```

## 8. 자동 테스트

### .NET 테스트

```bash
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation
dotnet test ServerAuthoritativeAnimation.sln --configuration Release
```

방향 입력, 정규화, 서버 이동, 실제 FBX 포즈, 루프·정지 동작, 히트박스와 Unity 기준 포즈의 일치를 검사합니다.

### Unity EditMode 테스트

Unity 에디터를 닫고 실행합니다. 서버는 필요하지 않습니다.

```bash
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation

/home/jiwoo-bae/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD/UnityClient" \
  -runTests -testPlatform editmode \
  -testResults /tmp/bob-editmode.xml -logFile /tmp/bob-editmode.log
```

### Unity PlayMode 및 서버 통합 테스트

먼저 2절의 서버를 실행하고, 별도 터미널에서 다음 명령을 실행합니다. 같은 프로젝트의 Unity 에디터는 닫혀 있어야 합니다.

```bash
cd /home/jiwoo-bae/Projects/ServerAuthoritative_Animation

SAA_INTEGRATION_WS=ws://127.0.0.1:5000/ws \
/home/jiwoo-bae/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD/UnityClient" \
  -runTests -testPlatform playmode \
  -testResults /tmp/bob-playmode.xml -logFile /tmp/bob-playmode.log
```

실제 WebSocket으로 입력을 보내 Bob의 본 위치·회전이 서버 스냅샷과 일치하는지 확인합니다. `SAA_INTEGRATION_WS`를 생략하면 해당 네트워크 통합 테스트는 건너뜁니다.

테스트 명령에는 `-quit`를 추가하지 않습니다. Unity Test Runner가 결과 XML을 저장하고 종료합니다. 테스트가 끝난 뒤 서버는 별도로 `Ctrl+C`로 종료합니다.

## 9. 동작 구조 알아보기

```text
Bob 모델 + WalkForward FBX
  → Unity 에디터에서 전체 본 포즈 추출
  → 서버용 JSON + 표시용 프리팹 생성

Unity WASD 입력 {x, z}
  → .NET 서버: 루트 회전·이동·걷기 위상 계산
  → 전체 본 계층과 월드 히트박스 계산
  → 서버 스냅샷 전송
  → Unity: Bob 스킨 메시와 히트박스 표시
```

Unity의 Animator는 오프라인 추출 단계에만 사용됩니다. 실행 중인 클라이언트에는 Animator, Idle 애니메이션, 방향별 애니메이션, Blend Tree가 없습니다. FBX를 변경한 뒤 추출하는 과정과 실제 게임 실행은 별도 단계입니다.
