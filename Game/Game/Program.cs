//TargetFramework: net8.0

using System.Text;

class Program
{
    public static void Main(string[] args)
    {
        // 10프레임 게임 인스턴스 생성 (의존성 주입 없이 기본 생성자 사용)
        var game = new Game();

        // 테스트: 정상 입력 시나리오
        // 1프레임: 스페어 (4+6)
        game.KnockDownPins(4);
        game.KnockDownPins(6);

        // 2프레임: 스페어 (5+5)
        game.KnockDownPins(5);
        game.KnockDownPins(5);

        // 3프레임: 스트라이크
        game.KnockDownPins(10);

        // 4프레임: 진행 중 (6)
        game.KnockDownPins(6);
        while (true)
            game.KnockDownPins(int.Parse( Console.ReadLine()));
    }
}

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

#region 기본 구현체
/// <summary>
/// 표준 볼링 규칙을 구현한 순수 로직 계산기입니다.
/// </summary>
public class StandardScoreCalculator : IScoreCalculator
{
    private readonly int _totalFrames;
    private const int MaxPins = 10;

    public StandardScoreCalculator(int totalFrames = 12)
    {
        _totalFrames = totalFrames;
    }

    /// <summary>
    /// 투구 기록을 받아 프레임별 점수 데이터를 생성합니다.
    /// </summary>
    /// <param name="rolls">전체 투구 기록 (읽기 전용)</param>
    public List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls)
    {
        var frames = new List<ScoreFrameDTO>();

        // [Domain Validation] 입력값 범위 검증
        // 도메인 규칙(핀 0~10개)은 계산기가 가장 잘 알기에 여기서 수행합니다.
        var errorFrame = ValidateInputRange(rolls);
        if (errorFrame != null)
        {
            frames.Add(errorFrame);
            return frames;
        }

        // 상태 변수 (ref로 전달되어 메서드 간 공유됨)
        int rollIndex = 0;
        int runningScore = 0;

        // [Phase 1] 1 ~ (Total-1) 일반 프레임 처리
        for (int frameNum = 1; frameNum < _totalFrames; frameNum++)
        {
            // Process 메서드는 rollIndex를 증가시킵니다 (Side-effect 명시)
            ConsumeAndCalculateNormalFrame(frameNum, rolls, frames, ref rollIndex, ref runningScore);
        }

        // [Phase 2] 마지막 프레임 처리 (특수 로직)
        ConsumeAndCalculateFinalFrame(_totalFrames, rolls, frames, ref rollIndex, ref runningScore);


        // [Validation] 추가 투구 감지 (게임 종료 후 입력 방지)
        var extraError = ValidateExtra(rollIndex, rolls);
        if (extraError != null)
        {
            frames.Add(extraError);
        }

        return frames;
    }

    // 입력된 핀 개수가 유효 범위(0~10)인지 확인
    private ScoreFrameDTO? ValidateInputRange(IReadOnlyList<int> rolls)
    {
        for (int i = 0; i < rolls.Count; i++)
        {
            if (rolls[i] < 0 || rolls[i] > MaxPins)
            {
                return new ScoreFrameDTO
                {
                    FrameNumber = 1,
                    ErrorMessage = $"[System Error] Invalid input detected at index {i}: {rolls[i]}. Pins must be 0~10."
                };
            }
        }
        return null;
    }

    // 모든 프레임 처리가 끝났는데 데이터가 남아있는지 확인
    private ScoreFrameDTO? ValidateExtra(int rollIndex, IReadOnlyList<int> rolls)
    {
        if (rollIndex < rolls.Count)
        {
            var extraFrame = new ScoreFrameDTO
            {
                FrameNumber = _totalFrames + 1, // 가상의 에러 프레임
                ErrorMessage = "[System Error] Extra rolls detected beyond the final frame."
            };
            return extraFrame;
        }
        return null;
    }

    /// <summary>
    /// 일반 프레임 로직: '2번의 기회' 안에 '10핀 제거' 과업을 수행
    /// </summary>
    private void ConsumeAndCalculateNormalFrame(int frameNum, IReadOnlyList<int> rolls, List<ScoreFrameDTO> frames, ref int rollIndex, ref int runningScore)
    {
        var dto = new ScoreFrameDTO { FrameNumber = frameNum, Type = FrameType.Normal };

        // 데이터가 부족하면 빈 프레임 저장 후 종료 (Waiting)
        if (rollIndex >= rolls.Count)
        {
            frames.Add(dto);
            return;
        }

        int currentPins = MaxPins;
        int maxTries = 2; // 일반 프레임 기회
        int tries = 0;
        bool isFrameCleared = false;

        // [Loop] 기회만큼 반복하며 투구 처리
        while (tries < maxTries)
        {
            if (rollIndex >= rolls.Count) break;

            int roll = rolls[rollIndex];

            // [Validation] 물리적 불가능 체크 (남은 핀보다 많이 쓰러뜨림)
            if (roll > currentPins)
            {
                dto.Rolls.Add(roll);
                dto.ErrorMessage = $"ERROR: Frame {frameNum} roll is {roll} (Remain: {currentPins}). Input ignored.";
                rollIndex++;
                break;
            }

            dto.Rolls.Add(roll);
            currentPins -= roll;
            rollIndex++;
            tries++;

            // 핀 클리어(스트라이크/스페어) 시 즉시 종료 (남은 기회 소멸)
            if (currentPins == 0)
            {
                isFrameCleared = true;
                break;
            }
        }

        if (!string.IsNullOrEmpty(dto.ErrorMessage))
        {
            frames.Add(dto);
            return;
        }

        // 점수 계산 (후행성: 미래의 투구 점수가 필요함)
        if (isFrameCleared)
        {
            if (tries < maxTries) // Strike (1구 클리어)
            {
                // 다음 2구 필요
                if (rollIndex + 1 < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex] + rolls[rollIndex + 1];
                    dto.CurrentFrameScore = runningScore;
                }
            }
            else // Spare (2구 클리어)
            {
                // 다음 1구 필요
                if (rollIndex < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex];
                    dto.CurrentFrameScore = runningScore;
                }
            }
        }
        else if (tries == maxTries) // Open (실패)
        {
            runningScore += (MaxPins - currentPins);
            dto.CurrentFrameScore = runningScore;
        }

        frames.Add(dto);
    }

    /// <summary>
    /// 마지막 프레임 로직: 핀 클리어(Reset) 시 보너스 기회 부여
    /// </summary>
    private void ConsumeAndCalculateFinalFrame(int frameNum, IReadOnlyList<int> rolls, List<ScoreFrameDTO> frames, ref int rollIndex, ref int runningScore)
    {
        var lastFrame = new ScoreFrameDTO { FrameNumber = frameNum, Type = FrameType.Final };

        if (rollIndex >= rolls.Count)
        {
            frames.Add(lastFrame);
            return;
        }

        int currentPins = MaxPins;

        // 최대 3번의 기회 (보너스 포함)
        for (int i = 0; i < 3; i++)
        {
            if (rollIndex >= rolls.Count) break;

            int roll = rolls[rollIndex];

            // [Validation] 현재 세워진 핀 기준 검증
            if (roll > currentPins)
            {
                lastFrame.ErrorMessage = $"ERROR: Frame {frameNum} roll {i + 1} is {roll} (Remain: {currentPins}). Input ignored.";
                lastFrame.Rolls.Add(roll);
                rollIndex++;
                break;
            }

            lastFrame.Rolls.Add(roll);
            currentPins -= roll;
            rollIndex++;

            // [Reset Logic] 핀을 모두 넘겼다면 다시 세움 (보너스 진행 자격 획득)
            if (currentPins == 0)
            {
                currentPins = MaxPins;
            }
            else
            {
                // [End Logic] 2구째(i=1)인데 핀이 남았다면(오픈) 종료
                // 단, 1구가 스트라이크였다면 2구에 핀이 남아도 3구를 던질 수 있음 (X, 3, ?)
                // 따라서 '스트라이크가 아닌 상태에서' 2구 오픈이면 종료
                bool firstRollWasStrike = lastFrame.Rolls.Count > 0 && lastFrame.Rolls[0] == MaxPins;

                if (i == 1 && !firstRollWasStrike)
                {
                    break;
                }
            }
        }

        // 마지막 프레임 점수는 투구가 완전히 끝났을 때만 합산 (보너스 점수 개념 없음, 단순 합산)
        if (IsFinalFrameFinished(lastFrame.Rolls))
        {
            runningScore += lastFrame.Rolls.Sum();
            lastFrame.CurrentFrameScore = runningScore;
        }

        frames.Add(lastFrame);
    }

    private bool IsFinalFrameFinished(List<int> rolls)
    {
        if (rolls.Count == 3) return true; // 3구 던짐 -> 종료
        if (rolls.Count == 2)
        {
            // 2구 합이 10 미만(오픈)이면 종료, 아니면 미종료
            return (rolls[0] + rolls[1] < 10);
        }
        return false; // 0~1구 -> 미종료
    }
}
/// <summary>
/// 콘솔 환경에 점수판을 출력하는 뷰(View) 구현체입니다.
/// </summary>
public class ConsoleScoreRenderer : IScoreBoardRenderer
{
    private readonly int _totalFrames;

    public ConsoleScoreRenderer(int totalFrames = 10)
    {
        _totalFrames = totalFrames;
    }

    public void Render(List<ScoreFrameDTO> frames)
    {
        StringBuilder sbRoll = new StringBuilder();
        StringBuilder sbScore = new StringBuilder();

        // 설정된 총 프레임 수만큼 반복하여 빈 공간까지 포함한 전체 틀을 그립니다.
        for (int i = 0; i < _totalFrames; i++)
        {
            int frameNum = i + 1;
            string rollView = "";
            string scoreView = "";

            // 아직 생성되지 않은 미래의 프레임 타입 추론 (마지막만 Final)
            FrameType currentType = (frameNum == _totalFrames) ? FrameType.Final : FrameType.Normal;

            if (i < frames.Count)
            {
                var frame = frames[i];
                // 에러 발생 시 상세 내용은 로그로, 화면엔 "ERR"로 간략 표시
                rollView = string.IsNullOrEmpty(frame.ErrorMessage) ? GetRollView(frame) : "ERR";
                scoreView = frame.CurrentFrameScore?.ToString() ?? "";

                // 실제 데이터가 있다면 그 타입을 따름 (Fact over Guess)
                currentType = frame.Type;
            }

            // 프레임 번호 정렬을 위한 라벨 생성 ("1:" vs "10:")
            string topPrefix = $"{frameNum}:";
            // 라벨 길이만큼 공백을 채워 점수 줄의 수직 정렬을 맞춤
            string botPrefix = new string(' ', topPrefix.Length);

            // 프레임 타입에 따라 칸 너비를 동적으로 조정
            // Final: 3구("X X X") 고려 5칸 / Normal: 2구("9 /") 고려 3칸
            int width = (currentType == FrameType.Final) ? 5 : 3;

            string rollContent = $"[{rollView.PadRight(width)}]";
            string scoreContent = $"[{scoreView.PadLeft(width)}]";

            sbRoll.Append($"{topPrefix}{rollContent} ");
            sbScore.Append($"{botPrefix}{scoreContent} ");
        }

        Console.WriteLine(sbRoll.ToString());
        Console.WriteLine(sbScore.ToString());
        Console.WriteLine();
    }

    // 내부 데이터를 볼링 표기법(X, /, -)으로 변환
    private string GetRollView(ScoreFrameDTO frame)
    {
        string[] displaySymbols = new string[frame.Rolls.Count];
        for (int i = 0; i < displaySymbols.Length; i++)
        {
            displaySymbols[i] = ConvertNumSymbol(frame.Rolls[i]);
        }

        // [Logic 1] 일반적인 스페어 (1구 + 2구 = 10)
        // 단, 1구가 스트라이크면 스페어가 성립할 수 없음 (10+0=10은 스페어가 아님)
        bool isStandardSpare = frame.Rolls.Count >= 2 &&
                               frame.Rolls[0] < 10 &&
                               (frame.Rolls[0] + frame.Rolls[1] == 10);

        if (isStandardSpare)
        {
            displaySymbols[1] = "/";
        }

        // [Logic 2] 10프레임 특수 스페어 (2구 + 3구 = 10)
        // 조건: 3구가 있고, 1구가 스트라이크이며, 2구가 스트라이크가 아니고, 2+3구가 10인 경우
        // 예: X, 5, 5 -> X, 5, /
        if (frame.Type == FrameType.Final && frame.Rolls.Count == 3)
        {
            // 1구가 10이고, 2구가 10이 아닐 때 (X X X 나 X X 9 는 제외)
            if (frame.Rolls[0] == 10 && frame.Rolls[1] < 10)
            {
                if (frame.Rolls[1] + frame.Rolls[2] == 10)
                {
                    displaySymbols[2] = "/";
                }
            }
        }

        // 출력 조합 (콤마 구분)
        if (frame.Rolls.Count > 1)
            return string.Join(",", displaySymbols);

        if (frame.Rolls.Count == 1)
        {
            if (frame.Rolls[0] == 10 && frame.Type == FrameType.Normal)
            {
                return " X ";
            }
            return $"{displaySymbols[0]},";
        }

        return " ";
    }

    // 0점은 거터(-)로, 10점은 스트라이크(X)로 변환
    private string ConvertNumSymbol(int pins)
    {
        if (pins == 10) return "X";
        if (pins == 0) return "-";
        return pins.ToString();
    }
}
public class ConsoleLogger : ILogger
{
    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n*** {message} ***\n");
        Console.ResetColor();
    }

    public void LogInfo(string message)
    {
        Console.WriteLine(message);
    }
}
#endregion

#region 인터페이스 정의
// 모듈 간 결합도를 낮추기 위한 인터페이스 정의
public interface IScoreBoardRenderer
{
    void Render(List<ScoreFrameDTO> frames);
}

public interface IScoreCalculator
{
    List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls);
}

public interface ILogger
{
    void LogError(string message);
    void LogInfo(string message);
}
#endregion



#region DTO 및 지원 타입 정의
public enum FrameType
{
    Normal,
    Final
}
/// <summary>
/// 뷰와 모델 사이에서 데이터를 전달하는 DTO(Data Transfer Object)입니다.
/// 로직을 포함하지 않는 순수 데이터 컨테이너입니다.
/// </summary>
public class ScoreFrameDTO
{
    public int FrameNumber { get; set; }

    // 렌더러가 프레임 성격을 추측하지 않도록 타입을 명시 (Normal/Final)
    public FrameType Type { get; set; } = FrameType.Normal;

    // 읽기 전용으로 노출하여 데이터 무결성 보호 의도 전달
    public List<int> Rolls { get; } = new List<int>();

    public int? CurrentFrameScore { get; set; }
    public string? ErrorMessage { get; set; }

    // 디버깅 편의를 위한 문자열 변환 오버라이드
    public override string ToString()
    {
        string rollsStr = string.Join(",", Rolls);
        string scoreStr = CurrentFrameScore.HasValue ? CurrentFrameScore.Value.ToString() : "null";
        string typeStr = Type == FrameType.Final ? "Final" : "Normal";
        string status = string.IsNullOrEmpty(ErrorMessage) ? "Valid" : $"Error({ErrorMessage})";

        return $"F{FrameNumber:00} [{typeStr}] Rolls:[{rollsStr}] Score:{scoreStr} ({status})";
    }
}
#endregion