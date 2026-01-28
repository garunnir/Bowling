using BowlingGame.Core;
namespace BowlingGame.Logic
{
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
}
