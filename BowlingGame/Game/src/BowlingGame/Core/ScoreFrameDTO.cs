
namespace BowlingGame.Core
{
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
}