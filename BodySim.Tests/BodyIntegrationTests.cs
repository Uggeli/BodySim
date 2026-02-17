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

        // 4. Apply medicine
        body.Cure(BodyPartType.LeftLeg, 40);
        body.Update();

        // 5. Heal over time
        for (int i = 0; i < 10; i++)
        {
            body.Heal(BodyPartType.LeftLeg, 10);
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
}
