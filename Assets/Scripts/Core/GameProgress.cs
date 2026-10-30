/// <summary>
/// Статистика одного проходження гри (живе між сценами).
/// </summary>
public static class GameProgress
{
    public static float RunTime;
    public static int TimesCaught;
    public static bool JustWon;

    public static void ResetRun()
    {
        RunTime = 0f;
        TimesCaught = 0;
        JustWon = false;
    }

    public static string FormatTime(float seconds)
    {
        int s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }
}
