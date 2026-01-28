
using BowlingGame.Core;
namespace BowlingGame.UI
{
    /// <summary>
    /// 콘솔 환경에 로그를 출력하는 로거(Logger) 구현체입니다.
    /// </summary>
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
}