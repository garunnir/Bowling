using System.Text;
using BowlingGame.Core;
namespace BowlingGame.UI
{
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
}
