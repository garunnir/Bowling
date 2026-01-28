using Xunit;
using Xunit.Abstractions; // [Log] 출력을 위한 네임스페이스 추가
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BowlingGame.Logic;
public class StandardScoreCalculatorTests
{
    private readonly StandardScoreCalculator _calculator;
    private readonly ITestOutputHelper _output; // [Log] 로그 출력을 위한 헬퍼 객체

    // 생성자에 ITestOutputHelper를 추가하면 xUnit이 알아서 주입해줍니다.
    public StandardScoreCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new StandardScoreCalculator(10);
    }

    // [Theory] 데이터 기반 테스트 (Data-Driven Test)
    [Theory]
    [InlineData("0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0", 0)]
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10", 300)]
    [InlineData("5,5, 3,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0, 0,0", 16)]
    [InlineData("9,0, 9,0, 9,0, 9,0, 9,0, 9,0, 9,0, 9,0, 9,0, 9,0", 90)]
    [InlineData("5,5, 5,5, 5,5, 5,5, 5,5, 5,5, 5,5, 5,5, 5,5, 5,5, 5", 150)]
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 2, 2, 2", 250)]//10프레임 오픈게임 초과입력 방어 확인
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 2, 2", 250)]//10프레임 오픈게임
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 5, 5, 10", 275)]//10프레임 스페어
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 2, 2", 276)]//10프레임 스트라이크
    [InlineData("10, 10, 10, 10, 10, 10", 120)]
    [InlineData("10, 10, 10, 10, 10, 10, 2, 3, 10, 10, 10, 5, 5", 237)]
    [InlineData("10, 10, 10", 30)]
    public void Calculate_Scenarios_ReturnsExpectedTotalScore(string rollsStr, int expectedScore)
    {
        // 1. Arrange
        var rolls = ParseRolls(rollsStr);

        // 2. Act
        var frames = _calculator.Calculate(rolls);
        int? lastScore = frames.Where(x => x.CurrentFrameScore != null).LastOrDefault()?.CurrentFrameScore;

        // [Log] 계산된 점수 확인용 로그 (필요시 주석 해제)
        // _output.WriteLine($"입력: {rollsStr} / 예상점수: {expectedScore} / 실제점수: {lastScore ?? 0}");

        // 3. Assert
        if (frames.Any())
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var item in frames)
            {
                stringBuilder.Append(item.CurrentFrameScore + ",");
            }
            _output.WriteLine($"[테스트 로그] 입력값: {stringBuilder}");
            // 마지막 점수와 예상 점수 비교
            Assert.Equal(expectedScore, lastScore ?? 0);

        }
        else
        {
            Assert.Equal(0, expectedScore);
        }
    }

    // [Theory] 에러 케이스 검증
    [Theory]
    [InlineData("11")]       // 핀 개수 초과
    [InlineData("5, 6")]     // 일반 프레임 합계 초과
    [InlineData("-5")]       // 음수 입력
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 2, 2, 2, 2")] // 10프레임 초과 입력
    [InlineData("10, 10, 10, 10, 10, 10, 10, 10, 10, 5, 10, 10")]// 10프레임 앞 2구 합계 초과
    public void Calculate_InvalidInput_ReturnsErrorDTO(string rollsStr)
    {
        // Arrange
        var rolls = ParseRolls(rollsStr);

        // Act
        var frames = _calculator.Calculate(rolls);

        // Assert
        var firstFrame = frames.FirstOrDefault(x => !string.IsNullOrEmpty(x.ErrorMessage));

        // [Log] 에러 메시지를 테스트 결과 창에 출력
        if (firstFrame != null)
        {
            _output.WriteLine($"[테스트 로그] 입력값: {rollsStr}");
            _output.WriteLine($"[테스트 로그] 발생한 에러: {firstFrame.ErrorMessage}");
        }
        else
        {
            _output.WriteLine($"[테스트 로그] 입력값: {rollsStr} -> 에러가 발생하지 않았습니다 (테스트 실패 예상)");
        }

        Assert.NotNull(firstFrame); // 에러 프레임이 있어야 함
        Assert.False(string.IsNullOrEmpty(firstFrame.ErrorMessage), "에러 메시지가 비어있습니다.");

        // 데이터 무결성 확인: 에러가 났으므로 점수는 null이어야 함
        Assert.Null(firstFrame.CurrentFrameScore);
    }

    // --- Helper Method ---
    private List<int> ParseRolls(string rollsStr)
    {
        if (string.IsNullOrWhiteSpace(rollsStr)) return new List<int>();

        return rollsStr.Split(',')
                       .Select(s => int.Parse(s.Trim()))
                       .ToList();
    }
}