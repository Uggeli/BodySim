namespace BodySim.Tests;

public class IntegumentarySystemTests
{
    private static BodyResourcePool PoolWithResources()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Oxygen, 100);
        pool.AddResource(BodyResourceType.Glucose, 100);
        pool.AddResource(BodyResourceType.Water, 100);
        pool.AddResource(BodyResourceType.Blood, 50);
        return pool;
    }

    // ── Initialisation ────────────────────────────────────

    [Fact]
    public void Init_AllPartsHaveHealthAndSkinIntegrity()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            var node = sys.GetNode(part);
            Assert.NotNull(node);
            Assert.True(node.HasComponent(BodyComponentType.Health));
            Assert.True(node.HasComponent(BodyComponentType.SkinIntegrity));
        }
    }

    [Fact]
    public void Init_ChestIsLargeSurface()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());
        var chest = sys.GetNode(BodyPartType.Chest) as SkinNode;
        Assert.NotNull(chest);
        Assert.True(chest.IsLargeSurface);
    }

    [Fact]
    public void Init_HandIsNotLargeSurface()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());
        var hand = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsLargeSurface);
    }

    [Fact]
    public void Init_AllNodesStartHealthyNoWounds()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        Assert.Equal(0, sys.GetWoundCount());
        Assert.Empty(sys.GetBurnedParts());
        Assert.Equal(100f, sys.GetOverallIntegrity());
    }

    // ── Damage absorption ─────────────────────────────────

    [Fact]
    public void Damage_ReducesSkinIntegrity()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 40));

        float integrity = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);
        Assert.True(integrity < 100,
            $"Damage should reduce skin integrity, got {integrity}");
    }

    [Fact]
    public void Damage_ReducesSkinHealth()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 40));

        var health = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(health);
        Assert.True(health.Current < 100,
            $"Damage should reduce skin health, got {health.Current}");
    }

    [Fact]
    public void Damage_ProtectionLevelDrops()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        float protBefore = sys.GetProtectionLevel(BodyPartType.LeftUpperArm);
        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 50));
        float protAfter = sys.GetProtectionLevel(BodyPartType.LeftUpperArm);

        Assert.Equal(1f, protBefore);
        Assert.True(protAfter < protBefore,
            $"Protection should drop after damage (before {protBefore}, after {protAfter})");
    }

    // ── Wounds ────────────────────────────────────────────

    [Fact]
    public void HeavyDamage_CausesWound()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        // Each hit absorbs ~30% scaled by remaining integrity, degrading skin.
        // Need sustained heavy damage to breach the wound threshold (integrity < 40).
        for (int i = 0; i < 5; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 80));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);
        Assert.True(skin.IsWounded,
            $"Heavy sustained damage should cause wound. Integrity: {sys.GetSkinIntegrity(BodyPartType.LeftUpperArm)}");
    }

    [Fact]
    public void Wound_IsExposed_WhenNoBandage()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        // Wound the skin with sustained damage
        for (int i = 0; i < 5; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 80));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);

        if (skin.IsWounded)
        {
            Assert.True(skin.IsExposed, "Wound without bandage should be exposed");
            Assert.Contains(BodyPartType.LeftUpperArm, sys.GetExposedParts());
        }
    }

    [Fact]
    public void Wound_NotExposed_WhenBandaged()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        for (int i = 0; i < 5; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 80));
        sys.HandleMessage(new BandageEvent(BodyPartType.LeftUpperArm));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);

        if (skin.IsWounded)
        {
            Assert.False(skin.IsExposed, "Bandaged wound should not be exposed");
            Assert.True(skin.IsBandaged);
        }
    }

    // ── Burns ─────────────────────────────────────────────

    [Fact]
    public void Burn_DamagesIntegrityAndHealth()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40));

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        Assert.True(skin.IsBurned);
        Assert.True(sys.GetSkinIntegrity(BodyPartType.LeftHand) < 100);
    }

    [Fact]
    public void Burn_MildIntensity_FirstDegree()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 20));

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        Assert.Equal(1, skin.BurnDegree);
    }

    [Fact]
    public void Burn_ModerateIntensity_SecondDegree()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40));

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        Assert.Equal(2, skin.BurnDegree);
    }

    [Fact]
    public void Burn_HighIntensity_ThirdDegree()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 70));

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        Assert.Equal(3, skin.BurnDegree);
    }

    [Fact]
    public void Burn_SecondDegreeOrHigher_StopsRegen()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40)); // 2nd degree

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);

        var integrity = skin.GetComponent(BodyComponentType.SkinIntegrity);
        Assert.NotNull(integrity);
        Assert.Equal(0, integrity.RegenRate);
    }

    [Fact]
    public void Burn_EmitsPainEvent()
    {
        var hub = new EventHub();
        var sys = new IntegumentarySystem(PoolWithResources(), hub);
        PainEvent? receivedPain = null;
        var listener = new TestListener(evt =>
        {
            if (evt is PainEvent pe) receivedPain = pe;
        });
        hub.RegisterListener<PainEvent>(listener);

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 50));

        ((IListener)listener).Update();
        Assert.NotNull(receivedPain);
        Assert.Equal(BodyPartType.LeftHand, receivedPain.Value.BodyPartType);
    }

    [Fact]
    public void Burn_ThirdDegree_EmitsDamageToDeepTissue()
    {
        var hub = new EventHub();
        var sys = new IntegumentarySystem(PoolWithResources(), hub);
        DamageEvent? receivedDamage = null;
        var listener = new TestListener(evt =>
        {
            if (evt is DamageEvent de) receivedDamage = de;
        });
        hub.RegisterListener<DamageEvent>(listener);

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 70)); // 3rd degree

        ((IListener)listener).Update();
        Assert.NotNull(receivedDamage);
        Assert.Equal(BodyPartType.LeftHand, receivedDamage.Value.BodyPartType);
    }

    [Fact]
    public void GetBurnedParts_ReturnsBurnedParts()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40));
        sys.HandleMessage(new BurnEvent(BodyPartType.RightFoot, 30));

        var burned = sys.GetBurnedParts();
        Assert.Contains(BodyPartType.LeftHand, burned);
        Assert.Contains(BodyPartType.RightFoot, burned);
        Assert.Equal(2, burned.Count);
    }

    // ── Healing ───────────────────────────────────────────

    [Fact]
    public void Heal_RestoresSkinHealthAndIntegrity()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 40));
        float integrityDamaged = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);

        sys.HandleMessage(new HealEvent(BodyPartType.LeftUpperArm, 30));
        float integrityHealed = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);

        Assert.True(integrityHealed > integrityDamaged,
            $"Healing should restore integrity (damaged {integrityDamaged}, healed {integrityHealed})");
    }

    [Fact]
    public void Heal_CanClosePreviousWound()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        // Wound the skin with sustained damage
        for (int i = 0; i < 5; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 80));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);

        if (skin.IsWounded)
        {
            // Heal enough to close wound (integrity back above threshold)
            for (int i = 0; i < 5; i++)
                sys.HandleMessage(new HealEvent(BodyPartType.LeftUpperArm, 50));

            // Wound should be closed (integrity back above threshold)
            Assert.False(skin.IsWounded,
                $"Sufficient healing should close wound. Integrity: {sys.GetSkinIntegrity(BodyPartType.LeftUpperArm)}");
        }
    }

    // ── Bandage ───────────────────────────────────────────

    [Fact]
    public void Bandage_AppliesBandageState()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BandageEvent(BodyPartType.LeftUpperArm));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);
        Assert.True(skin.IsBandaged);
    }

    [Fact]
    public void RemoveBandage_ClearsBandageState()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BandageEvent(BodyPartType.LeftUpperArm));
        sys.HandleMessage(new RemoveBandageEvent(BodyPartType.LeftUpperArm));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);
        Assert.False(skin.IsBandaged);
    }

    [Fact]
    public void Bandage_BoostsRegenOnBurnedSkin()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40)); // 2nd degree
        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        float regenBefore = skin.GetComponent(BodyComponentType.SkinIntegrity)!.RegenRate;

        sys.HandleMessage(new BandageEvent(BodyPartType.LeftHand));
        float regenAfter = skin.GetComponent(BodyComponentType.SkinIntegrity)!.RegenRate;

        Assert.True(regenAfter > regenBefore,
            $"Bandage should boost regen (before {regenBefore}, after {regenAfter})");
    }

    // ── Metabolic Update ──────────────────────────────────

    [Fact]
    public void MetabolicUpdate_ExposedWound_LosesIntegritySlowly()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        // Create a wound with sustained damage
        for (int i = 0; i < 5; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 80));

        var skin = sys.GetNode(BodyPartType.LeftUpperArm) as SkinNode;
        Assert.NotNull(skin);

        if (skin.IsWounded && skin.IsExposed)
        {
            float intBefore = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);
            sys.MetabolicUpdate();
            float intAfter = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);

            Assert.True(intAfter < intBefore,
                $"Exposed wound should lose integrity (before {intBefore}, after {intAfter})");
        }
    }

    [Fact]
    public void MetabolicUpdate_SevereBurn_OngoingDamage()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new BurnEvent(BodyPartType.LeftHand, 40)); // 2nd degree

        var health = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(health);
        float healthBefore = health.Current;

        sys.MetabolicUpdate();
        float healthAfter = health.Current;

        Assert.True(healthAfter < healthBefore,
            $"2nd+ degree burn should cause ongoing health loss (before {healthBefore}, after {healthAfter})");
    }

    [Fact]
    public void MetabolicUpdate_ConsumesResources()
    {
        var pool = PoolWithResources();
        var sys = new IntegumentarySystem(pool, new EventHub());

        float waterBefore = pool.GetResource(BodyResourceType.Water);
        sys.MetabolicUpdate();
        float waterAfter = pool.GetResource(BodyResourceType.Water);

        Assert.True(waterAfter < waterBefore,
            $"Skin should consume water (before {waterBefore}, after {waterAfter})");
    }

    // ── Aggregate queries ─────────────────────────────────

    [Fact]
    public void GetOverallIntegrity_FullHealth_Returns100()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());
        Assert.Equal(100f, sys.GetOverallIntegrity());
    }

    [Fact]
    public void GetOverallIntegrity_AfterDamage_DropsBelowFull()
    {
        var sys = new IntegumentarySystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.Chest, 60));

        Assert.True(sys.GetOverallIntegrity() < 100f);
    }

    // ── EventHub integration ──────────────────────────────

    [Fact]
    public void EventHub_DamageEvent_ProcessedBySkin()
    {
        var hub = new EventHub();
        var sys = new IntegumentarySystem(PoolWithResources(), hub);

        hub.Emit(new DamageEvent(BodyPartType.LeftUpperArm, 30));

        // IntegumentarySystem processes DamageEvents immediately via OnMessage
        // so integrity should already be reduced
        float integrity = sys.GetSkinIntegrity(BodyPartType.LeftUpperArm);
        Assert.True(integrity < 100,
            $"Skin should process damage via EventHub, integrity: {integrity}");
    }

    [Fact]
    public void EventHub_BurnEvent_QueuedAndProcessed()
    {
        var hub = new EventHub();
        var sys = new IntegumentarySystem(PoolWithResources(), hub);

        hub.Emit(new BurnEvent(BodyPartType.LeftHand, 40));
        Assert.Single(sys.EventQueue);

        sys.Update();

        var skin = sys.GetNode(BodyPartType.LeftHand) as SkinNode;
        Assert.NotNull(skin);
        Assert.True(skin.IsBurned);
    }

    // ── Helper ────────────────────────────────────────────

    private class TestListener(Action<IEvent> handler) : IListener
    {
        public System.Collections.Concurrent.ConcurrentBag<IEvent> EventQueue { get; set; } = [];
        public void HandleMessage(IEvent evt) => handler(evt);
    }
}
