namespace LifeSupportAlarms
{
    public class LifeSupportAlarmsSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Life Support Alarms"; } }

        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

        public override string Section { get { return "Life Support Alarms"; } }

        public override string DisplaySection { get { return Section; } }

        public override int SectionOrder { get { return 2; } }

        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Enable Alarms", toolTip = "Create and maintain life support alarms automatically")]
        public bool EnableAlarms = true;

        [GameParameters.CustomIntParameterUI("Lead Time (hours)", minValue = 1, maxValue = 720, stepSize = 1,
            toolTip = "How many hours before resource expiry to fire the alarm")]
        public int LeadTimeHours = 6;

        [GameParameters.CustomIntParameterUI("Alarm Action", minValue = 0, maxValue = 2, stepSize = 1,
            toolTip = "0 = Do Nothing, 1 = Kill Warp, 2 = Pause Game")]
        public int AlarmAction = 1;

        // Convenience accessor
        public static LifeSupportAlarmsSettings Instance
        {
            get
            {
                if (HighLogic.CurrentGame == null) return null;
                if (HighLogic.CurrentGame.Parameters == null) return null;
                return HighLogic.CurrentGame.Parameters.CustomParams<LifeSupportAlarmsSettings>();
            }
        }
    }
}
