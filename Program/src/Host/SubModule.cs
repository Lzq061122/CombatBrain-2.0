using System;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 模块入口。模块 ID、程序集名、日志路径与旧 CombatBrain 完全区分。
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            RuntimeIdentity.Emit();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission == null) return;
            mission.AddMissionBehavior(new SamplingBehavior());
        }
    }
}
