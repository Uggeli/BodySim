namespace BodySim.Tests;

public class BodyBlueprintTests
{
    // ── Backward compatibility ──────────────────────────────

    [Fact]
    public void NoArgBody_StillWorks()
    {
        var body = new Body();
        Assert.NotNull(body);
        Assert.NotNull(body.Blueprint);
    }

    [Fact]
    public void DefaultBlueprint_MatchesNoArgBody()
    {
        var defaultBody = new Body();
        var blueprintBody = new Body(BodyBlueprint.Default);

        // Both should produce equivalent systems
        var defaultMuscular = defaultBody.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        var blueprintMuscular = blueprintBody.GetSystem(BodySystemType.Muscular) as MuscularSystem;

        Assert.NotNull(defaultMuscular);
        Assert.NotNull(blueprintMuscular);

        var defaultNode = defaultMuscular!.GetNode(BodyPartType.Chest);
        var blueprintNode = blueprintMuscular!.GetNode(BodyPartType.Chest);

        Assert.Equal(
            defaultNode?.GetComponent(BodyComponentType.MuscleStrength)?.Max,
            blueprintNode?.GetComponent(BodyComponentType.MuscleStrength)?.Max);
    }

    [Fact]
    public void SystemConstructors_WithoutBlueprint_ProduceDefaultValues()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();

        var muscular = new MuscularSystem(pool, hub);
        var node = muscular.GetNode(BodyPartType.Chest);
        Assert.Equal(100f, node?.GetComponent(BodyComponentType.MuscleStrength)?.Max);
        Assert.Equal(100f, node?.GetComponent(BodyComponentType.Stamina)?.Max);
    }

    // ── Blueprint stored and queryable ──────────────────────

    [Fact]
    public void Blueprint_StoredAndQueryableOnBody()
    {
        var bp = new BodyBlueprint { Frame = 0.9f, MuscleStrengthInitial = 70f };
        var body = new Body(bp);

        Assert.Same(bp, body.Blueprint);
        Assert.Equal(0.9f, body.Blueprint.Frame);
        Assert.Equal(70f, body.Blueprint.MuscleStrengthInitial);
    }

    // ── Custom blueprint affects node stats ──────────────────

    [Fact]
    public void CustomBlueprint_AffectsMuscleStrengthMax()
    {
        var bp = new BodyBlueprint { MuscleStrengthInitial = 60f };
        var body = new Body(bp);

        var muscular = body.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        var node = muscular!.GetNode(BodyPartType.Chest);
        Assert.Equal(60f, node?.GetComponent(BodyComponentType.MuscleStrength)?.Max);
        Assert.Equal(60f, node?.GetComponent(BodyComponentType.MuscleStrength)?.Current);
    }

    [Fact]
    public void CustomBlueprint_AffectsStaminaMax()
    {
        var bp = new BodyBlueprint { StaminaInitial = 75f };
        var body = new Body(bp);

        var muscular = body.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        var node = muscular!.GetNode(BodyPartType.LeftThigh);
        Assert.Equal(75f, node?.GetComponent(BodyComponentType.Stamina)?.Max);
    }

    [Fact]
    public void CustomBlueprint_AffectsBoneDensityMax()
    {
        var bp = new BodyBlueprint { BoneDensityInitial = 80f };
        var body = new Body(bp);

        var skeletal = body.GetSystem(BodySystemType.Skeletal) as SkeletalSystem;
        var node = skeletal!.GetNode(BodyPartType.Chest);
        Assert.Equal(80f, node?.GetComponent(BodyComponentType.BoneDensity)?.Max);
    }

    [Fact]
    public void CustomBlueprint_AffectsBoneIntegrityMax()
    {
        var bp = new BodyBlueprint { BoneIntegrityInitial = 90f };
        var body = new Body(bp);

        var skeletal = body.GetSystem(BodySystemType.Skeletal) as SkeletalSystem;
        var node = skeletal!.GetNode(BodyPartType.Pelvis);
        Assert.Equal(90f, node?.GetComponent(BodyComponentType.Integrity)?.Max);
    }

    [Fact]
    public void CustomBlueprint_AffectsNerveSignalMax()
    {
        var bp = new BodyBlueprint { NerveSignalInitial = 70f };
        var body = new Body(bp);

        var nervous = body.GetSystem(BodySystemType.Nerveus) as NervousSystem;
        var node = nervous!.GetNode(BodyPartType.Head);
        Assert.Equal(70f, node?.GetComponent(BodyComponentType.NerveSignal)?.Max);
    }

    [Fact]
    public void CustomBlueprint_AffectsPainTolerance()
    {
        var bp = new BodyBlueprint { PainToleranceInitial = 50f };
        var body = new Body(bp);

        var nervous = body.GetSystem(BodySystemType.Nerveus) as NervousSystem;
        var node = nervous!.GetNode(BodyPartType.Chest) as NerveNode;
        Assert.NotNull(node);
        Assert.Equal(50f, node!.PainOverloadThreshold);
    }

    [Fact]
    public void CustomBlueprint_AffectsImmunePotency()
    {
        var bp = new BodyBlueprint { ImmunePotencyInitial = 85f };
        var body = new Body(bp);

        var immune = body.GetSystem(BodySystemType.Immune) as ImmuneSystem;
        var node = immune!.GetNode(BodyPartType.Neck);
        Assert.Equal(85f, node?.GetComponent(BodyComponentType.ImmunePotency)?.Max);
    }

    [Fact]
    public void CustomBlueprint_HealthStaysAt100()
    {
        var bp = new BodyBlueprint { MuscleStrengthInitial = 60f, BoneDensityInitial = 50f };
        var body = new Body(bp);

        var muscular = body.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        Assert.Equal(100f, muscular!.GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Max);

        var skeletal = body.GetSystem(BodySystemType.Skeletal) as SkeletalSystem;
        Assert.Equal(100f, skeletal!.GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Max);
    }

    // ── Weight calculation ──────────────────────────────────

    [Fact]
    public void DefaultBody_WeightApprox80kg()
    {
        var body = new Body();
        float weight = body.GetWeight();
        Assert.InRange(weight, 78f, 82f);
    }

    [Fact]
    public void SmallRookie_WeightApprox55kg()
    {
        var bp = new BodyBlueprint
        {
            Frame = 0.85f,
            MuscleStrengthInitial = 55f,
            BoneDensityInitial = 80f,
        };
        var body = new Body(bp);
        float weight = body.GetWeight();
        Assert.InRange(weight, 50f, 60f);
    }

    [Fact]
    public void LargeVeteran_WeightApprox120kg()
    {
        var bp = new BodyBlueprint
        {
            Frame = 1.3f,
            MuscleStrengthInitial = 130f,
            BoneDensityInitial = 120f,
        };
        var body = new Body(bp);
        float weight = body.GetWeight();
        Assert.InRange(weight, 110f, 130f);
    }

    [Fact]
    public void Composition_BreakdownSumsToTotal()
    {
        var body = new Body();
        var comp = body.GetBodyComposition();

        float sum = comp.BaseWeight + comp.MuscleMass + comp.BoneMass;
        Assert.Equal(comp.TotalWeight, sum, precision: 2);
    }

    [Fact]
    public void LargerFrame_ProducesHeavierBody()
    {
        var smallBody = new Body(new BodyBlueprint { Frame = 0.8f });
        var largeBody = new Body(new BodyBlueprint { Frame = 1.2f });

        Assert.True(largeBody.GetWeight() > smallBody.GetWeight());
    }

    // ── Force output scales with blueprint ──────────────────

    [Fact]
    public void ForceOutput_ScalesWithBlueprintStrength()
    {
        var weakBp = new BodyBlueprint { MuscleStrengthInitial = 50f };
        var strongBp = new BodyBlueprint { MuscleStrengthInitial = 100f };

        var weakBody = new Body(weakBp);
        var strongBody = new Body(strongBp);

        var weakMuscular = weakBody.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        var strongMuscular = strongBody.GetSystem(BodySystemType.Muscular) as MuscularSystem;

        float weakForce = weakMuscular!.GetForceOutput(BodyPartType.Chest);
        float strongForce = strongMuscular!.GetForceOutput(BodyPartType.Chest);

        // Weak body should output about half the force of a strong body
        Assert.True(strongForce > weakForce);
        Assert.InRange(weakForce, 45f, 55f);  // ~50 force at max health/stamina
        Assert.InRange(strongForce, 95f, 105f); // ~100 force at max health/stamina
    }

    [Fact]
    public void ForceOutput_LowStrengthBody_OutputsCorrectForce()
    {
        // A body with strength Max=60 at full health/stamina should output force=60, not 36
        var bp = new BodyBlueprint { MuscleStrengthInitial = 60f };
        var body = new Body(bp);

        var muscular = body.GetSystem(BodySystemType.Muscular) as MuscularSystem;
        float force = muscular!.GetForceOutput(BodyPartType.Chest);

        Assert.InRange(force, 58f, 62f); // Should be ~60, not 36
    }

    [Fact]
    public void SignalStrength_FullHealth_ReturnsOne()
    {
        // A body with nerveSignalMax=60 at full health should still have signal strength = 1.0
        var bp = new BodyBlueprint { NerveSignalInitial = 60f };
        var body = new Body(bp);

        var nervous = body.GetSystem(BodySystemType.Nerveus) as NervousSystem;
        float signal = nervous!.GetSignalStrength(BodyPartType.Chest);

        Assert.InRange(signal, 0.95f, 1.05f); // Should be ~1.0 at full health
    }

    // ── Default blueprint values ────────────────────────────

    [Fact]
    public void DefaultBlueprint_HasExpectedDefaults()
    {
        var bp = BodyBlueprint.Default;

        Assert.Equal(1.0f, bp.Frame);
        Assert.Equal(100f, bp.MuscleStrengthCeiling);
        Assert.Equal(100f, bp.MuscleStrengthInitial);
        Assert.Equal(100f, bp.StaminaCeiling);
        Assert.Equal(100f, bp.StaminaInitial);
        Assert.Equal(100f, bp.BoneDensityCeiling);
        Assert.Equal(100f, bp.BoneDensityInitial);
        Assert.Equal(100f, bp.BoneIntegrityCeiling);
        Assert.Equal(100f, bp.BoneIntegrityInitial);
        Assert.Equal(100f, bp.NerveSignalCeiling);
        Assert.Equal(100f, bp.NerveSignalInitial);
        Assert.Equal(80f, bp.PainToleranceCeiling);
        Assert.Equal(80f, bp.PainToleranceInitial);
        Assert.Equal(100f, bp.ImmunePotencyCeiling);
        Assert.Equal(100f, bp.ImmunePotencyInitial);
    }
}
