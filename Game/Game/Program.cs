//TargetFramework: net8.0

using System.Text;

class Program
{
    public static void Main(string[] args)
    {
        // 10프레임 게임 생성 (생성자에 숫자를 바꿔 12프레임 게임 등도 가능)
        var game = new Game(totalFrames: 10);

        // 테스트 데이터 입력
        int[] inputRolls = {
            4, 6, // 1F (Spare)
            5, 5, // 2F (Spare)
            0, 6, // 3F (Open)
            6, 6, // 4F (Error - 합 12) -> 로그 출력 및 무시됨
            2, 2, // 4F (Open - 위 에러 후 정상 입력)
            8, 8, // 5F (Error - 합 16) -> 무시됨
            8, 2, // 5F (Spare)
            10,   // 6F (Strike)
            10,   // 7F (Strike)
            10,   // 8F (Strike)
            10, 5, 5 // 9F (Strike), 10F 시작... (데이터 예시가 조금 섞여있어 정리함)
        };

        // 실제 실행 루프
        foreach (var pin in inputRolls)
        {
            game.KnockDownPins(pin);
        }

        // 추가 테스트 (10프레임 마무리)
        game.KnockDownPins(10); // 10F 1구 (X)
        game.KnockDownPins(10); // 10F 2구 (X)
        game.KnockDownPins(10); // 10F 3구 (X)
    }
}

/// <summary>
/// 게임의 전체 흐름을 관리하는 컨트롤러 클래스입니다.
/// </summary>
public class Game
{
    // 실제 확정된 투구 기록 저장소
    private readonly List<int> _rolls = new List<int>();

    // 핵심 의존성 객체들 (계산기, 렌더러, 로거)
    private readonly IScoreCalculator _calculator;
    private readonly IScoreBoardRenderer _renderer;
    private readonly ILogger _logger;

    // 화면 갱신 최적화를 위한 마지막 계산 결과 캐시
    private List<ScoreFrameDTO> _lastCalculatedFrames = new List<ScoreFrameDTO>();

    // 게임 설정값 (총 프레임 수)
    private readonly int _totalFrames;

    // 기본 생성자 (10프레임 표준 게임)
    public Game(int totalFrames = 10)
    {
        _totalFrames = totalFrames;
        _logger = new ConsoleLogger();

        // 설정값(프레임 수)을 각 모듈에 전파하여 동기화
        _calculator = new StandardScoreCalculator(_totalFrames);
        _renderer = new ConsoleScoreRenderer(_totalFrames);
    }

    // 테스트 및 확장을 위한 의존성 주입 생성자
    public Game(IScoreCalculator calculator, IScoreBoardRenderer renderer, ILogger logger)
    {
        _calculator = calculator;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>
    /// 사용자가 핀을 쓰러뜨렸을 때 호출되는 메인 메서드입니다.
    /// [가상 실행 패턴]을 사용하여 데이터 무결성을 보장합니다.
    /// </summary>
    public void KnockDownPins(int pins)
    {
        // [Refactoring] 입력값 범위 체크(0~10)를 여기서 하지 않고 계산기에게 위임합니다.
        // 이유는 도메인 규칙(핀의 개수)은 계산기가 가장 잘 알기 때문입니다.

        // 1. 가상 실행 (Simulation/Dry-run)
        // 입력값을 임시 리스트에 넣어 계산을 먼저 시도합니다.
        var tempRolls = new List<int>(_rolls) { pins };

        // 계산기 내부에서 유효성 검사가 수행됩니다.
        var tempFrames = _calculator.Calculate(tempRolls);

        // 2. 결과 검증 (Validation)
        var errorFrame = tempFrames.FirstOrDefault(f => !string.IsNullOrEmpty(f.ErrorMessage));

        if (errorFrame != null)
        {
            _logger.LogError(errorFrame.ErrorMessage);
            return; // 에러 발생 시 저장하지 않고 리턴 (Rollback)
        }

        // 3. 커밋 (Commit) & 렌더링
        _rolls.Add(pins);
        Console.WriteLine($"[Info] Knocked down {pins} pins. Total rolls: {_rolls.Count}");
        _lastCalculatedFrames = tempFrames;
        _renderer.Render(tempFrames);
    }
}

/// <summary>
/// 콘솔 창에 점수판을 그리는 렌더러 구현체입니다.
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

        // 설정된 프레임 수만큼 반복하며 UI를 구성합니다.
        for (int i = 0; i < _totalFrames; i++)
        {
            int frameNum = i + 1;
            string rollView = "";
            string scoreView = "";

            // 현재 프레임 타입 결정 (기본값: 일반, 마지막 프레임: 파이널)
            // 데이터가 아직 없어도 UI 틀을 잡기 위해 타입을 추론합니다.
            FrameType currentType = (frameNum == _totalFrames) ? FrameType.Final : FrameType.Normal;

            if (i < frames.Count)
            {
                var frame = frames[i];
                // 에러 메시지가 있으면 "ERR" 표시, 아니면 정상 점수 표시
                rollView = string.IsNullOrEmpty(frame.ErrorMessage) ? GetRollView(frame) : "ERR"; // 에러 메시지가 길면 UI가 깨지므로 "ERR"로 축약
                scoreView = frame.CurrentFrameScore?.ToString() ?? "";

                // DTO에 타입 정보가 있다면 그것을 따릅니다.
                currentType = frame.Type;
            }

            // UI 포맷팅 (1:[] 스타일)
            string headerLabel = $"{frameNum}:";
            string padding = new string(' ', headerLabel.Length);

            string rollContent;
            string scoreContent;

            // 프레임 타입에 따라 칸 너비를 조정합니다.
            int width = (currentType == FrameType.Final) ? 5 : 3;

            rollContent = $"[{rollView.PadRight(width)}]";
            scoreContent = $"[{scoreView.PadLeft(width)}]";

            sbRoll.Append($"{headerLabel}{rollContent} ");
            sbScore.Append($"{padding}{scoreContent} ");
        }

        Console.WriteLine("===============================================================================");
        Console.WriteLine(sbRoll.ToString());
        Console.WriteLine(sbScore.ToString());
        Console.WriteLine("===============================================================================");
    }

    // 투구 점수(숫자)를 볼링 기호(X, /, -)로 변환하여 문자열로 반환합니다.
    private string GetRollView(ScoreFrameDTO frame)
    {
        // [Refactoring] 변수명 명확화: rollsTmp -> displaySymbols
        string[] displaySymbols = new string[frame.Rolls.Count];
        for (int i = 0; i < displaySymbols.Length; i++)
        {
            displaySymbols[i] = ConvertNumSymbol(frame.Rolls[i]);
        }

        // 스페어 처리: 두 번째 투구로 10개를 채웠다면 '/'로 덮어쓰기
        bool isSpare = frame.Rolls.Count >= 2 && (frame.Rolls[0] + frame.Rolls[1] == 10);
        if (isSpare)
        {
            displaySymbols[1] = "/";
        }

        // 출력 조합
        if (frame.Rolls.Count > 1)
            return string.Join(",", displaySymbols);

        if (frame.Rolls.Count == 1)
        {
            // 일반 프레임 스트라이크는 가운데 정렬하여 강조
            if (frame.Rolls[0] == 10 && frame.Type == FrameType.Normal)
            {
                return " X ";
            }
            // 투구가 하나만 있을 때(진행 중)는 콤마를 붙여 대기 상태 표현
            return $"{displaySymbols[0]},";
        }

        return " ";
    }

    private string ConvertNumSymbol(int pins)
    {
        if (pins == 10) return "X";
        if (pins == 0) return "-";
        return pins.ToString();
    }
}

/// <summary>
/// 표준 볼링 규칙을 따르는 점수 계산기입니다.
/// </summary>
public class StandardScoreCalculator : IScoreCalculator
{
    private readonly int _totalFrames;
    private const int MaxPins = 10;

    public StandardScoreCalculator(int totalFrames = 10)
    {
        _totalFrames = totalFrames;
    }

    public List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls)
    {
        var frames = new List<ScoreFrameDTO>();

        // [Validation] 입력값 검증 (Guard Clause)
        // 코드를 읽는 흐름을 방해하지 않도록 별도 메서드로 분리했습니다.
        var errorFrame = ValidateInputRange(rolls);
        if (errorFrame != null)
        {
            frames.Add(errorFrame);
            return frames;
        }

        int rollIndex = 0;
        int runningScore = 0;

        for (int frameNum = 1; frameNum < _totalFrames; frameNum++)
        {
            ProcessNormalFrame(frameNum, rolls, frames, ref rollIndex, ref runningScore);
        }

        ProcessFinalFrame(_totalFrames, rolls, frames, ref rollIndex, ref runningScore);


        errorFrame = ValidateExtra(rollIndex, rolls);
        if (errorFrame != null)
        {
            frames.Add(errorFrame);
        }

        return frames;
    }
    // [New Helper] 입력값 범위 검증 메서드
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
        return null; // 정상
    }
    private ScoreFrameDTO? ValidateExtra(int rollIndex, IReadOnlyList<int> rolls)
    {
        if (rollIndex < rolls.Count)
        {
            var extraFrame = new ScoreFrameDTO
            {
                FrameNumber = _totalFrames + 1,
                ErrorMessage = "[System Error] Extra rolls detected beyond the final frame."
            };
            return extraFrame;
        }
        return null; // 정상
    }
    /// <summary>
    /// 일반 프레임(Normal Frame) 하나를 처리합니다.
    /// 규칙: 2번의 기회(Try) 안에 핀을 모두 넘겨야 합니다.
    /// </summary>
    private void ProcessNormalFrame(int frameNum, IReadOnlyList<int> rolls, List<ScoreFrameDTO> frames, ref int rollIndex, ref int runningScore)
    {
        var dto = new ScoreFrameDTO { FrameNumber = frameNum, Type = FrameType.Normal };

        // 데이터 부족 시 빈 프레임 추가 후 종료
        if (rollIndex >= rolls.Count)
        {
            frames.Add(dto);
            return;
        }

        int currentPins = MaxPins;
        int maxTries = 2; // [Naming] maxThrows -> maxTries (기회 횟수 강조)
        int tries = 0;
        bool isFrameCleared = false;

        // [Core Logic] 주어진 기회만큼 반복하며 투구
        while (tries < maxTries)
        {
            if (rollIndex >= rolls.Count) break;

            int roll = rolls[rollIndex];

            // 에러 체크: 남은 핀보다 많이 쓰러뜨릴 수 없음
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

            // 핀을 모두 넘겼다면 (Clean) 즉시 종료 (스트라이크 또는 스페어)
            if (currentPins == 0)
            {
                isFrameCleared = true;
                break;
            }
        }

        // 에러가 있다면 저장 후 로직 종료
        if (!string.IsNullOrEmpty(dto.ErrorMessage))
        {
            frames.Add(dto);
            return;
        }

        // 점수 계산 (Strike, Spare, Open)
        if (isFrameCleared)
        {
            if (tries < maxTries) // Strike: 기회가 남았는데 다 넘김 (효율적 승리)
            {
                // 스트라이크는 다음 2개의 공 점수가 필요함
                if (rollIndex + 1 < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex] + rolls[rollIndex + 1];
                    dto.CurrentFrameScore = runningScore;
                }
            }
            else // Spare: 기회를 다 써서 다 넘김
            {
                // 스페어는 다음 1개의 공 점수가 필요함
                if (rollIndex < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex];
                    dto.CurrentFrameScore = runningScore;
                }
            }
        }
        else if (tries == maxTries) // Open: 기회를 다 썼는데 핀이 남음
        {
            runningScore += (MaxPins - currentPins);
            dto.CurrentFrameScore = runningScore;
        }
        // else: Waiting (기회가 남았고 데이터도 없음)

        frames.Add(dto);
    }

    /// <summary>
    /// 마지막 프레임(Final Frame) 하나를 처리합니다.
    /// 규칙: 핀을 모두 넘기면(Reset) 추가 기회를 얻습니다.
    /// </summary>
    private void ProcessFinalFrame(int frameNum, IReadOnlyList<int> rolls, List<ScoreFrameDTO> frames, ref int rollIndex, ref int runningScore)
    {
        if(rolls.Count>1)
        Console.WriteLine($"[Debug] {rolls[^1]} / {rolls[^2]}");
        var lastFrame = new ScoreFrameDTO { FrameNumber = frameNum, Type = FrameType.Final };

        // 데이터 부족 시 빈 프레임 추가 후 종료
        if (rollIndex >= rolls.Count)
        {
            frames.Add(lastFrame);
            return;
        }

        int currentPins = MaxPins;

        // 마지막 프레임은 최대 3번까지 던질 수 있음
        for (int i = 0; i < 3; i++)
        {
            if (rollIndex >= rolls.Count) break;//기록이 없으면 중단

            int roll = rolls[rollIndex];

            // 에러 체크
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

            // [Reset Logic] 핀을 모두 넘겼다면 핀을 다시 세움 (보너스 기회 자격 획득)
            if (currentPins == 0)
            {
                currentPins = MaxPins;
            }
            else
            {
                // [Bug Fix] 2번째 투구(i=1)에서 핀이 남았을 때 종료하는 조건
                // 단순히 '핀이 남았으면 종료'가 아니라, '1구가 스트라이크가 아닌데 핀이 남았으면' 종료해야 함.
                // 1구 스트라이크(10) -> 2구(3) -> 핀 남음(7) -> 하지만 3구 기회 있음 (스트라이크 보너스)
                // 1구 오픈(3) -> 2구(5) -> 핀 남음(2) -> 종료 (오픈)

                // lastFrame.Rolls[0]은 1번째 투구 점수
                bool firstRollWasStrike = lastFrame.Rolls.Count > 0 && lastFrame.Rolls[0] == MaxPins;

                if (i == 1 && !firstRollWasStrike)
                {
                    break;
                }
            }
        }

        // 점수 확정 여부 확인 후 계산
        if (IsFinalFrameFinished(lastFrame.Rolls))
        {
            runningScore += lastFrame.Rolls.Sum();
            lastFrame.CurrentFrameScore = runningScore;
        }
        else
        {
            Console.WriteLine($"[Info] Frame {frameNum} is not yet finished. Current rolls: {string.Join(",", lastFrame.Rolls)}");
        }

        frames.Add(lastFrame);
    }

    private bool IsFinalFrameFinished(List<int> rolls)
    {
        if (rolls.Count == 3) return true; // 3구 던졌으면 끝
        if (rolls.Count == 2)
        {
            // 2구 합이 10 미만(오픈)이면 끝, 10 이상(스페어, 스트라이크)이면 미종료(3구 필요)
            return (rolls[0] + rolls[1] < 10);
        }
        return false; // 0~1구는 미종료
    }
}

public enum FrameType
{
    Normal,
    Final
}

public class ScoreFrameDTO
{
    public int FrameNumber { get; set; }
    public FrameType Type { get; set; } = FrameType.Normal;
    public List<int> Rolls { get; } = new List<int>();
    public int? CurrentFrameScore { get; set; }
    public string? ErrorMessage { get; set; }
}

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