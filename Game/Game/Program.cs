//TargetFramework: net8.0

using System.Text;

class Program
{
    /*- 한 명의 플레이어가 한 판의 게임을 합니다.
- 매번 플레이어가 쓰러뜨린 핀의 숫자가 입력으로 들어옵니다.
- 매번 입력이 들어올 때마다 점수판을 출력하세요.
- 점수판에는 현재 투구까지의 기록과 각 프레임까지 확정된 총점이 보여야 합니다.*/
    public static void Main(string[] args)
    {
        var game = new Game();
        game.KnockDownPins(4);
        game.KnockDownPins(6);
        game.KnockDownPins(5);
        game.KnockDownPins(5);
        game.KnockDownPins(0);
        game.KnockDownPins(6);
        game.KnockDownPins(6);
        game.KnockDownPins(2);
        game.KnockDownPins(2);
        game.KnockDownPins(8);
        game.KnockDownPins(8);
        game.KnockDownPins(8);
        game.KnockDownPins(2);
        game.KnockDownPins(10);
        game.KnockDownPins(10);
        game.KnockDownPins(10);

        game.KnockDownPins(10);
        game.KnockDownPins(5);
        game.KnockDownPins(1);
        game.KnockDownPins(1);
        game.KnockDownPins(1);
        game.KnockDownPins(5);
        game.KnockDownPins(10);
        game.KnockDownPins(1);


        //몇개의 핀을 넘어트렸나만 정수로 입력받는다.
        //같은 메서드가 반복되는것으로 몇번 굴렸느냐가 고정인것을 알 수 있다.
    }
}





/* * [설계 결정 사항]
 * 스플릿(Split) 구현 제외:
 * 현재 입력 인터페이스(int pins)만으로는 핀의 잔여 배치를 확정할 수 없어 스플릿 판정이 불가능합니다.
 * 임의의 확률(Random)이나 가정에 의한 구현은 데이터 무결성을 해친다고 판단하여,
 * 정확한 점수 계산 로직(Strike, Spare, 10th Frame) 구현에 집중했습니다.
 * 추후 핀 상태 배열(bool[]) 입력이 지원된다면 ScoreFrameDTO에 IsSplit 속성을 추가하여 확장 가능합니다.
 */
public class Game
{

    private readonly List<int> _rolls = new List<int>();

    // 핵심 부품 2개
    private readonly IScoreCalculator _calculator;
    private readonly IScoreBoardRenderer _renderer;

    private readonly ILogger _logger;

    //렌더 이슈때 재계산 하지 않도록 스냅샷 보관
    private List<ScoreFrameDTO> _lastCalculatedFrames = new List<ScoreFrameDTO>();

    // 1. Main(고정)용 생성자: 표준 부품 조립 (Constructor Chaining)
    public Game() : this(new StandardScoreCalculator(), new ConsoleScoreRenderer(), new ConsoleLogger())
    {
    }

    // 2. 테스트/확장용 생성자: 부품 교체 가능 (의존성 주입)
    // 예: 나중에 'NoGutterModeCalculator' 같은 걸 넣을 수 있음
    public Game(IScoreCalculator calculator, IScoreBoardRenderer renderer, ILogger logger)
    {
        _calculator = calculator;
        _renderer = renderer;
        _logger = logger;
    }

    public void KnockDownPins(int pins)
    {
        // 1. 기본 유효성 검사
        if (pins < 0 || pins > 10)
        {
            _logger.LogError($"[Input Error] Pins must be between 0 and 10. (Input: {pins})");
            return;
        }

        // 2. 가상 실행 (Simulation/Dry-run)
        var tempRolls = new List<int>(_rolls) { pins };

        // 3. 결과 검증 (Validation)
        var tempFrames = _calculator.Calculate(tempRolls);
        var errorFrame = tempFrames.FirstOrDefault(f => !string.IsNullOrEmpty(f.ErrorMessage));

        if (errorFrame != null)
        {
            // [Change] Console.WriteLine -> _logger.LogError 사용
            // Game 클래스는 구체적인 출력 방식(Console)을 몰라도 됩니다.
            _logger.LogError(errorFrame.ErrorMessage);
            return;
        }

        // 4. 커밋 (Commit) & 렌더링
        _rolls.Add(pins);
        _lastCalculatedFrames = tempFrames;
        _renderer.Render(tempFrames);

        //계산기와 렌더가 데이터 커플링처럼 보이지만, 중간 객체는 중립적이므로, 서로 완전히 독립적이다. 중간 객체 포맷이 달라질 경우 둘 다 바꿔야 할 수 있음.
        //하지만 중간 계약이 바뀌는 경우는 거의 없으므로, 실질적으로는 독립적이라고 볼 수 있다.
        //필요할경우 맵퍼를 하나 더 둬서 중간 객체 포맷을 변환해줄 수도 있다.


    }
}

// 실제 콘솔 출력 구현체
public class ConsoleScoreRenderer : IScoreBoardRenderer
{
    //출력 예시를 보았을때, 10프레임까지 항상 공간을 확보하고, 마지막 10프레임의 크기가 정해져 있다는것을 중점으로 판단한다.
    private const int MaxFrames = 10;
    public void Render(List<ScoreFrameDTO> frames)
    {
        StringBuilder sbRoll = new StringBuilder();
        StringBuilder sbScore = new StringBuilder();

        // [UX Improvement] 게임 진행 상황과 관계없이 항상 10개 프레임 공간을 확보
        for (int i = 0; i < MaxFrames; i++)
        {
            // 1. 데이터 준비
            int frameNum = i + 1;
            string rollView = "";
            string scoreView = "";

            if (i < frames.Count)
            {
                var frame = frames[i];
                // 에러 메시지가 있으면 "ERR" 표시, 아니면 정상 출력
                rollView = string.IsNullOrEmpty(frame.ErrorMessage) ? GetRollView(frame) : frame.ErrorMessage;
                scoreView = frame.CurrentFrameScore?.ToString() ?? "";
                //에러 메시지가 있으면 점수는 표시하지 않는다. 아예 무시하고 다음 프레임으로 넘어간다.
            }

            // 2. 포맷팅 (요청하신 1:[] 스타일 적용)
            // Roll 줄: "1:[...]"
            // Score 줄: "  [...]" (프레임 번호 길이 + 1 만큼 공백을 줘서 [] 위치를 맞춤)

            string topPrefix = $"{frameNum}:";
            // topPrefix 길이만큼 공백 생성 ("1:" -> "  ", "10:" -> "   ")
            string botPrefix = new string(' ', topPrefix.Length);

            // 내부 내용물 너비 고정 (10프레임 "X X X" 고려하여 5칸 확보)
            // Roll은 왼쪽 정렬(X    ), Score는 오른쪽 정렬(   30)이 보기에 좋음
            string rollContent;
            string scoreContent;
            if (frameNum == 10)
            {
                rollContent = $"[{rollView.PadRight(5)}]";
                scoreContent = $"[{scoreView.PadLeft(5)}]";
            }
            else
            {
                rollContent = $"[{rollView.PadRight(3)}]";
                scoreContent = $"[{scoreView.PadLeft(3)}]";
            }




            sbRoll.Append($"{topPrefix}{rollContent} ");
            sbScore.Append($"{botPrefix}{scoreContent} ");
        }

        Console.WriteLine("===============================================================================");
        Console.WriteLine(sbRoll.ToString());
        Console.WriteLine(sbScore.ToString());
        Console.WriteLine("===============================================================================");
    }

    private string GetRollView(ScoreFrameDTO frame)
    {
        // 숫자 -> 문자 변환 (X, -, 숫자)
        string[] rollsTmp = new string[frame.Rolls.Count];
        for (int i = 0; i < rollsTmp.Length; i++)
        {
            rollsTmp[i] = ConvertNumSymbol(frame.Rolls[i]);
        }

        // 스페어 확인 및 덮어쓰기.
        bool isSpare = frame.Rolls.Count >= 2 && (frame.Rolls[0] + frame.Rolls[1] == 10);

        if (isSpare)
        {
            rollsTmp[1] = "/";
        }

        // 일반 출력 (콤마 구분)
        if (frame.Rolls.Count > 1)
            return string.Join(",", rollsTmp);

        if (frame.Rolls.Count == 1)
        {
            if (frame.Rolls[0]==10&&frame.FrameNumber!=10)//스트라이크이고 일반프레임이면 가운데
            {
                return " X ";
            }
            // 투구가 하나만 있을 때 뒤에 콤마를 붙일지 여부 (예: "8,")
            return $"{rollsTmp[0]},";
        }
        

        return " ";
    }

    // [New Helper] 누락되었던 심볼 변환 메서드 추가
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
    // 유지보수를 위해 매직 넘버를 상수로 관리
    private const int MaxFrames = 10;
    private const int MaxPins = 10;

    public List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls)
    {
        var frames = new List<ScoreFrameDTO>();
        int rollIndex = 0;
        int runningScore = 0;

        // [Phase 1] 1~9 프레임 처리
        for (int frameNum = 1; frameNum < MaxFrames; frameNum++)
        {
            var dto = new ScoreFrameDTO { FrameNumber = frameNum };

            if (rollIndex >= rolls.Count)
            {
                frames.Add(dto);
                continue;
            }

            // [New Strategy] 남은 핀(RemainPins)을 추적하여 로직 단순화
            int remainPins = MaxPins;
            int firstRoll = rolls[rollIndex];

            // 1. 첫 번째 투구 처리
            if (firstRoll > remainPins) // 에러: 10개보다 많이 쓰러뜨림
            {
                dto.Rolls.Add(firstRoll);
                dto.ErrorMessage = $"ERROR: Frame {frameNum} roll is {firstRoll}. Input ignored.";
                frames.Add(dto);
                rollIndex++; // 에러 데이터 소비
                continue;
            }

            dto.Rolls.Add(firstRoll);
            remainPins -= firstRoll;

            // Case A: 스트라이크 (첫 투구에 핀 0개 됨)
            if (remainPins == 0)
            {
                // 스트라이크 점수 계산 (다음 2개 공 필요)
                if (rollIndex + 2 < rolls.Count)
                {
                    runningScore += 10 + GetNextTwoRollsScore(rolls, rollIndex);
                    dto.CurrentFrameScore = runningScore;
                }
                rollIndex++; // 스트라이크는 공 1개 소비
            }
            // Case B: 일반 투구 (핀이 남았으므로 2번째 공 필요)
            else
            {
                // 데이터 대기 (2구 없음)
                if (rollIndex + 1 >= rolls.Count)
                {
                    // 현재 상태로 프레임 저장하고 대기
                    frames.Add(dto);
                    rollIndex++;
                    break;
                }

                int secondRoll = rolls[rollIndex + 1];

                // 에러 체크: 남은 핀보다 많이 쓰러뜨림 (합계 > 10)
                if (secondRoll > remainPins)
                {
                    dto.Rolls.Add(secondRoll);
                    dto.ErrorMessage = $"ERROR: Frame {frameNum} sum is {firstRoll + secondRoll}. Input ignored.";
                    frames.Add(dto);
                    rollIndex += 2; // 에러 데이터 소비
                    continue;
                }

                dto.Rolls.Add(secondRoll);
                remainPins -= secondRoll;

                // 점수 계산
                if (remainPins == 0) // 스페어 (2구 합쳐서 0개 됨)
                {
                    if (rollIndex + 2 < rolls.Count) // 보너스 공(1개) 필요
                    {
                        runningScore += 10 + GetNextRollScore(rolls, rollIndex + 1);
                        dto.CurrentFrameScore = runningScore;
                    }
                }
                else // 오픈 (핀이 남음)
                {
                    runningScore += (firstRoll + secondRoll);
                    dto.CurrentFrameScore = runningScore;
                }

                rollIndex += 2; // 일반 프레임은 공 2개 소비
            }

            frames.Add(dto);
        }

        // [Phase 2] 10프레임 처리 (핀 리셋 로직 적용)
        if (rollIndex < rolls.Count)
        {
            var lastFrame = new ScoreFrameDTO { FrameNumber = MaxFrames };

            // 10프레임은 핀 상태를 계속 추적해야 함 (스트라이크/스페어 시 리셋)
            int currentPins = MaxPins;

            // 최대 3번의 기회
            for (int i = 0; i < 3; i++)
            {
                // 데이터 없으면 중단
                if (rollIndex >= rolls.Count) break;

                int roll = rolls[rollIndex];

                // 에러 체크: 현재 세워진 핀보다 많이 쓰러뜨릴 수 없음
                if (roll > currentPins)
                {
                    lastFrame.ErrorMessage = $"ERROR: Frame 10 roll {i + 1} is {roll} (Remain: {currentPins}). Input ignored.";
                    lastFrame.Rolls.Add(roll); // 에러 데이터도 보여주기 위해 추가
                    rollIndex++; // 소비 후 중단
                    break;
                }

                lastFrame.Rolls.Add(roll);
                currentPins -= roll;
                rollIndex++;

                // [Core Logic] 핀이 다 쓰러졌으면(0), 핀을 다시 세운다(Reset) -> 이것이 보너스 기회의 근거
                // 3번째 투구 직전이라면, 이 리셋이 3구를 던질 수 있게 해주는 논리적 근거가 됨
                if (currentPins == 0)
                {
                    currentPins = MaxPins;
                }
                else
                {
                    // 핀이 남았는데, 2번째 투구였다면? -> 게임 종료 (오픈)
                    // (즉, i==1(2구) 시점에 핀이 0이 아니면 루프 종료)
                    if (i == 1) break;
                }
            }

            // 점수 계산 (투구 완료 여부 확인)
            if (IsFinalFrameFinished(lastFrame.Rolls))
            {
                runningScore += lastFrame.Rolls.Sum();
                lastFrame.CurrentFrameScore = runningScore;
            }

            frames.Add(lastFrame);
        }

        return frames;
    }
    // --- Helper Methods (규칙이 바뀌면 여기만 고치면 됩니다) ---

    private int GetNextTwoRollsScore(IReadOnlyList<int> rolls, int index)
    {
        return rolls[index + 1] + rolls[index + 2];
    }

    private int GetNextRollScore(IReadOnlyList<int> rolls, int currentIndex)
    {
        return rolls[currentIndex + 1];
    }

    // 10프레임 종료 여부 판단
    private bool IsFinalFrameFinished(List<int> rolls)
    {
        if (rolls.Count == 3) return true; // 3구 던졌으면 끝
        if (rolls.Count == 2)
        {
            // 2구 합이 10 미만(오픈)이면 끝, 10 이상(스트라이크/스페어)이면 3구 필요
            return (rolls[0] + rolls[1] < 10);
        }
        return false; // 0~1구는 미종료
    }
}
//각 프레임을 구분짓기
public class ScoreFrameDTO
{
    public int FrameNumber { get; set; }
    public List<int> Rolls { get; } = new List<int>();
    public int? CurrentFrameScore { get; set; }
    public string? ErrorMessage { get; set; }
}

// [전략 1] 출력 담당
public interface IScoreBoardRenderer
{
    void Render(List<ScoreFrameDTO> frames);
}

// [전략 2] 계산 담당 (NEW)
// 입력: 투구 기록 리스트 -> 출력: 점수판 데이터
public interface IScoreCalculator
{
    List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls);
}
// [전략 3] 로깅 담당 (NEW - Reintroduced for Game Controller)
public interface ILogger
{
    void LogError(string message);
    void LogInfo(string message); // 필요 시 일반 메시지도 출력 가능
}

// 로거 구현체 (콘솔용)
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
public static class StringExtensions
{
    public static string Center(this string text, int totalWidth, char paddingChar = ' ')
    {
        if (string.IsNullOrEmpty(text))
            return new string(paddingChar, totalWidth);

        int length = text.Length;
        if (length >= totalWidth)
            return text; // 공간이 부족하면 원본 반환

        // 왼쪽 여백 = (전체 폭 - 글자 길이) / 2
        int leftPadding = (totalWidth - length) / 2;
        int rightPadding = totalWidth - length - leftPadding;

        return new string(paddingChar, leftPadding) + text + new string(paddingChar, rightPadding);
    }
}
