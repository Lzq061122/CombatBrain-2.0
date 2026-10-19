using System;
using CombatBrain2.Host;
using TaleWorlds.MountAndBlade;

internal static class SelectionProbe
{
    private static float MaxCharge = 0f;
    private static float MaxOurs = 0f;

    private static void Main()
    {
        int failures = 0;

        // A: our behavior is registered but the native tactic zeroed its weight.
        failures += ScenarioA() ? 0 : 1;

        // B: weight restored to 8.0 while the native tactic keeps Charge at 10000.
        failures += ScenarioB() ? 0 : 1;

        Console.WriteLine();
        Console.WriteLine("max_charge_weight=" + MaxCharge.ToString("F4"));
        Console.WriteLine("max_ours_weight=" + MaxOurs.ToString("F4"));
        Console.WriteLine(failures == 0 ? "SELFPROBE PASS" : "SELFPROBE FAIL");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    // Vanilla Charge behaviors installed the way TacticCharge leaves them:
    // every weight zeroed, defaults restored, Charge set to 10000.
    private static Formation BuildFormation(float distanceMeters, float speed, bool activeIsOurs)
    {
        Team enemy = new Team { TeamIndex = 0, Side = BattleSideEnum.Defender, IsPlayerTeam = false };
        Team ally = new Team { TeamIndex = 1, Side = BattleSideEnum.Attacker, IsPlayerTeam = true };
        Mission.Current.Teams.Clear();
        Mission.Current.Teams.Add(enemy);
        Mission.Current.Teams.Add(ally);

        Formation target = new Formation(enemy, 0);
        target.CachedMedianPosition = new Vec2(0f, 0f);
        target.QuerySystem.MovementSpeedMaximum = speed;
        enemy.Formations.Add(target);

        Formation f = new Formation(ally, 2);
        f.CachedAveragePosition = new Vec2(distanceMeters, 0f);
        f.QuerySystem.IsCavalryFormation = true;
        f.QuerySystem.MovementSpeedMaximum = speed;
        f.SetClosest(target);
        f.AI.AddAiBehavior(new BehaviorCharge(f));
        f.AI.AddAiBehavior(new BehaviorPullBack(f));
        f.AI.AddAiBehavior(new BehaviorStop(f));
        f.AI.AddAiBehavior(new BehaviorReserve(f));
        ally.Formations.Add(f);

        if (activeIsOurs)
        {
            CbHoldBehavior pre = new CbHoldBehavior(f);
            pre.Arm();
            f.AI.AddAiBehavior(pre);
            f.AI.SetBehaviorWeight<CbHoldBehavior>(8f);
            f.AI.SetActiveProbe(pre);
        }
        return f;
    }

    private static bool ScenarioA()
    {
        BehaviorTickSink.Count = 0;
        Formation f = BuildFormation(30f, 3f, activeIsOurs: true);
        CbHoldBehavior ours = f.AI.GetBehavior<CbHoldBehavior>();

        float before = f.AI.WeightOfProbe(ours);
        new TacticCharge(f.Team).TickOccasionally();
        float after = f.AI.WeightOfProbe(ours);
        float charge = f.AI.WeightOfProbe(f.AI.GetBehavior<BehaviorCharge>());

        f.AI.FindBestProbe();
        Behaviour selected = Kind(f.AI.ActiveBehavior);

        Console.WriteLine("[A] weight_before_zero=" + before.ToString("F3") + " weight_after_zero=" + after.ToString("F4") + " charge_weight=" + charge.ToString("F3"));
        Console.WriteLine("[A] active_after_find=" + selected + " our_tick=" + BehaviorTickSink.Count + " armed=" + ours.Armed);
        bool ok = before > 1f && after <= 0f && selected == Behaviour.Charge && BehaviorTickSink.Count == 0;
        Console.WriteLine("[A] " + (ok ? "PASS (zeroed behavior is not eligible; chain still selects something else)" : "FAIL"));
        return ok;
    }

    private static bool ScenarioB()
    {
        bool ok = true;
        float[] distances = { 20f, 30f, 45f, 60f };
        foreach (float d in distances)
        {
            BehaviorTickSink.Count = 0;
            Formation f = BuildFormation(d, 3f, activeIsOurs: true);
            CbHoldBehavior ours = f.AI.GetBehavior<CbHoldBehavior>();
            new TacticCharge(f.Team).TickOccasionally();

            float zeroed = f.AI.WeightOfProbe(ours);
            f.AI.SetBehaviorWeight<CbHoldBehavior>(8f);
            float w = f.AI.WeightOfProbe(ours);
            BehaviorCharge charge = f.AI.GetBehavior<BehaviorCharge>();
            float chargeScore = charge.GetAIWeight() * charge.WeightFactor;
            float ourScore = ours.GetAIWeight() * ours.WeightFactor;
            MaxCharge = Math.Max(MaxCharge, chargeScore);
            MaxOurs = Math.Max(MaxOurs, ourScore);

            f.AI.FindBestProbe();
            Behaviour selected = Kind(f.AI.ActiveBehavior);

            Console.WriteLine("[B] distance=" + d.ToString("F1") + " charge=" + chargeScore.ToString("F3")
                + " ours=" + ourScore.ToString("F3") + " zeroed=" + zeroed.ToString("F3")
                + " restored=" + w.ToString("F3") + " active=" + selected
                + " our_tick=" + BehaviorTickSink.Count);
            if (selected != Behaviour.Ours || w <= 0f) ok = false;
        }
        Console.WriteLine("[B] " + (ok ? "PASS (restored 8.0 wins inside one native tactic cycle)" : "FAIL (restored 8.0 does not win)"));
        return ok;
    }

    private static Behaviour Kind(BehaviorComponent b)
    {
        if (b == null) return Behaviour.None;
        if (b is CbHoldBehavior) return Behaviour.Ours;
        if (b is BehaviorCharge) return Behaviour.Charge;
        if (b is BehaviorTrailingProbe) return Behaviour.Trailing;
        return Behaviour.Other;
    }

    private enum Behaviour
    {
        None,
        Ours,
        Charge,
        Trailing,
        Other
    }

    private sealed class BehaviorTrailingProbe : BehaviorComponent
    {
        public BehaviorTrailingProbe(Formation formation) : base(formation) { CalculateCurrentOrder(); }

        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderMove;
        }

        protected override float GetAiWeight()
        {
            return 0.2f;
        }
    }
}
