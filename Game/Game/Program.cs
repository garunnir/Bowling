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
        var game=new Game();
        game.KnockDownPins(4);
        game.KnockDownPins(6);
        game.KnockDownPins(5);
        game.KnockDownPins(5);
        game.KnockDownPins(11);
        game.KnockDownPins(6);
        game.KnockDownPins(6);
        game.KnockDownPins(2);
        game.KnockDownPins(2);
        game.KnockDownPins(10);
        game.KnockDownPins(10);
        game.KnockDownPins(10);
        game.KnockDownPins(10);

        game.KnockDownPins(5);
        game.KnockDownPins(5);

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
            if (frameNum==10)
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
        // 10프레임 예외 처리: 스트라이크가 나와도 계속 던질 수 있으므로 다 보여줌
        if (frame.FrameNumber == 10)
        {
            //frame.Rolls의 각 투구 결과를 변환
            return string.Join(",", frame.Rolls.ConvertAll(roll =>
            {
                if (roll == 10) return "X";
                return roll.ToString();
            }));
        }

        // [View Logic]
        // 1. 스트라이크 확인
        bool isStrike = frame.Rolls.Count >= 1 && frame.Rolls[0] == 10;
        if (isStrike) return " X ";

        // 2. 스페어 확인
        bool isSpare = frame.Rolls.Count == 2 && (frame.Rolls[0] + frame.Rolls[1] == 10);
        if (isSpare) return $"{frame.Rolls[0]},/";

        // 3. 일반 투구
        if (frame.Rolls.Count == 2) return $"{frame.Rolls[0]},{frame.Rolls[1]}";
        if (frame.Rolls.Count == 1) return $"{frame.Rolls[0]}";

        return " ";
    }
}
// 표준 볼링 점수 계산기
public class StandardScoreCalculator : IScoreCalculator
{
    // 유지보수를 위해 매직 넘버를 상수로 관리
    private const int MaxFrames = 10;
    private const int MaxPins = 10;

    public List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls)//받은 정보를 변경해서 안된다는 약속을 명확히 하기 위해 IReadOnlyList를 사용
    {
        //rolls는 각 프레임에 규칙에 따라 배분되어야 하므로 프레임넘버에 따라서 들어가지 않는다.
        var frames = new List<ScoreFrameDTO>();
        int rollIndex = 0;
        int runningScore = 0; // 누적 점수

        for (int frameNum = 1; frameNum <= MaxFrames; frameNum++)//프레임은 10까지 이지만 변경 가능도 염두에 두자 이 프레임으로 의도를 보기 좋게 표현한 AI의 방식은 채용하였음.
        {
            // 더 이상 계산할 투구 기록이 없으면 중단
            if (rollIndex >= rolls.Count) break;

            var dto = new ScoreFrameDTO { FrameNumber = frameNum };//프레임 객체 생성 및 독립적 아이덴티티를 확보.

            if (IsStrike(rolls, rollIndex))
            {
                // [Logic 1] 점수 계산 (Calculation)
                if (CanCalculateStrike(rolls, rollIndex))
                {
                    int score = 10 + GetNextTwoRollsScore(rolls, rollIndex);
                    runningScore += score;
                    dto.CurrentFrameScore = runningScore;
                }

                // [Logic 2] 프레임 구성 및 인덱스 이동 (Construction & Navigation)
                dto.Rolls.Add(MaxPins);
                rollIndex += dto.Rolls.Count;
            }
            else if (IsSpare(rolls, rollIndex))
            {
                // [Logic 1] 점수 계산
                if (CanCalculateSpare(rolls, rollIndex))
                {
                    int score = 10 + GetNextRollScore(rolls, rollIndex + 1);
                    runningScore += score;
                    dto.CurrentFrameScore = runningScore;
                }

                // [Logic 2] 프레임 구성 및 인덱스 이동
                dto.Rolls.Add(rolls[rollIndex]);
                dto.Rolls.Add(rolls[rollIndex + 1]);
                rollIndex += dto.Rolls.Count;
            }
            else if(IsOpen(rolls, rollIndex))
            {
                // [Logic 1] 점수 계산
                runningScore += (rolls[rollIndex] + rolls[rollIndex + 1]);
                dto.CurrentFrameScore = runningScore;
                // [Logic 2] 프레임 구성 및 인덱스 이동
                dto.Rolls.Add(rolls[rollIndex]);
                dto.Rolls.Add(rolls[rollIndex + 1]);
                rollIndex += dto.Rolls.Count;
            }
            else
            {
                // [Error Handling]

                // Case 1: 데이터 부족 (Waiting)
                if (rollIndex + 1 >= rolls.Count)
                {
                    dto.Rolls.Add(rolls[rollIndex]);
                    rollIndex++;
                }
                // Case 2: 데이터 오류 (Invalid Input - e.g., sum > 10)
                else
                {
                    int invalidSum = rolls[rollIndex] + rolls[rollIndex + 1];

                    // 계산기는 로거를 쓰지 않고, 데이터(DTO)에 사실만 기록합니다.
                    dto.ErrorMessage = $"ERROR: Frame {frameNum} sum is {invalidSum} (Max 10). Input ignored.";

                    dto.Rolls.Add(rolls[rollIndex]);
                    dto.Rolls.Add(rolls[rollIndex + 1]);

                    rollIndex += 2;
                }
            }

            frames.Add(dto);
        }

        return frames;
    }
    // --- Helper Methods (규칙이 바뀌면 여기만 고치면 됩니다) ---

    // 헬퍼 메서드들도 모두 IReadOnlyList를 받도록 수정
    private bool IsStrike(IReadOnlyList<int> rolls, int index)
    {
        return index < rolls.Count && rolls[index] == MaxPins;
    }

    private bool IsSpare(IReadOnlyList<int> rolls, int index)
    {
        return index + 1 < rolls.Count && (rolls[index] + rolls[index + 1] == MaxPins);
    }
    private bool IsOpen(IReadOnlyList<int> rolls, int index)
    {
        return index + 1 < rolls.Count && (rolls[index] + rolls[index + 1] < MaxPins);
    }

    private bool CanCalculateStrike(IReadOnlyList<int> rolls, int index)
    {
        return index + 2 < rolls.Count;
    }

    private bool CanCalculateSpare(IReadOnlyList<int> rolls, int index)
    {
        return index + 2 < rolls.Count;
    }

    private int GetNextTwoRollsScore(IReadOnlyList<int> rolls, int index)
    {
        return rolls[index + 1] + rolls[index + 2];
    }

    private int GetNextRollScore(IReadOnlyList<int> rolls, int currentIndex)
    {
        return rolls[currentIndex + 1];
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
