
using BowlingGame.Core;
using BowlingGame.Logic;
using BowlingGame.UI;
/// <summary>
/// 게임의 전체 흐름을 관리하는 Presenter(Controller) 클래스입니다.
/// 사용자 입력을 받아 유효성을 검증하고, 모델(계산기)과 뷰(렌더러)를 조율합니다.
/// </summary>
public class Game
{
    // 확정된 투구 기록을 저장하는 저장소
    private readonly List<int> _rolls = new List<int>();

    // 핵심 의존성 객체 (계산 로직, 화면 출력, 로깅)
    private readonly IScoreCalculator _calculator;
    private readonly IScoreBoardRenderer _renderer;
    private readonly ILogger _logger;

    // 화면 재출력(Repaint) 요청 시 계산 비용을 아끼기 위한 캐시
    private List<ScoreFrameDTO> _lastCalculatedFrames = new List<ScoreFrameDTO>();

    // 게임의 총 프레임 수 (기본 10)
    private readonly int _totalFrames;

    /// <summary>
    /// 기본 설정을 사용하는 생성자입니다.
    /// </summary>
    /// <param name="totalFrames">게임의 총 프레임 수 (기본값: 10)</param>
    public Game(int totalFrames = 10)
    {
        _totalFrames = totalFrames;
        _logger = new ConsoleLogger();
        // 각 모듈에 프레임 수 설정을 전파하여 동기화
        _calculator = new StandardScoreCalculator(_totalFrames);
        _renderer = new ConsoleScoreRenderer(_totalFrames);
    }

    /// <summary>
    /// 테스트 및 확장을 위한 의존성 주입(DI) 생성자입니다.
    /// </summary>
    public Game(IScoreCalculator calculator, IScoreBoardRenderer renderer, ILogger logger)
    {
        _calculator = calculator;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>
    /// 핀을 쓰러뜨립니다. 이 메서드는 [가상 실행(Dry-run)] 패턴을 사용하여
    /// 잘못된 입력이 들어왔을 때 게임 상태가 오염되는 것을 방지합니다.
    /// </summary>
    /// <param name="pins">쓰러뜨린 핀의 개수</param>
    public void KnockDownPins(int pins)
    {
        // 1. 가상 실행 (Simulation/Dry-run)
        // 원본(_rolls)을 건드리지 않고, 입력값을 포함한 임시 리스트를 생성합니다.
        var tempRolls = new List<int>(_rolls) { pins };

        // 2. 계산 및 검증 (Calculate & Validate)
        // 계산기에게 임시 리스트를 전달합니다. 
        // 계산기는 점수 계산뿐만 아니라 도메인 규칙 위반 여부(Validation)도 수행합니다.
        var tempFrames = _calculator.Calculate(tempRolls);

        // 결과 중 에러 메시지가 있는 프레임이 있는지 확인합니다.
        var errorFrame = tempFrames.FirstOrDefault(f => !string.IsNullOrEmpty(f.ErrorMessage));

        if (errorFrame != null)
        {
            // [Rollback] 에러가 발견되면 입력을 저장하지 않고 로그만 출력 후 종료합니다.
            _logger.LogError(errorFrame.ErrorMessage);
            return;
        }

        // 3. 커밋 (Commit)
        // 검증을 통과했으므로 실제 저장소에 반영합니다.
        _rolls.Add(pins);
        _lastCalculatedFrames = tempFrames;

        // 4. 렌더링 (Update View)
        _renderer.Render(tempFrames);
    }
}