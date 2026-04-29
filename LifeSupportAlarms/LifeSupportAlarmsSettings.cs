namespace LifeSupportAlarms
{
    public class LifeSupportAlarmsSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "Life Support Alarms";

        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;

        public override string Section => "Life Support Alarms";

        public override string DisplaySection => Section;

        public override int SectionOrder => 2;

        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("Enable Alarms", toolTip = "Create and maintain life support alarms automatically")]
        public bool EnableAlarms = true;

        [GameParameters.CustomIntParameterUI("Lead Time (hours)", minValue = 1, maxValue = 720, stepSize = 1,
            toolTip = "How many hours before resource expiry to fire the alarm")]
        public int LeadTimeHours = 6;

        [GameParameters.CustomIntParameterUI("Alarm Action", minValue = 0, maxValue = 2, stepSize = 1,
            toolTip = "0 = Do Nothing, 1 = Kill Warp, 2 = Pause Game")]
        public int AlarmAction = 1;

        [GameParameters.CustomParameterUI("Supplies Alarm", toolTip = "Create alarms for Supplies depletion")]
        public bool EnableSuppliesAlarm = true;

        [GameParameters.CustomParameterUI("Electric Charge Alarm", toolTip = "Create alarms for Electric Charge depletion")]
        public bool EnableECAlarm = false;

        [GameParameters.CustomParameterUI("Hab Alarm", toolTip = "Create alarms for Hab time expiry")]
        public bool EnableHabAlarm = true;

        [GameParameters.CustomParameterUI("Home Alarm", toolTip = "Create alarms for Home time expiry")]
        public bool EnableHomeAlarm = true;

        [GameParameters.CustomParameterUI("Group Alarms by Vessel",
            toolTip = "Show one alarm per vessel (earliest resource) instead of one per resource")]
        public bool GroupAlarmsByVessel = true;

        // Convenience accessor
        public static LifeSupportAlarmsSettings Instance =>
            HighLogic.CurrentGame?.Parameters?.CustomParams<LifeSupportAlarmsSettings>();
    }
}
