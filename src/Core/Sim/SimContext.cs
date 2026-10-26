using Core.Character;
using Core.Content;
using Core.Rng;
using Core.World;

namespace Core.Sim;

/// <summary>
/// What systems need besides the state: content and the rules built from it, plus the live RNG
/// streams. Built once per <see cref="Simulation"/>; none of it is saved except the RNG states.
/// </summary>
public sealed class SimContext
{
    public SimContext(ContentDb content, RngStreams rng)
    {
        Content = content;
        Rng = rng;
        Config = content.Get<RunConfigDef>(RunConfigDef.RunId);
        Economy = content.Get<EconomyConfigDef>(EconomyConfigDef.EconomyId);
        Breakthrough = content.Get<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId);
        Rules = new CharacterRules(content);
        Map = new WorldMap(content);
    }

    public ContentDb Content { get; }
    public RngStreams Rng { get; }
    public RunConfigDef Config { get; }
    public EconomyConfigDef Economy { get; }
    public BreakthroughConfigDef Breakthrough { get; }
    public CharacterRules Rules { get; }
    public WorldMap Map { get; }
}
