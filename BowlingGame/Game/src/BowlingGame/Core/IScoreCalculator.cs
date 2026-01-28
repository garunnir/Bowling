
namespace BowlingGame.Core
{
    public interface IScoreCalculator
    {
        List<ScoreFrameDTO> Calculate(IReadOnlyList<int> rolls);
    }
}