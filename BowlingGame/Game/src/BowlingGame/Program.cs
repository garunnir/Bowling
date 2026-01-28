//TargetFramework: net8.0
class Program
{
    public static void Main(string[] args)
    {
        // 10프레임 게임 인스턴스 생성 (의존성 주입 없이 기본 생성자 사용)
        var game = new Game();

        // 테스트: 정상 입력 시나리오
        // 1프레임: 스페어 (4+6)
        game.KnockDownPins(4);
        game.KnockDownPins(6);

        // 2프레임: 스페어 (5+5)
        game.KnockDownPins(5);
        game.KnockDownPins(5);

        // 3프레임: 스트라이크
        game.KnockDownPins(10);

        // 4프레임: 진행 중 (6)
        game.KnockDownPins(6);
        while (true)
            game.KnockDownPins(int.Parse( Console.ReadLine()));
    }
}
