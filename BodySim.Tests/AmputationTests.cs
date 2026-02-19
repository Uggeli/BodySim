namespace BodySim.Tests;

public class AmputationTests
{
    private static Body CreateBody() => new();

    private static MuscularSystem Muscular(Body body) =>
        (MuscularSystem)body.GetSystem(BodySystemType.Muscular)!;

    private static SkeletalSystem Skeletal(Body body) =>
        (SkeletalSystem)body.GetSystem(BodySystemType.Skeletal)!;

    private static NervousSystem Nervous(Body body) =>
        (NervousSystem)body.GetSystem(BodySystemType.Nerveus)!;

    private static CirculatorySystem Circulatory(Body body) =>
        (CirculatorySystem)body.GetSystem(BodySystemType.Circulatory)!;

    // ── Basic amputation ─────────────────────────────────────

    [Fact]
    public void Amputate_Hand_MarksPartAsMissing()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.LeftHand);

        Assert.True(body.IsPartMissing(BodyPartType.LeftHand));
    }

    [Fact]
    public void Amputate_NonAmputatablePart_IsIgnored()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.Head);
        body.Amputate(BodyPartType.Chest);
        body.Amputate(BodyPartType.Pelvis);

        Assert.False(body.IsPartMissing(BodyPartType.Head));
        Assert.False(body.IsPartMissing(BodyPartType.Chest));
        Assert.False(body.IsPartMissing(BodyPartType.Pelvis));
    }

    [Fact]
    public void Amputate_SamePart_Twice_IsIdempotent()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.RightHand);
        body.Amputate(BodyPartType.RightHand);

        Assert.True(body.IsPartMissing(BodyPartType.RightHand));
    }

    // ── Cascade ──────────────────────────────────────────────

    [Fact]
    public void Amputate_UpperArm_CascadesToForearmAndHand()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.LeftUpperArm);

        Assert.True(body.IsPartMissing(BodyPartType.LeftUpperArm));
        Assert.True(body.IsPartMissing(BodyPartType.LeftForearm));
        Assert.True(body.IsPartMissing(BodyPartType.LeftHand));
    }

    [Fact]
    public void Amputate_Shoulder_CascadesToEntireArm()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.RightShoulder);

        Assert.True(body.IsPartMissing(BodyPartType.RightShoulder));
        Assert.True(body.IsPartMissing(BodyPartType.RightUpperArm));
        Assert.True(body.IsPartMissing(BodyPartType.RightForearm));
        Assert.True(body.IsPartMissing(BodyPartType.RightHand));
    }

    // ── System node removal ─────────────────────────────────

    [Fact]
    public void Amputate_RemovesNodeFromAllSystems()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.LeftHand);
        body.Update(); // Process queued AmputationEvents

        // Every system should have lost the LeftHand node
        Assert.Null(Muscular(body).GetNode(BodyPartType.LeftHand));
        Assert.Null(Skeletal(body).GetNode(BodyPartType.LeftHand));
        Assert.Null(Nervous(body).GetNode(BodyPartType.LeftHand));
        Assert.Null(Circulatory(body).GetNode(BodyPartType.LeftHand));
    }

    // ── Kinetic chain blocking ──────────────────────────────

    [Fact]
    public void Amputate_BlocksKineticChainContainingMissingPart()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.RightForearm);
        body.Update(); // Process queued events

        var chain = new[]
        {
            BodyPartType.RightShoulder, BodyPartType.RightUpperArm,
            BodyPartType.RightForearm, BodyPartType.RightHand,
        };

        var result = Muscular(body).GetKineticChainForce(chain, 0f);

        Assert.Equal(0f, result.Force);
        Assert.True(result.IsBlocked);
        Assert.Contains("Missing", result.BlockedReason);
    }

    // ── Weight reduction ────────────────────────────────────

    [Fact]
    public void Amputate_ReducesTotalWeight()
    {
        var body = CreateBody();

        float weightBefore = body.GetBodyComposition().TotalWeight;

        body.Amputate(BodyPartType.LeftUpperArm);
        body.Update(); // Process queued events

        float weightAfter = body.GetBodyComposition().TotalWeight;

        Assert.True(weightAfter < weightBefore,
            $"Weight should decrease after amputation: before={weightBefore}, after={weightAfter}");
    }

    // ── Event triggering ────────────────────────────────────

    [Fact]
    public void Amputate_TriggersShock()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.LeftUpperArm);
        body.Update(); // Process queued events

        var nervous = Nervous(body);
        Assert.True(nervous.ShockLevel > 0,
            "Shock should be triggered by amputation");
    }

    // ── Downstream-only cascade ─────────────────────────────

    [Fact]
    public void Amputate_Forearm_DoesNotAffectUpperArm()
    {
        var body = CreateBody();

        body.Amputate(BodyPartType.LeftForearm);

        // Only forearm and hand should be missing — NOT the upper arm
        Assert.True(body.IsPartMissing(BodyPartType.LeftForearm));
        Assert.True(body.IsPartMissing(BodyPartType.LeftHand));
        Assert.False(body.IsPartMissing(BodyPartType.LeftUpperArm));
        Assert.False(body.IsPartMissing(BodyPartType.LeftShoulder));
    }
}
