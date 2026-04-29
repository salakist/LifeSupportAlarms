namespace LifeSupportAlarms.Domain
{
    // Controls what KSP does when a life-support alarm fires.
    // Mirrors the 0/1/2 int stored in LifeSupportAlarmsSettings.AlarmAction
    // (which must stay an int because KSP's CustomIntParameterUI requires it).
    internal enum AlarmAction
    {
        DoNothing = 0,
        KillWarp = 1,
        PauseGame = 2,
    }
}
