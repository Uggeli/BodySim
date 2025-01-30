
namespace BodySim.Tests;
public class SkeletalSystemTests
{
    [Fact]
    public void DamageTaken_ReduceHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 10));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Test damage Event trough EventHub
        EventHub.Emit(new DamageEvent(BodyPartType.Head, 10));
        Assert.Single(skeletalSystem.EventQueue); 
        skeletalSystem.Update();
        Assert.Equal(80, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Damage taken should not go below 0
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        Assert.Equal(0, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void HealTaken_IncreaseHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        // set health to 80
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 20));

        Assert.Equal(80, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        skeletalSystem.HandleMessage(new HealEvent(BodyPartType.Head, 10));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Test heal Event trough EventHub
        EventHub.Emit(new HealEvent(BodyPartType.Head, 10));
        Assert.Single(skeletalSystem.EventQueue); 
        skeletalSystem.Update();
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Heal taken should not go above 100
        skeletalSystem.HandleMessage(new HealEvent(BodyPartType.Head, 100));
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void PropagateDamage_ReduceHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.LeftShoulder)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current);

        skeletalSystem.HandleMessage(new PropagateEffectEvent(BodyPartType.Head, new ImpactEffect(10)));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(93, skeletalSystem.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current);
    }


}