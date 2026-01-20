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
        game.KnockDownPins(10);
        game.KnockDownPins(6);
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

    // 1. Main(고정)용 생성자: 표준 부품 조립 (Constructor Chaining)
    public Game() : this(new StandardScoreCalculator(), new ConsoleScoreRenderer())
    {
    }

    // 2. 테스트/확장용 생성자: 부품 교체 가능 (의존성 주입)
    // 예: 나중에 'NoGutterModeCalculator' 같은 걸 넣을 수 있음
    public Game(IScoreCalculator calculator, IScoreBoardRenderer renderer)
    {
        _calculator = calculator;
        _renderer = renderer;
    }

    public void KnockDownPins(int pins)
    {
        // 1. 데이터 저장
        _rolls.Add(pins);

        // 2. 계산 위임 (Calculator야 계산해줘)
        var scoreBoard = _calculator.Calculate(_rolls);

        // 3. 출력 위임 (Renderer야 그려줘)
        _renderer.Render(scoreBoard);
    }
}
//각 프레임을 구분짓기
public class ScoreFrameDTO
{
    public int FrameNumber { get; set; }//아이덴티티 부여를 하지 않으면 개별로서 이용이 어렵다. 나중에 7프레임만 찾아라 등 정렬의 필요성이 생길때를 위함.
    public List<int> Rolls { get; set; } = new List<int>();
    public int? CurrentFrameScore { get; set; }

    // 뷰를 돕기 위한 헬퍼 프로퍼티
    public bool IsStrike => Rolls.Count == 1 && Rolls[0] == 10;
    public bool IsSpare => Rolls.Count == 2 && (Rolls.Sum() == 10);
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
    List<ScoreFrameDTO> Calculate(List<int> rolls);
}
// 실제 콘솔 출력 구현체
public class ConsoleScoreRenderer : IScoreBoardRenderer
{
    public void Render(List<ScoreFrameDTO> frames)
    {
        Console.Clear(); // 갱신될 때마다 화면 지우기 (선택사항)
        foreach (var frame in frames)
        {
            // 데이터(DTO)를 시각적 표현으로 변환
            string rollView = GetRollView(frame);
            Console.Write($"[{frame.FrameNumber}프레임: {rollView} \n| 총점: {frame.CurrentFrameScore}] ");
        }
        Console.WriteLine();
    }

    private string GetRollView(ScoreFrameDTO frame)
    {
        if (frame.IsStrike) return "X";
        if (frame.IsSpare) return $"{frame.Rolls[0]} /";
        return $"{frame.Rolls[0]} {frame.Rolls[1]}";
    }
}
// 표준 볼링 점수 계산기
public class StandardScoreCalculator : IScoreCalculator
{
    public List<ScoreFrameDTO> Calculate(List<int> rolls)
    {
        var frames = new List<ScoreFrameDTO>();
        int rollIndex = 0;

        for (int frame = 1; frame <= 10; frame++)
        {
            var dto = new ScoreFrameDTO { FrameNumber = frame };

            // 아직 던진 기록이 부족하면 중단
            if (rollIndex >= rolls.Count) break;

            // 스트라이크 로직 (간단 예시)
            if (rolls[rollIndex] == 10)
            {
                dto.Rolls.Add(10);
                // 보너스 점수 계산 로직 등은 여기에 구현
                rollIndex++;
            }
            else
            {
                dto.Rolls.Add(rolls[rollIndex]);
                if (rollIndex + 1 < rolls.Count)
                {
                    dto.Rolls.Add(rolls[rollIndex + 1]);
                }
                rollIndex += 2;
            }

            // 누적 점수 계산 로직 추가 필요
            // dto.CurrentFrameScore = ... 

            frames.Add(dto);
        }

        return frames;
    }
}
