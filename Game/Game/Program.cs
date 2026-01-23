//TargetFramework: net8.0

using System.Text;

class Program
{
    public static void Main(string[] args)
    {
        var game = new Game();

        // 테스트: 정상 입력
        game.KnockDownPins(4);
        game.KnockDownPins(6);
        game.KnockDownPins(5);
        game.KnockDownPins(5);
        game.KnockDownPins(10);
        game.KnockDownPins(6);
    }
}

public class Game
{
    private readonly List<int> _rolls = new List<int>();
    private readonly IScoreCalculator _calculator;
    private readonly IScoreBoardRenderer _renderer;
    private readonly ILogger _logger;

    private List<ScoreFrameDTO> _lastCalculatedFrames = new List<ScoreFrameDTO>();
    private readonly int _totalFrames;

    public Game(int totalFrames = 10)
    {
        _totalFrames = totalFrames;
        _logger = new ConsoleLogger();
        _calculator = new StandardScoreCalculator(_totalFrames);
        _renderer = new ConsoleScoreRenderer(_totalFrames);
    }

    public Game(IScoreCalculator calculator, IScoreBoardRenderer renderer, ILogger logger)
    {
        _calculator = calculator;
        _renderer = renderer;
        _logger = logger;
    }

    public void KnockDownPins(int pins)
    {
        // 1. 가상 실행 (Simulation/Dry-run)
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
        _lastCalculatedFrames = tempFrames;
        _renderer.Render(tempFrames);
    }
}

public class ScoreFrameDTO
{
    public int FrameNumber { get; set; }
    public FrameType Type { get; set; } = FrameType.Normal;
    public List<int> Rolls { get; } = new List<int>();
    public int? CurrentFrameScore { get; set; }
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        string rollsStr = string.Join(",", Rolls);
        string scoreStr = CurrentFrameScore.HasValue ? CurrentFrameScore.Value.ToString() : "null";
        string typeStr = Type == FrameType.Final ? "Final" : "Normal";
        string status = string.IsNullOrEmpty(ErrorMessage) ? "Valid" : $"Error({ErrorMessage})";

        return $"F{FrameNumber:00} [{typeStr}] Rolls:[{rollsStr}] Score:{scoreStr} ({status})";
    }
}

// 실제 콘솔 출력 구현체
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

        for (int i = 0; i < _totalFrames; i++)
        {
            int frameNum = i + 1;
            string rollView = "";
            string scoreView = "";
            FrameType currentType = (frameNum == _totalFrames) ? FrameType.Final : FrameType.Normal;

            if (i < frames.Count)
            {
                var frame = frames[i];
                rollView = string.IsNullOrEmpty(frame.ErrorMessage) ? GetRollView(frame) : "ERR";
                scoreView = frame.CurrentFrameScore?.ToString() ?? "";
                currentType = frame.Type;
            }

            string topPrefix = $"{frameNum}:";
            string botPrefix = new string(' ', topPrefix.Length);

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

    private string GetRollView(ScoreFrameDTO frame)
    {
        string[] displaySymbols = new string[frame.Rolls.Count];
        for (int i = 0; i < displaySymbols.Length; i++)
        {
            displaySymbols[i] = ConvertNumSymbol(frame.Rolls[i]);
        }

        bool isSpare = frame.Rolls.Count >= 2 && (frame.Rolls[0] + frame.Rolls[1] == 10);

        if (isSpare)
        {
            displaySymbols[1] = "/";
        }

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

    private string ConvertNumSymbol(int pins)
    {
        if (pins == 10) return "X";
        if (pins == 0) return "-";
        return pins.ToString();
    }
}

// 표준 볼링 점수 계산기
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
            // [Rename] Process -> ConsumeAndCalculate
            // 메서드가 rollIndex(데이터 소비 위치)를 변경한다는 것을 명시적으로 표현합니다.
            ConsumeAndCalculateNormalFrame(frameNum, rolls, frames, ref rollIndex, ref runningScore);
        }

        ConsumeAndCalculateFinalFrame(_totalFrames, rolls, frames, ref rollIndex, ref runningScore);


        var extraError = ValidateExtra(rollIndex, rolls);
        if (extraError != null)
        {
            frames.Add(extraError);
        }

        return frames;
    }

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
        return null;
    }

    /// <summary>
    /// 일반 프레임을 처리합니다. 
    /// 주의: 이 메서드는 rolls 리스트에서 데이터를 소비하며, ref rollIndex를 증가시킵니다.
    /// </summary>
    private void ConsumeAndCalculateNormalFrame(int frameNum, IReadOnlyList<int> rolls, List<ScoreFrameDTO> frames, ref int rollIndex, ref int runningScore)
    {
        var dto = new ScoreFrameDTO { FrameNumber = frameNum, Type = FrameType.Normal };

        if (rollIndex >= rolls.Count)
        {
            frames.Add(dto);
            return;
        }

        int currentPins = MaxPins;
        int maxTries = 2;
        int tries = 0;
        bool isFrameCleared = false;

        while (tries < maxTries)
        {
            if (rollIndex >= rolls.Count) break;

            int roll = rolls[rollIndex];

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

        if (isFrameCleared)
        {
            if (tries < maxTries) // Strike
            {
                if (rollIndex + 1 < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex] + rolls[rollIndex + 1];
                    dto.CurrentFrameScore = runningScore;
                }
            }
            else // Spare
            {
                if (rollIndex < rolls.Count)
                {
                    runningScore += 10 + rolls[rollIndex];
                    dto.CurrentFrameScore = runningScore;
                }
            }
        }
        else if (tries == maxTries) // Open
        {
            runningScore += (MaxPins - currentPins);
            dto.CurrentFrameScore = runningScore;
        }

        frames.Add(dto);
    }

    /// <summary>
    /// 마지막 프레임을 처리합니다.
    /// 주의: 이 메서드는 rolls 리스트에서 데이터를 소비하며, ref rollIndex를 증가시킵니다.
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

        for (int i = 0; i < 3; i++)
        {
            if (rollIndex >= rolls.Count) break;

            int roll = rolls[rollIndex];

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

            if (currentPins == 0)
            {
                currentPins = MaxPins;
            }
            else
            {
                bool firstRollWasStrike = lastFrame.Rolls.Count > 0 && lastFrame.Rolls[0] == MaxPins;

                if (i == 1 && !firstRollWasStrike)
                {
                    break;
                }
            }
        }

        if (IsFinalFrameFinished(lastFrame.Rolls))
        {
            runningScore += lastFrame.Rolls.Sum();
            lastFrame.CurrentFrameScore = runningScore;
        }

        frames.Add(lastFrame);
    }

    private bool IsFinalFrameFinished(List<int> rolls)
    {
        if (rolls.Count == 3) return true;
        if (rolls.Count == 2)
        {
            return (rolls[0] + rolls[1] < 10);
        }
        return false;
    }
}

public enum FrameType
{
    Normal,
    Final
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