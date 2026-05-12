
# 🎮 Team Omok

> Unity + Photon 기반 실시간 멀티플레이 오목 프로젝트  

-----

# 🖼 게임 스크린샷

| 타이틀 | 매칭 | 코인토스 |
|---|---|---|
| <img src="" width="300"> | <img src="" width="300"> | <img src=""> |

| 인게임 | 금수 표시 | 결과 화면 |
|---|---|---|
| <img src="" width="300"> | <img src=""> | <img src=""> |



---

# 📌 프로젝트 소개

**Team Omok**은 단순한 오목 게임이 아닌,  
실시간 멀티플레이 환경에서의 안정적인 동기화와 게임 흐름 제어를 목표로 개발한 프로젝트입니다.

Photon PUN2 기반 네트워크 구조를 사용하여:

- 실시간 턴 동기화
- 재접속 및 룸 복구
- 금수/쌍삼 판정
- 코인 토스 선공 결정
- 제한 시간 기반 자동 착수
- 게임 상태 동기화

등의 기능을 구현하고 있습니다.

---

# 🛠 기술 스택

| 분야 | 사용 기술 |
|---|---|
| Engine | Unity 6 |
| Language | C# |
| Network | Photon PUN2 |
| UI | TextMeshPro |
| Version Control | GitHub |
| Collaboration | Jira |
| IDE | Rider / Visual Studio |

---

# 🎯 프로젝트 목표

## 1. 안정적인 멀티플레이 구조 구현

- Photon 기반 실시간 동기화
- RaiseEvent/RPC 기반 이벤트 처리
- 중복 실행 방지
- 네트워크 지연 대응
- 재접속 안정화

## 2. 게임 규칙 시스템 구현

- 턴 제어
- 금수/쌍삼 판정
- 승리 판정
- 제한 시간 시스템
- 랜덤 자동 착수

## 3. 협업 중심 개발 경험

- Jira 기반 Sprint 관리
- Git Flow 기반 브랜치 전략
- 코드 리뷰 및 역할 분리
- 기능 단위 이슈 관리

---

# 👥 팀 역할

| 역할 | 담당 |
|---|---|
| Multiplayer / Network | Photon 동기화, 재접속, 룸 관리 |
| Client Gameplay | 보드 시스템, UI, 게임 흐름 |
| UI/UX | HUD, 매칭 UI, 연출 |
| QA & Stabilization | 예외 처리 및 버그 수정 |

---

# 🌐 주요 기능

## 🎲 매칭 시스템
- 자동 룸 매칭
- 플레이어 동기화
- 3-2-1 시작 카운트

## ⚫ 오목 시스템
- 흑/백 턴 제어
- 돌 배치 동기화
- 승리 판정

## 🚫 금수 시스템
- 쌍삼 판정
- 금수 위치 표시
- 예외 처리

## 🪙 코인 토스 시스템
- 선공 결정
- 결과 동기화
- 자동 선택 타이머

## ⏱ 턴 제한 시간 시스템
- 20초 제한 시간
- 시간 초과 시 자동 착수
- MasterClient 기준 동기화

## 🔄 재접속 시스템
- 룸 복귀
- 게임 상태 복구
- 플레이어 상태 동기화

---

# 📂 브랜치 전략

| 브랜치 | 설명 |
|---|---|
| main | 배포 및 안정 버전 |
| develop | 통합 개발 브랜치 |
| feat/* | 기능 개발 |
| fix/* | 버그 수정 |
| refactor/* | 구조 개선 |

### 브랜치 예시

```bash
feat/SCRUM-170-reconnect-sync
fix/SCRUM-171-start-duplication
refactor/network-stabilization
```

---

# 📝 커밋 컨벤션

```bash
feat: 제한 시간 자동 착수 추가
fix: 재접속 시 게임 상태 복구 수정
refactor: RPC 동기화 구조 개선
```

### Jira 연동 예시

```bash
feat(SCRUM-170): 재접속 동기화 추가
fix(SCRUM-171): 중복 시작 버그 수정
```

---

# 📋 Jira 규칙

## 이슈 타입

| 타입 | 설명 |
|---|---|
| Feature | 신규 기능 |
| Bug | 버그 수정 |
| Refactor | 구조 개선 |
| Stabilization | 동기화 및 안정화 |

---

# 🔥 현재 중점 작업

- 네트워크 안정화
- 재접속 처리
- 승패 동기화
- 예외 처리 강화
- UI 연출 개선
- 최종 빌드 안정화

---

# 📅 개발 일정

| 기간 | 목표 |
|---|---|
| ~ 5/13 | 기능 구현 및 안정화 |
| ~ 5/15 | 최종 빌드 및 발표 준비 |

---

# 🚀 실행 방법

```bash
1. Unity 프로젝트 실행
2. Photon 설정 확인
3. Start Scene 실행
4. Lobby 입장 후 매칭 시작
```

---

# 📷 주요 개발 포인트

- Photon 기반 실시간 턴 동기화
- MasterClient 권한 기반 처리
- 네트워크 지연 대응 설계
- RPC 중복 실행 방지
- 재접속 복구 시스템 구현

---

# 📚 협업 방식

- Jira 기반 Sprint 관리
- GitHub Pull Request 사용
- 기능 단위 브랜치 작업
- Merge 전 테스트 진행
- 커밋 메시지 규칙 통일

---

# 🧩 프로젝트 구조

```bash
Assets/
├── Scripts/
│   ├── Network/
│   ├── Board/
│   ├── UI/
│   ├── Game/
│   └── Managers/
├── Prefabs/
├── Scenes/
└── Resources/
```

---

# ✅ 목표

단순한 오목 게임 제작이 아니라,  
실시간 멀티플레이 환경에서 발생하는 동기화 문제를 해결하며  
안정적인 네트워크 게임 구조를 구현하는 것을 목표로 합니다.
