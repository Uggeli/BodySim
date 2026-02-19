namespace BodySim.Tests;

public class KineticChainTests
{
    private static Body CreateBody() => new();

    private static MuscularSystem Muscular(Body body) =>
        (MuscularSystem)body.GetSystem(BodySystemType.Muscular)!;

    private static SkeletalSystem Skeletal(Body body) =>
        (SkeletalSystem)body.GetSystem(BodySystemType.Skeletal)!;

    private static NervousSystem Nervous(Body body) =>
        (NervousSystem)body.GetSystem(BodySystemType.Nerveus)!;

    private static readonly BodyPartType[] SlashChain =
    [
        BodyPartType.RightShoulder, BodyPartType.RightUpperArm,
        BodyPartType.RightForearm, BodyPartType.RightHand,
        BodyPartType.Chest, BodyPartType.Abdomen,
    ];

    // ── Healthy body, no load ────────────────────────────────

    [Fact]
    public void HealthyBody_NoLoad_ReturnsBaselineForce()
    {
        var body = CreateBody();
        var result = body.GetKineticChainForce(SlashChain);

        // All parts at full health/strength/stamina -> 100 force each, 6 parts = 600
        Assert.Equal(600f, result.RawMuscleForce, 1f);
        Assert.Equal(600f, result.Force, 1f);
        Assert.Equal(1f, result.LoadRatio);
        Assert.True(result.StaminaCost > 0);
    }

    // ── Healthy body, heavy load with momentum bonus ─────────

    [Fact]
    public void HealthyBody_HeavyLoad_GetsMomentumBonus()
    {
        var body = CreateBody();
        float swordWeight = 2f;

        var result = body.GetKineticChainForce(SlashChain, swordWeight);

        // loadRatio = 600 / (2 * 10) = 30 -> well above 1
        Assert.True(result.LoadRatio > 1f);
        // Force should be higher than raw due to momentum bonus
        Assert.True(result.Force > result.RawMuscleForce);
        // Bonus is capped at MaxMomentumBonus (30%)
        Assert.True(result.Force <= result.RawMuscleForce * 1.3f);
    }

    // ── Weak body, heavy load ────────────────────────────────

    [Fact]
    public void WeakBody_HeavyLoad_ReducedForce()
    {
        var body = CreateBody();
        var muscular = Muscular(body);

        // Weaken all muscles in the chain to 10% strength
        foreach (var part in SlashChain)
        {
            var muscle = muscular.GetNode(part) as MuscleNode;
            if (muscle != null)
                muscle.GetComponent(BodyComponentType.MuscleStrength)!.Current = 10;
        }

        // Very heavy load
        float loadWeight = 50f;
        var result = body.GetKineticChainForce(SlashChain, loadWeight);

        // loadRatio should be well below 1 — body can't handle this weapon
        Assert.True(result.LoadRatio < 1f);
        // Final force should be less than raw force (penalized)
        Assert.True(result.Force < result.RawMuscleForce);
    }

    // ── Fractured bone in chain ──────────────────────────────

    [Fact]
    public void FracturedBone_DropsForceToZeroAtThatLink()
    {
        var body = CreateBody();

        // Fracture the right forearm bone
        body.TakeDamage(BodyPartType.RightForearm, 200);
        body.Update(); // Process events

        var skeletal = Skeletal(body);
        Assert.Equal(0f, skeletal.GetBoneIntegrityFactor(BodyPartType.RightForearm));

        var result = body.GetKineticChainForce(SlashChain);

        // Fracture now blocks the entire chain
        Assert.True(result.IsBlocked);
        Assert.Equal(0f, result.Force);
        // RightForearm should be in the limiting parts
        Assert.Contains(BodyPartType.RightForearm, result.LimitingParts);
    }

    // ── Severed nerve ────────────────────────────────────────

    [Fact]
    public void SeveredNerve_MuscleCannotFire()
    {
        var body = CreateBody();

        // Sever the nerve at the right shoulder — kills signal to downstream arm
        body.SeverNerve(BodyPartType.RightShoulder);
        body.Update();

        var nervous = Nervous(body);
        // Downstream signal should be near zero
        Assert.True(nervous.GetSignalStrength(BodyPartType.RightUpperArm) < 0.1f);

        var result = body.GetKineticChainForce(SlashChain);

        // Force should be significantly reduced vs a healthy 600
        Assert.True(result.RawMuscleForce < 500f);
    }

    // ── Torn muscle in chain ─────────────────────────────────

    [Fact]
    public void TornMuscle_ZeroContributionFromThatPart()
    {
        var body = CreateBody();
        var muscular = Muscular(body);

        // Tear the right forearm muscle
        var muscle = muscular.GetNode(BodyPartType.RightForearm) as MuscleNode;
        Assert.NotNull(muscle);
        muscle.Tear();

        var result = body.GetKineticChainForce(SlashChain);

        // RightForearm outputs 0 force -> total is 500
        Assert.Equal(500f, result.RawMuscleForce, 1f);
        Assert.Contains(BodyPartType.RightForearm, result.LimitingParts);
    }

    // ── Load ratio calculation ───────────────────────────────

    [Fact]
    public void LoadRatio_NoLoad_ReturnsOne()
    {
        var body = CreateBody();
        var result = body.GetKineticChainForce(SlashChain);
        Assert.Equal(1f, result.LoadRatio);
    }

    [Fact]
    public void LoadRatio_LightLoad_AboveOne()
    {
        var body = CreateBody();
        var result = body.GetKineticChainForce(SlashChain, 1f);
        // 600 / (1 * 10) = 60
        Assert.True(result.LoadRatio > 1f);
    }

    // ── Stamina cost scales with load ────────────────────────

    [Fact]
    public void StaminaCost_IncreasesWithLoad()
    {
        var body = CreateBody();
        var noLoad = body.GetKineticChainForce(SlashChain);
        var withLoad = body.GetKineticChainForce(SlashChain, 5f);

        Assert.True(withLoad.StaminaCost > noLoad.StaminaCost);
    }

    // ── ExertKineticChain drains stamina ─────────────────────

    [Fact]
    public void ExertKineticChain_DrainsStaminaOnAllParts()
    {
        var body = CreateBody();
        var muscular = Muscular(body);

        body.ExertKineticChain(SlashChain, 50f);
        body.Update(); // Process exert events

        foreach (var part in SlashChain)
        {
            var muscle = muscular.GetNode(part) as MuscleNode;
            if (muscle != null)
            {
                var stamina = muscle.GetComponent(BodyComponentType.Stamina);
                Assert.NotNull(stamina);
                Assert.True(stamina.Current < 100f, $"Stamina for {part} should have decreased");
            }
        }
    }

    // ── Bone integrity factor ────────────────────────────────

    [Fact]
    public void BoneIntegrityFactor_HealthyBone_ReturnsOne()
    {
        var body = CreateBody();
        var skeletal = Skeletal(body);
        Assert.Equal(1f, skeletal.GetBoneIntegrityFactor(BodyPartType.RightForearm));
    }

    [Fact]
    public void BoneIntegrityFactor_FracturedBone_ReturnsZero()
    {
        var body = CreateBody();
        var skeletal = Skeletal(body);

        // Fracture via heavy damage
        body.TakeDamage(BodyPartType.RightForearm, 200);
        body.Update();

        Assert.Equal(0f, skeletal.GetBoneIntegrityFactor(BodyPartType.RightForearm));
    }

    [Fact]
    public void BoneIntegrityFactor_DegradedBone_ReturnsProportional()
    {
        var body = CreateBody();
        var skeletal = Skeletal(body);

        // Damage the bone but not enough to fracture
        body.TakeDamage(BodyPartType.RightForearm, 30);
        body.Update();

        float factor = skeletal.GetBoneIntegrityFactor(BodyPartType.RightForearm);
        Assert.True(factor > 0f);
        Assert.True(factor < 1f);
    }

    // ── Body convenience methods ─────────────────────────────

    [Fact]
    public void Body_GetKineticChainForce_DelegatesToMuscular()
    {
        var body = CreateBody();
        var muscular = Muscular(body);

        var fromBody = body.GetKineticChainForce(SlashChain, 2f);
        var fromMuscular = muscular.GetKineticChainForce(SlashChain, 2f);

        Assert.Equal(fromMuscular.Force, fromBody.Force);
        Assert.Equal(fromMuscular.RawMuscleForce, fromBody.RawMuscleForce);
        Assert.Equal(fromMuscular.LoadRatio, fromBody.LoadRatio);
    }

    // ── Fracture blocks entire chain ─────────────────────────

    [Fact]
    public void FracturedBone_BlocksEntireChain()
    {
        var body = CreateBody();

        // Fracture the right forearm bone
        body.TakeDamage(BodyPartType.RightForearm, 200);
        body.Update();

        var result = body.GetKineticChainForce(SlashChain);

        Assert.True(result.IsBlocked);
        Assert.Equal(0f, result.Force);
        Assert.Equal(0f, result.RawMuscleForce);
        Assert.Contains("Fracture", result.BlockedReason);
        Assert.Contains(BodyPartType.RightForearm, result.LimitingParts);
    }

    // ── Pain blocks chain ────────────────────────────────────

    [Fact]
    public void HighPain_BlocksChain()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Inject pain >= 150 across chain parts (30 per part × 6 = 180)
        foreach (var part in SlashChain)
        {
            var nerve = nervous.GetNode(part) as NerveNode;
            Assert.NotNull(nerve);
            nerve!.ReceivePain(30f);
        }

        var result = body.GetKineticChainForce(SlashChain);

        Assert.True(result.IsBlocked);
        Assert.Equal(0f, result.Force);
        Assert.Contains("Pain", result.BlockedReason);
        Assert.True(result.ChainPainLevel >= 150f);
    }

    [Fact]
    public void ModeratePain_DoesNotBlockChain()
    {
        var body = CreateBody();
        var nervous = Nervous(body);

        // Inject pain < 150 across chain parts (20 per part × 6 = 120)
        foreach (var part in SlashChain)
        {
            var nerve = nervous.GetNode(part) as NerveNode;
            Assert.NotNull(nerve);
            nerve!.ReceivePain(20f);
        }

        var result = body.GetKineticChainForce(SlashChain);

        Assert.False(result.IsBlocked);
        Assert.True(result.Force > 0f);
        Assert.True(result.ChainPainLevel > 0f);
        Assert.True(result.ChainPainLevel < 150f);
    }

    [Fact]
    public void HealthyBody_ChainPainLevelIsZero()
    {
        var body = CreateBody();
        var result = body.GetKineticChainForce(SlashChain);

        Assert.Equal(0f, result.ChainPainLevel);
        Assert.False(result.IsBlocked);
    }
}
