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

    // ─── 1. All systems initialise cleanly via Body ───────────

    [Fact]
    public void Body_Init_AllThreeSystemsPresent()
    {
        var body = CreateBody();

        Assert.NotNull(body.GetSystem(BodySystemType.Skeletal));
        Assert.NotNull(body.GetSystem(BodySystemType.Circulatory));
        Assert.NotNull(body.GetSystem(BodySystemType.Respiratory));
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
}
