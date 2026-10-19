// Offline probe: does the real FormationAI selection chain admit our behavior?
// This compiles the decompiled engine sources (formation selector, behavior
// scorers) against a minimal semantic model of the engine surface. Every
// decision that matters -- the WeightFactor > 1e-7 gate, GetAIWeight() *
// WeightFactor, the 1.2..2.0 active-behavior bonus, the navmeshless penalty,
// and the rotation that makes _behaviors[0] stick -- is the original code,
// not a re-implementation.
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace TaleWorlds.Library
{
    public class MBException : Exception
    {
        public MBException(string m) : base(m) { }
    }

    public static class MBDebug
    {
        public static bool IsDisplayingHighLevelAI = false;
        public static void Print(string s) { }
    }

    public static class MBMath
    {
        public static float ClampFloat(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Lerp(float a, float b, float t, float unused) { return a + (b - a) * t; }
    }
}

namespace TaleWorlds.Core
{
    public static class Extensions
    {
        public static T MaxBy<T, K>(IEnumerable<T> source, Func<T, K> selector)
        {
            var cmp = Comparer<K>.Default;
            T best = default(T);
            bool any = false;
            foreach (var item in source)
            {
                if (!any) { best = item; any = true; continue; }
                if (cmp.Compare(selector(item), selector(best)) > 0) best = item;
            }
            return best;
        }
    }
}

namespace TaleWorlds.Engine
{
    public class EngineEntity
    {
        public string Name = "entity";
    }
}

namespace TaleWorlds.MountAndBlade
{
    public enum BattleSideEnum { Attacker = 0, Defender = 1, None = 2 }

    public class Vec2
    {
        public float X;
        public float Y;
        public Vec2() { }
        public Vec2(float x, float y) { X = x; Y = y; }
        public float Distance(Vec2 other) { float dx = X - other.X, dy = Y - other.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
    }

    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public Vec2 AsVec2 => new Vec2(X, Y);
    }

    public static class Vec2Extensions
    {
        public static Vec2 Normalized(this Vec2 v)
        {
            float l = v.Length;
            if (l <= 1e-6f) return new Vec2(0f, 0f);
            return new Vec2(v.X / l, v.Y / l);
        }

        public static float Dot(this Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        public static Vec2 Rotate(this Vec2 v, float degrees)
        {
            float r = degrees * (float)Math.PI / 180f;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }

    public class Team
    {
        public int TeamIndex = 0;
        public BattleSideEnum Side = BattleSideEnum.Attacker;
        public bool IsPlayerGeneral = false;
        public bool IsPlayerSergeant = false;
        public bool IsPlayerTeam = true;
        public object TeamAI = null;
        public Mission Mission;
        public readonly List<Formation> Formations = new List<Formation>();
        public Agent GeneralAgent = null;
        public Agent PlayerOwner = null;
        public IEnumerable<Formation> FormationsIncludingEmpty => Formations;
        public IEnumerable<Formation> FormationsIncludingSpecialAndEmpty => Formations;
        public bool IsEnemyOf(Team t) { return t != null && t.Side != Side; }
        public bool IsFriendOf(Team t) { return t != null && t.Side == Side; }
        public bool HasAnyEnemyTeamsWithAgents() { return true; }
        public float RemainingPowerRatio = 1f;
        public float TotalPowerRatio = 1f;
        public class QuerySystemStub { }
    }

    public class Agent
    {
        public static Agent Main = new Agent();
        public bool IsPlayerControlled { get; set; }
    }

    public static class MBTextManager
    {
        public static void SetTextVariable(string k, object v) { }
    }

    public class TextObject
    {
        public TextObject(string s) { }
    }

    public static class GameTexts
    {
        public static TextObject FindText(string id, string a = null) { return new TextObject(id); }
    }

    public class MissionTime
    {
        public float NumberOfSeconds;
        public bool IsPast => true;
        public static MissionTime SecondsFromNow(float s) { return new MissionTime { NumberOfSeconds = s }; }
    }

    public class Timer
    {
        public float PreviousDeltaTime = 0.5f;
        public float ElapsedTime() { return 0f; }
        public void Reset() { }
        public void Start() { }
        public void Stop() { }
        public void Tick(float dt) { PreviousDeltaTime = dt; }
        public void Set(float interval) { }
        public Timer(float now, float interval) { }
        public bool Check(float now) { return true; }
        public void Reset(float now) { }
    }

    public class Mission
    {
        public static Mission Current = new Mission();
        public float CurrentTime = 0f;
        public bool AllowAiTicking = true;
        public bool ForceTickOccasionally = true;
        public bool IsMissionEnding = false;
        public bool IsMultiplayer = false;
        public Agent MainAgent = null;
        public readonly List<Team> Teams = new List<Team>();
        public bool IsDeploymentFinished_Override = true;
        public T GetMissionBehavior<T>() where T : class { return null; }
    }

    public static class GameNetwork
    {
        public static bool IsMultiplayer = false;
        public static bool IsServer = false;
    }

    public static class MBInformationManager
    {
        public static void AddQuickInformation(TextObject t, int n, object o) { }
    }

    public struct FormationClass
    {
        public int Index;
        public const int NumberOfRegularFormations = 10;
        public const int Bodyguard = 10;
        public bool IsRanged() { return Index == 1; }
        public bool IsMeleeCavalry() { return Index == 2; }
    }

    public struct MovementOrder
    {
        public bool IsValid;
        public static MovementOrder MovementOrderCharge => new MovementOrder { IsValid = true };
        public static MovementOrder MovementOrderStop => new MovementOrder { IsValid = true };
        public static MovementOrder MovementOrderMove => new MovementOrder { IsValid = true };
        public static MovementOrder MovementOrderChargeToTarget(Formation f) { return new MovementOrder { IsValid = true }; }
        public int GetPosition(object f) { return 0; }
    }

    public struct FiringOrder
    {
        public static FiringOrder FiringOrderFireAtWill => new FiringOrder();
    }

    public struct ArrangementOrder
    {
        public enum ArrangementOrderEnum { Line = 0, ShieldWall = 1 }
        public ArrangementOrderEnum OrderEnum;
        public static ArrangementOrder ArrangementOrderLine => new ArrangementOrder();
    }

    public struct FacingOrder
    {
        public static FacingOrder FacingOrderLookAtEnemy => new FacingOrder();
    }

    public class FormationQuerySystem
    {
        public bool IsCavalryFormation;
        public bool IsRangedCavalryFormation;
        public float MovementSpeedMaximum = 3f;
        public float FormationMeleeFightingPower = 1f;
        public float GetClassWeightedFactor(params float[] w) { return 1f; }
        public static implicit operator Formation.FormationQuerySystemHolder(FormationQuerySystem qs) { return qs == null ? null : qs.holder; }
        public bool IsSameAs(Formation.FormationQuerySystemHolder h) { return h != null && h.QuerySystem == this; }
        public float GetClassWeightedFactorOf(params float[] w) { return owner.QuerySystem.GetClassWeightedFactor(w); }
        internal TaleWorlds.MountAndBlade.Formation.FormationQuerySystemHolder holder;
        public float RangedUnitRatio = 0.5f;
        public Team Team => owner.Team;
        public Formation owner;
    }

    public class TeamAISiegeComponent
    {
        public class Gate { public bool IsGateOpen = true; public bool IsUsedByFormation(Formation f) { return true; } }
        public Gate OuterGate { get; set; }
        public Gate InnerGate { get; set; }
        public static bool IsFormationInsideCastle(Formation f, bool includeOnlyPositionedUnits) { return false; }
    }

    public class Formation
    {
        public Team Team;
        public FormationAI AI;
        public FormationClass PhysicalClass;
        public FormationClass FormationClassNum;
        public int FormationIndex;
        public bool IsAIControlled = true;
        public bool IsAITickedAfterSplit;
        public bool IsPlayerTroopInFormation = false;
        public Agent PlayerOwner;
        public FormationQuerySystem QuerySystem = new FormationQuerySystem();
        public Vec2 CachedAveragePosition = new Vec2();
        public Vec2 CachedMedianPosition = new Vec2();
        public TaleWorlds.MountAndBlade.Formation.FormationQuerySystemHolder CachedClosestEnemyFormation;
        public int CountOfUnits = 50;
        public ArrangementOrder ArrangementOrder;
        public int MovementOrderSetCount;
        public MovementOrder LastMovementOrder;

        public class FormationQuerySystemHolder
        {
            public Formation Formation;
            public FormationQuerySystem QuerySystem;
        }

        public Formation(Team team, int index)
        {
            Team = team;
            FormationIndex = index;
            AI = new FormationAI(this);
            QuerySystem.owner = this;
            QuerySystem.holder = new TaleWorlds.MountAndBlade.Formation.FormationQuerySystemHolder { Formation = this, QuerySystem = QuerySystem };
            CachedClosestEnemyFormation = QuerySystem.holder;
        }

        public bool HasAnyEnemyFormationsThatIsNotEmpty() { return true; }
        public void SetMovementOrder(MovementOrder order) { MovementOrderSetCount++; LastMovementOrder = order; }
        public void SetClosest(Formation f) { CachedClosestEnemyFormation = new TaleWorlds.MountAndBlade.Formation.FormationQuerySystemHolder { Formation = f, QuerySystem = f.QuerySystem }; f.CachedClosestEnemyFormation = new TaleWorlds.MountAndBlade.Formation.FormationQuerySystemHolder { Formation = this, QuerySystem = QuerySystem }; }
        public void SetFiringOrder(FiringOrder order) { }
        public void SetArrangementOrder(ArrangementOrder order) { ArrangementOrder = order; }
        public void StartUsingMachine(object machine) { }
        public void ForceCalculateCaches() { }
    }

    public abstract class BehaviorComponent
    {
        protected FormationAI.BehaviorSide _behaviorSide;
        protected const float FormArrangementDistanceToOrderPosition = 10f;
        private const float _playerInformCooldown = 60f;
        protected float _lastPlayerInformTime;
        private Timer _navmeshlessTargetPenaltyTime;
        private float _navmeshlessTargetPositionPenalty = 1f;
        public bool IsCurrentOrderChanged;
        private MovementOrder _currentOrder;
        protected FacingOrder CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
        public Formation Formation { get; private set; }
        public float BehaviorCoherence { get; set; }

        public virtual float NavmeshlessTargetPositionPenalty
        {
            get
            {
                if (_navmeshlessTargetPositionPenalty == 1f) return 1f;
                _navmeshlessTargetPenaltyTime.Check(Mission.Current.CurrentTime);
                float num = _navmeshlessTargetPenaltyTime.ElapsedTime();
                if (num >= 10f) { _navmeshlessTargetPositionPenalty = 1f; return 1f; }
                if (num <= 5f) return _navmeshlessTargetPositionPenalty;
                return MBMath.Lerp(_navmeshlessTargetPositionPenalty, 1f, (num - 5f) / 5f, 0f);
            }
            set
            {
                _navmeshlessTargetPenaltyTime.Reset(Mission.Current.CurrentTime);
                _navmeshlessTargetPositionPenalty = value;
            }
        }

        public MovementOrder CurrentOrder
        {
            get { return _currentOrder; }
            protected set { _currentOrder = value; IsCurrentOrderChanged = true; }
        }

        public float PreserveExpireTime { get; set; }

        public float WeightFactor { get; set; }

        protected BehaviorComponent(Formation formation)
        {
            Formation = formation;
            PreserveExpireTime = 0f;
            _navmeshlessTargetPenaltyTime = new Timer(Mission.Current.CurrentTime, 50f);
        }

        protected BehaviorComponent() { }

        internal void OnBehaviorActivated()
        {
            if (!Formation.Team.IsPlayerGeneral && !Formation.Team.IsPlayerSergeant && Formation.IsPlayerTroopInFormation && Mission.Current.MainAgent != null)
            {
                MBInformationManager.AddQuickInformation(new TextObject("x"), 2000, Mission.Current.MainAgent);
            }
            if (!GameNetwork.IsMultiplayer) { _lastPlayerInformTime = Mission.Current.CurrentTime; }
            if (Formation.IsAIControlled) { OnBehaviorActivatedAux(); }
        }

        protected virtual void OnBehaviorActivatedAux() { }
        public virtual void OnBehaviorCanceled() { }
        public virtual void OnLostAIControl() { }
        public virtual void OnAgentRemoved(Agent agent) { }
        public void RemindSergeantPlayer() { }
        public virtual void TickOccasionally() { }

        public float GetAIWeight()
        {
            return GetAiWeight() * NavmeshlessTargetPositionPenalty;
        }

        protected abstract float GetAiWeight();

        public virtual void ResetBehavior()
        {
            WeightFactor = 0f;
        }

        public virtual TextObject GetBehaviorString() { return new TextObject(GetType().Name); }
        public virtual void OnValidBehaviorSideChanged() { _behaviorSide = Formation.AI.Side; }
        protected virtual void CalculateCurrentOrder() { }
        public void PrecalculateMovementOrder() { CalculateCurrentOrder(); CurrentOrder.GetPosition(Formation); }
        public override bool Equals(object obj) { return GetType() == obj.GetType(); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public virtual void OnDeploymentFinished() { }
    }
}
