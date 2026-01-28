## 📂 핵심 코드 네비게이션 (Core Implementation)

본 프로젝트는 **MVP (Model-View-Presenter)** 아키텍처를 기반으로 모듈화되어 있습니다.

### 🏗️ Architecture
| 역할 (Role) | 파일 (File) | 설명 (Description) |
|:---:|:---|:---|
| **Presenter** | [📄 Game.cs](./BowlingGame/Game/src/BowlingGame/Game.cs) | 전체 흐름 제어, 트랜잭션 및 입력 검증 관리 |
| **Model** | [📄 StandardScoreCalculator.cs](./BowlingGame/Game/src/BowlingGame/Logic/StandardScoreCalculator.cs) | **(핵심)** 볼링 점수 계산 로직, 도메인 규칙 구현 |
| **View** | [📄 ConsoleScoreRenderer.cs](./BowlingGame/Game/src/BowlingGame/UI/ConsoleScoreRenderer.cs) | DTO 기반의 수동적 뷰, 가변폭 렌더링 로직 |
| **Contract** | [📂 Core/](./BowlingGame/Game/src/BowlingGame/Core/) | 인터페이스(`Interface`) 및 데이터 전송 객체(`DTO`) 정의 |

### ✅ Quality Assurance
| 항목 | 파일 (File) | 설명 |
|:---:|:---|:---|
| **Unit Tests** | [🧪 Tests](./BowlingGame/Game/tests/BowlingGame.Tests/StandardScoreCalculatorTests.cs) | `xUnit` 기반의 데이터 주도 테스트 (20+ 케이스) |
| **Design Docs** | [📝 DESIGN_DECISIONS.md](./Design_Decisions.md) | **(필독)** 아키텍처 설계 의도 및 기술적 의사결정 배경 |
