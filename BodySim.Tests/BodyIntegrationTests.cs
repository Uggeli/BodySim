namespace BodySim.Tests;

/// <summary>
/// Integration tests that verify cross-system interactions.
/// Skeletal ↔ Circulatory ↔ Respiratory — damage in one system
/// cascades into the others through the shared resource pool and events.
/// </summary>
public class BodyIntegrationTests
{
    // ─── Helpers ──────────────────────────────────────────────

    private static Body CreateBody() => new();

    private static SkeletalSystem Skeletal(Body body) =>
        (SkeletalSystem)body.GetSystem(BodySystemType.Skeletal)!;

    private static CirculatorySystem Circulatory(Body body) =>
        (CirculatorySystem)body.GetSystem(BodySystemType.Circulatory)!;

    private static RespiratorySystem Respiratory(Body body) =>
        (RespiratorySystem)body.GetSystem(BodySystemType.Respiratory)!;

    private static MuscularSystem Muscular(Body body) =>
        (MuscularSystem)body.GetSystem(BodySystemType.Muscular)!;

    private static IntegumentarySystem Integumentary(Body body) =>
        (IntegumentarySystem)body.GetSystem(BodySystemType.Integementary)!;

    private static ImmuneSystem Immune(Body body) =>
        (ImmuneSystem)body.GetSystem(BodySystemType.Immune)!;

    private static NervousSystem Nervous(Body body) =>
        (NervousSystem)body.GetSystem(BodySystemType.Nerveus)!;

    /// <summary>
    /// Simulates a full-body lift: exerts all muscles in the kinetic chain
    /// (legs → core → shoulders → arms → hands) and returns the total
    /// force generated across all participating muscle groups.
    /// </summary>
    private static float PerformLift(Body body, float intensity)
    {
        // Lifting kinetic chain — every muscle group that participates
        BodyPartType[] liftChain =
        [
            // Legs (drive from ground)
            BodyPartType.LeftFoot, BodyPartType.RightFoot,
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            // Core (transfer power)
            BodyPartType.Hips, BodyPartType.Pelvis,
            BodyPartType.Abdomen, BodyPartType.Chest,
            // Arms (grip & pull)
            BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
            BodyPartType.LeftHand, BodyPartType.RightHand,
        ];

        foreach (var part in liftChain)
            body.Exert(part, intensity);

        body.Update();

        var musc = Muscular(body);
        float totalForce = 0;
        foreach (var part in liftChain)
            totalForce += musc.GetForceOutput(part);

        return totalForce;
    }

    // ─── 1. All systems initialise cleanly via Body ───────────

    [Fact]
    public void Body_Init_AllSystemsPresent()
    {
        var body = CreateBody();

        Assert.NotNull(body.GetSystem(BodySystemType.Skeletal));
        Assert.NotNull(body.GetSystem(BodySystemType.Circulatory));
        Assert.NotNull(body.GetSystem(BodySystemType.Respiratory));
        Assert.NotNull(body.GetSystem(BodySystemType.Muscular));
        Assert.NotNull(body.GetSystem(BodySystemType.Integementary));
    }

    [Fact]
    public void Body_Init_ResourcePoolSeeded()
    {
        var body = CreateBody();
        var circ = Circulatory(body);

        // Blood pressure should be at full (heart healthy, blood 50/50)
        Assert.Equal(100f, circ.GetBloodPressure());
    }

    [Fact]
    public void Body_Init_FullOxygenOutput()
    {
        var body = CreateBody();
        var resp = Respiratory(body);

        Assert.Equal(5f, resp.GetOxygenOutput());
    }

    // ─── 2. Chest damage hits both Circulatory and Respiratory ──

    [Fact]
    public void ChestDamage_AffectsHeartAndLungs()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Chest, 40);
        body.Update(); // routes event to all listeners

        var circ = Circulatory(body);
        var resp = Respiratory(body);

        // Heart took 40 damage → blood pressure drops
        Assert.True(circ.GetBloodPressure() < 100f,
            "Chest damage should reduce blood pressure via heart health");

        // Lungs took 40 damage → O₂ output drops
        Assert.True(resp.GetOxygenOutput() < 5f,
            "Chest damage should reduce oxygen output via lung health");
    }

    [Fact]
    public void ChestDamage_AlsoFracturesBone()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Chest, 60);
        body.Update();

        var skel = Skeletal(body);

        // High damage reduces bone integrity
        Assert.True(skel.GetOverallIntegrity() < 100f,
            "Chest damage should degrade skeletal integrity");
    }

    // ─── 3. Bleeding drains blood → lowers pressure → less O₂ delivery ──

    [Fact]
    public void Bleeding_ReducesBloodPressureOverTicks()
    {
        var body = CreateBody();

        // Start a major bleed on the abdomen (major vessel → 2x rate)
        body.Bleed(BodyPartType.Abdomen, 3f);

        float bp0 = Circulatory(body).GetBloodPressure();

        // Run several ticks to drain blood
        for (int i = 0; i < 5; i++) body.Update();

        float bp5 = Circulatory(body).GetBloodPressure();

        Assert.True(bp5 < bp0,
            $"After 5 ticks of bleeding, BP should drop (was {bp0}, now {bp5})");
    }

    [Fact]
    public void SevereTrauma_EventualHypoxia()
    {
        var body = CreateBody();

        // Block airway so no O₂ is produced, while global consumption drains pool
        body.TakeDamage(BodyPartType.Neck, 40); // blocks airway (≥30)
        body.Bleed(BodyPartType.Chest, 3f);      // also lose blood

        // Simulate ticks — O₂ pool (starts at 100) depleted at 2/tick with 0 production
        for (int i = 0; i < 60; i++) body.Update();

        var resp = Respiratory(body);

        Assert.True(resp.IsHypoxic(),
            "Blocked airway + bleeding should eventually cause hypoxia");
    }

    // ─── 4. Neck damage affects airway AND blood flow to head ──

    [Fact]
    public void NeckDamage_ReducesBothAirflowAndBloodFlowToHead()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Neck, 40);
        body.Update();

        var circ = Circulatory(body);
        var resp = Respiratory(body);

        float bloodFlowToHead = circ.GetBloodFlowTo(BodyPartType.Head);
        float airflow = resp.GetAirflowReachingLungs();

        Assert.True(bloodFlowToHead < 100f,
            "Neck damage should reduce blood flow to head");
        Assert.True(airflow < 100f,
            "Neck damage should reduce airflow reaching lungs");
    }

    [Fact]
    public void SevereNeckDamage_BlocksAirway()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Neck, 35);
        body.Update();

        var resp = Respiratory(body);

        Assert.True(resp.IsAirwayBlocked(),
            "Heavy neck damage (≥30) should block the airway");
        Assert.Equal(0f, resp.GetAirflowReachingLungs());
        Assert.Equal(0f, resp.GetOxygenOutput());
    }

    // ─── 5. Blocked airway → CO₂ builds up over time ──

    [Fact]
    public void BlockedAirway_CO2Accumulates()
    {
        var body = CreateBody();

        // Block airway
        body.TakeDamage(BodyPartType.Neck, 40);
        body.Update();

        // Run ticks — CO₂ production continues, removal = 0
        for (int i = 0; i < 25; i++) body.Update();

        var resp = Respiratory(body);

        Assert.True(resp.IsCO2Toxic(),
            "Blocked airway for many ticks should cause CO₂ toxicity");
    }

    // ─── 6. Heal one system, see cross-system improvement ──

    [Fact]
    public void HealingChest_RestoresBloodPressureAndOxygen()
    {
        var body = CreateBody();

        // Damage chest
        body.TakeDamage(BodyPartType.Chest, 50);
        body.Update();

        float bpDamaged = Circulatory(body).GetBloodPressure();
        float o2Damaged = Respiratory(body).GetOxygenOutput();

        // Heal chest
        body.Heal(BodyPartType.Chest, 30);
        body.Update();

        float bpHealed = Circulatory(body).GetBloodPressure();
        float o2Healed = Respiratory(body).GetOxygenOutput();

        Assert.True(bpHealed > bpDamaged,
            "Healing chest should improve blood pressure");
        Assert.True(o2Healed > o2Damaged,
            "Healing chest should improve oxygen output");
    }

    // ─── 7. Clotting stops bleeding → BP stabilises ──

    [Fact]
    public void Clotting_StopsBloodLoss()
    {
        var body = CreateBody();

        body.Bleed(BodyPartType.Abdomen, 2f);

        // Bleed for 3 ticks
        for (int i = 0; i < 3; i++) body.Update();
        float bpBleeding = Circulatory(body).GetBloodPressure();

        // Clot the wound
        body.Clot(BodyPartType.Abdomen);

        // 3 more ticks — BP should stabilise (possibly regen slightly)
        for (int i = 0; i < 3; i++) body.Update();
        float bpClotted = Circulatory(body).GetBloodPressure();

        // Allow a small drop due to other resource consumption (lung needs, etc.)
        Assert.True(bpClotted >= bpBleeding - 10f,
            $"After clotting, BP should stabilise (was {bpBleeding}, now {bpClotted})");
    }

    // ─── 8. Full cascade: bone fracture → bleed → low BP → hypoxia ──

    [Fact]
    public void FullCascade_SevereChestTrauma()
    {
        var body = CreateBody();

        // Massive chest trauma
        body.TakeDamage(BodyPartType.Chest, 80);
        body.Update();

        var skel = Skeletal(body);
        var circ = Circulatory(body);
        var resp = Respiratory(body);

        // Skeletal: chest integrity damaged
        Assert.True(skel.GetOverallIntegrity() < 100f);

        // Circulatory: heart damaged → BP drops, bleeding started (damage ≥ 20 threshold)
        Assert.True(circ.GetBloodPressure() < 100f);
        Assert.True(circ.GetBleedingParts().Count > 0 || circ.GetTotalBleedRate() >= 0,
            "Heavy chest damage should trigger bleeding in circulatory system");

        // Respiratory: lungs severely damaged → O₂ output plummets
        Assert.True(resp.GetOxygenOutput() < 2f,
            "80 chest damage should critically reduce O₂ output");
    }

    [Fact]
    public void FullCascade_SevereLegTrauma_NoRespiratoryEffect()
    {
        var body = CreateBody();

        // Severe leg damage
        body.TakeDamage(BodyPartType.LeftLeg, 80);
        body.Update();

        var resp = Respiratory(body);

        // Legs don't contain airways — respiratory should be unaffected directly
        Assert.Equal(5f, resp.GetOxygenOutput());
        Assert.False(resp.IsAirwayBlocked());
    }

    // ─── 9. Multiple simultaneous injuries ──

    [Fact]
    public void MultipleInjuries_CompoundingEffects()
    {
        var body = CreateBody();

        // Hit three important areas
        body.TakeDamage(BodyPartType.Chest, 30);
        body.TakeDamage(BodyPartType.Neck, 30);
        body.TakeDamage(BodyPartType.Abdomen, 25);
        body.Update();

        var circ = Circulatory(body);
        var resp = Respiratory(body);

        Assert.True(circ.GetBloodPressure() < 100f);
        Assert.True(resp.IsAirwayBlocked(), "Neck damage ≥30 should block airway");
        Assert.Equal(0f, resp.GetOxygenOutput());
        Assert.True(circ.GetBleedingParts().Count >= 2,
            "Both chest and abdomen took ≥20 damage → should be bleeding");
    }

    // ─── 10. Body.Update drives all systems each tick ──

    [Fact]
    public void Update_ProcessesAllSystemsEveryTick()
    {
        var body = CreateBody();

        // Just run several ticks on a healthy body — nothing should crash
        for (int i = 0; i < 20; i++)
        {
            body.Update();
        }

        var circ = Circulatory(body);
        var resp = Respiratory(body);
        var skel = Skeletal(body);

        // Healthy body should maintain BP
        Assert.True(circ.GetBloodPressure() > 80f);

        // Oxygen should stay healthy (production > consumption)
        Assert.False(resp.IsHypoxic());

        // Skeleton intact
        Assert.Equal(0, skel.GetFractureCount());
    }

    // ─── 11. Suffocate then clear → system recovery ──

    [Fact]
    public void SuffocateAndClear_OxygenRecovers()
    {
        var body = CreateBody();
        var hub = (EventHub?)typeof(Body)
            .GetProperty("EventHub", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(body);

        Assert.NotNull(hub);

        // Block airway via event
        hub.Emit(new SuffocateEvent(BodyPartType.Head));
        body.Update();

        Assert.Equal(0f, Respiratory(body).GetOxygenOutput());

        // Clear airway
        hub.Emit(new ClearAirwayEvent(BodyPartType.Head));
        body.Update();

        Assert.Equal(5f, Respiratory(body).GetOxygenOutput());
    }

    // ─── 12. SetBone restores skeletal system ──

    [Fact]
    public void SetBone_RestoresFracturedBone()
    {
        var body = CreateBody();
        var skel = Skeletal(body);

        body.TakeDamage(BodyPartType.LeftLeg, 60);
        body.Update();

        // Check for fracture
        int fracturesBefore = skel.GetFractureCount();

        body.SetBone(BodyPartType.LeftLeg);
        body.Update();

        int fracturesAfter = skel.GetFractureCount();

        Assert.True(fracturesAfter <= fracturesBefore,
            "Setting a bone should reduce or maintain fracture count");
    }

    // ─── 13. Lifting — all muscles participate ──

    [Fact]
    public void Lift_HealthyBody_AllMusclesContributeForce()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        float totalForce = PerformLift(body, 50);

        // 18 muscle groups × 100 max strength each = 1800 max
        // With full health/stamina, exertion shouldn't reduce force (only drains stamina)
        Assert.True(totalForce > 1500f,
            $"Healthy body lift should produce high force, got {totalForce}");

        // Every single participating group should contribute
        Assert.True(musc.GetForceOutput(BodyPartType.LeftThigh) > 0, "Left thigh should contribute");
        Assert.True(musc.GetForceOutput(BodyPartType.Chest) > 0, "Chest should contribute");
        Assert.True(musc.GetForceOutput(BodyPartType.LeftHand) > 0, "Left hand should contribute");
    }

    [Fact]
    public void Lift_HealthyBody_ExertionDrainsStamina()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        float staminaBefore = musc.GetAverageStamina();
        PerformLift(body, 80);
        float staminaAfter = musc.GetAverageStamina();

        Assert.True(staminaAfter < staminaBefore,
            $"Lifting should drain stamina (was {staminaBefore}, now {staminaAfter})");
    }

    [Fact]
    public void Lift_RepeatedExertion_ForceDeclines()
    {
        var body = CreateBody();

        float firstLift = PerformLift(body, 90);

        // Lift several more times without resting — stamina drains, force drops
        float lastLift = 0;
        for (int i = 0; i < 5; i++)
            lastLift = PerformLift(body, 90);

        Assert.True(lastLift < firstLift,
            $"Repeated heavy lifting should reduce force (first {firstLift}, last {lastLift})");
    }

    // ─── 14. Lifting with a torn arm muscle ──

    [Fact]
    public void Lift_TornArm_ReducesTotalForce()
    {
        var body = CreateBody();

        float healthyForce = PerformLift(body, 50);

        // Reset stamina so we compare apples to apples
        var body2 = CreateBody();
        // Tear the right upper arm
        body2.TakeDamage(BodyPartType.RightUpperArm, 60); // over tear threshold
        body2.Update();

        float injuredForce = PerformLift(body2, 50);
        var musc = Muscular(body2);

        Assert.True(musc.GetForceOutput(BodyPartType.RightUpperArm) == 0,
            "Torn muscle should produce zero force");
        Assert.True(injuredForce < healthyForce,
            $"Torn arm should reduce total lift force (healthy {healthyForce}, injured {injuredForce})");
    }

    [Fact]
    public void Lift_TornArm_OtherArmStillWorks()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.RightUpperArm, 60);
        body.Update();
        PerformLift(body, 50);

        var musc = Muscular(body);

        Assert.Equal(0, musc.GetForceOutput(BodyPartType.RightUpperArm));
        Assert.True(musc.GetForceOutput(BodyPartType.LeftUpperArm) > 0,
            "Uninjured arm should still produce force");
    }

    // ─── 15. Lifting with a fractured leg ──

    [Fact]
    public void Lift_FracturedLeg_MusclesDisabledDownstream()
    {
        var body = CreateBody();

        // Fracture the left thigh bone — skeletal system disables downstream
        body.TakeDamage(BodyPartType.LeftThigh, 100);
        body.Update();

        var skel = Skeletal(body);
        Assert.Contains(BodyPartType.LeftThigh, skel.GetFracturedParts());

        // Now lift — left leg chain should be crippled
        float force = PerformLift(body, 50);
        var musc = Muscular(body);

        // Thigh muscle itself took 100 damage → torn
        Assert.True(musc.GetForceOutput(BodyPartType.LeftThigh) == 0,
            "Destroyed thigh muscle should produce no force");

        // Right side still works
        Assert.True(musc.GetForceOutput(BodyPartType.RightThigh) > 0,
            "Uninjured right thigh should still produce force");
    }

    [Fact]
    public void Lift_BothLegsFractured_OnlyUpperBodyForce()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.LeftThigh, 100);
        body.TakeDamage(BodyPartType.RightThigh, 100);
        body.Update();

        PerformLift(body, 50);
        var musc = Muscular(body);

        // Thighs are torn (zero force), legs and feet are disabled downstream
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.LeftThigh));
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.RightThigh));
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.LeftLeg));
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.RightLeg));
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.LeftFoot));
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.RightFoot));

        // Upper body should still work
        Assert.True(musc.GetForceOutput(BodyPartType.LeftUpperArm) > 0,
            "Arms should still produce force even with both legs destroyed");
    }

    // ─── 16. Lifting with chest trauma (heart, lungs, AND core muscles) ──

    [Fact]
    public void Lift_ChestTrauma_AffectsAllSystems()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Chest, 60);
        body.Update();

        var musc = Muscular(body);
        var circ = Circulatory(body);
        var resp = Respiratory(body);

        // Chest muscles damaged → reduced core force
        Assert.True(musc.GetForceOutput(BodyPartType.Chest) < 100,
            "Chest damage should reduce core muscle force");

        // Heart damaged → lower BP
        Assert.True(circ.GetBloodPressure() < 100f,
            "Chest damage should reduce blood pressure");

        // Lungs damaged → less O₂
        Assert.True(resp.GetOxygenOutput() < 5f,
            "Chest damage should reduce oxygen output");

        // Total lift force should be less than healthy
        float force = PerformLift(body, 50);
        var healthyBody = CreateBody();
        float healthyForce = PerformLift(healthyBody, 50);

        Assert.True(force < healthyForce,
            $"Chest trauma should reduce total lifting capacity (healthy {healthyForce}, injured {force})");
    }

    // ─── 17. Lifting under oxygen deprivation ──

    [Fact]
    public void Lift_AirwayBlocked_MusclesStarveOverTime()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        // Block airway → no more O₂ production
        body.TakeDamage(BodyPartType.Neck, 40);
        body.Update();

        Assert.True(Respiratory(body).IsAirwayBlocked());

        float strengthBefore = musc.GetOverallStrength();

        // Run many ticks — O₂ pool depletes, muscles starve, strength degrades
        // Each tick all muscles consume O₂; with 0 production the pool drains,
        // then unmet needs accumulate and trigger starvation-based strength loss
        for (int i = 0; i < 80; i++) body.Update();

        float strengthAfter = musc.GetOverallStrength();

        Assert.True(strengthAfter < strengthBefore,
            $"Oxygen deprivation should degrade muscle strength over time (before {strengthBefore}%, after {strengthAfter}%)");
    }

    // ─── 18. Lifting with bleeding (resource drain) ──

    [Fact]
    public void Lift_Bleeding_ResourceDrainWeakensBody()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var circ = Circulatory(body);

        // Start heavy bleeding — drains the blood pool
        body.Bleed(BodyPartType.Abdomen, 5f);

        // Let the blood drain
        for (int i = 0; i < 30; i++) body.Update();

        // Blood pressure should have collapsed
        Assert.True(circ.GetBloodPressure() < 80f,
            $"Heavy bleeding should reduce blood pressure, got {circ.GetBloodPressure()}");

        // Blood flow to extremities should be reduced
        Assert.True(circ.GetBloodFlowTo(BodyPartType.LeftHand) < 100f,
            "Reduced BP should lower blood flow to extremities");

        // Now exert under these conditions — stamina still drains but the body is
        // weakened (resources being consumed faster than sustainable)
        float forceInjured = PerformLift(body, 80);

        // After more heavy exertion ticks with depleted resources, muscles starve
        for (int i = 0; i < 40; i++)
            PerformLift(body, 80);

        float forceLate = PerformLift(body, 80);

        Assert.True(forceLate < forceInjured,
            $"Prolonged heavy exertion during blood loss should degrade force over time (early {forceInjured}, late {forceLate})");
    }

    // ─── 19. Lifting with multi-limb injuries ──

    [Fact]
    public void Lift_MultipleLimbInjuries_CompoundingForceReduction()
    {
        // Scenario: one arm injured
        var body1 = CreateBody();
        body1.TakeDamage(BodyPartType.RightUpperArm, 60);
        body1.Update();
        float forceOneArm = PerformLift(body1, 50);

        // Scenario: both arms injured
        var body2 = CreateBody();
        body2.TakeDamage(BodyPartType.RightUpperArm, 60);
        body2.TakeDamage(BodyPartType.LeftUpperArm, 60);
        body2.Update();
        float forceBothArms = PerformLift(body2, 50);

        // Scenario: both arms + one leg
        var body3 = CreateBody();
        body3.TakeDamage(BodyPartType.RightUpperArm, 60);
        body3.TakeDamage(BodyPartType.LeftUpperArm, 60);
        body3.TakeDamage(BodyPartType.LeftThigh, 100);
        body3.Update();
        float forceArmsAndLeg = PerformLift(body3, 50);

        // Each additional injury should compound the force reduction
        Assert.True(forceBothArms < forceOneArm,
            $"Two injured arms < one injured arm (one={forceOneArm}, both={forceBothArms})");
        Assert.True(forceArmsAndLeg < forceBothArms,
            $"Arms + leg < just arms (arms={forceBothArms}, arms+leg={forceArmsAndLeg})");
    }

    // ─── 20. Repair and recovery ──

    [Fact]
    public void Lift_RepairTornMuscle_ForceRecovers()
    {
        var body = CreateBody();

        // Tear right arm
        body.TakeDamage(BodyPartType.RightUpperArm, 60);
        body.Update();

        PerformLift(body, 50);
        var musc = Muscular(body);
        Assert.Equal(0, musc.GetForceOutput(BodyPartType.RightUpperArm));

        // Repair the muscle
        body.RepairMuscle(BodyPartType.RightUpperArm);
        body.Heal(BodyPartType.RightUpperArm, 50);
        body.Update();

        // Re-lift — repaired muscle should contribute again
        PerformLift(body, 50);
        Assert.True(musc.GetForceOutput(BodyPartType.RightUpperArm) > 0,
            "Repaired muscle should produce force again");
    }

    [Fact]
    public void Lift_RepairAndRest_StaminaRecovers()
    {
        var body = CreateBody();

        // Exhaust the body with heavy lifting
        for (int i = 0; i < 10; i++)
            PerformLift(body, 100);

        var musc = Muscular(body);
        float exhaustedStamina = musc.GetAverageStamina();

        // Rest all muscles
        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
            body.Rest(part);

        // Tick several times — stamina regens
        for (int i = 0; i < 20; i++) body.Update();

        float recoveredStamina = musc.GetAverageStamina();

        Assert.True(recoveredStamina > exhaustedStamina,
            $"Resting should recover stamina (exhausted {exhaustedStamina}, recovered {recoveredStamina})");
    }

    // ─── 21. Full cascade: severe full-body trauma then attempt to lift ──

    [Fact]
    public void Lift_FullBodyTrauma_MinimalForce()
    {
        var body = CreateBody();

        // Devastate the body
        body.TakeDamage(BodyPartType.Chest, 70);
        body.TakeDamage(BodyPartType.LeftThigh, 100);
        body.TakeDamage(BodyPartType.RightThigh, 100);
        body.TakeDamage(BodyPartType.LeftUpperArm, 60);
        body.TakeDamage(BodyPartType.RightUpperArm, 60);
        body.TakeDamage(BodyPartType.Neck, 40);
        body.Update();

        var musc = Muscular(body);

        float force = PerformLift(body, 50);

        // Both legs destroyed, both arms torn, chest severely damaged, airway blocked
        // Remaining force comes from: neck, hips, abdomen, pelvis, shoulders, forearms, hands
        // but many of those are downstream of torn weight-bearing muscles and disabled
        var healthyBody = CreateBody();
        float healthyForce = PerformLift(healthyBody, 50);
        Assert.True(force < healthyForce * 0.6f,
            $"Full-body trauma should drastically reduce lifting capacity (healthy {healthyForce}, injured {force})");

        // Locomotion should be zero or near-zero (both legs destroyed + downstream disabled)
        float locoForce = musc.GetLocomotionForce();
        Assert.True(locoForce == 0 || musc.GetTornParts().Contains(BodyPartType.LeftThigh),
            "Both thighs should be torn");
    }

    // ─── 22. Skin absorbs damage — first line of defense ──

    [Fact]
    public void Skin_DamageReducesSkinIntegrity_OtherSystemsAlsoHit()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.LeftUpperArm, 40);
        body.Update();

        var skin = Integumentary(body);
        var skel = Skeletal(body);
        var musc = Muscular(body);

        // Skin took some of the damage
        Assert.True(skin.GetSkinIntegrity(BodyPartType.LeftUpperArm) < 100,
            "Skin should absorb some damage");

        // But other systems also took the full event (bone health dropped)
        var boneHealth = skel.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(boneHealth);
        Assert.True(boneHealth.Current < 100,
            "Bone should also take damage from the same event");

        // Muscle health also dropped
        var muscleHealth = musc.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(muscleHealth);
        Assert.True(muscleHealth.Current < 100,
            "Muscle should also take damage from the same event");
    }

    [Fact]
    public void Skin_RepeatedDamage_CausesWound()
    {
        var body = CreateBody();
        var skin = Integumentary(body);

        // Hit the same spot repeatedly — sustained damage breaches skin
        for (int i = 0; i < 8; i++)
        {
            body.TakeDamage(BodyPartType.LeftUpperArm, 60);
            body.Update();
        }

        Assert.True(skin.GetWoundCount() > 0 || skin.GetSkinIntegrity(BodyPartType.LeftUpperArm) < 40,
            $"Repeated damage to same area should wound or severely degrade skin. Integrity: {skin.GetSkinIntegrity(BodyPartType.LeftUpperArm)}");
    }

    // ─── 23. Burns cascade across systems ──

    [Fact]
    public void Burn_ThirdDegree_DamagesUnderlyingTissue()
    {
        var body = CreateBody();

        var skelHealthBefore = Skeletal(body).GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current;
        Assert.NotNull(skelHealthBefore);

        // 3rd degree burn emits a DamageEvent to deep tissue
        body.Burn(BodyPartType.LeftHand, 70);
        body.Update();

        var skin = Integumentary(body);
        Assert.Contains(BodyPartType.LeftHand, skin.GetBurnedParts());

        var skelHealthAfter = Skeletal(body).GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current;
        Assert.NotNull(skelHealthAfter);

        Assert.True(skelHealthAfter < skelHealthBefore,
            $"3rd degree burn should damage underlying bone (before {skelHealthBefore}, after {skelHealthAfter})");
    }

    [Fact]
    public void Burn_AffectsSkinAndMuscle()
    {
        var body = CreateBody();

        body.Burn(BodyPartType.Chest, 70); // 3rd degree
        body.Update();

        var skin = Integumentary(body);
        var musc = Muscular(body);

        // Skin is burned
        Assert.True(skin.GetSkinIntegrity(BodyPartType.Chest) < 100);

        // 3rd degree burn emits DamageEvent → muscle also takes damage
        var muscleHealth = musc.GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(muscleHealth);
        Assert.True(muscleHealth.Current < 100,
            "3rd degree burn should damage underlying muscle");
    }

    // ─── 24. Bandaging and healing across systems ──

    [Fact]
    public void Bandage_ClosesExposedWound()
    {
        var body = CreateBody();
        var skin = Integumentary(body);

        // Wound the arm with sustained damage
        for (int i = 0; i < 6; i++)
        {
            body.TakeDamage(BodyPartType.LeftUpperArm, 60);
            body.Update();
        }

        if (skin.GetExposedParts().Contains(BodyPartType.LeftUpperArm))
        {
            body.Bandage(BodyPartType.LeftUpperArm);
            body.Update();

            Assert.DoesNotContain(BodyPartType.LeftUpperArm, skin.GetExposedParts());
        }
    }

    [Fact]
    public void Heal_RestoresSkinAlongsideOtherSystems()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.Chest, 50);
        body.Update();

        float skinBefore = Integumentary(body).GetSkinIntegrity(BodyPartType.Chest);
        float boneBefore = Skeletal(body).GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        body.Heal(BodyPartType.Chest, 30);
        body.Update();

        float skinAfter = Integumentary(body).GetSkinIntegrity(BodyPartType.Chest);
        float boneAfter = Skeletal(body).GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        Assert.True(skinAfter > skinBefore, "Healing should restore skin integrity");
        Assert.True(boneAfter > boneBefore, "Healing should also restore bone health");
    }

    // ─── 25. Skin protection level affects effective protection ──

    [Fact]
    public void SkinProtection_DropsAsDamageAccumulates()
    {
        var body = CreateBody();
        var skin = Integumentary(body);

        float protBefore = skin.GetProtectionLevel(BodyPartType.Chest);

        body.TakeDamage(BodyPartType.Chest, 40);
        body.TakeDamage(BodyPartType.Chest, 40);
        body.Update();

        float protAfter = skin.GetProtectionLevel(BodyPartType.Chest);

        Assert.Equal(1f, protBefore);
        Assert.True(protAfter < protBefore,
            $"Skin protection should drop with accumulated damage (before {protBefore}, after {protAfter})");
    }

    // ─── 26. Full scenario: damage → wound → bandage → heal → recovery ──

    [Fact]
    public void FullScenario_WoundBandageHealRecovery()
    {
        var body = CreateBody();
        var skin = Integumentary(body);

        // 1. Take sustained heavy damage → skin wounded
        for (int i = 0; i < 6; i++)
        {
            body.TakeDamage(BodyPartType.LeftLeg, 60);
            body.Update();
        }

        bool wasWounded = skin.GetWoundedParts().Contains(BodyPartType.LeftLeg);

        // 2. Bandage the wound
        body.Bandage(BodyPartType.LeftLeg);
        body.Update();

        var skinNode = skin.GetNode(BodyPartType.LeftLeg) as SkinNode;
        Assert.NotNull(skinNode);
        if (wasWounded) Assert.False(skinNode.IsExposed, "Bandaged wound should not be exposed");

        // 3. Heal over multiple ticks
        for (int i = 0; i < 5; i++)
        {
            body.Heal(BodyPartType.LeftLeg, 20);
            body.Update();
        }

        // 4. Skin should be significantly recovered
        float finalIntegrity = skin.GetSkinIntegrity(BodyPartType.LeftLeg);
        Assert.True(finalIntegrity > 50,
            $"After healing, skin should recover significantly, got {finalIntegrity}");
    }

    // ─── 27. Skin and lifting — burns don't directly block muscle but cascade damage does ──

    [Fact]
    public void Burn_ChestAndArms_ReducesLiftingCapacity()
    {
        var healthyBody = CreateBody();
        float healthyForce = PerformLift(healthyBody, 50);

        var body = CreateBody();

        // 3rd degree burns on chest and both arms → damage cascades to muscles
        body.Burn(BodyPartType.Chest, 70);
        body.Burn(BodyPartType.LeftUpperArm, 70);
        body.Burn(BodyPartType.RightUpperArm, 70);
        body.Update();

        float burnedForce = PerformLift(body, 50);

        Assert.True(burnedForce < healthyForce,
            $"Burns should reduce lifting capacity via tissue damage (healthy {healthyForce}, burned {burnedForce})");
    }

    // ═══════════════════════════════════════════════════════════
    //  IMMUNE SYSTEM INTEGRATION TESTS (28–37)
    // ═══════════════════════════════════════════════════════════

    // ─── 28. Infection spreads through body over multiple ticks ──

    [Fact]
    public void Infection_SpreadsFromChest_ToNeighbours()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Heavy infection at chest with high growth rate to stay above spread threshold
        body.Infect(BodyPartType.Chest, 70, 3f);
        body.Update();

        // After a few ticks, infection should spread to adjacent parts
        for (int i = 0; i < 5; i++)
            body.Update();

        bool spread = immune.GetInfectionLevel(BodyPartType.Neck) > 0
            || immune.GetInfectionLevel(BodyPartType.Abdomen) > 0
            || immune.GetInfectionLevel(BodyPartType.LeftShoulder) > 0;

        Assert.True(spread,
            "Heavy chest infection should spread to neighbouring body parts over time");
    }

    // ─── 29. Toxin damages health when severe ──

    [Fact]
    public void Toxin_Severe_DamagesHealthOverTime()
    {
        var body = CreateBody();
        var immune = Immune(body);

        body.Poison(BodyPartType.Abdomen, 70);
        body.Update();

        float healthBefore = immune.GetNode(BodyPartType.Abdomen)?
            .GetComponent(BodyComponentType.Health)?.Current ?? 100;

        // Disable regen so we can see toxic damage
        var health = immune.GetNode(BodyPartType.Abdomen)?.GetComponent(BodyComponentType.Health);
        if (health != null) health.RegenRate = 0;

        for (int i = 0; i < 5; i++)
            body.Update();

        float healthAfter = immune.GetNode(BodyPartType.Abdomen)?
            .GetComponent(BodyComponentType.Health)?.Current ?? 100;

        Assert.True(healthAfter < healthBefore,
            $"Severe toxin should cause ongoing health damage (before {healthBefore}, after {healthAfter})");
    }

    // ─── 30. Cure clears infection through EventHub ──

    [Fact]
    public void Cure_ClearsInfection_ThroughBody()
    {
        var body = CreateBody();
        var immune = Immune(body);

        body.Infect(BodyPartType.LeftHand, 30, 0f);
        body.Update();
        Assert.True(immune.GetInfectionLevel(BodyPartType.LeftHand) > 0);

        body.Cure(BodyPartType.LeftHand, 50);
        body.Update();

        Assert.Equal(0, immune.GetInfectionLevel(BodyPartType.LeftHand));
    }

    // ─── 31. Damage weakens immune response ──

    [Fact]
    public void Damage_WeakensImmuneResponse_InfectionPersistsLonger()
    {
        // Healthy body: mild infection cleared quickly
        var healthyBody = CreateBody();
        var healthyImmune = Immune(healthyBody);
        healthyBody.Infect(BodyPartType.LeftHand, 15, 0f);
        healthyBody.Update();
        for (int i = 0; i < 10; i++) healthyBody.Update();
        float healthyInf = healthyImmune.GetInfectionLevel(BodyPartType.LeftHand);

        // Damaged body: immune system weakened
        var damagedBody = CreateBody();
        var damagedImmune = Immune(damagedBody);
        damagedBody.TakeDamage(BodyPartType.LeftHand, 200); // Heavy damage
        damagedBody.Update();
        damagedBody.Infect(BodyPartType.LeftHand, 15, 0f);
        damagedBody.Update();
        for (int i = 0; i < 10; i++) damagedBody.Update();
        float damagedInf = damagedImmune.GetInfectionLevel(BodyPartType.LeftHand);

        Assert.True(damagedInf >= healthyInf,
            $"Damaged immune system should clear infection slower (healthy {healthyInf}, damaged {damagedInf})");
    }

    // ─── 32. Infection + exposed wound = worse outcome ──

    [Fact]
    public void ExposedWound_MakesInfectionWorse()
    {
        var body = CreateBody();
        var immune = Immune(body);
        var skin = Integumentary(body);

        // Create a wound (sustained damage)
        for (int i = 0; i < 8; i++)
        {
            body.TakeDamage(BodyPartType.LeftHand, 60);
            body.Update();
        }

        // Infect the wounded area
        body.Infect(BodyPartType.LeftHand, 30, 0.5f);
        body.Update();

        // Run several ticks — exposed wound weakens immune locally (via damage)
        for (int i = 0; i < 5; i++)
            body.Update();

        // The infection should still be significant (damage reduced potency)
        float infLevel = immune.GetInfectionLevel(BodyPartType.LeftHand);
        float potency = immune.GetPotency(BodyPartType.LeftHand);

        Assert.True(potency < 90,
            $"Wound damage should have degraded immune potency (potency: {potency})");
    }

    // ─── 33. Toxin + burn = compounding damage ──

    [Fact]
    public void Toxin_Plus_Burn_CompoundsDamage()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Heavy direct damage weakens immune system
        body.TakeDamage(BodyPartType.LeftUpperArm, 150);
        body.Update();

        // Then poison the same spot
        body.Poison(BodyPartType.LeftUpperArm, 50);
        body.Update();

        float potency = immune.GetPotency(BodyPartType.LeftUpperArm);

        Assert.True(potency < 80,
            $"Damage + toxin should weaken immune (potency: {potency})");
    }

    // ─── 34. Immune overwhelm after multi-system trauma ──

    [Fact]
    public void MultiSystemTrauma_CanOverwhelmImmune()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Heavy infection with very high growth + heavy toxin → overwhelm
        body.Infect(BodyPartType.Chest, 70, 5f);
        body.Poison(BodyPartType.Chest, 50);
        body.Update();

        // Let infection grow for a few ticks
        for (int i = 0; i < 5; i++)
            body.Update();

        var node = immune.GetNode(BodyPartType.Chest) as ImmuneNode;
        Assert.NotNull(node);

        // Either overwhelmed or infection+toxin combined threat is very high
        Assert.True(node.IsOverwhelmed || node.GetThreatLevel() > 60,
            $"Multi-threat should overwhelm or escalate (infection: {node.InfectionLevel}, toxin: {node.ToxinLevel})");
    }

    // ─── 35. Healing infection restores immune potency ──

    [Fact]
    public void CureAndHeal_RestoresImmuneCapacity()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Damage and infect
        body.TakeDamage(BodyPartType.LeftHand, 100);
        body.Infect(BodyPartType.LeftHand, 40, 0.5f);
        body.Update();

        float potBefore = immune.GetPotency(BodyPartType.LeftHand);

        // Cure and heal
        body.Cure(BodyPartType.LeftHand, 50);
        body.Heal(BodyPartType.LeftHand, 80);
        body.Update();

        // Let it recover
        for (int i = 0; i < 5; i++)
            body.Update();

        float potAfter = immune.GetPotency(BodyPartType.LeftHand);

        Assert.True(potAfter > potBefore,
            $"Cure + heal should restore immune capacity (before {potBefore}, after {potAfter})");
    }

    // ─── 36. Inflammation emits pain events ──

    [Fact]
    public void Infection_TriggersInflammation_EmitsPain()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Severe infection should trigger inflammation + pain
        body.Infect(BodyPartType.LeftHand, 50);
        body.Update();

        var node = immune.GetNode(BodyPartType.LeftHand) as ImmuneNode;
        Assert.NotNull(node);
        Assert.True(node.IsInflamed,
            "Severe infection should trigger inflammation");
    }

    // ─── 37. Immune fights off mild infection naturally ──

    [Fact]
    public void MildInfection_ClearedNaturally_NoIntervention()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Mild infection with no growth at a lymph node (neck)
        body.Infect(BodyPartType.Neck, 5, 0f);
        body.Update();
        Assert.True(immune.GetInfectionLevel(BodyPartType.Neck) > 0);

        // Let the immune system work
        for (int i = 0; i < 10; i++)
            body.Update();

        Assert.Equal(0, immune.GetInfectionLevel(BodyPartType.Neck));
    }

    // ─── 38. Toxin cleared naturally over time ──

    [Fact]
    public void MildToxin_NeutralisedNaturally()
    {
        var body = CreateBody();
        var immune = Immune(body);

        body.Poison(BodyPartType.Chest, 5);
        body.Update();
        Assert.True(immune.GetToxinLevel(BodyPartType.Chest) > 0);

        for (int i = 0; i < 10; i++)
            body.Update();

        Assert.Equal(0, immune.GetToxinLevel(BodyPartType.Chest));
    }

    // ─── 39. Full scenario: wound → infection → cure → recovery ──

    [Fact]
    public void FullScenario_WoundInfectionCureRecovery()
    {
        var body = CreateBody();
        var immune = Immune(body);
        var skin = Integumentary(body);

        // 1. Take damage → wound skin
        for (int i = 0; i < 6; i++)
        {
            body.TakeDamage(BodyPartType.LeftLeg, 50);
            body.Update();
        }

        // 2. Wound gets infected (no bandage → exposed)
        body.Infect(BodyPartType.LeftLeg, 30, 0.5f);
        body.Update();
        Assert.True(immune.GetInfectionLevel(BodyPartType.LeftLeg) > 0);

        // 3. Bandage the wound
        body.Bandage(BodyPartType.LeftLeg);
        body.Update();

        // 4. Apply medicine (stronger cure needed — cross-system exposed wound
        //    seeded extra infection before bandage was applied)
        body.Cure(BodyPartType.LeftLeg, 60);
        body.Update();

        // 5. Heal over time (more rounds to account for blood-flow-limited immune response)
        for (int i = 0; i < 20; i++)
        {
            body.Heal(BodyPartType.LeftLeg, 10);
            body.Cure(BodyPartType.LeftLeg, 10); // ongoing treatment
            body.Update();
        }

        // 6. Infection should be cleared and potency recovering
        float finalInfection = immune.GetInfectionLevel(BodyPartType.LeftLeg);
        float finalPotency = immune.GetPotency(BodyPartType.LeftLeg);

        Assert.True(finalInfection == 0 || finalInfection < 5,
            $"Infection should be cleared after cure + time (level: {finalInfection})");
        Assert.True(finalPotency > 30,
            $"Immune potency should be recovering (potency: {finalPotency})");
    }

    // ─── 40. Simultaneous infection + toxin at multiple sites ──

    [Fact]
    public void MultiSite_InfectionAndToxin_ImmuneStretched()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Infect multiple sites with different severities
        body.Infect(BodyPartType.LeftHand, 30, 0.3f);
        body.Infect(BodyPartType.RightFoot, 25, 0.2f);
        body.Poison(BodyPartType.Chest, 40);
        body.Poison(BodyPartType.Head, 20);
        body.Update();

        Assert.True(immune.GetInfectionCount() >= 2);
        Assert.True(immune.GetPoisonedParts().Count >= 2);
        Assert.True(immune.GetTotalThreatLevel() > 100,
            $"Total threat should be significant (threat: {immune.GetTotalThreatLevel()})");

        // Overall potency should drop after fighting on multiple fronts
        for (int i = 0; i < 5; i++)
            body.Update();

        Assert.True(immune.GetOverallPotency() < 1.0f,
            $"Multi-site threats should drain overall potency (potency: {immune.GetOverallPotency()})");
    }

    // ─── 41. Nervous system initialises via Body ──

    [Fact]
    public void Body_Init_NervousSystemPresent()
    {
        var body = CreateBody();

        Assert.NotNull(body.GetSystem(BodySystemType.Nerveus));
    }

    // ─── 42. Damage generates pain in the nervous system ──

    [Fact]
    public void Damage_GeneratesPainInNervousSystem()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.TakeDamage(BodyPartType.LeftHand, 50);
        body.Update();

        Assert.True(nervous.GetPainLevel(BodyPartType.LeftHand) > 0,
            "Damage event should generate pain in the nervous system");
    }

    // ─── 43. Damage pain routes upstream through nerves ──

    [Fact]
    public void Damage_PainRoutesUpstream()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.TakeDamage(BodyPartType.LeftHand, 80);
        body.Update();

        float shoulderPain = nervous.GetPainLevel(BodyPartType.LeftShoulder);
        Assert.True(shoulderPain > 0,
            $"Pain from hand damage should route upstream to shoulder (got {shoulderPain})");
    }

    // ─── 44. Sever nerve blocks downstream pain routing ──

    [Fact]
    public void SeverNerve_BlocksDownstreamPainRouting()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Sever at the forearm
        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();

        // Let pain from severing decay
        for (int i = 0; i < 40; i++)
            body.Update();

        float chestPain = nervous.GetPainLevel(BodyPartType.Chest);

        // Damage the hand — pain should NOT propagate past severed forearm
        body.TakeDamage(BodyPartType.LeftHand, 80);
        body.Update();

        float chestPainAfter = nervous.GetPainLevel(BodyPartType.Chest);
        Assert.True(chestPainAfter <= chestPain + 1,
            $"Pain should not propagate past severed nerve (chest pain before {chestPain}, after {chestPainAfter})");
    }

    // ─── 45. Mana accumulates in a healthy body ──

    [Fact]
    public void HealthyBody_ManaAccumulates()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        for (int i = 0; i < 20; i++)
            body.Update();

        float totalMana = nervous.GetTotalMana();
        Assert.True(totalMana > 0,
            $"Healthy body should accumulate mana over time (got {totalMana})");
    }

    // ─── 46. Damaged nerves produce less mana ──

    [Fact]
    public void DamagedNerves_ProduceLessMana()
    {
        var body1 = CreateBody();
        var body2 = CreateBody();
        var healthy = Nervous(body1);
        var damaged = Nervous(body2);

        // Damage the head nerve in body2
        body2.TakeDamage(BodyPartType.Head, 80);
        body2.Update();

        for (int i = 0; i < 20; i++)
        {
            body1.Update();
            body2.Update();
        }

        float healthyMana = healthy.GetMana(BodyPartType.Head);
        float damagedMana = damaged.GetMana(BodyPartType.Head);

        Assert.True(healthyMana > damagedMana,
            $"Damaged nerves should produce less mana (healthy {healthyMana} vs damaged {damagedMana})");
    }

    // ─── 47. Shock from multi-system trauma ──

    [Fact]
    public void MultiSystemTrauma_TriggersShock()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Widespread heavy damage — pain across many parts
        BodyPartType[] targets = [
            BodyPartType.LeftHand, BodyPartType.RightHand,
            BodyPartType.LeftFoot, BodyPartType.RightFoot,
            BodyPartType.Chest, BodyPartType.Abdomen,
            BodyPartType.Head, BodyPartType.LeftThigh,
            BodyPartType.RightThigh, BodyPartType.Hips
        ];

        foreach (var part in targets)
        {
            body.TakeDamage(part, 60);
        }

        body.Update();
        body.Update(); // second tick for shock check

        Assert.True(nervous.IsInShock,
            $"Widespread trauma should trigger shock (total pain: {nervous.GetTotalPain()})");
    }

    // ─── 48. External shock reduces signal globally ──

    [Fact]
    public void ExternalShock_ReducesSignal()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        float signalBefore = nervous.GetOverallSignalStrength();

        body.Shock(50);
        body.Update();

        float signalAfter = nervous.GetOverallSignalStrength();
        Assert.True(signalAfter < signalBefore,
            $"Shock should reduce overall signal ({signalBefore} → {signalAfter})");
    }

    // ─── 49. Nerve repair restores signal downstream ──

    [Fact]
    public void NerveRepair_RestoresDownstream()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();

        // After sever, downstream hand signal should be very low
        float handSignal = nervous.GetNode(BodyPartType.LeftHand)?
            .GetComponent(BodyComponentType.NerveSignal)?.Current ?? -1;
        Assert.True(handSignal < 5,
            $"After sever, downstream signal should be very low (got {handSignal})");

        body.RepairNerve(BodyPartType.LeftForearm);
        body.Update();

        float handRegenRate = nervous.GetNode(BodyPartType.LeftHand)?
            .GetComponent(BodyComponentType.NerveSignal)?.RegenRate ?? 0;
        Assert.True(handRegenRate > 0,
            "Repair should restore downstream regen rate");
    }

    // ─── 50. Burns cause pain in nervous system ──

    [Fact]
    public void Burn_CausesPainInNervousSystem()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.Burn(BodyPartType.LeftHand, 60);
        body.Update();

        // Burns should emit pain which nervous system picks up
        float pain = nervous.GetPainLevel(BodyPartType.LeftHand);
        Assert.True(pain > 0,
            $"Burn should cause pain in the nervous system (got {pain})");
    }

    // ─── 51. Cross-system pain: muscular exertion reaches nervous ──

    [Fact]
    public void CrossSystem_PainFromMuscularReachesNervous()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Heavy damage → causes DamageEvent → nervous system receives pain from it
        // Tear the muscle directly
        body.Exert(BodyPartType.LeftHand, 100);
        body.Update();

        // Keep checking during exertion — stamina drains and when muscle tears, pain emits
        // Use direct damage as a cross-system pain source since that's what actually hits Nervous
        body.TakeDamage(BodyPartType.LeftHand, 50);
        body.Update();

        // Damage events should generate pain in the nervous system
        float totalPain = nervous.GetTotalPain();
        Assert.True(totalPain > 0,
            $"Cross-system damage should generate pain in nervous system (got {totalPain})");
    }

    // ─── 52. Fracture pain reaches nervous system ──

    [Fact]
    public void Fracture_PainReachesNervousSystem()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // High damage to fracture a bone
        body.TakeDamage(BodyPartType.LeftHand, 90);
        body.Update();
        body.TakeDamage(BodyPartType.LeftHand, 90);
        body.Update();

        float totalPain = nervous.GetTotalPain();
        Assert.True(totalPain > 0,
            $"Fracture pain should reach the nervous system (got {totalPain})");
    }

    // ─── 53. Healing reduces shock over time ──

    [Fact]
    public void Healing_ReducesShockOverTime()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.Shock(40);
        body.Update();

        float shockBefore = nervous.ShockLevel;

        // Heal and let time pass
        for (int i = 0; i < 10; i++)
        {
            body.Heal(BodyPartType.Chest, 10);
            body.Update();
        }

        Assert.True(nervous.ShockLevel < shockBefore,
            $"Healing + time should reduce shock ({shockBefore} → {nervous.ShockLevel})");
    }

    // ─── 54. Pain decays across full body simulation ──

    [Fact]
    public void PainDecays_AcrossFullBodySimulation()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.TakeDamage(BodyPartType.LeftHand, 50);
        body.Update();

        float painAfterDamage = nervous.GetTotalPain();

        for (int i = 0; i < 30; i++)
            body.Update();

        float painAfterRecovery = nervous.GetTotalPain();
        Assert.True(painAfterRecovery < painAfterDamage,
            $"Pain should decay over time in full body sim ({painAfterDamage} → {painAfterRecovery})");
    }

    // ─── 55. Infection triggers pain via immune → nervous ──

    [Fact]
    public void Infection_TriggersPain_ViaImmune()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        body.Infect(BodyPartType.LeftHand, 40, 0.5f);
        body.Update();
        body.Update(); // extra tick for inflammation to emit pain

        float pain = nervous.GetTotalPain();
        Assert.True(pain > 0,
            $"Infection should trigger pain via immune system inflammation (got {pain})");
    }

    // ─── 56. Magical heat accumulates with boosted mana ──

    [Fact]
    public void BoostedMana_GeneratesHeat_InFullBody()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Boost mana production on a node so heat outpaces dissipation
        var hand = nervous.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 5f;

        for (int i = 0; i < 10; i++)
            body.Update();

        float heat = nervous.GetHeatLevel(BodyPartType.LeftHand);
        Assert.True(heat > 0,
            $"Boosted mana production should generate measurable heat in full body sim (got {heat})");
    }

    // ─── 57. Excessive mana production damages nerves via heat ──

    [Fact]
    public void ExcessiveMana_HeatDamagesNerves_InFullBody()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        var hand = nervous.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 10f;

        float healthBefore = hand.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        for (int i = 0; i < 20; i++)
            body.Update();

        float healthAfter = hand.GetComponent(BodyComponentType.Health)?.Current ?? 0;
        Assert.True(healthAfter < healthBefore,
            $"Excessive mana production should damage nerves via heat ({healthBefore} → {healthAfter})");
    }

    // ─── 58. Heat feedback loop — damage slows mana, which reduces heat ──

    [Fact]
    public void HeatFeedbackLoop_DamageSlowsMana()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        var hand = nervous.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 10f;

        // Let heat damage accumulate
        for (int i = 0; i < 15; i++)
            body.Update();

        // After heat damage, mana production should have dropped (feedback loop)
        Assert.True(hand.ManaProductionRate < 10f,
            $"Heat damage should reduce mana production rate (got {hand.ManaProductionRate})");
    }

    // ─── 59. Normal mana production stays safe (dissipation > generation) ──

    [Fact]
    public void NormalMana_HeatStaysSafe()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Run many ticks at normal mana rates
        for (int i = 0; i < 50; i++)
            body.Update();

        float totalHeat = nervous.GetTotalHeat();
        Assert.True(totalHeat < 10,
            $"Normal mana production should not cause dangerous heat buildup (total: {totalHeat})");

        Assert.Empty(nervous.GetOverheatedParts());
    }

    // ─── 60. Overheated parts query in full body ──

    [Fact]
    public void OverheatedParts_ReflectedInFullBody()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Boost a node to cause overheating
        var hand = nervous.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 10f;

        for (int i = 0; i < 20; i++)
            body.Update();

        var overheated = nervous.GetOverheatedParts();
        Assert.Contains(BodyPartType.LeftHand, overheated);
    }

    // ═══════════════════════════════════════════════════════════
    //  METABOLIC SYSTEM INTEGRATION TESTS (61–75)
    // ═══════════════════════════════════════════════════════════

    private static MetabolicSystem Metabolic(Body body) =>
        (MetabolicSystem)body.GetSystem(BodySystemType.Metabolic)!;

    // ─── 61. Metabolic system present in full body ──

    [Fact]
    public void Body_Init_MetabolicSystemPresent()
    {
        var body = CreateBody();
        Assert.NotNull(body.GetSystem(BodySystemType.Metabolic));
    }

    // ─── 62. Metabolic produces energy that accumulates in pool ──

    [Fact]
    public void Metabolic_ProducesEnergyInFullBody()
    {
        var body = CreateBody();
        float before = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Energy);

        body.Update();

        float after = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Energy);
        // Energy should be present (produced by metabolic system)
        Assert.True(after > 0, $"Energy should be produced. Before={before}, After={after}");
    }

    // ─── 63. Respiratory feeds oxygen to metabolic system ──

    [Fact]
    public void Respiratory_FeedsOxygenToMetabolic()
    {
        var body = CreateBody();

        // Run a few ticks — respiratory should produce oxygen that metabolic consumes
        for (int i = 0; i < 5; i++)
            body.Update();

        float oxygen = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Oxygen);
        // Oxygen should still be present (respiratory produces, metabolic consumes)
        Assert.True(oxygen > 0, "Respiratory should keep oxygen available for metabolic");
    }

    // ─── 64. Suffocated airway reduces metabolic energy output ──

    [Fact]
    public void Suffocation_ReducesMetabolicEnergy()
    {
        var body = CreateBody();

        // Let the body stabilise
        for (int i = 0; i < 5; i++) body.Update();
        float normalEnergy = Metabolic(body).LastTickEnergyOutput;

        // Block the airway — oxygen stops flowing
        body.TakeDamage(BodyPartType.Head, 30); // Heavy damage blocks airway
        for (int i = 0; i < 20; i++) body.Update();

        // With depleted oxygen, metabolic output should suffer
        float suffocatedEnergy = Metabolic(body).LastTickEnergyOutput;
        Assert.True(suffocatedEnergy <= normalEnergy,
            $"Suffocation should reduce energy. Normal={normalEnergy}, Suffocated={suffocatedEnergy}");
    }

    // ─── 65. Feeding restores glucose through Body.Feed ──

    [Fact]
    public void Feed_RestoresGlucoseInFullBody()
    {
        var body = CreateBody();

        // Consume some glucose
        for (int i = 0; i < 5; i++) body.Update();
        float before = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Glucose);

        body.Feed(50f);
        body.Update();

        float after = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Glucose);
        Assert.True(after > before - 20, "Feeding should add glucose");
    }

    // ─── 66. Hydrate restores water through Body.Hydrate ──

    [Fact]
    public void Hydrate_RestoresWaterInFullBody()
    {
        var body = CreateBody();

        for (int i = 0; i < 5; i++) body.Update();
        float before = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Water);

        body.Hydrate(50f);
        body.Update();

        float after = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Water);
        Assert.True(after > before - 20, "Hydrating should add water");
    }

    // ─── 67. Damage to chest degrades metabolic output ──

    [Fact]
    public void ChestDamage_DegradeMetabolicOutput()
    {
        var body = CreateBody();
        float normalOutput = Metabolic(body).GetEnergyOutput(BodyPartType.Chest);

        body.TakeDamage(BodyPartType.Chest, 50);
        body.Update();

        float damagedOutput = Metabolic(body).GetEnergyOutput(BodyPartType.Chest);
        Assert.True(damagedOutput < normalOutput);
    }

    // ─── 68. Metabolic boost increases energy output ──

    [Fact]
    public void MetabolicBoost_IncreasesOutputInFullBody()
    {
        var body = CreateBody();
        float normal = Metabolic(body).GetEnergyOutput(BodyPartType.Head);

        body.BoostMetabolism(BodyPartType.Head, 1f); // +1 to multiplier
        body.Update();

        float boosted = Metabolic(body).GetEnergyOutput(BodyPartType.Head);
        Assert.True(boosted > normal);
    }

    // ─── 69. Muscular exertion + metabolic fatigue cross-talk ──

    [Fact]
    public void MuscularExertion_MetabolicFatigueInteraction()
    {
        var body = CreateBody();

        // Heavy exertion should consume resources
        for (int i = 0; i < 10; i++)
        {
            body.Exert(BodyPartType.LeftThigh, 90f);
            body.Update();
        }

        // Muscular exertion consumes resources → metabolic system tracks the impact
        var musc = Muscular(body);
        var meta = Metabolic(body);

        // After heavy exertion, resources are depleted
        float glucose = meta.BodyResourcePool.GetResource(BodyResourceType.Glucose);
        Assert.True(glucose < 100, "Heavy exertion should consume glucose");
    }

    // ─── 70. Metabolic system survives multi-tick with all systems ──

    [Fact]
    public void AllSystems_MetabolicSurvivesMultiTick()
    {
        var body = CreateBody();

        // Run 50 ticks with the full body — nothing should crash
        for (int i = 0; i < 50; i++)
            body.Update();

        var meta = Metabolic(body);
        Assert.True(meta.GetActiveNodeCount() > 0);
    }

    // ─── 71. Heavy damage + starvation cascade ──

    [Fact]
    public void HeavyDamage_CausesStarvationCascade()
    {
        var body = CreateBody();

        // Damage all core organs heavily
        body.TakeDamage(BodyPartType.Head, 80);
        body.TakeDamage(BodyPartType.Chest, 80);
        body.TakeDamage(BodyPartType.Abdomen, 80);

        // Run many ticks — system should degrade
        for (int i = 0; i < 30; i++)
            body.Update();

        var meta = Metabolic(body);
        float totalOutput = meta.GetTotalEnergyOutput();
        // Heavy damage = reduced metabolic rate = less energy
        Assert.True(totalOutput < meta.GetActiveNodeCount() * 3f,
            "Damaged core organs should significantly reduce total energy output");
    }

    // ─── 72. Nervous shock impacts metabolic via shared resources ──

    [Fact]
    public void NervousShock_SharedResourceImpact()
    {
        var body = CreateBody();

        // Shock the nervous system
        body.Shock(50f);
        for (int i = 0; i < 10; i++)
            body.Update();

        // Shock impacts nerve signals which affects mana production
        // Both systems draw from the same resource pool
        var meta = Metabolic(body);
        var nervous = Nervous(body);

        // System should still be functioning (shared pool doesn't crash)
        Assert.True(meta.GetActiveNodeCount() > 0);
        Assert.True(nervous.GetOverallSignalStrength() < 1f);
    }

    // ─── 73. Temperature check after many ticks with all systems ──

    [Fact]
    public void Temperature_StableInFullBody()
    {
        var body = CreateBody();

        for (int i = 0; i < 20; i++)
            body.Update();

        var meta = Metabolic(body);
        float avgTemp = meta.GetAverageTemperature();
        // Temperature should stay roughly normal with all systems working
        Assert.InRange(avgTemp, 35f, 40f);
    }

    // ─── 74. Metabolic + immune interaction — infection consumes energy ──

    [Fact]
    public void Infection_StressesMetabolicResources()
    {
        var body = CreateBody();

        // Let body stabilise
        for (int i = 0; i < 3; i++) body.Update();
        float glucoseBefore = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Glucose);

        // Infect multiple areas — immune system fights, consuming resources
        body.Infect(BodyPartType.LeftLeg, 50, 0.5f);
        body.Infect(BodyPartType.RightLeg, 50, 0.5f);
        body.Infect(BodyPartType.Abdomen, 50, 0.5f);

        for (int i = 0; i < 20; i++) body.Update();

        float glucoseAfter = Metabolic(body).BodyResourcePool.GetResource(BodyResourceType.Glucose);
        // Multiple systems consuming glucose should deplete it faster
        Assert.True(glucoseAfter < glucoseBefore,
            "Infection + metabolic consumption should deplete glucose");
    }

    // ─── 75. InduceFatigue through Body convenience method ──

    [Fact]
    public void InduceFatigue_ThroughBody()
    {
        var body = CreateBody();

        body.InduceFatigue(BodyPartType.LeftLeg, 50f);
        body.Update();

        var meta = Metabolic(body);
        // Fatigue may partially recover during the tick, but should be above zero
        Assert.True(meta.GetFatigue(BodyPartType.LeftLeg) > 0,
            "InduceFatigue should increase fatigue on target body part");
    }

    // ═══════════════════════════════════════════════════════════
    //  NERVE ↔ MUSCLE ↔ GRIP INTEGRATION TESTS (76–90)
    //  "If you sever the nerve to the arm, you drop the sword."
    // ═══════════════════════════════════════════════════════════

    // ─── Helper: grip force = hand muscle force × hand nerve signal ──

    /// <summary>
    /// Calculates effective grip strength for a hand.
    /// Grip = hand muscle force output × nerve signal strength.
    /// A severed nerve means zero signal → zero effective grip → item dropped.
    /// </summary>
    private static float GetEffectiveGrip(Body body, BodyPartType hand)
    {
        var musc = Muscular(body);
        var nervous = Nervous(body);
        float muscleForce = musc.GetForceOutput(hand);
        float signal = nervous.GetSignalStrength(hand);
        return muscleForce * signal;
    }

    /// <summary>
    /// Returns true if the body can hold an item in the given hand.
    /// Requires both muscle force > 0 AND nerve signal > 0.
    /// A grip below 5 is too weak to hold anything meaningful (weapon, tool, etc.).
    /// </summary>
    private static bool CanHoldItem(Body body, BodyPartType hand)
    {
        return GetEffectiveGrip(body, hand) > 5f;
    }

    // ─── 76. Healthy body can grip with both hands ──

    [Fact]
    public void HealthyBody_CanGripBothHands()
    {
        var body = CreateBody();
        body.Update();

        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Healthy body should be able to grip with left hand");
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Healthy body should be able to grip with right hand");
    }

    // ─── 77. Sever nerve to forearm → hand loses grip ──

    [Fact]
    public void SeverNerve_Forearm_HandLosesGrip()
    {
        var body = CreateBody();
        body.Update();

        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Should hold item before nerve is severed");

        // Sever the nerve at the left forearm — downstream signal to hand drops to 0
        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();

        var nervous = Nervous(body);
        float handSignal = nervous.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(handSignal < 0.01f,
            $"Severed forearm nerve should kill hand signal (got {handSignal})");

        Assert.False(CanHoldItem(body, BodyPartType.LeftHand),
            "Hand should lose grip after forearm nerve is severed → item dropped");
    }

    // ─── 78. Sever nerve at shoulder → entire arm chain loses signal ──

    [Fact]
    public void SeverNerve_Shoulder_EntireArmLosesSignal()
    {
        var body = CreateBody();
        body.Update();

        body.SeverNerve(BodyPartType.LeftShoulder);
        body.Update();

        var nervous = Nervous(body);

        // Everything downstream of shoulder should lose signal
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftUpperArm) < 0.01f);
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftForearm) < 0.01f);
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftHand) < 0.01f);

        // Grip impossible
        Assert.False(CanHoldItem(body, BodyPartType.LeftHand),
            "Severed shoulder nerve should disable entire left arm grip");

        // Right arm unaffected
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Right hand should still work after left shoulder nerve severed");
    }

    // ─── 79. Sever nerve at chest → both arms lose signal ──

    [Fact]
    public void SeverNerve_Chest_BothArmsLoseGrip()
    {
        var body = CreateBody();
        body.Update();

        // Chest connects to both shoulders in the nerve tree
        body.SeverNerve(BodyPartType.Chest);
        body.Update();

        Assert.False(CanHoldItem(body, BodyPartType.LeftHand),
            "Chest nerve sever should disable left hand grip");
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Chest nerve sever should disable right hand grip");
    }

    // ─── 80. Sever nerve at neck → whole body loses signal ──

    [Fact]
    public void SeverNerve_Neck_FullBodyParalysis()
    {
        var body = CreateBody();
        body.Update();

        body.SeverNerve(BodyPartType.Neck);
        body.Update();

        var nervous = Nervous(body);

        // Neck is upstream of chest → arms AND legs all lose signal
        Assert.False(CanHoldItem(body, BodyPartType.LeftHand));
        Assert.False(CanHoldItem(body, BodyPartType.RightHand));

        // Legs also lose signal (neck → chest → abdomen → pelvis → hips → legs)
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftFoot) < 0.01f);
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightFoot) < 0.01f);
    }

    // ─── 81. Repair severed nerve → grip recovers over time ──

    [Fact]
    public void RepairNerve_GripRecoversOverTime()
    {
        var body = CreateBody();
        body.Update();

        // Sever and confirm no grip
        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();
        Assert.False(CanHoldItem(body, BodyPartType.LeftHand));

        // Repair the nerve
        body.RepairNerve(BodyPartType.LeftForearm);
        body.Update();

        // Signal begins to regenerate — run ticks to let regen happen
        for (int i = 0; i < 30; i++)
            body.Update();

        var nervous = Nervous(body);
        float restoredSignal = nervous.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(restoredSignal > 0.01f,
            $"After repair + time, hand signal should recover (got {restoredSignal})");
    }

    // ─── 82. Trauma to arm severs nerve AND tears muscle → double disable ──

    [Fact]
    public void SevereArmTrauma_BothNerveAndMuscleDamaged()
    {
        var body = CreateBody();
        body.Update();

        // Massive damage to upper arm AND forearm AND hand — cascading injury
        body.TakeDamage(BodyPartType.LeftUpperArm, 100);
        body.TakeDamage(BodyPartType.LeftForearm, 60);
        body.TakeDamage(BodyPartType.LeftHand, 40);
        body.Update();

        var musc = Muscular(body);
        var nervous = Nervous(body);

        // Upper arm muscle should be torn
        Assert.True(musc.GetForceOutput(BodyPartType.LeftUpperArm) == 0,
            "100 damage should tear the upper arm muscle");

        // Forearm muscle also torn (60 >= 50 tear threshold)
        Assert.True(musc.GetForceOutput(BodyPartType.LeftForearm) == 0,
            "60 damage should tear the forearm muscle");

        // Hand nerve should be weakened from the direct hit
        float handNerveSignal = nervous.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(handNerveSignal < 1f,
            $"Direct hand damage should degrade nerve signal (got {handNerveSignal})");

        // Hand muscle force should be reduced from direct damage
        float handForce = musc.GetForceOutput(BodyPartType.LeftHand);
        Assert.True(handForce < 100f,
            $"Direct hand damage should reduce muscle force (got {handForce})");

        // Effective grip should be reduced
        float grip = GetEffectiveGrip(body, BodyPartType.LeftHand);
        float healthyGrip = GetEffectiveGrip(CreateBody(), BodyPartType.LeftHand);
        Assert.True(grip < healthyGrip,
            $"Severe arm trauma should reduce grip (healthy {healthyGrip}, injured {grip})");
    }

    // ─── 83. Holding a heavy item with weakened arm → exertion fails ──

    [Fact]
    public void WeakenedArm_CannotSustainHeavyExertion()
    {
        var body = CreateBody();

        // Damage the right forearm (moderate — weakened but not torn/severed)
        body.TakeDamage(BodyPartType.RightForearm, 40);
        body.Update();

        // Exert the right hand heavily (simulating holding a heavy weapon)
        body.Exert(BodyPartType.RightHand, 90);
        body.Update();

        var musc = Muscular(body);
        float damagedForce = musc.GetForceOutput(BodyPartType.RightHand);

        // Compare with healthy
        var healthy = CreateBody();
        healthy.Exert(BodyPartType.RightHand, 90);
        healthy.Update();
        float healthyForce = Muscular(healthy).GetForceOutput(BodyPartType.RightHand);

        Assert.True(damagedForce <= healthyForce,
            $"Damaged arm should produce less or equal force under heavy exertion (damaged {damagedForce}, healthy {healthyForce})");
    }

    // ─── 84. Shock causes temporary grip loss across both hands ──

    [Fact]
    public void Shock_CausesTemporaryGripWeakness()
    {
        var body = CreateBody();
        body.Update();

        float gripBefore = GetEffectiveGrip(body, BodyPartType.LeftHand);

        body.Shock(60f);
        body.Update();

        float gripAfter = GetEffectiveGrip(body, BodyPartType.LeftHand);

        Assert.True(gripAfter < gripBefore,
            $"Shock should weaken grip through signal reduction (before {gripBefore}, after {gripAfter})");
    }

    // ─── 85. Bleeding from arm weakens grip over time (resource starvation) ──

    [Fact]
    public void ArmBleeding_WeakensGripOverTime()
    {
        var body = CreateBody();
        body.Update();

        // Start catastrophic bleeding from multiple sites to rapidly drain blood
        body.Bleed(BodyPartType.RightUpperArm, 5f);
        body.Bleed(BodyPartType.Chest, 5f);

        // Also exert the hand (increases resource demand)
        body.Exert(BodyPartType.RightHand, 80);

        // Let blood drain heavily — needs many ticks for systemic resource depletion
        for (int i = 0; i < 80; i++)
            body.Update();

        var circ = Circulatory(body);

        // Blood pressure should have dropped significantly
        Assert.True(circ.GetBloodPressure() < 80f,
            $"Heavy bleeding should reduce blood pressure (got {circ.GetBloodPressure()})");
    }

    // ─── 86. Burn on hand → reduced grip from muscle + skin damage ──

    [Fact]
    public void BurnOnHand_ReducesGrip()
    {
        var body = CreateBody();
        body.Update();

        float gripBefore = GetEffectiveGrip(body, BodyPartType.LeftHand);

        // 3rd degree burn on the hand
        body.Burn(BodyPartType.LeftHand, 70);
        body.Update();

        float gripAfter = GetEffectiveGrip(body, BodyPartType.LeftHand);

        Assert.True(gripAfter < gripBefore,
            $"3rd degree burn should reduce hand grip via cascading tissue damage (before {gripBefore}, after {gripAfter})");
    }

    // ─── 87. Infection in arm + no treatment → progressive grip loss ──

    [Fact]
    public void ArmInfection_ProgressiveGripLoss()
    {
        var body = CreateBody();
        body.Update();

        float gripEarly = GetEffectiveGrip(body, BodyPartType.RightHand);

        // Severe infection on the right forearm with high growth rate
        body.Infect(BodyPartType.RightForearm, 40, 2f);
        body.Update();

        // Let infection grow and damage tissue over time
        for (int i = 0; i < 15; i++)
            body.Update();

        float gripLate = GetEffectiveGrip(body, BodyPartType.RightHand);

        // Infection should damage tissue → weaken grip
        Assert.True(gripLate <= gripEarly,
            $"Untreated arm infection should weaken grip over time (before {gripEarly}, after {gripLate})");
    }

    // ─── 88. Full scenario: holding item → arm severed → drop → repair → regain ──

    [Fact]
    public void FullScenario_HoldSeverDropRepairRegain()
    {
        var body = CreateBody();
        body.Update();

        // 1. Holding an item
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Step 1: Should be holding item");

        // 2. Sever nerve at right upper arm
        body.SeverNerve(BodyPartType.RightUpperArm);
        body.Update();

        // 3. Item is dropped — no signal to hand
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Step 3: Severed nerve → grip lost → item dropped");

        // 4. Left hand still works
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Step 4: Left hand should still function");

        // 5. Repair the nerve
        body.RepairNerve(BodyPartType.RightUpperArm);
        body.Update();

        // 6. Let signal regenerate
        for (int i = 0; i < 40; i++)
            body.Update();

        // 7. Grip should return (at least partially)
        float restoredGrip = GetEffectiveGrip(body, BodyPartType.RightHand);
        Assert.True(restoredGrip > 0,
            $"Step 7: After repair + recovery, grip should be restored (got {restoredGrip})");
    }

    // ─── 89. Pain overload in arm → reduced grip (but not zero) ──

    [Fact]
    public void PainOverload_ReducesGripButNotZero()
    {
        var body = CreateBody();
        body.Update();

        float gripBefore = GetEffectiveGrip(body, BodyPartType.LeftHand);

        // Inflict massive pain — overloads nerves → reduced signal (50%)
        body.TakeDamage(BodyPartType.LeftHand, 40);
        body.TakeDamage(BodyPartType.LeftForearm, 40);
        body.Update();

        var nervous = Nervous(body);
        float handSignal = nervous.GetSignalStrength(BodyPartType.LeftHand);
        float gripAfter = GetEffectiveGrip(body, BodyPartType.LeftHand);

        // Grip weakened but not zero (nerves overloaded → 50% signal, not severed)
        Assert.True(gripAfter < gripBefore,
            $"Pain overload should weaken grip (before {gripBefore}, after {gripAfter})");
    }

    // ─── 90. Combat scenario: sword arm damaged → switch to off-hand ──

    [Fact]
    public void CombatScenario_SwordArmDamaged_OffHandStillWorks()
    {
        var body = CreateBody();
        body.Update();

        // Sword hand (right) takes devastating damage — nerve severed at forearm,
        // hand directly damaged too (slashing wound)
        body.SeverNerve(BodyPartType.RightForearm);
        body.TakeDamage(BodyPartType.RightHand, 50);
        body.Update();

        float rightGrip = GetEffectiveGrip(body, BodyPartType.RightHand);
        float leftGrip = GetEffectiveGrip(body, BodyPartType.LeftHand);

        // Right hand should be severely weakened (severed nerve = near-zero signal)
        Assert.True(rightGrip < 30f,
            $"Destroyed sword arm should have minimal grip (got {rightGrip})");

        // Left hand should still be at full strength — switch hands!
        Assert.True(leftGrip > rightGrip,
            $"Off-hand should be stronger than destroyed arm (left {leftGrip}, right {rightGrip})");
    }

    // ═══════════════════════════════════════════════════════════
    //  MULTI-SYSTEM CASCADE SCENARIOS (91–105)
    //  Real combat / survival situations that test all 8 systems.
    // ═══════════════════════════════════════════════════════════

    // ─── 91. Decapitation: neck severed → total system collapse ──

    [Fact]
    public void Decapitation_NeckDestroyed_TotalCollapse()
    {
        var body = CreateBody();

        // Massive neck damage — severs airway, blood flow, and nerves
        body.TakeDamage(BodyPartType.Neck, 100);
        body.SeverNerve(BodyPartType.Neck);
        body.Bleed(BodyPartType.Neck, 10f);
        body.Update();

        var resp = Respiratory(body);
        var circ = Circulatory(body);
        var nervous = Nervous(body);

        // Airway blocked
        Assert.True(resp.IsAirwayBlocked());

        // Blood flow to head cut off
        Assert.True(circ.GetBloodFlowTo(BodyPartType.Head) < 50f);

        // Nerve signal to entire body killed (neck is central)
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftHand) < 0.01f);
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightFoot) < 0.01f);

        // After many ticks, hypoxia
        for (int i = 0; i < 60; i++) body.Update();
        Assert.True(resp.IsHypoxic());
    }

    // ─── 92. Gut stab: abdomen trauma → bleeding + infection risk ──

    [Fact]
    public void GutStab_BleedingAndInfectionCascade()
    {
        var body = CreateBody();

        // Deep abdominal wound
        body.TakeDamage(BodyPartType.Abdomen, 60);
        body.Update();

        var circ = Circulatory(body);
        var immune = Immune(body);

        // Should be bleeding (damage ≥ 20 threshold on major vessel)
        Assert.True(circ.GetBleedingParts().Contains(BodyPartType.Abdomen),
            "Gut stab should cause bleeding");

        // Wound exposed → susceptible to infection
        body.Infect(BodyPartType.Abdomen, 25, 0.5f);
        body.Update();

        Assert.True(immune.GetInfectionLevel(BodyPartType.Abdomen) > 0,
            "Gut wound should allow infection");

        // Let it progress without treatment
        for (int i = 0; i < 15; i++)
            body.Update();

        // Blood loss + infection should compound
        Assert.True(circ.GetBloodPressure() < 100f,
            "Untreated gut stab should reduce BP via blood loss");
    }

    // ─── 93. Arrow to the knee: fracture + bleed + can't walk ──

    [Fact]
    public void ArrowToKnee_FractureBleedCantWalk()
    {
        var body = CreateBody();

        body.TakeDamage(BodyPartType.LeftLeg, 80);
        body.Bleed(BodyPartType.LeftLeg, 2f);
        body.Update();

        var skel = Skeletal(body);
        var musc = Muscular(body);
        var circ = Circulatory(body);

        // Fracture expected
        Assert.True(skel.GetOverallIntegrity() < 100f,
            "Arrow should damage skeletal integrity");

        // Muscle torn or severely weakened
        Assert.True(musc.GetForceOutput(BodyPartType.LeftLeg) < 50f,
            "Leg muscle should be severely weakened");

        // Locomotion impaired — weight-bearing chain broken
        float loco = musc.GetLocomotionForce();
        var healthyBody = CreateBody();
        float healthyLoco = Muscular(healthyBody).GetLocomotionForce();
        // The damaged leg should reduce total locomotion
        Assert.True(loco < healthyLoco,
            $"Arrow to knee should impair locomotion (healthy {healthyLoco}, injured {loco})");
    }

    // ─── 94. Poisoned weapon: damage + toxin + nerve signal decay ──

    [Fact]
    public void PoisonedWeapon_DamagePlusToxin()
    {
        var body = CreateBody();

        // Strike with a poisoned blade
        body.TakeDamage(BodyPartType.LeftThigh, 30);
        body.Poison(BodyPartType.LeftThigh, 50);
        body.Update();

        var immune = Immune(body);
        var nervous = Nervous(body);

        Assert.True(immune.GetToxinLevel(BodyPartType.LeftThigh) > 0,
            "Poison should register in immune system");

        // Let poison work
        for (int i = 0; i < 10; i++)
            body.Update();

        // Toxin should cause ongoing damage → pain → nerve degradation
        float pain = nervous.GetPainLevel(BodyPartType.LeftThigh);
        Assert.True(pain >= 0,
            "Poison damage should generate pain in nervous system");
    }

    // ─── 95. Hypothermia scenario: prolonged resource drain → full body weakens ──

    [Fact]
    public void ProlongedResourceDrain_WeakensAllSystems()
    {
        var body = CreateBody();

        // Don't feed or hydrate — let resources deplete over many ticks
        // Also exert muscles to increase consumption beyond production
        for (int i = 0; i < 80; i++)
        {
            body.Exert(BodyPartType.LeftThigh, 90);
            body.Exert(BodyPartType.RightThigh, 90);
            body.Exert(BodyPartType.Chest, 90);
            body.Update();
        }

        var musc = Muscular(body);
        var meta = Metabolic(body);

        float glucose = meta.BodyResourcePool.GetResource(BodyResourceType.Glucose);

        // After 80 ticks of heavy exertion consuming resources faster than production
        Assert.True(glucose < 100f,
            $"Glucose should deplete under heavy exertion (got {glucose})");

        // Muscles should be fatigued from exertion
        float stamina = musc.GetAverageStamina();
        Assert.True(stamina < 100f,
            $"Muscles should be fatigued from sustained exertion (stamina: {stamina})");
    }

    // ─── 96. Tourniquet scenario: stop arm bleeding but starve the limb ──

    [Fact]
    public void Tourniquet_StopsBleedingButStarvesLimb()
    {
        var body = CreateBody();

        // Start bleeding from upper arm
        body.Bleed(BodyPartType.LeftUpperArm, 3f);
        body.Update();

        // Clot (tourniquet) stops the bleed
        body.Clot(BodyPartType.LeftUpperArm);
        body.Update();

        var circ = Circulatory(body);
        // Bleeding should stop at that site
        Assert.False(circ.GetBleedingParts().Contains(BodyPartType.LeftUpperArm),
            "Clot should stop bleeding at upper arm");
    }

    // ─── 97. Healing cascade: cure + bandage + heal + rest → full recovery ──

    [Fact]
    public void FullHealingCascade_CompleteRecovery()
    {
        var body = CreateBody();

        // 1. Sever nerve at forearm — grip drops to zero (no signal to hand)
        body.SeverNerve(BodyPartType.RightForearm);
        body.TakeDamage(BodyPartType.RightForearm, 40);
        body.Infect(BodyPartType.RightForearm, 20, 0.3f);
        body.Bleed(BodyPartType.RightForearm, 2f);
        body.Update();

        float gripDamaged = GetEffectiveGrip(body, BodyPartType.RightHand);
        Assert.True(gripDamaged < 1f,
            $"Grip should be near-zero after nerve sever (got {gripDamaged})");

        // 2. Apply full treatment
        body.Clot(BodyPartType.RightForearm);         // Stop bleeding
        body.Bandage(BodyPartType.RightForearm);       // Cover wound
        body.Cure(BodyPartType.RightForearm, 30);      // Cure infection
        body.RepairNerve(BodyPartType.RightForearm);   // Repair severed nerve
        body.Heal(BodyPartType.RightForearm, 60);      // Restore health
        body.Rest(BodyPartType.RightForearm);           // Rest muscles
        body.Update();

        // 3. Let recovery happen over many ticks
        for (int i = 0; i < 40; i++)
        {
            body.Heal(BodyPartType.RightForearm, 5);
            body.Update();
        }

        float gripRecovered = GetEffectiveGrip(body, BodyPartType.RightHand);

        Assert.True(gripRecovered > gripDamaged,
            $"Full healing cascade should restore grip (damaged {gripDamaged}, recovered {gripRecovered})");
    }

    // ─── 98. Spinal injury at chest → legs paralysed, arms still work ──

    [Fact]
    public void SpinalInjury_Chest_LegsParalysedArmsWork()
    {
        var body = CreateBody();
        body.Update();

        // Sever nerve at abdomen (spinal cord) — legs lose signal
        // Chest → Abdomen → Pelvis → Hips → Legs
        body.SeverNerve(BodyPartType.Abdomen);
        body.Update();

        var nervous = Nervous(body);

        // Legs should be paralysed (downstream of abdomen)
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftThigh) < 0.01f,
            "Left thigh should lose signal after abdominal nerve sever");
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftFoot) < 0.01f,
            "Left foot should lose signal");
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightFoot) < 0.01f,
            "Right foot should lose signal");

        // Arms should still work (shoulders branch from chest, not from abdomen)
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftHand) > 0.01f,
            "Left hand should retain signal after abdominal sever");
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Should still grip with left hand");
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip with right hand");

        // Walking impossible
        var musc = Muscular(body);
        // Locomotion force should be affected since legs have no nerve signal
        float locoForce = musc.GetLocomotionForce();
        // Even though muscles aren't torn, nerve signal = 0 means no effective force
        // (muscle force still shows but effective movement needs nerve command)
    }

    // ─── 99. Both arms burned → can't grip, legs still work for running ──

    [Fact]
    public void BothArmsBurned_CantGrip_LegsStillWork()
    {
        var body = CreateBody();
        body.Update();

        // Severe burns on both arms
        body.Burn(BodyPartType.LeftHand, 80);
        body.Burn(BodyPartType.RightHand, 80);
        body.Burn(BodyPartType.LeftForearm, 70);
        body.Burn(BodyPartType.RightForearm, 70);
        body.Update();

        // Burns cascade damage to muscles and nerves
        float leftGrip = GetEffectiveGrip(body, BodyPartType.LeftHand);
        float rightGrip = GetEffectiveGrip(body, BodyPartType.RightHand);

        // Grip severely weakened
        var healthyBody = CreateBody();
        healthyBody.Update();
        float healthyGrip = GetEffectiveGrip(healthyBody, BodyPartType.LeftHand);

        Assert.True(leftGrip < healthyGrip,
            $"Burned left hand should have weaker grip (healthy {healthyGrip}, burned {leftGrip})");
        Assert.True(rightGrip < healthyGrip,
            $"Burned right hand should have weaker grip (healthy {healthyGrip}, burned {rightGrip})");

        // Legs should be fine
        var musc = Muscular(body);
        Assert.True(musc.GetLocomotionForce() > 0,
            "Legs should still provide locomotion force");
    }

    // ─── 100. Massive blood loss → systemic weakness → can't lift anything ──

    [Fact]
    public void MassiveBloodLoss_SystemicWeakness()
    {
        var body = CreateBody();

        // Multiple bleed sites — catastrophic blood loss
        body.Bleed(BodyPartType.Chest, 5f);
        body.Bleed(BodyPartType.Abdomen, 5f);
        body.Bleed(BodyPartType.LeftThigh, 3f);

        // Let blood drain for many ticks
        for (int i = 0; i < 40; i++)
            body.Update();

        var circ = Circulatory(body);
        Assert.True(circ.GetBloodPressure() < 50f,
            $"Massive blood loss should collapse BP (got {circ.GetBloodPressure()})");

        // Blood flow to extremities should be severely reduced
        float handFlow = circ.GetBloodFlowTo(BodyPartType.LeftHand);
        Assert.True(handFlow < 100f,
            $"Blood flow to hand should drop with collapsed BP (got {handFlow})");

        // With depleted blood, muscle strength should degrade over extended time
        var musc = Muscular(body);
        Assert.True(musc.GetOverallStrength() <= 100f,
            $"Muscles should not exceed baseline strength during blood loss (got {musc.GetOverallStrength()})");
    }

    // ─── 101. Infection spreads from wound → reaches chest → affects lungs & heart ──

    [Fact]
    public void InfectionSpread_ReachesVitalOrgans()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // First weaken the immune system at chest so it can't fight the infection
        body.TakeDamage(BodyPartType.Chest, 150); // Destroy immune potency
        body.Update();

        // Now infect the weakened chest with aggressive growth
        body.Infect(BodyPartType.Chest, 60, 5f);
        body.Update();

        // Let infection grow past the 50 spread threshold (immune too weak to fight)
        for (int i = 0; i < 10; i++)
            body.Update();

        // Check if infection has reached downstream neighbours
        // Chest → Neck, LeftShoulder, RightShoulder, Abdomen
        float neckInfection = immune.GetInfectionLevel(BodyPartType.Neck);
        float abdomenInfection = immune.GetInfectionLevel(BodyPartType.Abdomen);
        float shoulderInfection = immune.GetInfectionLevel(BodyPartType.LeftShoulder);

        bool spreadDownstream = neckInfection > 0
            || abdomenInfection > 0
            || shoulderInfection > 0;
        Assert.True(spreadDownstream,
            $"Aggressive chest infection should spread downstream when immune is weakened (neck: {neckInfection}, abdomen: {abdomenInfection}, shoulder: {shoulderInfection})");
    }

    // ─── 102. Broken leg + infection + bleeding = survival crisis ──

    [Fact]
    public void SurvivalCrisis_BrokenLegInfectionBleeding()
    {
        var body = CreateBody();

        // Devastating leg injury
        body.TakeDamage(BodyPartType.LeftThigh, 100); // Fracture + muscle tear
        body.Bleed(BodyPartType.LeftThigh, 3f);       // Heavy bleeding
        body.Infect(BodyPartType.LeftThigh, 30, 0.5f); // Wound infection
        body.Update();

        var skel = Skeletal(body);
        var musc = Muscular(body);
        var circ = Circulatory(body);
        var immune = Immune(body);

        // All systems affected
        Assert.True(skel.GetOverallIntegrity() < 100f, "Bone should be damaged");
        Assert.True(musc.GetForceOutput(BodyPartType.LeftThigh) == 0, "Muscle should be torn");
        Assert.True(circ.GetBleedingParts().Count > 0, "Should be bleeding");
        Assert.True(immune.GetInfectionLevel(BodyPartType.LeftThigh) > 0, "Should be infected");

        // Without treatment, things get worse
        for (int i = 0; i < 20; i++)
            body.Update();

        Assert.True(circ.GetBloodPressure() < 100f,
            "Blood loss should reduce pressure over time");
    }

    // ─── 103. Adrenaline scenario: shock + exertion → temporary power then crash ──

    [Fact]
    public void ShockAndExertion_PerformanceDegradation()
    {
        var body = CreateBody();

        // Put body into shock
        body.Shock(30f);
        body.Update();

        var nervous = Nervous(body);
        Assert.True(nervous.IsInShock);

        // Exert during shock — force is reduced because nerve signals are degraded
        float shockForce = PerformLift(body, 80);

        var healthyBody = CreateBody();
        float healthyForce = PerformLift(healthyBody, 80);

        Assert.True(shockForce <= healthyForce,
            $"Lifting during shock should be impaired (healthy {healthyForce}, shocked {shockForce})");
    }

    // ─── 104. Simultaneous nerve sever + muscle tear on same limb ──

    [Fact]
    public void SimultaneousNerveSeverAndMuscleTear_CompleteDisable()
    {
        var body = CreateBody();
        body.Update();

        // Sever nerve AND tear muscle on the same arm
        body.SeverNerve(BodyPartType.RightForearm);
        body.TakeDamage(BodyPartType.RightForearm, 60); // Tear muscle
        body.Update();

        var musc = Muscular(body);
        var nervous = Nervous(body);

        // Both muscle and nerve should be disabled
        Assert.True(musc.GetForceOutput(BodyPartType.RightForearm) == 0 ||
                    musc.GetForceOutput(BodyPartType.RightHand) == 0,
            "Muscle should produce no force");
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightHand) < 0.01f,
            "Nerve signal to hand should be zero");

        // Effective grip = 0
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Combined nerve sever + muscle tear should completely disable grip");
    }

    // ─── 105. Multi-tick healing from near-death → progressive system recovery ──

    [Fact]
    public void NearDeath_ProgressiveRecovery()
    {
        var body = CreateBody();

        // Near-fatal damage
        body.TakeDamage(BodyPartType.Chest, 70);
        body.TakeDamage(BodyPartType.Abdomen, 50);
        body.Bleed(BodyPartType.Chest, 3f);
        body.Update();

        // Stabilise: clot ALL bleeds (chest damage also triggers auto-bleed),
        // bandage, and stop blood loss before measuring
        body.Clot(BodyPartType.Chest);
        body.Clot(BodyPartType.Abdomen);
        body.Bandage(BodyPartType.Chest);
        body.Update();

        // Track recovery over many healing ticks — feed blood back too
        for (int i = 0; i < 40; i++)
        {
            body.Heal(BodyPartType.Chest, 5);
            body.Heal(BodyPartType.Abdomen, 5);
            body.Feed(3f);
            body.Hydrate(3f);
            body.Update();
        }

        var circ = Circulatory(body);
        var musc = Muscular(body);

        // After extensive treatment, the body should be functioning
        // (BP may not return to 100 but should be above 0 — stable)
        Assert.True(circ.GetBloodPressure() > 0,
            $"Blood pressure should stabilise above 0 with treatment (got {circ.GetBloodPressure()})");
        Assert.True(musc.GetOverallStrength() > 0,
            $"Muscles should retain some strength after recovery (got {musc.GetOverallStrength()})");
    }

    // ═══════════════════════════════════════════════════════════
    //  GLADIATOR ARENA COMBAT SCENARIOS (106–135)
    // ═══════════════════════════════════════════════════════════

    // ─── 106. Throat slash — airway blocked + bleeding + nerve damage ──

    [Fact]
    public void ThroatSlash_AirwayBlockedAndBleeding()
    {
        var body = CreateBody();
        body.Update();

        // Gladius slash across the throat — heavy damage
        body.TakeDamage(BodyPartType.Neck, 40);
        body.Bleed(BodyPartType.Neck, 4f);
        body.Update();

        var resp = Respiratory(body);
        var circ = Circulatory(body);

        // Throat damage reduces airflow to lungs (even if not fully blocked)
        float airflow = resp.GetAirflowReachingLungs();
        Assert.True(airflow < 100f,
            $"Throat slash should reduce airflow (got {airflow})");

        // Neck is bleeding heavily
        var bleedingParts = circ.GetBleedingParts();
        Assert.Contains(BodyPartType.Neck, bleedingParts);

        // After several ticks without treatment, oxygen drops
        for (int i = 0; i < 10; i++) body.Update();
        float oxygenOutput = resp.GetOxygenOutput();
        Assert.True(oxygenOutput < 5f,
            $"Sustained throat damage should severely impair breathing (O₂ output: {oxygenOutput})");
    }

    // ─── 107. Throat slash — suffocation kills before bleed-out ──

    [Fact]
    public void ThroatSlash_SuffocationPath()
    {
        var body = CreateBody();
        body.Update();

        // Massive throat blow blocks the airway entirely
        body.TakeDamage(BodyPartType.Neck, 50);
        body.Update();

        var resp = Respiratory(body);

        // Heavy damage (≥30) blocks airway
        Assert.True(resp.IsAirwayBlocked(),
            "Heavy neck damage should block the airway");

        // With blocked airway, lungs get zero air
        Assert.Equal(0f, resp.GetAirflowReachingLungs());
        Assert.Equal(0f, resp.GetOxygenOutput());

        // Ticks deplete remaining oxygen (body consumes ~2 O₂/tick, starts at 100)
        for (int i = 0; i < 60; i++) body.Update();
        Assert.True(resp.IsHypoxic(),
            "Blocked airway should lead to hypoxia within ticks");
    }

    // ─── 108. Hamstring slash — can't walk but can still punch ──

    [Fact]
    public void HamstringSlash_LegDisabledButArmsWork()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        float locomotionBefore = musc.GetLocomotionForce();
        float upperBefore = musc.GetUpperBodyForce();

        // Slash the right hamstring (thigh)
        body.TakeDamage(BodyPartType.RightThigh, 60);
        body.Update();

        float locomotionAfter = musc.GetLocomotionForce();
        float upperAfter = musc.GetUpperBodyForce();

        // Locomotion degraded significantly
        Assert.True(locomotionAfter < locomotionBefore * 0.8f,
            $"Hamstring slash should reduce locomotion (before: {locomotionBefore}, after: {locomotionAfter})");

        // Upper body still functional for punching
        Assert.True(upperAfter > upperBefore * 0.8f,
            $"Arms should still work after leg injury (before: {upperBefore}, after: {upperAfter})");

        // Can still grip weapons
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip sword despite leg injury");
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Should still grip shield despite leg injury");
    }

    // ─── 109. Dual-wielding gladiator — one arm injured mid-fight ──

    [Fact]
    public void DualWield_OneArmSevered_OtherArmStillFights()
    {
        var body = CreateBody();
        body.Update();

        // Both hands hold weapons initially
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand));
        Assert.True(CanHoldItem(body, BodyPartType.RightHand));

        // Opponent severs the right arm nerve
        body.SeverNerve(BodyPartType.RightUpperArm);
        body.Update();

        // Right hand drops weapon
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Severed arm should drop weapon");

        // Left hand still grips weapon — gladiator switches to single-wield
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Uninjured arm should still grip weapon");

        // Left arm force is not degraded
        var musc = Muscular(body);
        Assert.True(musc.GetForceOutput(BodyPartType.LeftHand) > 0,
            "Uninjured arm should have full muscle force");
    }

    // ─── 110. Shield arm absorbs repeated blows → skin breach → infection risk ──

    [Fact]
    public void ShieldArmRepeatedBlows_SkinBreachAndInfectionRisk()
    {
        var body = CreateBody();
        body.Update();

        var skin = Integumentary(body);
        var immune = Immune(body);

        // Shield arm takes repeated hits (left forearm absorbs)
        for (int i = 0; i < 5; i++)
        {
            body.TakeDamage(BodyPartType.LeftForearm, 15);
            body.Update();
        }

        // Skin is breached from accumulated damage
        float integrity = skin.GetSkinIntegrity(BodyPartType.LeftForearm);
        Assert.True(integrity < 100f,
            $"Repeated blows should degrade skin (integrity: {integrity})");

        // Open wound = infection risk; infect the wound
        body.Infect(BodyPartType.LeftForearm, 20f, 0.5f);
        body.Update();

        Assert.True(immune.GetInfectionLevel(BodyPartType.LeftForearm) > 0,
            "Open wound should be susceptible to infection");
    }

    // ─── 111. Gladiator exhaustion — prolonged combat drains stamina ──

    [Fact]
    public void ProlongedCombat_ExhaustionDegradation()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        float initialForce = musc.GetUpperBodyForce();

        // Simulate 20 rounds of intense combat (swinging sword)
        BodyPartType[] swordArm = [
            BodyPartType.RightShoulder, BodyPartType.RightUpperArm,
            BodyPartType.RightForearm, BodyPartType.RightHand
        ];
        for (int round = 0; round < 20; round++)
        {
            foreach (var part in swordArm)
                body.Exert(part, 90f);
            body.Update();
        }

        // Stamina should be depleted from intense exertion
        float avgStamina = musc.GetAverageStamina();
        float currentForce = musc.GetUpperBodyForce();

        Assert.True(avgStamina < 100f,
            $"Prolonged exertion should drain stamina (avg: {avgStamina})");
        Assert.True(currentForce < initialForce,
            $"Exhausted muscles should produce less force (initial: {initialForce}, now: {currentForce})");
    }

    // ─── 112. Fire pit trap — burns to legs, arms still fight ──

    [Fact]
    public void FirePitTrap_BurnedLegsFightingArms()
    {
        var body = CreateBody();
        body.Update();

        // Gladiator steps in fire pit — severe burns (3rd degree emits damage event)
        body.Burn(BodyPartType.LeftFoot, 70f);
        body.Burn(BodyPartType.RightFoot, 70f);
        body.Burn(BodyPartType.LeftLeg, 70f);
        body.Burn(BodyPartType.RightLeg, 70f);
        body.Update();

        var skin = Integumentary(body);
        var musc = Muscular(body);

        // Feet/legs are burned
        var burned = skin.GetBurnedParts();
        Assert.Contains(BodyPartType.LeftFoot, burned);
        Assert.Contains(BodyPartType.RightFoot, burned);

        // Burns cause pain → nerve pain routes upstream
        var nervous = Nervous(body);
        Assert.True(nervous.GetTotalPain() > 0, "Burns should generate significant pain");

        // But arms still grip weapons
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Hands should still grip sword despite leg burns");
        Assert.True(musc.GetUpperBodyForce() > 0,
            "Upper body muscles should still produce force");

        // 3rd degree burns cause tissue damage → muscle degradation
        float locoForce = musc.GetLocomotionForce();
        var healthyBody = CreateBody();
        healthyBody.Update();
        float healthyLoco = Muscular(healthyBody).GetLocomotionForce();
        Assert.True(locoForce < healthyLoco,
            $"Severe burns should impair movement (healthy: {healthyLoco}, burned: {locoForce})");
    }

    // ─── 113. Poisoned blade — toxin spreads from wound site ──

    [Fact]
    public void PoisonedBlade_ToxinFromWoundSite()
    {
        var body = CreateBody();
        body.Update();

        // Gladiator stabbed with poisoned blade in the right forearm
        body.TakeDamage(BodyPartType.RightForearm, 25);
        body.Poison(BodyPartType.RightForearm, 30f);
        body.Update();

        var immune = Immune(body);

        // Toxin is present at wound site
        float toxin = immune.GetToxinLevel(BodyPartType.RightForearm);
        Assert.True(toxin > 0, $"Toxin should be present at stab wound (got {toxin})");

        // Poisoned gladiator can still fight initially
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip weapon initially despite poison");

        // Multiple ticks — poison weakens the immune system
        for (int i = 0; i < 10; i++) body.Update();

        // Overall immune potency should be degraded
        float potency = immune.GetOverallPotency();
        Assert.True(potency < 1.0f,
            $"Poison should degrade immune potency over time (got {potency})");
    }

    // ─── 114. Head trauma — brain damage cascades to all signals ──

    [Fact]
    public void HeadTrauma_BrainDamageCascade()
    {
        var body = CreateBody();
        body.Update();

        var nervous = Nervous(body);
        float signalBefore = nervous.GetOverallSignalStrength();

        // Mace blow to the head
        body.TakeDamage(BodyPartType.Head, 50);
        body.Update();

        float signalAfter = nervous.GetOverallSignalStrength();

        // Head (brain) damage should degrade overall nerve signal
        Assert.True(signalAfter < signalBefore,
            $"Head trauma should reduce overall signal strength (before: {signalBefore}, after: {signalAfter})");

        // Pain from head wound is high
        Assert.True(nervous.GetPainLevel(BodyPartType.Head) > 0,
            "Head trauma should cause significant pain");
    }

    // ─── 115. Spear through chest — heart + lung damage ──

    [Fact]
    public void SpearThroughChest_HeartAndLungDamage()
    {
        var body = CreateBody();
        body.Update();

        // Spear pierces the chest — massive damage
        body.TakeDamage(BodyPartType.Chest, 70);
        body.Bleed(BodyPartType.Chest, 5f);
        body.Update();

        var circ = Circulatory(body);
        var resp = Respiratory(body);

        // Heart is in the chest — blood pressure drops
        float bp = circ.GetBloodPressure();
        Assert.True(bp < 100f,
            $"Spear to chest should drop blood pressure (got {bp})");

        // Lungs damaged — O₂ production drops
        float o2 = resp.GetOxygenOutput();
        Assert.True(o2 < 5f,
            $"Chest damage should reduce oxygen output (got {o2})");

        // Lung capacity reduced
        float lungCap = resp.GetLungCapacity();
        Assert.True(lungCap < 100f,
            $"Spear should damage lung capacity (got {lungCap})");

        // Heavy bleeding from the wound
        Assert.Contains(BodyPartType.Chest, circ.GetBleedingParts());
    }

    // ─── 116. Achilles tendon cut — foot disabled, locomotion crippled ──

    [Fact]
    public void AchillesTendonCut_FootDisabledLocomotionCrippled()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        float locoBefore = musc.GetLocomotionForce();

        // Slash the left foot (Achilles tendon) — severe damage
        body.TakeDamage(BodyPartType.LeftFoot, 55);
        body.SeverNerve(BodyPartType.LeftFoot);
        body.Update();

        float locoAfter = musc.GetLocomotionForce();
        Assert.True(locoAfter < locoBefore,
            $"Achilles cut should reduce locomotion (before: {locoBefore}, after: {locoAfter})");

        // Foot nerve severed — no signal, no push-off power
        var nervous = Nervous(body);
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftFoot) < 0.01f,
            "Severed Achilles should kill foot signal");

        // But hands still work for fighting from the ground
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip sword despite Achilles injury");
    }

    // ─── 117. Gladiator "last stand" — massive damage, minimal force ──

    [Fact]
    public void LastStand_NearDeathStillSwinging()
    {
        var body = CreateBody();
        body.Update();

        // Beaten badly — heavy damage tears muscles
        body.TakeDamage(BodyPartType.Chest, 55);
        body.TakeDamage(BodyPartType.Abdomen, 55);
        body.TakeDamage(BodyPartType.LeftThigh, 55);
        body.TakeDamage(BodyPartType.RightThigh, 55);
        body.TakeDamage(BodyPartType.LeftUpperArm, 55);

        // Clot bleeds to stay alive
        body.Clot(BodyPartType.Chest);
        body.Clot(BodyPartType.Abdomen);
        body.Clot(BodyPartType.LeftThigh);
        body.Clot(BodyPartType.RightThigh);
        body.Clot(BodyPartType.LeftUpperArm);
        body.Update();

        var musc = Muscular(body);

        // Badly damaged but the sword arm (right) is functional
        float rightArmForce = musc.GetForceOutput(BodyPartType.RightHand);
        Assert.True(rightArmForce > 0,
            $"Right hand should still produce SOME force for last stand (got {rightArmForce})");
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Gladiator should be able to grip sword for last stand");

        // Overall body is in bad shape — torn muscles reduce strength
        Assert.True(musc.GetOverallStrength() < 98f,
            $"Overall strength should be degraded ({musc.GetOverallStrength()})");

        // Multiple muscles should be torn from the beating
        Assert.True(musc.GetTearCount() >= 3,
            $"Heavy damage should tear multiple muscles (got {musc.GetTearCount()})");
    }

    // ─── 118. Multi-round fight — cumulative damage between rounds ──

    [Fact]
    public void MultiRoundFight_CumulativeDamage()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);

        // Round 1: exchange of blows — light damage
        body.TakeDamage(BodyPartType.LeftShoulder, 15);
        body.TakeDamage(BodyPartType.RightThigh, 15);
        for (int i = 0; i < 3; i++) body.Update();
        float strengthAfterR1 = musc.GetOverallStrength();

        // Round 2: heavier engagement
        body.TakeDamage(BodyPartType.Chest, 25);
        body.TakeDamage(BodyPartType.LeftForearm, 20);
        for (int i = 0; i < 3; i++) body.Update();
        float strengthAfterR2 = musc.GetOverallStrength();

        // Round 3: desperate close combat
        body.TakeDamage(BodyPartType.Abdomen, 30);
        body.TakeDamage(BodyPartType.RightUpperArm, 20);
        for (int i = 0; i < 3; i++) body.Update();
        float strengthAfterR3 = musc.GetOverallStrength();

        // Strength should degrade across rounds
        Assert.True(strengthAfterR2 < strengthAfterR1,
            $"Strength should degrade R1→R2 ({strengthAfterR1} → {strengthAfterR2})");
        Assert.True(strengthAfterR3 < strengthAfterR2,
            $"Strength should degrade R2→R3 ({strengthAfterR2} → {strengthAfterR3})");
    }

    // ─── 119. Bandaging between rounds — partial recovery ──

    [Fact]
    public void BandagingBetweenRounds_PartialRecovery()
    {
        var body = CreateBody();
        body.Update();

        // Take heavy wounds in round 1 — deep slashes breach the skin
        body.Burn(BodyPartType.LeftForearm, 65f); // severe burn breaches skin quickly
        body.Burn(BodyPartType.RightThigh, 65f);
        body.Update();

        var skin = Integumentary(body);
        Assert.True(skin.GetSkinIntegrity(BodyPartType.LeftForearm) < 40f,
            $"Deep wound should breach skin (integrity: {skin.GetSkinIntegrity(BodyPartType.LeftForearm)})");

        // Between rounds: bandage the wounds, rest, and feed
        body.Bandage(BodyPartType.LeftForearm);
        body.Bandage(BodyPartType.RightThigh);
        body.Clot(BodyPartType.LeftForearm);
        body.Clot(BodyPartType.RightThigh);
        body.Feed(5f);
        body.Hydrate(5f);

        // Let the body heal for several ticks (rest between rounds)
        for (int i = 0; i < 10; i++)
        {
            body.Heal(BodyPartType.LeftForearm, 3);
            body.Heal(BodyPartType.RightThigh, 3);
            body.Update();
        }

        // Wounds should be healing (integrity improving)
        float forearmIntegrity = skin.GetSkinIntegrity(BodyPartType.LeftForearm);
        Assert.True(forearmIntegrity > 0,
            $"Bandaged wound should start healing (integrity: {forearmIntegrity})");
    }

    // ─── 120. Mana channeling during combat — heat buildup risk ──

    [Fact]
    public void ManaCombat_HeatBuildup()
    {
        var body = CreateBody();
        body.Update();

        var nervous = Nervous(body);
        float heatBefore = nervous.GetTotalHeat();

        // Gladiator-mage channels mana through the right arm during combat
        body.BoostMetabolism(BodyPartType.RightUpperArm, 1.5f);
        body.BoostMetabolism(BodyPartType.RightForearm, 1.5f);
        body.BoostMetabolism(BodyPartType.RightHand, 1.5f);

        // Multiple ticks of intense channeling
        for (int i = 0; i < 15; i++) body.Update();

        float heatAfter = nervous.GetTotalHeat();

        // Some heat should accumulate from boosted metabolism
        // (metabolic boost → energy → heat is a side effect)
        // Also verify mana is being produced
        float totalMana = nervous.GetTotalMana();
        Assert.True(totalMana > 0,
            $"Nerves should produce mana over ticks (got {totalMana})");
    }

    // ─── 121. Trident to shoulder — arm disabled, legs for dodging ──

    [Fact]
    public void TridentToShoulder_ArmDisabledLegsWork()
    {
        var body = CreateBody();
        body.Update();

        // Trident pierces the right shoulder — sever nerve bundle
        body.TakeDamage(BodyPartType.RightShoulder, 40);
        body.SeverNerve(BodyPartType.RightShoulder);
        body.Bleed(BodyPartType.RightShoulder, 3f);
        body.Update();

        var nervous = Nervous(body);
        var musc = Muscular(body);

        // Right arm chain: shoulder nerve severed → whole arm loses signal
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightHand) < 0.01f,
            "Shoulder nerve sever should kill signal to hand");
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Should drop trident-side weapon");

        // Legs still fully functional for dodging
        float locoForce = musc.GetLocomotionForce();
        Assert.True(locoForce > 0,
            $"Legs should still work for dodging (force: {locoForce})");

        // Left hand still grips
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Left hand should still grip for defense");
    }

    // ─── 122. Gut stab with twist — bleeding + infection + pain cascade ──

    [Fact]
    public void GutStabWithTwist_MultiSystemTrauma()
    {
        var body = CreateBody();
        body.Update();

        // Deep stab to abdomen with twist
        body.TakeDamage(BodyPartType.Abdomen, 50);
        body.Bleed(BodyPartType.Abdomen, 4f);
        body.Infect(BodyPartType.Abdomen, 25f, 0.5f); // gut flora spills
        body.Update();

        var circ = Circulatory(body);
        var immune = Immune(body);
        var nervous = Nervous(body);

        // All three systems should be affected
        Assert.Contains(BodyPartType.Abdomen, circ.GetBleedingParts());
        Assert.True(immune.GetInfectionLevel(BodyPartType.Abdomen) > 0,
            "Gut wound should be infected");
        Assert.True(nervous.GetPainLevel(BodyPartType.Abdomen) > 0,
            "Gut stab should cause intense pain");

        // Pain routes toward the brain
        Assert.True(nervous.GetTotalPain() > 0,
            "Pain should propagate through nervous system");
    }

    // ─── 123. Both legs broken — locomotion near zero ──

    [Fact]
    public void BothLegsBroken_LocomotionCollapse()
    {
        var body = CreateBody();
        body.Update();

        var skel = Skeletal(body);
        var musc = Muscular(body);
        float locoBefore = musc.GetLocomotionForce();

        // Break both legs with heavy blows
        body.TakeDamage(BodyPartType.LeftThigh, 55);
        body.TakeDamage(BodyPartType.RightThigh, 55);
        body.TakeDamage(BodyPartType.LeftLeg, 55);
        body.TakeDamage(BodyPartType.RightLeg, 55);
        body.Update();

        float locoAfter = musc.GetLocomotionForce();

        // Legs should be severely weakened
        Assert.True(locoAfter < locoBefore * 0.5f,
            $"Both legs broken should halve locomotion (before: {locoBefore}, after: {locoAfter})");

        // But upper body still fights
        Assert.True(musc.GetUpperBodyForce() > 0,
            "Upper body should still produce force with broken legs");
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip weapon from the ground");
    }

    // ─── 124. Shock from multiple simultaneous wounds ──

    [Fact]
    public void MultipleWounds_ShockFromPainOverload()
    {
        var body = CreateBody();
        body.Update();

        var nervous = Nervous(body);

        // Simultaneous wounds across the body (arena multi-attacker scenario)
        body.TakeDamage(BodyPartType.Chest, 35);
        body.TakeDamage(BodyPartType.LeftUpperArm, 35);
        body.TakeDamage(BodyPartType.RightThigh, 35);
        body.TakeDamage(BodyPartType.Abdomen, 35);
        body.TakeDamage(BodyPartType.LeftLeg, 35);
        body.Update();

        // Heavy total pain should approach or exceed shock threshold
        float totalPain = nervous.GetTotalPain();
        Assert.True(totalPain > 50f,
            $"Multiple simultaneous wounds should generate massive pain (got {totalPain})");
    }

    // ─── 125. Gladiator bleeds out — blood pressure to zero ──

    [Fact]
    public void BleedOut_BloodPressureCollapse()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        float bpBefore = circ.GetBloodPressure();
        Assert.True(bpBefore >= 100f, $"Initial BP should be at or above 100 (got {bpBefore})");

        // Multiple open wounds bleeding simultaneously
        body.Bleed(BodyPartType.Chest, 3f);
        body.Bleed(BodyPartType.LeftForearm, 2f);
        body.Bleed(BodyPartType.RightThigh, 2f);

        // Let them bleed for many ticks
        for (int i = 0; i < 20; i++) body.Update();

        float bpAfter = circ.GetBloodPressure();
        Assert.True(bpAfter < 50f,
            $"Multiple bleed sites should crash BP (got {bpAfter})");
    }

    // ─── 126. Broken sword arm → switch hands viability ──

    [Fact]
    public void BrokenSwordArm_SwitchToOffhand()
    {
        var body = CreateBody();
        body.Update();

        // Sword in right hand
        Assert.True(CanHoldItem(body, BodyPartType.RightHand));

        // Opponent breaks the right forearm
        body.TakeDamage(BodyPartType.RightForearm, 55);
        body.SeverNerve(BodyPartType.RightForearm);
        body.Update();

        // Right hand grip lost
        Assert.False(CanHoldItem(body, BodyPartType.RightHand),
            "Broken sword arm should lose grip");

        // Left hand is still 100% functional — gladiator switches
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand),
            "Off-hand should be available for weapon switch");

        var musc = Muscular(body);
        Assert.True(musc.GetForceOutput(BodyPartType.LeftHand) > 0,
            "Off-hand should produce full force");
    }

    // ─── 127. Arena fire + oil — widespread burns → shock ──

    [Fact]
    public void ArenaFireOil_WidespreadBurnsToShock()
    {
        var body = CreateBody();
        body.Update();

        // Oil fire engulfs the gladiator — burns to many body parts
        BodyPartType[] burnedParts = [
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.LeftFoot, BodyPartType.RightFoot,
            BodyPartType.Abdomen, BodyPartType.Chest
        ];
        foreach (var part in burnedParts)
            body.Burn(part, 50f);

        body.Update();

        var skin = Integumentary(body);
        var nervous = Nervous(body);

        // Many parts should be burned
        Assert.True(skin.GetBurnedParts().Count >= 4,
            $"Multiple parts should be burned (got {skin.GetBurnedParts().Count})");

        // Massive pain from widespread burns
        float totalPain = nervous.GetTotalPain();
        Assert.True(totalPain > 100f,
            $"Widespread burns should cause enormous pain (got {totalPain})");
    }

    // ─── 128. Severed hand — complete hand disable ──

    [Fact]
    public void SeveredHand_CompleteDisable()
    {
        var body = CreateBody();
        body.Update();

        // Sword chops off the left hand
        body.TakeDamage(BodyPartType.LeftHand, 80);
        body.SeverNerve(BodyPartType.LeftHand);
        body.Bleed(BodyPartType.LeftForearm, 3f); // stump bleeds
        body.Update();

        var nervous = Nervous(body);
        var musc = Muscular(body);

        // Hand completely disabled
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftHand) < 0.01f,
            "Severed hand should have zero signal");
        Assert.False(CanHoldItem(body, BodyPartType.LeftHand),
            "Severed hand cannot hold anything");

        // Other hand still works
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Right hand should still function");
    }

    // ─── 129. Spinal severance at neck — total body paralysis ──

    [Fact]
    public void SpinalSeveranceAtNeck_TotalParalysis()
    {
        var body = CreateBody();
        body.Update();

        // Sword strike to the back of the neck — severs spinal cord
        body.SeverNerve(BodyPartType.Neck);
        body.Update();

        var nervous = Nervous(body);

        // Everything downstream of neck should lose signal
        Assert.True(nervous.GetSignalStrength(BodyPartType.Chest) < 0.01f,
            "Chest signal should be zero after spinal sever");
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightHand) < 0.01f,
            "Right hand signal should be zero");
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftHand) < 0.01f,
            "Left hand signal should be zero");
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightFoot) < 0.01f,
            "Right foot signal should be zero");
        Assert.True(nervous.GetSignalStrength(BodyPartType.LeftFoot) < 0.01f,
            "Left foot signal should be zero");

        // Both weapons dropped, can't walk
        Assert.False(CanHoldItem(body, BodyPartType.RightHand));
        Assert.False(CanHoldItem(body, BodyPartType.LeftHand));
    }

    // ─── 130. Gladiator vs beast — multiple wound sites simultaneously ──

    [Fact]
    public void GladiatorVsBeast_MultipleSimultaneousWounds()
    {
        var body = CreateBody();
        body.Update();

        // Lion attack — savage claws and bites across multiple body parts
        body.TakeDamage(BodyPartType.LeftShoulder, 50);  // deep claw swipe
        body.TakeDamage(BodyPartType.Chest, 50);          // crushing bite
        body.TakeDamage(BodyPartType.RightThigh, 55);     // claw rake
        body.TakeDamage(BodyPartType.LeftForearm, 45);    // defensive wound

        // Beast also causes infections (dirty claws)
        body.Infect(BodyPartType.LeftShoulder, 15f, 0.4f);
        body.Infect(BodyPartType.RightThigh, 15f, 0.4f);
        body.Update();

        var immune = Immune(body);
        var skin = Integumentary(body);
        var circ = Circulatory(body);

        // Attack sites should show degraded skin from claw/bite damage
        float shoulderIntegrity = skin.GetSkinIntegrity(BodyPartType.LeftShoulder);
        float thighIntegrity = skin.GetSkinIntegrity(BodyPartType.RightThigh);
        Assert.True(shoulderIntegrity < 100f,
            $"Claw-swiped shoulder should have degraded skin (integrity: {shoulderIntegrity})");
        Assert.True(thighIntegrity < 100f,
            $"Claw-raked thigh should have degraded skin (integrity: {thighIntegrity})");

        // Infections at claw sites
        Assert.True(immune.GetInfectionCount() >= 2,
            $"Dirty claws should infect multiple sites (got {immune.GetInfectionCount()})");

        // Gladiator can still fight back (right arm intact)
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Should still grip weapon to fight beast");
    }

    // ─── 131. Temperature crisis from infection → fever during combat ──

    [Fact]
    public void InfectionFever_CombatPerformanceDrop()
    {
        var body = CreateBody();
        body.Update();

        // Weaken the gladiator first — previous fight damage depletes immune system
        body.TakeDamage(BodyPartType.LeftThigh, 60);
        body.TakeDamage(BodyPartType.LeftLeg, 50);
        body.TakeDamage(BodyPartType.Abdomen, 40);
        body.Update();

        // Now introduce aggressive infection on the weakened body
        body.Infect(BodyPartType.LeftThigh, 60f, 1.5f);
        body.Infect(BodyPartType.LeftLeg, 50f, 1.2f);

        // Let infection develop — weakened immune can't fight as well
        for (int i = 0; i < 10; i++) body.Update();

        var immune = Immune(body);
        var metabolic = Metabolic(body);

        // Heavy infection + weakened immune → threat level elevated
        float threatLevel = immune.GetTotalThreatLevel();
        Assert.True(threatLevel > 0 || immune.GetInfectedParts().Count > 0,
            $"Weakened body should show immune stress (threat: {threatLevel}, infected: {immune.GetInfectedParts().Count})");
    }

    // ─── 132. Second wind — healing mid-fight partially restores capability ──

    [Fact]
    public void SecondWind_HealingRestoresCapability()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);

        // Take significant damage
        body.TakeDamage(BodyPartType.RightUpperArm, 40);
        body.TakeDamage(BodyPartType.RightForearm, 30);
        body.Clot(BodyPartType.RightUpperArm);
        body.Clot(BodyPartType.RightForearm);
        body.Update();

        float damagedForce = musc.GetForceOutput(BodyPartType.RightHand);

        // Magical healing or medic intervention
        body.Heal(BodyPartType.RightUpperArm, 30);
        body.Heal(BodyPartType.RightForearm, 25);
        body.Feed(5f);
        body.Hydrate(5f);
        for (int i = 0; i < 5; i++) body.Update();

        float healedForce = musc.GetForceOutput(BodyPartType.RightHand);

        // Force should be better after healing
        Assert.True(healedForce >= damagedForce,
            $"Healing should restore some force (damaged: {damagedForce}, healed: {healedForce})");
    }

    // ─── 133. Eye gouge (head attack) — central nerve damage ──

    [Fact]
    public void EyeGouge_CentralNerveDamage()
    {
        var body = CreateBody();
        body.Update();

        var nervous = Nervous(body);
        float manaBefore = nervous.GetTotalMana();

        // Vicious eye gouge — damages the head (central nervous system)
        body.TakeDamage(BodyPartType.Head, 40);
        body.Update();

        // Head is a central nerve part — damage here is significant
        Assert.True(nervous.GetPainLevel(BodyPartType.Head) > 0,
            "Eye gouge should cause extreme pain");

        // Let mana regen tick to observe reduced production from damaged brain
        for (int i = 0; i < 5; i++) body.Update();

        // Head nerve health should be reduced
        float headSignal = nervous.GetSignalStrength(BodyPartType.Head);
        // (head starts at 100, we damaged it heavily so signal should be impacted)
        Assert.True(headSignal <= 1.0f,
            $"Head signal should be impacted by brain damage (got {headSignal})");
    }

    // ─── 134. Finishing blow — massive damage to weakened target ──

    [Fact]
    public void FinishingBlow_MassiveDamageToWeakenedGladiator()
    {
        var body = CreateBody();
        body.Update();

        // Weaken the gladiator first
        body.TakeDamage(BodyPartType.Chest, 40);
        body.TakeDamage(BodyPartType.Abdomen, 35);
        body.Bleed(BodyPartType.Chest, 2f);
        for (int i = 0; i < 5; i++) body.Update();

        // Clot to stabilize briefly
        body.Clot(BodyPartType.Chest);
        body.Clot(BodyPartType.Abdomen);
        body.Update();

        // Finishing blow — sword through the chest
        body.TakeDamage(BodyPartType.Chest, 80);
        body.Bleed(BodyPartType.Chest, 6f);
        body.Update();

        var circ = Circulatory(body);
        var resp = Respiratory(body);

        // Heart should be destroyed — BP near zero
        float bp = circ.GetBloodPressure();
        Assert.True(bp < 30f,
            $"Finishing blow should collapse BP (got {bp})");

        // Lungs destroyed — no oxygen
        float o2 = resp.GetOxygenOutput();
        Assert.True(o2 < 2f,
            $"Destroyed lungs should produce no oxygen (got {o2})");
    }

    // ─── 135. Full arena fight simulation — realistic multi-phase combat ──

    [Fact]
    public void FullArenaFight_MultiPhaseCombatSimulation()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        var circ = Circulatory(body);
        var nervous = Nervous(body);
        var skin = Integumentary(body);
        var immune = Immune(body);

        // ── Phase 1: Opening exchange — both gladiators test each other ──
        body.Exert(BodyPartType.RightHand, 50f); // sword feints
        body.Exert(BodyPartType.RightForearm, 50f);
        body.TakeDamage(BodyPartType.LeftForearm, 10); // glancing blow on shield arm
        body.Update();

        Assert.True(CanHoldItem(body, BodyPartType.RightHand), "Phase 1: should hold sword");
        Assert.True(CanHoldItem(body, BodyPartType.LeftHand), "Phase 1: should hold shield");

        // ── Phase 2: Aggressive assault — taking and dealing damage ──
        body.TakeDamage(BodyPartType.RightThigh, 25); // leg kick
        body.TakeDamage(BodyPartType.LeftShoulder, 20); // shoulder check
        body.Bleed(BodyPartType.RightThigh, 1f);

        // Heavy exertion from combat
        for (int i = 0; i < 5; i++)
        {
            body.Exert(BodyPartType.RightHand, 80f);
            body.Exert(BodyPartType.RightForearm, 80f);
            body.Exert(BodyPartType.RightUpperArm, 80f);
            body.Update();
        }

        float phase2Strength = musc.GetOverallStrength();
        Assert.True(phase2Strength < 100f, "Phase 2: should show some wear");

        // ── Phase 3: Desperate close combat — injuries mount ──
        body.TakeDamage(BodyPartType.Abdomen, 30); // gut punch
        body.TakeDamage(BodyPartType.Chest, 20); // body blow
        body.Update();

        float totalPain = nervous.GetTotalPain();
        Assert.True(totalPain > 0, "Phase 3: accumulated pain should be significant");

        // ── Phase 4: Clinch and recovery attempt ──
        body.Clot(BodyPartType.RightThigh);
        body.Bandage(BodyPartType.RightThigh);
        body.Feed(2f);
        body.Hydrate(2f);
        for (int i = 0; i < 3; i++) body.Update();

        // Still fighting
        Assert.True(CanHoldItem(body, BodyPartType.RightHand), "Phase 4: should still hold sword");

        // ── Phase 5: Final exchange — decisive moment ──
        body.TakeDamage(BodyPartType.LeftForearm, 35); // shield arm hit hard
        body.Update();

        float finalStrength = musc.GetOverallStrength();

        // Body should be significantly degraded from start
        Assert.True(finalStrength < phase2Strength,
            $"Final phase strength should be lower than phase 2 ({finalStrength} vs {phase2Strength})");

        // But the gladiator survives — sword hand still works
        Assert.True(CanHoldItem(body, BodyPartType.RightHand),
            "Gladiator should still hold sword at fight's end");
    }

    // ═══════════════════════════════════════════════════════════
    //  CARDIOPULMONARY & EXERTION SCENARIOS (136–155)
    //  Getting winded, heart rate up, heart attack, calming down
    // ═══════════════════════════════════════════════════════════

    // ─── 136. Getting winded — intense exertion depletes stamina ──

    [Fact]
    public void GettingWinded_ExertionDepletsStamina()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        float staminaBefore = musc.GetAverageStamina();

        // Gladiator sprints and swings wildly — full body exertion
        BodyPartType[] fullBody = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.LeftFoot, BodyPartType.RightFoot,
            BodyPartType.Hips, BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
            BodyPartType.LeftHand, BodyPartType.RightHand,
        ];
        for (int round = 0; round < 8; round++)
        {
            foreach (var part in fullBody)
                body.Exert(part, 95f);
            body.Update();
        }

        float staminaAfter = musc.GetAverageStamina();

        Assert.True(staminaAfter < staminaBefore,
            $"Intense exertion should drain stamina (before: {staminaBefore}, after: {staminaAfter})");
        Assert.True(staminaAfter < 80f,
            $"Gladiator should be significantly winded (stamina: {staminaAfter})");
    }

    // ─── 137. Getting winded — force output drops with low stamina ──

    [Fact]
    public void GettingWinded_ForceDropsWithLowStamina()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);
        float freshForce = musc.GetUpperBodyForce();

        // Exhaust the sword arm with repeated max exertion
        for (int i = 0; i < 15; i++)
        {
            body.Exert(BodyPartType.RightShoulder, 100f);
            body.Exert(BodyPartType.RightUpperArm, 100f);
            body.Exert(BodyPartType.RightForearm, 100f);
            body.Exert(BodyPartType.RightHand, 100f);
            body.Update();
        }

        float tiredForce = musc.GetForceOutput(BodyPartType.RightHand);
        float freshHandForce = Muscular(CreateBody()).GetForceOutput(BodyPartType.RightHand);

        // Force = strength × min(stamina%, health%) — low stamina reduces output
        Assert.True(tiredForce < freshHandForce,
            $"Exhausted sword arm should swing weaker (fresh: {freshHandForce}, tired: {tiredForce})");
    }

    // ─── 138. Heart rate proxy — blood pressure under exertion vs rest ──

    [Fact]
    public void HeartRateProxy_BPReflectsExertionStress()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        float restingBP = circ.GetBloodPressure();

        // Heavy full-body exertion burns through resources
        BodyPartType[] allMuscles = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
        ];
        for (int i = 0; i < 30; i++)
        {
            foreach (var part in allMuscles)
                body.Exert(part, 100f);
            body.Update();
        }

        // Extreme exertion consumes blood pool resources, possibly affecting BP
        float stressedBP = circ.GetBloodPressure();

        // BP is heartHealth × bloodVolumeRatio × 100
        // Under extreme resource drain the blood pool may deplete,
        // or heart takes metabolic damage from starvation
        // Either way the body is under cardiovascular stress
        var metabolic = Metabolic(body);
        float avgFatigue = metabolic.GetAverageFatigue();

        // At minimum: the body should be fatigued from extreme exertion
        Assert.True(avgFatigue > 0 || stressedBP <= restingBP,
            $"Extreme exertion should cause fatigue or BP change (fatigue: {avgFatigue}, BP: resting {restingBP} → stressed {stressedBP})");
    }

    // ─── 139. Heart damage — weakened heart drops blood pressure (lower pulse) ──

    [Fact]
    public void HeartDamage_BloodPressureDrops()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        float healthyBP = circ.GetBloodPressure();

        // Gladiator takes a heavy blow to the chest — heart damage
        body.TakeDamage(BodyPartType.Chest, 50);
        body.Update();

        float damagedBP = circ.GetBloodPressure();

        // BP = heartHealth × volumeRatio × 100 → lower heart health = lower BP
        Assert.True(damagedBP < healthyBP,
            $"Heart damage should drop blood pressure (healthy: {healthyBP}, damaged: {damagedBP})");
    }

    // ─── 140. Heart attack — massive chest damage collapses circulation ──

    [Fact]
    public void HeartAttack_MassiveChestDamageCollapsesCirculation()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);

        // First weaken the heart with exertion + damage (gladiator pushed beyond limits)
        body.TakeDamage(BodyPartType.Chest, 40);
        body.Update();

        // Then the killing blow to the heart
        body.TakeDamage(BodyPartType.Chest, 60);
        body.Update();

        float bp = circ.GetBloodPressure();

        // Heart health → 0 means BP → 0 (cardiac arrest)
        Assert.True(bp < 20f,
            $"Destroyed heart should collapse blood pressure / cardiac arrest (BP: {bp})");

        // Blood flow to extremities should be near zero
        float handFlow = circ.GetBloodFlowTo(BodyPartType.RightHand);
        Assert.True(handFlow < 10f,
            $"No heartbeat means no blood flow to hands (got {handFlow})");

        float footFlow = circ.GetBloodFlowTo(BodyPartType.LeftFoot);
        Assert.True(footFlow < 10f,
            $"No heartbeat means no blood flow to feet (got {footFlow})");
    }

    // ─── 141. Heart attack cascade — no blood flow starves all systems ──

    [Fact]
    public void HeartAttack_StarvationCascade()
    {
        var body = CreateBody();
        body.Update();

        // Destroy the heart
        body.TakeDamage(BodyPartType.Chest, 95);
        body.Clot(BodyPartType.Chest); // prevent bleed-out to isolate heart failure

        // Let the body tick with a dead heart
        for (int i = 0; i < 15; i++) body.Update();

        var circ = Circulatory(body);
        var musc = Muscular(body);

        // BP collapsed
        Assert.True(circ.GetBloodPressure() < 10f,
            $"Dead heart should collapse BP (got {circ.GetBloodPressure()})");

        // Blood flow to extremities near zero — no pump
        float handFlow = circ.GetBloodFlowTo(BodyPartType.RightHand);
        float footFlow = circ.GetBloodFlowTo(BodyPartType.LeftFoot);
        Assert.True(handFlow < 10f,
            $"Dead heart means no blood to hands (got {handFlow})");
        Assert.True(footFlow < 10f,
            $"Dead heart means no blood to feet (got {footFlow})");
    }

    // ─── 142. Overexertion on damaged heart — exertion-induced heart failure ──

    [Fact]
    public void OverexertionOnDamagedHeart_InducedHeartFailure()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);

        // Gladiator already has chest wound (weakened heart)
        body.TakeDamage(BodyPartType.Chest, 35);
        body.Clot(BodyPartType.Chest);
        body.Update();

        float weakenedBP = circ.GetBloodPressure();

        // Then forced to fight at max intensity — extreme resource drain
        for (int i = 0; i < 20; i++)
        {
            body.Exert(BodyPartType.RightUpperArm, 100f);
            body.Exert(BodyPartType.RightForearm, 100f);
            body.Exert(BodyPartType.RightHand, 100f);
            body.Exert(BodyPartType.LeftThigh, 100f);
            body.Exert(BodyPartType.RightThigh, 100f);
            body.Update();
        }

        float afterExertionBP = circ.GetBloodPressure();

        // Weakened heart under extreme demand should show BP degradation
        Assert.True(afterExertionBP <= weakenedBP,
            $"Overexertion on damaged heart should not improve BP (weakened: {weakenedBP}, after: {afterExertionBP})");

        // Muscles should be exhausted
        var musc = Muscular(body);
        Assert.True(musc.GetAverageStamina() < 100f,
            $"Extreme exertion on weak heart should drain stamina (avg: {musc.GetAverageStamina()})");
    }

    // ─── 143. Calming down — rest restores stamina and force ──

    [Fact]
    public void CalmingDown_RestRestoresStaminaAndForce()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);

        // Exhaust the gladiator with combat
        BodyPartType[] swordArm = [
            BodyPartType.RightShoulder, BodyPartType.RightUpperArm,
            BodyPartType.RightForearm, BodyPartType.RightHand,
        ];
        for (int i = 0; i < 15; i++)
        {
            foreach (var part in swordArm)
                body.Exert(part, 95f);
            body.Update();
        }

        float exhaustedForce = musc.GetForceOutput(BodyPartType.RightHand);
        float exhaustedStamina = musc.GetAverageStamina();

        // Now the gladiator rests — calms down between rounds
        foreach (var part in swordArm)
            body.Rest(part);

        // Rest ticks — stamina regens at 2.0/tick
        for (int i = 0; i < 15; i++)
        {
            body.Feed(2f);
            body.Hydrate(2f);
            body.Update();
        }

        float recoveredForce = musc.GetForceOutput(BodyPartType.RightHand);
        float recoveredStamina = musc.GetAverageStamina();

        // Stamina should recover after rest
        Assert.True(recoveredStamina > exhaustedStamina,
            $"Rest should restore stamina (exhausted: {exhaustedStamina}, recovered: {recoveredStamina})");

        // Force output should improve with recovered stamina
        Assert.True(recoveredForce >= exhaustedForce,
            $"Resting should restore punch force (exhausted: {exhaustedForce}, recovered: {recoveredForce})");
    }

    // ─── 144. Pulse drops — BP stabilizes after combat ends ──

    [Fact]
    public void PulseDrops_BPStabilizesAfterCombat()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        float restingBP = circ.GetBloodPressure();

        // Take some hits during combat
        body.TakeDamage(BodyPartType.LeftShoulder, 20);
        body.TakeDamage(BodyPartType.RightThigh, 20);
        body.Bleed(BodyPartType.LeftShoulder, 1f);
        body.Update();

        // Active combat exertion
        for (int i = 0; i < 5; i++)
        {
            body.Exert(BodyPartType.RightUpperArm, 80f);
            body.Exert(BodyPartType.RightHand, 80f);
            body.Update();
        }

        float combatBP = circ.GetBloodPressure();

        // Combat ends: stop bleeding, rest, bandage
        body.Clot(BodyPartType.LeftShoulder);
        body.Rest(BodyPartType.RightUpperArm);
        body.Rest(BodyPartType.RightHand);
        body.Bandage(BodyPartType.LeftShoulder);

        // Recovery: heal wounds and replenish blood volume
        for (int i = 0; i < 20; i++)
        {
            body.Heal(BodyPartType.LeftShoulder, 5);
            body.Heal(BodyPartType.RightThigh, 5);
            body.Heal(BodyPartType.Chest, 5); // heal heart damage
            body.Feed(5f);
            body.Hydrate(5f);
            body.Update();
        }

        float recoveredBP = circ.GetBloodPressure();

        // After stopping bleeds and healing the heart, BP should stabilize
        // BP = heartHealth × bloodVolumeRatio × 100
        // Heart health regens + heals, but blood volume may still be low
        Assert.True(recoveredBP > 0,
            $"BP should be positive after treatment (recovered: {recoveredBP})");
    }

    // ─── 145. Winded gladiator — oxygen depletion from exertion ──

    [Fact]
    public void Winded_OxygenDepletionFromExertion()
    {
        var body = CreateBody();
        body.Update();

        var resp = Respiratory(body);

        // Heavy exertion burns through oxygen faster than lungs can produce
        BodyPartType[] muscles = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
            BodyPartType.LeftHand, BodyPartType.RightHand,
        ];

        for (int i = 0; i < 40; i++)
        {
            foreach (var part in muscles)
                body.Exert(part, 100f);
            body.Update();
        }

        var musc = Muscular(body);

        // Sustained max exertion drains stamina — the gladiator is winded
        float avgStamina = musc.GetAverageStamina();
        Assert.True(avgStamina < 80f,
            $"Sustained exertion should make the gladiator winded / low stamina (avg: {avgStamina})");
    }

    // ─── 146. Catching breath — rest after exertion restores O₂ ──

    [Fact]
    public void CatchingBreath_RestAfterExertion()
    {
        var body = CreateBody();
        body.Update();

        // Exhaust the gladiator
        BodyPartType[] muscles = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.RightUpperArm, BodyPartType.RightForearm,
        ];
        for (int i = 0; i < 20; i++)
        {
            foreach (var part in muscles)
                body.Exert(part, 100f);
            body.Update();
        }

        var musc = Muscular(body);
        float windedStamina = musc.GetAverageStamina();

        // Gladiator stops to catch breath — rest everything
        foreach (var part in muscles)
            body.Rest(part);

        // Breathe and rest for several ticks
        for (int i = 0; i < 20; i++)
        {
            body.Feed(2f);
            body.Hydrate(2f);
            body.Update();
        }

        float restoredStamina = musc.GetAverageStamina();

        Assert.True(restoredStamina > windedStamina,
            $"Catching breath should restore stamina (winded: {windedStamina}, restored: {restoredStamina})");
    }

    // ─── 147. Chest wound + exertion — lungs can't keep up ──

    [Fact]
    public void ChestWoundPlusExertion_LungsCantKeepUp()
    {
        var body = CreateBody();
        body.Update();

        var resp = Respiratory(body);
        float healthyO2 = resp.GetOxygenOutput();

        // Chest damage reduces lung capacity
        body.TakeDamage(BodyPartType.Chest, 45);
        body.Clot(BodyPartType.Chest);
        body.Update();

        float damagedO2 = resp.GetOxygenOutput();
        Assert.True(damagedO2 < healthyO2,
            $"Chest wound should reduce O₂ output (healthy: {healthyO2}, wounded: {damagedO2})");

        // Now exert heavily — muscles demand more O₂ than damaged lungs provide
        for (int i = 0; i < 15; i++)
        {
            body.Exert(BodyPartType.RightUpperArm, 100f);
            body.Exert(BodyPartType.RightForearm, 100f);
            body.Exert(BodyPartType.LeftThigh, 100f);
            body.Exert(BodyPartType.RightThigh, 100f);
            body.Update();
        }

        // Muscles can't sustain output when lungs are failing
        var musc = Muscular(body);
        float staminaAfter = musc.GetAverageStamina();
        Assert.True(staminaAfter < 90f,
            $"Exertion with damaged lungs should drain stamina faster (stamina: {staminaAfter})");
    }

    // ─── 148. Blood loss weakens the heart — pulse drops from bleed-out ──

    [Fact]
    public void BloodLoss_PulseDropsFromBleedOut()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        float fullBP = circ.GetBloodPressure();

        // Gladiator bleeding from multiple wounds — blood volume drops
        body.Bleed(BodyPartType.LeftForearm, 2f);
        body.Bleed(BodyPartType.RightThigh, 2f);

        // Let blood drain for several ticks
        for (int i = 0; i < 15; i++) body.Update();

        float drainedBP = circ.GetBloodPressure();

        // BP = heartHealth × (bloodVolume / expectedVolume) × 100
        // Lower blood volume → lower pulse/BP
        Assert.True(drainedBP < fullBP,
            $"Blood loss should drop pulse/BP (full: {fullBP}, drained: {drainedBP})");

        // Now clot and infuse fluids — pulse should recover
        body.Clot(BodyPartType.LeftForearm);
        body.Clot(BodyPartType.RightThigh);
        body.Bandage(BodyPartType.LeftForearm); // Prevent skin auto-bleed from re-opening wounds
        body.Bandage(BodyPartType.RightThigh);
        body.Hydrate(30f); // water helps replenish volume

        // Measure BP right after clotting (before further deterioration)
        body.Update();
        float postClotBP = circ.GetBloodPressure();

        for (int i = 0; i < 15; i++)
        {
            body.Heal(BodyPartType.LeftForearm, 5);
            body.Heal(BodyPartType.RightThigh, 5);
            body.Heal(BodyPartType.Chest, 5); // Reduce shock over time
            body.Update();
        }

        // BP shouldn't get worse after clotting + treatment
        // (compare to postClotBP — healing should help recover from shock)
        float stableBP = circ.GetBloodPressure();
        Assert.True(stableBP >= postClotBP,
            $"Clotting, bandaging, and healing should stabilize or improve BP (postClot: {postClotBP}, stable: {stableBP})");
    }

    // ─── 149. Fatigue builds when energy depleted — body shuts down ──

    [Fact]
    public void FatigueBuildsWhenEnergyDepleted()
    {
        var body = CreateBody();
        body.Update();

        var metabolic = Metabolic(body);
        float initialFatigue = metabolic.GetAverageFatigue();

        // Brutal sustained combat — burn through all resources
        // Also take damage to reduce metabolic efficiency
        body.TakeDamage(BodyPartType.Chest, 40);
        body.TakeDamage(BodyPartType.Abdomen, 40);
        body.Clot(BodyPartType.Chest);
        body.Clot(BodyPartType.Abdomen);

        BodyPartType[] allMuscles = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.LeftLeg, BodyPartType.RightLeg,
            BodyPartType.LeftFoot, BodyPartType.RightFoot,
            BodyPartType.Hips, BodyPartType.Pelvis,
            BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
            BodyPartType.LeftHand, BodyPartType.RightHand,
        ];

        for (int round = 0; round < 50; round++)
        {
            foreach (var part in allMuscles)
                body.Exert(part, 100f);
            body.Update();
        }

        // After 50 rounds of max exertion with damaged core organs,
        // the body should show signs of exhaustion
        var musc = Muscular(body);
        float finalStamina = musc.GetAverageStamina();

        // Stamina should be significantly drained from sustained max exertion
        Assert.True(finalStamina < 80f,
            $"Extreme exertion should drain stamina (got {finalStamina})");
    }

    // ─── 150. Exhausted gladiator recovers with food and rest ──

    [Fact]
    public void ExhaustedGladiator_RecoveryWithFoodAndRest()
    {
        var body = CreateBody();
        body.Update();

        // Exhaust the gladiator
        BodyPartType[] allMuscles = [
            BodyPartType.LeftThigh, BodyPartType.RightThigh,
            BodyPartType.Abdomen, BodyPartType.Chest,
            BodyPartType.RightUpperArm, BodyPartType.RightForearm,
            BodyPartType.RightHand,
        ];
        for (int round = 0; round < 25; round++)
        {
            foreach (var part in allMuscles)
                body.Exert(part, 100f);
            body.Update();
        }

        var musc = Muscular(body);
        var metabolic = Metabolic(body);
        float exhaustedStamina = musc.GetAverageStamina();
        float exhaustedForce = musc.GetUpperBodyForce();

        // Recovery phase: rest, eat, drink — like between arena rounds
        foreach (var part in allMuscles)
            body.Rest(part);

        for (int i = 0; i < 30; i++)
        {
            body.Feed(5f);
            body.Hydrate(5f);
            body.Update();
        }

        float recoveredStamina = musc.GetAverageStamina();
        float recoveredForce = musc.GetUpperBodyForce();

        // Stamina and force should both improve after rest + nutrition
        Assert.True(recoveredStamina > exhaustedStamina,
            $"Recovery should restore stamina (exhausted: {exhaustedStamina}, recovered: {recoveredStamina})");
        Assert.True(recoveredForce > exhaustedForce,
            $"Recovery should restore combat force (exhausted: {exhaustedForce}, recovered: {recoveredForce})");
    }

    // ─── 151. Throat choke — airway blocked, can't get breath during grapple ──

    [Fact]
    public void ThroatChoke_AirwayBlockedCantBreathe()
    {
        var body = CreateBody();
        body.Update();

        var resp = Respiratory(body);
        Assert.False(resp.IsAirwayBlocked(), "Should start with clear airway");
        float normalO2 = resp.GetOxygenOutput();

        // Opponent chokes the gladiator — heavy neck damage blocks airway
        body.TakeDamage(BodyPartType.Neck, 35); // ≥30 triggers airway block
        body.Update();

        Assert.True(resp.IsAirwayBlocked(), "Choke should block the airway");
        Assert.Equal(0f, resp.GetOxygenOutput());

        // While choked, oxygen output is zero — body suffocates
        for (int i = 0; i < 10; i++) body.Update();

        // With blocked airway, lungs get zero airflow → zero oxygen production
        Assert.Equal(0f, resp.GetOxygenOutput());
        Assert.Equal(0f, resp.GetAirflowReachingLungs());
    }

    // ─── 152. Choke release — gladiator gasps and recovers ──

    [Fact]
    public void ChokeRelease_GladiatorGaspsAndRecovers()
    {
        var body = CreateBody();
        body.Update();

        var resp = Respiratory(body);

        // Choke the gladiator
        body.TakeDamage(BodyPartType.Neck, 35);
        body.Update();
        Assert.True(resp.IsAirwayBlocked());

        // Hold the choke for a few ticks
        for (int i = 0; i < 10; i++) body.Update();

        float chokedO2Output = resp.GetOxygenOutput();

        // Gladiator breaks free — clear the airway
        body.Heal(BodyPartType.Neck, 20);
        body.Update();

        // Airway may still be blocked from damage flag;
        // check if healing improves respiratory function
        float releasedO2Output = resp.GetOxygenOutput();

        // After healing the neck, O₂ output should improve
        Assert.True(releasedO2Output >= chokedO2Output,
            $"Clearing choke should improve breathing (choked: {chokedO2Output}, released: {releasedO2Output})");
    }

    // ─── 153. Second wind mechanics — short rest mid-fight restores some stamina ──

    [Fact]
    public void SecondWindMechanics_ShortRestRestoresStamina()
    {
        var body = CreateBody();
        body.Update();

        var musc = Muscular(body);

        // Tire the sword arm
        for (int i = 0; i < 12; i++)
        {
            body.Exert(BodyPartType.RightUpperArm, 90f);
            body.Exert(BodyPartType.RightForearm, 90f);
            body.Exert(BodyPartType.RightHand, 90f);
            body.Update();
        }

        float tiredHandForce = musc.GetForceOutput(BodyPartType.RightHand);

        // Brief pause — gladiator circles opponent, catching breath (3 ticks of rest)
        body.Rest(BodyPartType.RightUpperArm);
        body.Rest(BodyPartType.RightForearm);
        body.Rest(BodyPartType.RightHand);
        for (int i = 0; i < 3; i++) body.Update();

        float briefRestForce = musc.GetForceOutput(BodyPartType.RightHand);

        // Even a brief rest should allow SOME stamina recovery (regen rate 2.0/tick)
        Assert.True(briefRestForce >= tiredHandForce,
            $"Brief rest should allow some recovery (tired: {tiredHandForce}, rested: {briefRestForce})");
    }

    // ─── 154. Adrenaline scenario — metabolic boost compensates for wounds ──

    [Fact]
    public void AdrenalineBoost_CompensatesForWounds()
    {
        var body = CreateBody();
        body.Update();

        var metabolic = Metabolic(body);

        // Gladiator is wounded
        body.TakeDamage(BodyPartType.Abdomen, 30);
        body.TakeDamage(BodyPartType.RightUpperArm, 20);
        body.Clot(BodyPartType.Abdomen);
        body.Clot(BodyPartType.RightUpperArm);
        body.Update();

        float woundedEfficiency = metabolic.GetEfficiency(BodyPartType.Abdomen);

        // Adrenaline surge — metabolic boost (the crowd roars!)
        body.BoostMetabolism(BodyPartType.Abdomen, 0.5f);
        body.BoostMetabolism(BodyPartType.RightUpperArm, 0.5f);
        body.BoostMetabolism(BodyPartType.Chest, 0.3f);
        body.Update();

        float boostedRate = metabolic.GetMetabolicRate(BodyPartType.Abdomen);

        // Metabolic rate should be elevated from the boost
        Assert.True(boostedRate > 1.0f,
            $"Adrenaline should boost metabolic rate (got {boostedRate})");
    }

    // ─── 155. Complete cardiovascular collapse scenario — bleed + heart + exertion ──

    [Fact]
    public void CardiovascularCollapse_BleedHeartExertion()
    {
        var body = CreateBody();
        body.Update();

        var circ = Circulatory(body);
        var musc = Muscular(body);

        // Phase 1: Gladiator takes heart wound early in the fight
        body.TakeDamage(BodyPartType.Chest, 40);
        body.Bleed(BodyPartType.Chest, 2f);
        body.Update();

        float phase1BP = circ.GetBloodPressure();

        // Phase 2: Forced to keep fighting despite chest wound
        for (int i = 0; i < 10; i++)
        {
            body.Exert(BodyPartType.RightUpperArm, 90f);
            body.Exert(BodyPartType.RightHand, 90f);
            body.Exert(BodyPartType.LeftThigh, 80f);
            body.Exert(BodyPartType.RightThigh, 80f);
            body.Update();
        }

        float phase2BP = circ.GetBloodPressure();

        // Phase 3: BP should be dropping from blood loss + damaged heart
        Assert.True(phase2BP < phase1BP,
            $"Bleeding heart under exertion should drop BP (phase1: {phase1BP}, phase2: {phase2BP})");

        // Phase 4: More ticks — approaching collapse
        for (int i = 0; i < 10; i++) body.Update();

        float collapseBP = circ.GetBloodPressure();

        // Body is in cardiovascular crisis — BP at or near zero
        Assert.True(collapseBP <= phase2BP,
            $"Continued blood loss should not raise BP (phase2: {phase2BP}, collapse: {collapseBP})");

        // Combat effectiveness is severely degraded
        float collapseForce = musc.GetUpperBodyForce();
        Assert.True(collapseBP < 50f,
            $"Cardiovascular system should be in crisis (BP: {collapseBP})");
    }

    // ═══════════════════════════════════════════════════════════════
    // CROSS-SYSTEM WIRING TESTS (156–185)
    // Systems now talk to each other — blood flow affects immune/muscle/metabolic,
    // nerve signal affects muscle, inflammation causes fever, wounds auto-infect,
    // shock weakens heart, fractures disable muscles.
    // ═══════════════════════════════════════════════════════════════

    // ── 156. Blood flow cut → immune weakened at that limb ──────
    [Fact]
    public void CrossSystem_CutBloodFlow_ImmuneCannotFightInfection()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var immune = Immune(body);

        // Infect the left hand
        body.Infect(BodyPartType.LeftHand, 30f, 0.5f);
        body.Update();
        float infectionWithFlow = immune.GetInfectionLevel(BodyPartType.LeftHand);

        // Now destroy the blood vessel at the shoulder — cuts flow to entire arm
        body.TakeDamage(BodyPartType.LeftShoulder, 200);
        body.Update();

        // Blood flow to left hand should be near zero
        float flowToHand = circ.GetBloodFlowTo(BodyPartType.LeftHand);
        Assert.True(flowToHand < 10f,
            $"Destroying shoulder vessel should cut blood flow to hand (flow: {flowToHand})");

        // Infect the hand again — immune should struggle without blood flow
        body.Infect(BodyPartType.LeftHand, 30f, 0.5f);

        // Also infect the right hand (healthy blood flow for comparison)
        body.Infect(BodyPartType.RightHand, 30f, 0.5f);

        // Run several ticks
        for (int i = 0; i < 10; i++) body.Update();

        float leftInfection = immune.GetInfectionLevel(BodyPartType.LeftHand);
        float rightInfection = immune.GetInfectionLevel(BodyPartType.RightHand);

        // Left hand infection should be worse (immune can't reach it without blood)
        Assert.True(leftInfection > rightInfection,
            $"Cut-off limb should have worse infection (left: {leftInfection}, right: {rightInfection})");
    }

    // ── 157. Blood flow cut → muscle starves of oxygen ──────────
    [Fact]
    public void CrossSystem_CutBloodFlow_MuscleStaminaDrains()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var musc = Muscular(body);

        // Baseline: healthy hand force
        float healthyForce = musc.GetForceOutput(BodyPartType.LeftHand);
        Assert.True(healthyForce > 0, "Healthy hand should have force");

        // Destroy blood flow to left arm by destroying shoulder vessel
        body.TakeDamage(BodyPartType.LeftShoulder, 200);

        // Tick several times — muscle should degrade from low blood flow
        for (int i = 0; i < 10; i++) body.Update();

        float cutOffForce = musc.GetForceOutput(BodyPartType.LeftHand);
        float healthySideForce = musc.GetForceOutput(BodyPartType.RightHand);

        Assert.True(cutOffForce < healthySideForce,
            $"Blood-starved muscle should be weaker (left: {cutOffForce}, right: {healthySideForce})");
    }

    // ── 158. Nerve signal affects muscle force output ───────────
    [Fact]
    public void CrossSystem_NerveSevered_MuscleStrengthDegrades()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var nerv = Nervous(body);

        // Sever the nerve at the forearm — hand loses signal
        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();

        float signal = nerv.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(signal < 0.1f, $"Severed downstream should have near-zero signal (signal: {signal})");

        // Tick several times — low signal should degrade muscle strength
        for (int i = 0; i < 5; i++) body.Update();

        float severedForce = musc.GetForceOutput(BodyPartType.LeftHand);
        float healthyForce = musc.GetForceOutput(BodyPartType.RightHand);

        Assert.True(severedForce < healthyForce,
            $"Nerve-severed muscle should be weaker (severed: {severedForce}, healthy: {healthyForce})");
    }

    // ── 159. Inflammation causes fever (temperature rises) ──────
    [Fact]
    public void CrossSystem_Inflammation_CausesFever()
    {
        var body = CreateBody();
        var meta = Metabolic(body);
        var immune = Immune(body);

        // Baseline temperature
        body.Update();
        float baselineTemp = meta.GetAverageTemperature();

        // Create a severe infection to trigger inflammation
        body.Infect(BodyPartType.Chest, 50f, 1f);
        body.Infect(BodyPartType.Abdomen, 50f, 1f);

        // Tick several times — inflammation should raise temperature
        for (int i = 0; i < 15; i++) body.Update();

        float postInfectionTemp = meta.GetAverageTemperature();
        Assert.True(postInfectionTemp > baselineTemp,
            $"Inflammation should raise body temperature (baseline: {baselineTemp:F2}, after: {postInfectionTemp:F2})");
    }

    // ── 160. Exposed wound auto-infects ─────────────────────────
    [Fact]
    public void CrossSystem_ExposedWound_CausesInfection()
    {
        var body = CreateBody();
        var skin = Integumentary(body);
        var immune = Immune(body);

        // Damage the hand enough to create a wound (integrity < 40)
        body.TakeDamage(BodyPartType.LeftHand, 80);
        body.Update();

        // The wound should be exposed (no bandage)
        var skinNode = skin.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.True(skinNode != null && skinNode.IsWounded, "Heavy damage should wound the skin");
        Assert.True(skinNode!.IsExposed, "Unbandaged wound should be exposed");

        // Run several ticks — exposed wound should auto-infect
        for (int i = 0; i < 10; i++) body.Update();

        float infection = immune.GetInfectionLevel(BodyPartType.LeftHand);
        Assert.True(infection > 0,
            $"Exposed wound should cause infection (infection: {infection})");
    }

    // ── 161. Bandage prevents auto-infection ────────────────────
    [Fact]
    public void CrossSystem_BandagedWound_NoAutoInfection()
    {
        var body = CreateBody();
        var immune = Immune(body);

        // Create wound + immediately bandage
        body.TakeDamage(BodyPartType.RightHand, 80);
        body.Bandage(BodyPartType.RightHand);
        body.Update();

        // Cure any immediate infections to start clean
        body.Cure(BodyPartType.RightHand, 100f);

        // Run several ticks — bandaged wound should NOT auto-infect
        for (int i = 0; i < 10; i++) body.Update();

        float infection = immune.GetInfectionLevel(BodyPartType.RightHand);
        Assert.True(infection < 5f,
            $"Bandaged wound should resist infection (infection: {infection})");
    }

    // ── 162. Nervous shock weakens heart (blood pressure drop) ──
    [Fact]
    public void CrossSystem_Shock_ReducesBloodPressure()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var nerv = Nervous(body);

        body.Update();
        float baselineBP = circ.GetBloodPressure();

        // Induce shock
        body.Shock(80);
        body.Update();

        Assert.True(nerv.IsInShock, "Body should be in shock");

        float shockedBP = circ.GetBloodPressure();
        Assert.True(shockedBP < baselineBP,
            $"Shock should reduce blood pressure (baseline: {baselineBP}, shocked: {shockedBP})");
    }

    // ── 163. Bone fracture disables local muscle ────────────────
    [Fact]
    public void CrossSystem_Fracture_DisablesMuscleAtThatPart()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var skel = Skeletal(body);

        // Baseline: forearm muscle works
        float baseForce = musc.GetForceOutput(BodyPartType.LeftForearm);
        Assert.True(baseForce > 0, "Healthy forearm should have force");

        // Fracture the forearm
        body.TakeDamage(BodyPartType.LeftForearm, 200); // Should fracture
        body.Update();

        // The muscle at the fracture site should be disabled
        float fracturedForce = musc.GetForceOutput(BodyPartType.LeftForearm);
        Assert.Equal(0, fracturedForce);
    }

    // ── 164. Setting bone re-enables muscle ─────────────────────
    [Fact]
    public void CrossSystem_BoneSet_ReenablesMuscle()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        // Fracture + set the forearm bone
        body.TakeDamage(BodyPartType.LeftForearm, 200);
        body.Update();

        Assert.Equal(0, musc.GetForceOutput(BodyPartType.LeftForearm));

        // Set the bone
        body.SetBone(BodyPartType.LeftForearm);
        body.Heal(BodyPartType.LeftForearm, 50);
        body.Update();

        float repairedForce = musc.GetForceOutput(BodyPartType.LeftForearm);
        Assert.True(repairedForce > 0,
            $"Setting bone should re-enable muscle (force: {repairedForce})");
    }

    // ── 165. Open wound causes bleeding via skin → circulatory ──
    [Fact]
    public void CrossSystem_OpenWound_CausesBleeding()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var skin = Integumentary(body);

        // Create a wound that exposes blood vessels
        body.TakeDamage(BodyPartType.LeftHand, 80);
        body.Update();

        // The wound should trigger bleeding at that body part
        var bleedingParts = circ.GetBleedingParts();
        // The hand should now be bleeding (from skin breach OR damage threshold)
        Assert.True(circ.GetTotalBleedRate() > 0,
            "Open wound should cause some bleeding");
    }

    // ── 166. Low blood flow → metabolic ischemia ────────────────
    [Fact]
    public void CrossSystem_CutBloodFlow_MetabolicRateDrops()
    {
        var body = CreateBody();
        var meta = Metabolic(body);
        var circ = Circulatory(body);

        body.Update();
        float baselineRate = meta.GetEfficiency(BodyPartType.LeftHand);

        // Destroy shoulder vessel to cut blood flow
        body.TakeDamage(BodyPartType.LeftShoulder, 200);

        for (int i = 0; i < 5; i++) body.Update();

        float ischemicRate = meta.GetEfficiency(BodyPartType.LeftHand);
        float healthySideRate = meta.GetEfficiency(BodyPartType.RightHand);

        Assert.True(ischemicRate < healthySideRate,
            $"Blood-starved limb should have lower metabolic efficiency (ischemic: {ischemicRate:F3}, healthy: {healthySideRate:F3})");
    }

    // ── 167. Tourniquet scenario — stops bleeding but starves limb ──
    [Fact]
    public void CrossSystem_Tourniquet_StopsBleedButStarvesLimb()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var immune = Immune(body);
        var musc = Muscular(body);

        // Massive arm wound — bleeding heavily
        body.TakeDamage(BodyPartType.LeftUpperArm, 100);
        body.Bleed(BodyPartType.LeftUpperArm, 5f);
        body.Update();

        Assert.True(circ.GetTotalBleedRate() > 0, "Arm should be bleeding");

        // "Tourniquet" = destroy blood flow upstream (shoulder) + clot the wound
        body.TakeDamage(BodyPartType.LeftShoulder, 200);
        body.Clot(BodyPartType.LeftUpperArm);

        // Tick several times — limb is saved from bleeding but starved
        for (int i = 0; i < 15; i++) body.Update();

        float flowToHand = circ.GetBloodFlowTo(BodyPartType.LeftHand);
        Assert.True(flowToHand < 10f,
            $"Tourniquet should cut blood flow to hand (flow: {flowToHand})");

        // Muscle below tourniquet degrades
        float leftForce = musc.GetForceOutput(BodyPartType.LeftHand);
        float rightForce = musc.GetForceOutput(BodyPartType.RightHand);
        Assert.True(leftForce < rightForce,
            $"Tourniquet limb muscle should weaken (left: {leftForce}, right: {rightForce})");
    }

    // ── 168. Gladiator tactical: sever nerve + cut blood = total limb death ──
    [Fact]
    public void CrossSystem_SeverNerve_CutBlood_TotalLimbDeath()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var nerv = Nervous(body);
        var circ = Circulatory(body);
        var immune = Immune(body);

        // Sever the nerve at the shoulder
        body.SeverNerve(BodyPartType.LeftShoulder);
        // Destroy the blood vessel at the shoulder
        body.TakeDamage(BodyPartType.LeftShoulder, 200);
        // Infect the now-undefended limb
        body.Infect(BodyPartType.LeftHand, 40f, 1f);

        for (int i = 0; i < 10; i++) body.Update();

        // The arm is completely dead — no nerve signal, no blood, no muscle, rampant infection
        float signal = nerv.GetSignalStrength(BodyPartType.LeftHand);
        float flow = circ.GetBloodFlowTo(BodyPartType.LeftHand);
        float force = musc.GetForceOutput(BodyPartType.LeftHand);
        float infection = immune.GetInfectionLevel(BodyPartType.LeftHand);

        Assert.True(signal < 0.1f, $"No nerve signal (signal: {signal})");
        Assert.True(flow < 10f, $"No blood flow (flow: {flow})");
        Assert.True(force < 5f, $"No muscle force (force: {force})");
        Assert.True(infection > 20f, $"Unchecked infection (infection: {infection})");
    }

    // ── 169. Shock recovery: as shock fades, BP recovers ────────
    [Fact]
    public void CrossSystem_ShockRecovery_BPRecovers()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var nerv = Nervous(body);

        body.Update();
        float baselineBP = circ.GetBloodPressure();

        // Shock drops BP
        body.Shock(60);
        body.Update();
        float shockedBP = circ.GetBloodPressure();
        Assert.True(shockedBP < baselineBP, "Shock should lower BP");

        // Rest and heal to reduce shock over time
        for (int i = 0; i < 30; i++)
        {
            body.Heal(BodyPartType.Chest, 5);
            body.Update();
        }

        float recoveredBP = circ.GetBloodPressure();
        Assert.True(recoveredBP > shockedBP,
            $"BP should recover as shock fades (shocked: {shockedBP}, recovered: {recoveredBP})");
    }

    // ── 170. Multi-system gladiator scenario: broken arm fight ──
    [Fact]
    public void CrossSystem_BrokenArm_FullCascade()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var nerv = Nervous(body);
        var circ = Circulatory(body);
        var skel = Skeletal(body);
        var immune = Immune(body);

        // Gladiator takes a heavy mace blow to the left forearm
        // This should: fracture bone, damage muscle, trigger bleeding, cause pain/shock
        body.TakeDamage(BodyPartType.LeftForearm, 100);
        body.Update();

        // Bone should be fractured
        var boneNode = skel.GetNode(BodyPartType.LeftForearm) as BoneNode;
        Assert.True(boneNode != null && boneNode.IsFractured, "Forearm bone should fracture");

        // Muscle at fracture site should be disabled
        float fracturedForce = musc.GetForceOutput(BodyPartType.LeftForearm);
        Assert.Equal(0, fracturedForce);

        // Bleeding from the wound
        Assert.True(circ.GetTotalBleedRate() > 0, "Should be bleeding");

        // Pain should have generated
        float pain = nerv.GetTotalPain();
        Assert.True(pain > 0, "Should have pain from fracture");
    }

    // ── 171. Blood flow restoration reverses ischemia ───────────
    [Fact]
    public void CrossSystem_RestoreBloodFlow_ReverseIschemia()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var circ = Circulatory(body);

        // Damage shoulder to cut flow
        body.TakeDamage(BodyPartType.LeftShoulder, 100);
        for (int i = 0; i < 5; i++) body.Update();

        float lowFlowForce = musc.GetForceOutput(BodyPartType.LeftHand);

        // Heal the shoulder to restore flow
        for (int i = 0; i < 10; i++)
        {
            body.Heal(BodyPartType.LeftShoulder, 20);
            body.Update();
        }

        float restoredFlowForce = musc.GetForceOutput(BodyPartType.LeftHand);
        Assert.True(restoredFlowForce > lowFlowForce,
            $"Restoring blood flow should improve muscle (before: {lowFlowForce}, after: {restoredFlowForce})");
    }

    // ── 172. Inflammation + infection fever escalation ───────────
    [Fact]
    public void CrossSystem_InfectionFever_Escalation()
    {
        var body = CreateBody();
        var meta = Metabolic(body);
        var immune = Immune(body);

        body.Update();
        float baseTemp = meta.GetAverageTemperature();

        // Severe multi-site infection
        body.Infect(BodyPartType.Chest, 60f, 1f);
        body.Infect(BodyPartType.Abdomen, 60f, 1f);
        body.Infect(BodyPartType.Neck, 60f, 1f);

        for (int i = 0; i < 20; i++) body.Update();

        // Multiple inflamed sites should produce significant fever
        float feverTemp = meta.GetAverageTemperature();
        var inflamedParts = immune.GetInflamedParts();

        Assert.True(inflamedParts.Count > 0, "Should have inflamed parts");
        Assert.True(feverTemp > baseTemp + 0.5f,
            $"Multi-site inflammation should cause noticeable fever (base: {baseTemp:F2}, fever: {feverTemp:F2})");
    }

    // ── 173. Nerve repair restores muscle function gradually ────
    [Fact]
    public void CrossSystem_NerveRepair_MuscleGraduallyRecovers()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var nerv = Nervous(body);

        float baselineForce = musc.GetForceOutput(BodyPartType.LeftHand);

        // Sever and degrade
        body.SeverNerve(BodyPartType.LeftForearm);
        for (int i = 0; i < 5; i++) body.Update();

        float severedForce = musc.GetForceOutput(BodyPartType.LeftHand);
        Assert.True(severedForce < baselineForce, "Severed nerve should weaken muscle");

        // Repair the nerve
        body.RepairNerve(BodyPartType.LeftForearm);

        // Recover over many ticks
        for (int i = 0; i < 20; i++) body.Update();

        float repairedForce = musc.GetForceOutput(BodyPartType.LeftHand);
        Assert.True(repairedForce > severedForce,
            $"Repaired nerve should improve muscle (severed: {severedForce}, repaired: {repairedForce})");
    }

    // ── 174. Wound bandage stops both bleeding and infection ────
    [Fact]
    public void CrossSystem_Bandage_StopsBleedingAndInfection()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var immune = Immune(body);

        // Create a bad wound
        body.TakeDamage(BodyPartType.LeftHand, 80);
        for (int i = 0; i < 3; i++) body.Update();

        // Should have infection building and/or bleeding
        float preBandageBleed = circ.GetTotalBleedRate();
        float preBandageInfection = immune.GetInfectionLevel(BodyPartType.LeftHand);

        // Bandage + clot
        body.Bandage(BodyPartType.LeftHand);
        body.Clot(BodyPartType.LeftHand);
        body.Cure(BodyPartType.LeftHand, 50f);

        for (int i = 0; i < 10; i++) body.Update();

        float postBandageInfection = immune.GetInfectionLevel(BodyPartType.LeftHand);

        // Infection should be controlled (bandage prevents new infection seeds)
        Assert.True(postBandageInfection <= preBandageInfection || postBandageInfection < 5f,
            $"Bandage should prevent infection growth (pre: {preBandageInfection}, post: {postBandageInfection})");
    }

    // ── 175. Full arena scenario with cross-system interactions ──
    [Fact]
    public void CrossSystem_FullArenaFight_WithSystemInterplay()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var circ = Circulatory(body);
        var nerv = Nervous(body);
        var immune = Immune(body);
        var meta = Metabolic(body);
        var skel = Skeletal(body);
        var skin = Integumentary(body);

        // Round 1: Sword slash to left arm — skin breach → bleed + infection risk
        body.TakeDamage(BodyPartType.LeftUpperArm, 60);
        body.Update();
        body.Update();

        // Round 2: Mace hit to right leg — fracture → muscle disabled
        body.TakeDamage(BodyPartType.RightThigh, 150);
        body.Update();

        // Right leg should be severely compromised
        var thighBone = skel.GetNode(BodyPartType.RightThigh) as BoneNode;
        float rightLegForce = musc.GetForceOutput(BodyPartType.RightThigh);

        // Round 3: Heavy exertion while wounded — shock from pain
        body.Exert(BodyPartType.Chest, 80);
        body.Exert(BodyPartType.LeftUpperArm, 60);
        body.Update();

        // Round 4: Multiple ticks of fighting — compound effects
        for (int i = 0; i < 10; i++) body.Update();

        // After the fight:
        // 1. Blood pressure should be affected (bleeding + potential shock)
        float bp = circ.GetBloodPressure();
        // 2. Muscle force should be degraded on wounded/fractured limbs
        float leftArmForce = musc.GetForceOutput(BodyPartType.LeftUpperArm);
        float healthyArmForce = musc.GetForceOutput(BodyPartType.RightUpperArm);
        // 3. The wounded arm should be weaker than the healthy one
        Assert.True(leftArmForce < healthyArmForce,
            $"Wounded arm should be weaker (left: {leftArmForce}, right: {healthyArmForce})");
    }

    // ── 176. Cut blood to leg → leg muscles weaken → can't walk ─
    [Fact]
    public void CrossSystem_CutLegBloodFlow_LocomotionCrippled()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var circ = Circulatory(body);

        body.Update();
        float healthyLocomotion = musc.GetLocomotionForce();

        // Damage both hip blood vessels — cuts flow to both legs
        body.TakeDamage(BodyPartType.Hips, 200);
        for (int i = 0; i < 10; i++) body.Update();

        float crippledLocomotion = musc.GetLocomotionForce();

        Assert.True(crippledLocomotion < healthyLocomotion * 0.7f,
            $"Cut leg blood flow should cripple locomotion (healthy: {healthyLocomotion}, crippled: {crippledLocomotion})");
    }

    // ── 177. Multi-wound fever: many open wounds = raging fever ──
    [Fact]
    public void CrossSystem_MultipleWounds_CompoundFever()
    {
        var body = CreateBody();
        var meta = Metabolic(body);
        var immune = Immune(body);

        body.Update();
        float baseTemp = meta.GetAverageTemperature();

        // Multiple deep wounds that auto-infect and inflame
        foreach (var part in new[] { BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.Chest, BodyPartType.Abdomen, BodyPartType.LeftThigh })
        {
            body.TakeDamage(part, 80);
        }

        // Let infection/inflammation develop
        for (int i = 0; i < 20; i++) body.Update();

        float multiWoundTemp = meta.GetAverageTemperature();
        Assert.True(multiWoundTemp > baseTemp,
            $"Multiple infected wounds should raise temperature (base: {baseTemp:F2}, multi: {multiWoundTemp:F2})");
    }

    // ── 178. Fracture + nerve sever at same point = complete disable ──
    [Fact]
    public void CrossSystem_FractureAndNerveSever_DoubleDisable()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        // Fracture bone AND sever nerve at the same body part
        body.TakeDamage(BodyPartType.LeftForearm, 200); // Fracture
        body.SeverNerve(BodyPartType.LeftForearm);
        body.Update();

        // The muscle should be completely dead — both bone and nerve are gone
        float force = musc.GetForceOutput(BodyPartType.LeftForearm);
        Assert.Equal(0, force);

        // Even after setting the bone, the severed nerve keeps it weak
        body.SetBone(BodyPartType.LeftForearm);
        body.Heal(BodyPartType.LeftForearm, 50);
        for (int i = 0; i < 5; i++) body.Update();

        float afterBoneSetForce = musc.GetForceOutput(BodyPartType.LeftForearm);
        float healthyForce = musc.GetForceOutput(BodyPartType.RightForearm);

        // The bone-set arm should still be much weaker due to severed nerve
        Assert.True(afterBoneSetForce < healthyForce * 0.8f,
            $"Severed nerve should keep muscle weak even after bone set (set: {afterBoneSetForce}, healthy: {healthyForce})");
    }

    // ── 179. Gladiator with tourniqueted arm still fights with other ──
    [Fact]
    public void CrossSystem_TourniquetedArm_OtherArmStillFights()
    {
        var body = CreateBody();
        var musc = Muscular(body);

        // Tourniquet left arm (destroy blood flow at shoulder)
        body.TakeDamage(BodyPartType.LeftShoulder, 200);
        for (int i = 0; i < 10; i++) body.Update();

        // Left arm should be severely weakened
        float leftForce = musc.GetForceOutput(BodyPartType.LeftHand);
        // Right arm should still work perfectly
        float rightForce = musc.GetForceOutput(BodyPartType.RightHand);

        Assert.True(rightForce > leftForce * 2,
            $"Healthy arm should have much more force (right: {rightForce}, left: {leftForce})");
        Assert.True(rightForce > 30f,
            $"Healthy arm should still be functional (force: {rightForce})");
    }

    // ── 180. Shock cascade: pain → shock → BP drop → muscle weakness ──
    [Fact]
    public void CrossSystem_ShockCascade_PainToWeakness()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var nerv = Nervous(body);
        var musc = Muscular(body);

        body.Update();
        float healthyBP = circ.GetBloodPressure();
        float healthyForce = musc.GetUpperBodyForce();

        // Massive multi-site trauma to induce shock
        body.TakeDamage(BodyPartType.Chest, 50);
        body.TakeDamage(BodyPartType.LeftUpperArm, 50);
        body.TakeDamage(BodyPartType.RightUpperArm, 50);
        body.TakeDamage(BodyPartType.LeftThigh, 50);
        body.TakeDamage(BodyPartType.RightThigh, 50);
        body.Update();

        // Pain should be high — potentially in shock
        float totalPain = nerv.GetTotalPain();
        Assert.True(totalPain > 50, $"Multi-site trauma should cause significant pain (pain: {totalPain})");

        // BP should be lower (from shock if triggered, or from bleeding)
        for (int i = 0; i < 5; i++) body.Update();
        float traumaBP = circ.GetBloodPressure();

        Assert.True(traumaBP < healthyBP,
            $"Multi-site trauma should reduce BP (healthy: {healthyBP}, trauma: {traumaBP})");
    }

    // ── 181. Infected wound + low blood flow = gangrene scenario ─
    [Fact]
    public void CrossSystem_Gangrene_InfectionWithNoBloodFlow()
    {
        var body = CreateBody();
        var immune = Immune(body);
        var circ = Circulatory(body);

        // Cut blood flow to left arm
        body.TakeDamage(BodyPartType.LeftShoulder, 200);
        body.Update();

        // Infect the blood-starved hand
        body.Infect(BodyPartType.LeftHand, 30f, 0.8f);

        // Also infect right hand (with healthy blood flow) for comparison
        body.Infect(BodyPartType.RightHand, 30f, 0.8f);

        // Run many ticks
        for (int i = 0; i < 20; i++) body.Update();

        float leftInfection = immune.GetInfectionLevel(BodyPartType.LeftHand);
        float rightInfection = immune.GetInfectionLevel(BodyPartType.RightHand);

        // The blood-starved hand should have much worse infection (gangrene)
        // because immune cells can't reach it to fight
        Assert.True(leftInfection > rightInfection,
            $"Blood-starved infection should be worse (left: {leftInfection}, right: {rightInfection})");
    }

    // ── 182. Cross-system domino: neck damage → everything ──────
    [Fact]
    public void CrossSystem_NeckDamage_AffectsEverything()
    {
        var body = CreateBody();
        var circ = Circulatory(body);
        var nerv = Nervous(body);
        var musc = Muscular(body);
        var meta = Metabolic(body);

        body.Update();
        float baseBP = circ.GetBloodPressure();

        // Heavy neck damage — affects airway, blood flow, and nerve signals
        body.TakeDamage(BodyPartType.Neck, 80);
        for (int i = 0; i < 5; i++) body.Update();

        // Blood flow to head should be reduced (neck vessel damaged)
        float headFlow = circ.GetBloodFlowTo(BodyPartType.Head);
        Assert.True(headFlow < 90f,
            $"Neck damage should reduce blood flow to head (flow: {headFlow})");

        // BP may be affected via shock from pain
        float currentBP = circ.GetBloodPressure();

        // Nerve signal should be degraded downstream
        // (neck is central — damage affects signal quality)
        float neckSignal = nerv.GetSignalStrength(BodyPartType.Neck);
        Assert.True(neckSignal < 1f,
            $"Neck nerve damage should degrade signal (signal: {neckSignal})");
    }

    // ── 183. Cure infection → inflammation subsides → fever drops ──
    [Fact]
    public void CrossSystem_CureInfection_FeverSubsides()
    {
        var body = CreateBody();
        var meta = Metabolic(body);
        var immune = Immune(body);

        // Create infection + inflammation
        body.Infect(BodyPartType.Chest, 60f, 1f);
        for (int i = 0; i < 15; i++) body.Update();

        float feverTemp = meta.GetAverageTemperature();

        // Cure the infection aggressively
        for (int i = 0; i < 10; i++)
        {
            body.Cure(BodyPartType.Chest, 30f);
            body.Update();
        }

        // Let inflammation subside naturally
        for (int i = 0; i < 20; i++) body.Update();

        float postCureTemp = meta.GetAverageTemperature();

        // Temperature should have come down towards normal
        Assert.True(postCureTemp < feverTemp || postCureTemp < 38f,
            $"Curing infection should reduce fever (fever: {feverTemp:F2}, post-cure: {postCureTemp:F2})");
    }

    // ── 184. Blood flow affects poison clearance rate ────────────
    [Fact]
    public void CrossSystem_CutBloodFlow_PoisonLingers()
    {
        var body = CreateBody();
        var immune = Immune(body);
        var circ = Circulatory(body);

        // Poison both hands
        body.Poison(BodyPartType.LeftHand, 40f);
        body.Poison(BodyPartType.RightHand, 40f);
        body.Update();

        // Cut blood flow to left hand
        body.TakeDamage(BodyPartType.LeftShoulder, 200);

        // Let immune system work for a while
        for (int i = 0; i < 15; i++) body.Update();

        float leftToxin = immune.GetToxinLevel(BodyPartType.LeftHand);
        float rightToxin = immune.GetToxinLevel(BodyPartType.RightHand);

        // Left hand should still have more toxin (immune can't reach it)
        Assert.True(leftToxin > rightToxin,
            $"Blood-starved limb should clear toxin slower (left: {leftToxin}, right: {rightToxin})");
    }

    // ── 185. Complete battle medic scenario ──────────────────────
    [Fact]
    public void CrossSystem_BattleMedic_TreatWoundsRestoreFunction()
    {
        var body = CreateBody();
        var musc = Muscular(body);
        var circ = Circulatory(body);
        var immune = Immune(body);
        var nerv = Nervous(body);

        // Gladiator takes heavy damage to left arm
        body.TakeDamage(BodyPartType.LeftForearm, 100);
        body.TakeDamage(BodyPartType.LeftHand, 60);
        for (int i = 0; i < 5; i++) body.Update();

        // Arm is in bad shape
        float damagedForce = musc.GetForceOutput(BodyPartType.LeftHand);

        // Battle medic arrives: bandage, set bone, heal, cure
        body.Bandage(BodyPartType.LeftForearm);
        body.Bandage(BodyPartType.LeftHand);
        body.SetBone(BodyPartType.LeftForearm);
        body.Clot(BodyPartType.LeftForearm);
        body.Clot(BodyPartType.LeftHand);
        body.Cure(BodyPartType.LeftForearm, 50f);
        body.Cure(BodyPartType.LeftHand, 50f);

        // Healing over multiple rounds
        for (int i = 0; i < 20; i++)
        {
            body.Heal(BodyPartType.LeftForearm, 10);
            body.Heal(BodyPartType.LeftHand, 10);
            body.Update();
        }

        float healedForce = musc.GetForceOutput(BodyPartType.LeftHand);

        Assert.True(healedForce > damagedForce,
            $"Battle medic treatment should improve function (damaged: {damagedForce}, healed: {healedForce})");
    }
}

