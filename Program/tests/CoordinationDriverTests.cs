using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P2 协同状态机（派单 §3/§4/§6）。纯逻辑测试：外部喂读数，断言阶段与释放。
    public class CoordinationDriverTests
    {
        // 我方 30/55/15、敌方 100 含 10 远程 → RangedEdge = 0.45。
        // 实际中心与引擎人数分开给：P2 职责基线必须来自实际有效活跃成员，
        // 因此夹具默认给出实际中心，与生产采样一致；要测缺质心的分支
        // 用 RecWithoutCenter。
        private static FormationRecord Rec(int team, int index, string cls, int units,
                                           double x, double y, bool playerTeam)
        {
            var r = RecWithoutCenter(team, index, cls, units, x, y, playerTeam);
            r.HasActiveCenter = true;
            r.ActiveCenterX = x;
            r.ActiveCenterY = y;
            r.ActiveMemberCount = units;
            r.MemberDigest = "m" + team + index;
            return r;
        }

        private static FormationRecord RecWithoutCenter(int team, int index, string cls, int units,
                                                        double x, double y, bool playerTeam)
        {
            return new FormationRecord
            {
                TeamIndex = team,
                FormationIndex = index,
                FormationClass = cls,
                UnitCount = units,
                PosX = x,
                PosY = y,
                IsPlayerTeam = playerTeam,
                IsPlayerControlled = false,
                IsAiControlled = true,
                MovementOrder = "Move",
                ObjectIdentity = "obj" + team + index,
            };
        }

        // 全部编队都没有实际中心：P2 无从判定人数，不得回退引擎人数。
        private static FormationSnapshot SnapshotWithoutCenters(int inf, int rng, int cav,
                                                                double enemyUnits, double enemyX, double enemyY)
        {
            var s = new FormationSnapshot { SampledAt = TestParameters.P2Now };
            if (inf > 0) s.Records.Add(RecWithoutCenter(0, 0, "Infantry", inf, 0.0, 0.0, false));
            if (rng > 0) s.Records.Add(RecWithoutCenter(0, 1, "Ranged", rng, 0.0, 0.0, false));
            if (cav > 0) s.Records.Add(RecWithoutCenter(0, 2, "Cavalry", cav, 0.0, 0.0, false));
            int enemyRanged = 10;
            int enemyInf = (int)enemyUnits - enemyRanged;
            if (enemyInf > 0) s.Records.Add(RecWithoutCenter(1, 0, "Infantry", enemyInf, enemyX, enemyY, true));
            if (enemyRanged > 0) s.Records.Add(RecWithoutCenter(1, 1, "Ranged", enemyRanged, enemyX, enemyY + 30.0, true));
            return s;
        }

        private static FormationSnapshot Snapshot(int inf, int rng, int cav,
                                                  double enemyUnits, double enemyX, double enemyY)
        {
            var s = new FormationSnapshot { SampledAt = TestParameters.P2Now };
            if (inf > 0) s.Records.Add(Rec(0, 0, "Infantry", inf, 0.0, 0.0, false));
            if (rng > 0) s.Records.Add(Rec(0, 1, "Ranged", rng, 0.0, 0.0, false));
            if (cav > 0) s.Records.Add(Rec(0, 2, "Cavalry", cav, 0.0, 0.0, false));
            int enemyRanged = 10;
            int enemyInf = (int)enemyUnits - enemyRanged;
            if (enemyInf > 0) s.Records.Add(Rec(1, 0, "Infantry", enemyInf, enemyX, enemyY, true));
            if (enemyRanged > 0) s.Records.Add(Rec(1, 1, "Ranged", enemyRanged, enemyX, enemyY + 30.0, true));
            return s;
        }

        // 场景 A：我方 30/55/15，敌方 100（含 10 远程）。
        private static BattleState SceneA()
        {
            var state = BattleAssessment.Read(Snapshot(30, 55, 15, 100.0, 0.0, 300.0), "input-1");
            Assert.NotNull(state);
            return state;
        }

        // 走生产路径建立计划，确保夹具与实际创建入口一致。
        private static CoordinationDriver BoundDriver(out BattlePlan plan, out BattleState state)
        {
            state = SceneA();
            var ledger = new PlanLedger();
            plan = ledger.CreateCoordination(state, TestParameters.P2Now);
            Assert.NotNull(plan);
            var driver = new CoordinationDriver();
            driver.Bind(plan);
            Assert.True(driver.BindTasks(plan));
            return driver;
        }

        // 用目标任务自己的基线人数构造读数，避免夹具喂进低于该职责继续线的人数。
        private static DependencyObservation At(CoordinationGeometry g, FormationTask task,
                                                double x, double y, bool isGuard)
        {
            return At(g, task, x, y, task.RoleBaselineUnits, isGuard);
        }

        private static DependencyObservation At(CoordinationGeometry g, FormationTask task,
                                                double x, double y, int units, bool isGuard)
        {
            return new DependencyObservation
            {
                ObjectPresent = true,
                PositionKnown = true,
                X = x,
                Y = y,
                CurrentUnits = units,
                Eligible = true,
                FormationClass = task.FormationClass,
            };
        }

        // 释放主力推进要求保护与辅助的命令确实写入并生效：只喂位置读数不足以放行
        // （派单 §3 L86/L103；独立验收缺陷 1 正是只靠读数就放行了推进）。
        // 命令锚点是写给引擎的请求，与战术目标 T 分开：职责到位仍由实际质心判定，
        // 这里两个点相同只是让夹具聚焦"命令已写入且未被改写"。
        private static void CommandsInEffect(BattlePlan plan, CoordinationDriver driver, double writtenAt)
        {
            var g = plan.Geometry;
            driver.GuardTask.WrittenAt = writtenAt;
            driver.GuardTask.Feedback = TaskFeedbackState.Effective;
            driver.GuardTask.SetCanonicalTarget(g.GuardX, g.GuardY);
            driver.GuardTask.SetTarget(0.0, 0.0, g.GuardX, g.GuardY);
            driver.GuardTask.SetOrderTarget(g.GuardX, g.GuardY);
            driver.SupportTask.WrittenAt = writtenAt;
            driver.SupportTask.Feedback = TaskFeedbackState.Effective;
            driver.SupportTask.SetCanonicalTarget(g.SupportX, g.SupportY);
            driver.SupportTask.SetTarget(0.0, 0.0, g.SupportX, g.SupportY);
            driver.SupportTask.SetOrderTarget(g.SupportX, g.SupportY);
        }

        // ---- 绑定与准入 ----

        [Fact]
        public void CoordinationPlanBindsThreeDistinctRoles()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            Assert.True(CoordinationDriver.IsCoordinationPlan(plan));
            Assert.Equal(CoordinationPhase.AwaitingDependencies, driver.Phase);
            Assert.Equal(3, plan.NormalTaskCount());
            Assert.NotEqual(driver.GuardTask.FormationKey, driver.SupportTask.FormationKey);
            Assert.NotEqual(driver.MainWaitTask.FormationKey, driver.GuardTask.FormationKey);
            Assert.Equal(FormationRole.Main, driver.MainWaitTask.Role);
            Assert.Equal(FormationRole.Guard, driver.GuardTask.Role);
            Assert.Equal(FormationRole.Support, driver.SupportTask.Role);
        }

        [Fact]
        public void PlainP1PlanIsNotCoordinationPlan()
        {
            var state = SceneA();
            var p1 = PlanBuilder.Build(state, "plan-p1", TestParameters.P2Now);
            Assert.NotNull(p1);
            Assert.False(CoordinationDriver.IsCoordinationPlan(p1));
        }

        // 职责基线必须取自实际有效活跃成员；缺实际质心时不得回退引擎人数。
        [Fact]
        public void CoordinationRoleBaselineFallsBackToEngineCountWhenNoActiveCenter()
        {
            var state = BattleAssessment.Read(SnapshotWithoutCenters(30, 55, 15, 100.0, 0.0, 300.0), "input-1");
            Assert.NotNull(state);
            var plan = new PlanLedger().CreateCoordination(state, TestParameters.P2Now);
            Assert.NotNull(plan);
            foreach (var t in plan.Tasks)
                Assert.Equal(0, t.RoleBaselineUnits);
        }

        // 有实际质心时，基线等于实际活跃人数，而不是引擎人数。
        [Fact]
        public void CoordinationRoleBaselineUsesActiveMemberCountNotEngineCount()
        {
            var snapshot = Snapshot(30, 55, 15, 100.0, 0.0, 300.0);
            foreach (var r in snapshot.Records)
            {
                if (r.TeamIndex != 0) continue;
                r.ActiveMemberCount = r.FormationClass == "Ranged" ? 40 : r.UnitCount;
                r.HasActiveCenter = true;
                r.ActiveCenterX = r.PosX;
                r.ActiveCenterY = r.PosY;
            }
            var state = BattleAssessment.Read(snapshot, "input-1");
            Assert.NotNull(state);
            var plan = new PlanLedger().CreateCoordination(state, TestParameters.P2Now);
            Assert.NotNull(plan);
            foreach (var t in plan.Tasks)
            {
                if (t.FormationClass != "Ranged") continue;
                Assert.Equal(40, t.RoleBaselineUnits);
            }
        }

        // ---- 依赖释放：单次读数不够 ----

        [Fact]
        public void SingleReadDoesNotReleaseMainAdvance()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            var outcome = driver.Observe(TestParameters.P2Now, guard, support);

            Assert.False(outcome.MainAdvanceAllowed);
            Assert.Equal(1, driver.GuardReadiness.ReadCount);
            Assert.Equal(1, driver.SupportReadiness.ReadCount);
        }

        [Fact]
        public void TwoReadsSpanningOneSecondReleaseMainAdvance()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);

            Assert.True(outcome.MainAdvanceAllowed);
            Assert.True(outcome.GuardReady);
            Assert.True(outcome.SupportReady);
        }

        // ---- §9.1 敏感性证明：去掉保护反馈后主力不得释放 ----

        [Fact]
        public void RemovingGuardFeedbackBlocksMainAdvance()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var released = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.True(released.MainAdvanceAllowed);

            // 保护编队离开到位区：连续窗口清零，主力立即不得推进。
            var strayed = At(g, driver.GuardTask, g.GuardX + 40.0, g.GuardY + 40.0, true);
            var blocked = driver.Observe(TestParameters.P2Now + 2.0, strayed, support);
            Assert.False(blocked.MainAdvanceAllowed);

            // 恢复读数后仍需重新累计 1 s，不能瞬间放行。
            driver.Observe(TestParameters.P2Now + 3.0, guard, support);
            var again = driver.Observe(TestParameters.P2Now + 4.0, guard, support);
            Assert.True(again.MainAdvanceAllowed);
        }

        [Fact]
        public void SupportOutsideLateralBandIsNotReady()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            // 辅助贴到主力正后方（侧向 0），不在 [7,17] 区间。
            var support = At(g, driver.SupportTask, g.MainX, g.MainY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.False(outcome.SupportReady);
            Assert.False(outcome.MainAdvanceAllowed);
        }

        // ---- §6 人数继续线 ----

        [Fact]
        public void RoleBelowUnitFloorIsPremiseFailure()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, 5, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.Equal(RolePremiseFailure.StrengthBelowLine, outcome.GuardFailure);
            Assert.False(outcome.MainAdvanceAllowed);
        }

        // 人数继续线：基线 12、当前 6 同时落在 ≥6 与 ≥50% 两条边界上，允许继续。
        [Fact]
        public void RoleAtBothFloorsContinues()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            Assert.True(PlanBuilder.RoleStrengthHolds(6, 12));
            Assert.True(PlanBuilder.RoleStrengthHolds(12, 12));
            // 奇数基线的半数不得向下取整后算通过：基線 11 要求 >= 5.5，即 6 人通过、5 人失败。
            Assert.True(PlanBuilder.RoleStrengthHolds(6, 11));
            Assert.False(PlanBuilder.RoleStrengthHolds(5, 11));
            // 基线 55、当前 6：虽然达到绝对下限 6，但低于 50% 基线，必须失败。
            Assert.False(PlanBuilder.RoleStrengthHolds(6, 55));
        }

        // 保护任务的实际基线取自计划分配，夹具必须按真实基线给人数。
        [Fact]
        public void GuardBelowItsOwnBaselineFailsStrengthPremise()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            Assert.Equal(55, driver.GuardTask.RoleBaselineUnits);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, 6, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.Equal(RolePremiseFailure.StrengthBelowLine, outcome.GuardFailure);
        }

        [Fact]
        public void LostEligibilityIsPremiseFailure()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            guard.Eligible = false;
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.Equal(RolePremiseFailure.EligibilityLost, outcome.GuardFailure);
            Assert.False(outcome.MainAdvanceAllowed);
        }

        [Fact]
        public void MissingObjectIsPremiseFailure()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            guard.ObjectPresent = false;
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.Equal(RolePremiseFailure.ObjectGone, outcome.GuardFailure);
        }

        // ---- 阶段转换：依赖成立才关等待任务并指定推进任务 ----

        [Fact]
        public void AdvanceTaskReplacesMainWaitOnlyAfterRelease()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now));

            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);

            Assert.NotNull(advance);
            Assert.Equal(TaskKind.Reposition, advance.Kind);
            Assert.Equal(FormationRole.Main, advance.Role);
            Assert.Equal(driver.MainWaitTask.FormationKey, advance.FormationKey);
            Assert.True(driver.MainWaitTask.IsClosed);
            Assert.Equal(CoordinationPhase.MainAdvancing, driver.Phase);
        }

        // 门控反例一：只有单次读数时，任何时刻都不得指定推进任务。
        [Fact]
        public void SingleReadNeverReleasesAdvanceTask()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 5.0));
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 40.0));
            Assert.Equal(CoordinationPhase.AwaitingDependencies, driver.Phase);
        }

        // 门控反例二：连续有效读数才释放；释放后依赖失效，不得继续使用缓存就绪状态。
        [Fact]
        public void DependencyLossAfterReleaseBlocksCachedReadiness()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(
                plan, driver.GuardTask, driver.SupportTask,
                driver.GuardReadiness, driver.SupportReadiness));

            // 保护编队离开到位区：就绪窗口清零，缓存不得再放行。
            var strayed = At(g, driver.GuardTask, g.GuardX + 60.0, g.GuardY + 60.0, true);
            var after = driver.Observe(TestParameters.P2Now + 2.0, strayed, support);
            Assert.False(after.MainAdvanceAllowed);
            Assert.False(DependencyReadiness.MainAdvanceAllowed(
                plan, driver.GuardTask, driver.SupportTask,
                driver.GuardReadiness, driver.SupportReadiness));
        }

        [Fact]
        public void AdvanceTaskKeepsRevisionWhenAssignmentUnchanged()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);

            // 同一角色/对象/分配不变 → 不增 revision（派单 §3）。
            Assert.Equal(plan.Revision, advance.Revision);
        }

        [Fact]
        public void AdvanceTaskOnlyCreatedOnce()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.NotNull(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0));
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 2.0));
        }

        // ---- 关系建立超时 ----

        // 未写令的职责不构成超时：窗口尚未开始。
        [Fact]
        public void UnwrittenRolesNeverExpire()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 19.0));
            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 20.0));
            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 9999.0));
        }

        // 每个职责分别从自己的实际写令时刻起算二十秒到位窗口。
        [Fact]
        public void EachRoleHasItsOwnDeadlineWindow()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);

            // 两者都未写令：两个窗口都尚未开始。
            Assert.Equal(-1.0, driver.GuardDeadline());
            Assert.Equal(-1.0, driver.SupportDeadline());
            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 100.0));

            // 保护在 218 写令 → 保护截止 238；辅助未写令 → 窗口未开始。
            driver.GuardTask.WrittenAt = TestParameters.P2Now + 18.0;
            Assert.Equal(TestParameters.P2Now + 38.0, driver.GuardDeadline());
            Assert.Equal(-1.0, driver.SupportDeadline());
            Assert.False(driver.GuardExpired(plan, TestParameters.P2Now + 37.0));
            Assert.True(driver.GuardExpired(plan, TestParameters.P2Now + 38.0));
            // 保护到期不牵连辅助：辅助窗口尚未开始，其自身仍未超时。
            Assert.False(driver.SupportExpired(plan, TestParameters.P2Now + 38.0));

            // 辅助在 222 写令 → 辅助截止 242，独立于保护的 238。
            driver.SupportTask.WrittenAt = TestParameters.P2Now + 22.0;
            Assert.Equal(TestParameters.P2Now + 42.0, driver.SupportDeadline());
            Assert.False(driver.SupportExpired(plan, TestParameters.P2Now + 41.0));
            Assert.True(driver.SupportExpired(plan, TestParameters.P2Now + 42.0));
        }

        // 保护已到位后不得提前截断辅助仍在进行的窗口。
        [Fact]
        public void ReadyGuardDoesNotTruncateSupportWindow()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            driver.GuardTask.WrittenAt = TestParameters.P2Now;
            driver.SupportTask.WrittenAt = TestParameters.P2Now + 10.0;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            // 保护到位且已建立连续窗口。
            driver.Observe(TestParameters.P2Now, guard, null);
            var after = driver.Observe(TestParameters.P2Now + 1.0, guard, null);
            Assert.True(driver.GuardReadiness.Ready);
            Assert.True(after.GuardReady);

            // 保护窗口在其自身二十秒后已过，但因已就绪不得算超时；
            // 辅助窗口自 210 起算，到 230 才到期。
            Assert.False(driver.GuardExpired(plan, TestParameters.P2Now + 25.0));
            Assert.False(driver.SupportExpired(plan, TestParameters.P2Now + 25.0));
            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 25.0));
            Assert.True(driver.SupportExpired(plan, TestParameters.P2Now + 30.0));
        }

        [Fact]
        public void RelationshipDeadlineDoesNotExpireAfterRelease()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);

            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);

            Assert.False(driver.RelationshipExpired(plan, TestParameters.P2Now + 60.0));
        }

        // 只喂位置读数不够：保护命令尚未写入时不得释放主力推进。
        // 这正是独立验收缺陷 1 的反例（旧实现只要两次位置读数就放行）。
        [Fact]
        public void UnwrittenCommandDoesNotReleaseMainAdvance()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            // 保护/辅助的位置读数连续到位，但命令从未写入。
            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            var unwritten = driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.True(unwritten.GuardReady);
            Assert.True(unwritten.SupportReady);
            Assert.False(unwritten.MainAdvanceAllowed);
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0));

            // 命令写入并生效后才允许释放。
            CommandsInEffect(plan, driver, TestParameters.P2Now + 1.5);
            var released = driver.Observe(TestParameters.P2Now + 2.0, guard, support);
            Assert.True(released.MainAdvanceAllowed);
            Assert.NotNull(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 2.0));
        }

        // 命令写入后目标点若已偏离到位区，同样不得释放（有效目标仍须复核）。
        [Fact]
        public void CommandTargetOutsideBandDoesNotReleaseMainAdvance()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);
            // 当前实际命令目标点偏出保护到位区（前向偏移远超上限），请求点不变。
            driver.GuardTask.SetOrderTarget(g.MainX, g.MainY + 200.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);

            Assert.False(outcome.MainAdvanceAllowed);
            Assert.Null(driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0));
        }

        // 同一计划再次释放推进必须换新身份：阶段转换不增 revision，
        // 复用旧 ID 会让第二次真实获选被自己的首次写令记录拒绝（独立验收缺陷 2）。
        [Fact]
        public void SecondAdvanceReleaseUsesNewIdentity()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var first = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(first);

            // 依赖失效触发一次重分配：回到等待阶段并推进版本。
            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 2.0);
            CommandsInEffect(plan, driver, TestParameters.P2Now + 3.0);

            var guard2 = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support2 = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now + 3.0, guard2, support2);
            driver.Observe(TestParameters.P2Now + 4.0, guard2, support2);
            var second = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 4.0);

            Assert.NotNull(second);
            Assert.NotEqual(first.TaskId, second.TaskId);
        }

        // 写令前的依赖复核只适用于主力推进任务。重分配产生的新主力等待任务
        // 来源同为 PhaseTransition，但那一刻新的保护/辅助尚未写令，按推进任务
        // 复核必然不过 —— 实测 p2-A-20261014-022054 因此让重分配后的三条新任务
        // 整场没有写出任何命令。
        [Fact]
        public void AdvanceDependencyRecheckOnlyAppliesToAdvanceTask()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);
            Assert.True(CoordinationDriver.RequiresAdvanceDependencyRecheck(advance));

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 2.0);

            var mainWait = driver.MainWaitTask;
            Assert.NotNull(mainWait);
            Assert.NotSame(advance, mainWait);
            Assert.Equal(FormationRole.Main, mainWait.Role);
            Assert.Equal(TaskKind.Hold, mainWait.Kind);
            Assert.False(CoordinationDriver.RequiresAdvanceDependencyRecheck(mainWait));
        }

        // 重分配后依赖集必须指向本版本的任务：沿用旧 ID 会让新分配
        // 仍然挂着已关闭的旧主力任务（独立验收缺陷 4）。
        [Fact]
        public void ReassignmentRebindsDependenciesToCurrentTasks()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            string oldMainId = driver.MainWaitTask.TaskId;

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 5.0);

            Assert.NotEqual(oldMainId, driver.MainWaitTask.TaskId);
            Assert.Equal(driver.MainWaitTask.TaskId, driver.GuardTask.GuardsTaskId);
            Assert.Equal(driver.MainWaitTask.TaskId, driver.GuardTask.DependsOnTaskId);
            Assert.Equal(driver.MainWaitTask.TaskId, driver.SupportTask.SupportsTaskId);
            Assert.Equal(driver.MainWaitTask.TaskId, driver.SupportTask.DependsOnTaskId);
            Assert.Equal("", driver.MainWaitTask.DependsOnTaskId);
        }

        // ---- 职责重分配：换对象必须增 revision 并重绑 ----

        [Fact]
        public void RebindingRoleBumpsRevisionAndClosesOldTask()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var old = driver.GuardTask;
            int revisionBefore = plan.Revision;

            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            // 版本由计划级重分配入口统一递增，调用方不再自行 BumpRevision。
            var next = driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 5.0);

            Assert.NotNull(next);
            Assert.True(old.IsClosed);
            Assert.Equal(old.TaskId, next.ReplacesTaskId);
            Assert.Equal(revisionBefore + 1, plan.Revision);
            Assert.Equal(plan.Revision, next.Revision);
            Assert.Equal(12, next.RoleBaselineUnits);
            Assert.Equal(replacement.Key(), next.FormationKey);
            Assert.Same(next, driver.GuardTask);
        }

        // 重分配后三任务版本必须一致，且各自 ID 不同。
        [Fact]
        public void ReassignmentKeepsThreeTaskRevisionsConsistent()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var old = driver.GuardTask;

            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 5.0);

            Assert.Equal(plan.Revision, driver.MainWaitTask.Revision);
            Assert.Equal(plan.Revision, driver.GuardTask.Revision);
            Assert.Equal(plan.Revision, driver.SupportTask.Revision);
            Assert.NotEqual(driver.MainWaitTask.TaskId, driver.GuardTask.TaskId);
            Assert.NotEqual(driver.GuardTask.TaskId, driver.SupportTask.TaskId);
        }

        // 换保护或辅助必须重置就绪窗口，不得沿用旧窗口直接释放主力。
        [Fact]
        public void ReplacingGuardResetsReadinessWindow()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            Assert.True(driver.GuardReadiness.Ready);

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 2.0);

            Assert.False(driver.GuardReadiness.Ready);
            Assert.Equal(0, driver.GuardReadiness.ReadCount);
        }

        // ---- 重分配后的结构完整性（实机第三轮暴露的三处缺陷） ----

        // 替补任务必须进入 plan.Tasks，否则发布与裁决都看不到它。
        [Fact]
        public void ReplacementTaskIsAddedToPlan()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var old = driver.GuardTask;

            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            var next = driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 5.0);

            Assert.NotNull(next);
            Assert.Contains(next, plan.Tasks);
            Assert.Equal(plan.Revision, next.Revision);
        }

        // 推进阶段依赖失效后重分配，三职责必须仍然齐全，
        // 否则依赖再次成立时没有等待任务可转为推进。
        [Fact]
        public void ReassignmentDuringAdvanceRestoresAllThreeRoles()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);
            Assert.True(driver.MainWaitTask.IsClosed);

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 2.0);

            Assert.NotNull(driver.MainWaitTask);
            Assert.False(driver.MainWaitTask.IsClosed);
            Assert.Equal(plan.Revision, driver.MainWaitTask.Revision);
            Assert.NotNull(driver.GuardTask);
            Assert.NotNull(driver.SupportTask);
            Assert.Null(driver.AdvanceTask);
            Assert.True(advance.IsClosed);
        }

        // 旧推进任务必须被关闭：否则它作为旧 revision 的开放普通任务
        // 会一直充当前序依赖，把新职责的写令永久拖住。
        [Fact]
        public void StaleAdvanceTaskIsClosedOnReassignment()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 2.0);

            Assert.True(advance.IsClosed);
            Assert.Empty(driver.AdvanceTaskId);
        }

        // 未换职责的编队也要清裁决槽：否则新 ID 任务会被槽内旧任务永久判为"被取代"。
        [Fact]
        public void ReassignmentClearsSlotsOfUnchangedRolesToo()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var supportKey = driver.SupportTask.FormationKey;

            driver.Arbiter.Submit(
                Proposal.Create(plan.MissionKey, plan, driver.SupportTask, TaskKind.Reposition,
                    ProposalSource.TaskSubmission, 1.0, 2.0, "seed", TestParameters.P2Now),
                plan, driver.SupportTask);
            driver.Arbiter.PublishBatch(supportKey);
            Assert.NotNull(driver.Arbiter.CurrentFor(supportKey));

            var old = driver.GuardTask;
            var replacement = Rec(0, 3, "Cavalry", 12, 10.0, 10.0, false);
            driver.ReassignRole(plan, old, FormationRole.Guard, replacement, TestParameters.P2Now + 5.0);

            Assert.Null(driver.Arbiter.CurrentFor(supportKey));
        }

        // ---- 依赖失效的职责归类 ----
        //
        // 前提失效为 None 不等于该职责仍然成立：到位窗口断开、命令被改写都会让它
        // 失去依据。归类必须按实际读数，不能默认落到另一侧（实机
        // p3-P-20261017-181046 在 131.041 把 Guard 越界报成了 role=Support failure=None）。

        [Fact]
        public void GuardWindowBreakWithNoPremiseFailureIsReportedAsGuard()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            Assert.True(driver.Observe(TestParameters.P2Now + 1.0, guard, support).MainAdvanceAllowed);

            // 保护编队刚越过到位半径：前提（人数/资格/对象）完全正常，只是不在位。
            var strayed = At(g, driver.GuardTask, g.GuardX, g.GuardY + 5.2, true);
            var outcome = driver.Observe(TestParameters.P2Now + 2.0, strayed, support);

            Assert.False(outcome.MainAdvanceAllowed);
            Assert.Equal(RolePremiseFailure.None, outcome.GuardFailure);
            Assert.Equal(RoleDependencyLoss.NotReady, outcome.GuardLoss);
            Assert.Equal(RoleDependencyLoss.None, outcome.SupportLoss);
            FormationRole role;
            var kind = CoordinationDriver.LostRoleOf(outcome, out role);
            Assert.Equal(FormationRole.Guard, role);
            Assert.Equal(RoleDependencyLoss.NotReady, kind);
        }

        [Fact]
        public void SupportWindowBreakWithNoPremiseFailureIsReportedAsSupport()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            Assert.True(driver.Observe(TestParameters.P2Now + 1.0, guard, support).MainAdvanceAllowed);

            // 辅助编队离开到位区，保护不受影响。
            var strayed = At(g, driver.SupportTask, g.SupportX, g.SupportY + 9.0, false);
            var outcome = driver.Observe(TestParameters.P2Now + 2.0, guard, strayed);

            Assert.False(outcome.MainAdvanceAllowed);
            Assert.Equal(RolePremiseFailure.None, outcome.SupportFailure);
            Assert.Equal(RoleDependencyLoss.NotReady, outcome.SupportLoss);
            Assert.Equal(RoleDependencyLoss.None, outcome.GuardLoss);
            FormationRole role;
            var kind = CoordinationDriver.LostRoleOf(outcome, out role);
            Assert.Equal(FormationRole.Support, role);
            Assert.Equal(RoleDependencyLoss.NotReady, kind);
        }

        // 命令仍被原生改写：前提与位置都正常，只有命令不再成立。
        [Fact]
        public void GuardCommandOverrideIsReportedAsCommandInvalid()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            Assert.True(driver.Observe(TestParameters.P2Now + 1.0, guard, support).MainAdvanceAllowed);

            // 原生把保护改成别的目标：读回与本次请求偏离。
            driver.GuardTask.SetOrderTarget(g.GuardX + 30.0, g.GuardY);
            var outcome = driver.Observe(TestParameters.P2Now + 2.0, guard, support);

            Assert.False(outcome.MainAdvanceAllowed);
            Assert.Equal(RoleDependencyLoss.CommandInvalid, outcome.GuardLoss);
            Assert.True(DependencyReadiness.IsLost(outcome.GuardLoss));
            FormationRole role;
            Assert.Equal(RoleDependencyLoss.CommandInvalid, CoordinationDriver.LostRoleOf(outcome, out role));
            Assert.Equal(FormationRole.Guard, role);
        }

        // 前提失效仍是更硬的原因：它必须盖住同一侧的窗口/命令判定。
        [Fact]
        public void PremiseFailureOutranksWindowAndCommandLoss()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            CommandsInEffect(plan, driver, TestParameters.P2Now - 1.0);

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, 5, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            var outcome = driver.Observe(TestParameters.P2Now + 1.0, guard, support);

            Assert.Equal(RolePremiseFailure.StrengthBelowLine, outcome.GuardFailure);
            Assert.Equal(RoleDependencyLoss.PremiseFailure, outcome.GuardLoss);
            FormationRole role;
            Assert.Equal(RoleDependencyLoss.PremiseFailure, CoordinationDriver.LostRoleOf(outcome, out role));
            Assert.Equal(FormationRole.Guard, role);
        }

        private static FormationRecord DriftedRole(BattleState state, FormationTask task)
        {
            var record = state.MyFormations.Find(r => r.Key() == task.FormationKey);
            record.PosX = task.RequestedX;
            record.PosY = task.RequestedY;
            record.ActiveCenterX = task.CanonicalX + 12.0;
            record.ActiveCenterY = task.CanonicalY;
            return record;
        }

        [Fact]
        public void RoleCorrectionReplacesOnlyDriftedTaskWithFreshWriteIdentity()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var old = driver.GuardTask;
            var healthy = driver.SupportTask;
            var actual = DriftedRole(state, old);
            var correction = driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 205.0, 201.5);
            Assert.NotNull(correction);
            Assert.True(old.IsClosed);
            Assert.NotEqual(old.TaskId, correction.TaskId);
            Assert.Equal(-1.0, correction.WrittenAt);
            Assert.Equal(old.TaskId, correction.ReplacesTaskId);
            Assert.Equal(old.CanonicalX, correction.CanonicalX);
            Assert.Equal(old.CanonicalY, correction.CanonicalY);
            Assert.Same(healthy, driver.SupportTask);
            Assert.False(healthy.IsClosed);
            Assert.Equal(200.0, healthy.WrittenAt);
            Assert.Equal(1, plan.Revision);
            Assert.Contains(correction.TaskId, driver.MainWaitTask.ReplacesTaskId);
            Assert.DoesNotContain("|" + old.TaskId + "|", driver.MainWaitTask.ReplacesTaskId);
        }

        [Theory]
        [InlineData("identity")]
        [InlineData("center_unknown")]
        [InlineData("anchor_not_arrived")]
        [InlineData("already_in_position")]
        [InlineData("command_changed")]
        [InlineData("too_late")]
        [InlineData("ineligible")]
        public void RoleCorrectionRejectsUnprovedOrUnbudgetedChange(string problem)
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var old = driver.GuardTask;
            var actual = DriftedRole(state, old);
            double now = 205.0;
            if (problem == "identity") actual.ObjectIdentity = "another-object";
            if (problem == "center_unknown") actual.HasActiveCenter = false;
            if (problem == "anchor_not_arrived") actual.PosX += 2.0;
            if (problem == "already_in_position")
            { actual.ActiveCenterX = old.CanonicalX; actual.ActiveCenterY = old.CanonicalY; }
            if (problem == "command_changed") old.SetOrderTarget(old.RequestedX + 5.0, old.RequestedY);
            if (problem == "too_late") now = 218.0;
            if (problem == "ineligible") actual.IsAiControlled = false;
            Assert.Null(driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, now, 201.5));
            Assert.False(old.IsClosed);
        }

        // 实机 p3-P-20261018-102648：保护 5.134 秒写令、5.134~8.134 秒反馈窗口内一直是 Pending，
        // 8.135 秒直接 NotEffective 关闭计划。纠偏若要求"反馈已生效"，窗口内永远不成立，
        // 而反馈失败的那一刻计划已被关闭——该职责的纠偏在构造上不可达。
        // 已批准条件是「命令/对象/Plan/Revision 有效」，不是「反馈已生效」。
        [Fact]
        public void RoleCorrectionIsReachableWhileTheFeedbackWindowIsStillOpen()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var old = driver.GuardTask;
            // 写令已落地（读回等于本次请求），但 3 秒反馈窗口还没给出结论。
            old.Feedback = TaskFeedbackState.Pending;
            old.FailureReason = PlanInvalidReason.None;
            var actual = DriftedRole(state, old);
            Assert.Equal(203.0, old.FeedbackDeadline());
            var correction = driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 203.0, 201.5);
            Assert.NotNull(correction);
            Assert.True(old.IsClosed);
            Assert.Equal(-1.0, correction.WrittenAt);
        }

        [Fact]
        public void EachRoleGetsOnlyOneCorrectionAndHealthySideRemainsIndependent()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var actual = DriftedRole(state, driver.GuardTask);
            var correction = driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 205.0, 201.5);
            Assert.NotNull(correction);
            correction.WrittenAt = 207.0;
            correction.Feedback = TaskFeedbackState.Effective;
            correction.SetTarget(actual.ActiveCenterX, actual.ActiveCenterY, actual.PosX, actual.PosY);
            correction.SetOrderTarget(actual.PosX, actual.PosY);
            Assert.Null(driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 209.0, 207.0));
            var support = DriftedRole(state, driver.SupportTask);
            Assert.NotNull(driver.TryCreateRoleCorrection(plan, FormationRole.Support, support, 209.0, 207.0));
        }

        [Fact]
        public void PendingCorrectionStillExpiresAtOriginalRelationshipDeadline()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var actual = DriftedRole(state, driver.GuardTask);
            Assert.NotNull(driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 205.0, 201.5));
            Assert.Equal(220.0, driver.GuardDeadline());
            Assert.False(driver.GuardExpired(plan, 219.999));
            Assert.True(driver.GuardExpired(plan, 220.0));
        }

        [Fact]
        public void CorrectedCommandCanReleaseAdvanceWithoutExtendingDeadline()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var actual = DriftedRole(state, driver.GuardTask);
            var correction = driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 205.0, 201.5);
            Assert.NotNull(correction);
            double ax = FormationTask.AnchoredRequest(correction.CanonicalX, actual.PosX, actual.ActiveCenterX);
            double ay = FormationTask.AnchoredRequest(correction.CanonicalY, actual.PosY, actual.ActiveCenterY);
            correction.SetTarget(actual.ActiveCenterX, actual.ActiveCenterY, ax, ay);
            correction.SetOrderTarget(ax, ay);
            correction.WrittenAt = 207.0;
            correction.Feedback = TaskFeedbackState.Effective;
            Assert.Equal(220.0, driver.GuardDeadline());
            var g = plan.Geometry;
            var guard = At(g, correction, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(208.0, guard, support);
            Assert.True(driver.Observe(209.0, guard, support).MainAdvanceAllowed);
            Assert.NotNull(driver.CreateAdvanceTask(plan, 209.0));
            Assert.Equal(CoordinationPhase.MainAdvancing, driver.Phase);
        }

        [Fact]
        public void UnwrittenCorrectionCannotOutliveDeadlineOnNativePositionReadiness()
        {
            BattlePlan plan; BattleState state;
            var driver = BoundDriver(out plan, out state);
            CommandsInEffect(plan, driver, 200.0);
            var actual = DriftedRole(state, driver.GuardTask);
            var correction = driver.TryCreateRoleCorrection(plan, FormationRole.Guard, actual, 205.0, 201.5);
            Assert.NotNull(correction);
            var g = plan.Geometry;
            var guard = At(g, correction, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(208.0, guard, support);
            var outcome = driver.Observe(209.0, guard, support);
            Assert.True(outcome.GuardReady);
            Assert.False(outcome.MainAdvanceAllowed);
            Assert.True(driver.GuardExpired(plan, 220.0));
        }

        [Fact]
        public void CorrectionWriteGateRejectsSelectionAtOriginalDeadline()
        {
            var facts = new DeliveryFacts
            {
                HasPlan = true, Now = 220.0, PlanCreatedAt = 200.0,
                AssignedAt = 205.0, LastWriteAt = 210.0, RelationshipDeadlineAt = 220.0,
            };
            Assert.Equal("relationship_deadline", TaskDelivery.TimingDeferral(facts));
            facts.Now = 219.999;
            Assert.Equal("", TaskDelivery.TimingDeferral(facts));
        }

        [Fact]
        public void FailedCoordinationDoesNotRecreateOnTimeOrPositionJitter()
        {
            var state = SceneA(); var ledger = new PlanLedger();
            var plan = ledger.CreateCoordination(state, 200.0);
            ledger.RememberFailedCoordination(state);
            ledger.Invalidate(PlanInvalidReason.NotEffective, "relationship_not_established", 225.0);
            state.SampledAt = 230.0; state.PlanInputId = "another-input";
            foreach (var r in state.MyFormations) { r.PosX += 0.02; r.ActiveCenterX += 0.02; }
            Assert.Null(ledger.CreateCoordination(state, 230.0));
            Assert.Same(plan, ledger.Current);
            Assert.Equal(1, ledger.PlanCount);
            state.MyFormations[0].MemberDigest += "-new-member";
            Assert.NotNull(ledger.CreateCoordination(state, 233.0));
            ledger.ClearMission();
            Assert.NotNull(ledger.CreateCoordination(state, 0.0));
        }

        // ---- Mission 清理 ----

        [Fact]
        public void ClearMissionResetsPhaseAndReadiness()
        {
            BattlePlan plan;
            BattleState state;
            var driver = BoundDriver(out plan, out state);
            var g = plan.Geometry;
            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            driver.Observe(TestParameters.P2Now, guard, support);
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);

            driver.ClearMission();

            Assert.Equal(CoordinationPhase.None, driver.Phase);
            Assert.Equal(0, driver.GuardReadiness.ReadCount);
            Assert.Equal(0, driver.SupportReadiness.ReadCount);
            Assert.Null(driver.MainWaitTask);
            Assert.Null(driver.GuardTask);
            Assert.Null(driver.SupportTask);
            Assert.Null(driver.AdvanceTask);
        }
    }
}
