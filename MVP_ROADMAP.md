# Project Lanista - MVP Roadmap

## Target: Playable Vertical Slice
Full ludus with roster of gladiators, team-based arena combat with magic weaving,
management between fights, and minimal political layer. Solo dev.

## Design Decisions

| Decision | Answer |
|---|---|
| Death | Rare, mostly accidental. Gladiators yield when unable to fight or pain too high. Thumbs down from magistrate/emperor = execution (political event). |
| Turn system | HoMM5-style initiative queue. Speed stat determines slot order. Big spells consume multiple slots. |
| Team composition | Flexible (any size teams). Mostly team vs team. |
| Equipment | Full loadout (6+ slots: weapon, offhand, head, chest, legs, accessory). Cull unused slots later. |
| Info density | Minimal in combat. No HP bars. Player reads the simulation through visual feedback (limping, bleeding, staggering). Details available on drill-down outside combat. |
| Recruitment | Multiple sources: slave market, captured prisoners, patron gifts, volunteers. |
| Training injuries | Yes. Overtraining increases injury risk. Push too hard = muscle tears, stress fractures. |
| Art style | Godot icons / placeholder for MVP. Pixel art or hand-drawn later with hired artist. |
| UI aesthetic | Minimalistic, IBM design guidelines. Detail only when needed. |
| Grid size | Variable per arena. Generated ovals from tiny pits to colosseums. |
| Session length | Player-paced. No time pressure. |
| Camera | Fixed isometric angle, zoom only. |
| Save format | JSON |
| Modding | Data-driven from start. Spells, equipment, traits, arenas defined in JSON data files. |
| AI | Simple FSM for MVP. Nothing fancy. |
| Save mode | Ironman by default (single auto-save, no manual saves). Save scumming via backup copies acts as natural easy mode. No difficulty settings. |
| Power fantasy | Gladiators must get meaningfully stronger over time. Training builds muscle, bone density, nerve conductivity, pain tolerance. Veterans should obliterate rookies. The management payoff is visible dominance. |
| Magic power | Magic is powerful. A fully committed high-level weave can end a fight or cripple half a team. The risk (heat, snap, self-destruction) is the balancing factor, not low damage. Melee = consistent DPS, magic = all-in burst with catastrophic downside. |
| AI mages | Dumb for MVP. Scripted patterns per mage ("always releases at level 2", "greedy, goes for level 3+, sometimes explodes"). No real weave decision-making. |
| Info in combat | Player can inspect any gladiator on their turn to see body diagram with injury states. Reading your opponent is a player skill, not hidden information. Visual-first, detail-on-demand. |
| Body targeting | Default attack lets BodySim + weapon determine hit location. Precision targeting is an optional action costing extra AP or requiring skill check. Keeps turns fast, surgical strikes are special. |
| Match throwing | Unhappy gladiators fight badly (lazy, miss openings), never deliberately lose. Multiple escalating warnings before anything irreversible. No "oops your champion is dead because you ran out of wine." |
| The Rust | Grace period for injured gladiators. Rust only hits healthy fighters who aren't being used. Pushes roster rotation, doesn't punish healing. |
| Needs scaling | Legend-tier income should comfortably cover legend-tier needs. Scaling feels like rewarding champions, not being extorted. Net positive investment. |
| Crowd boredom | Crowd rewards escalation via arena tiers, not punishment for repetition. Higher tier arenas expect more spectacle. Tier 1 crowd is happy with basics. |
| Gear as playstyle | Equipment + physical stats determine combat identity. No class system. Strong guy + heavy armor + greataxe = tank. Fast guy + leather + daggers = flanker. Weight/stamina cost makes it a real tradeoff. |
| Management core | Match scheduling drives management. Upcoming matches announced with details (format, reward, opponent). Player preps candidates. Also: training, equipment, hiring, espionage, commissions. Random events are spice, not the main course. |
| World map | City/world map for spatial interaction. Order supplies from merchants, commission gear from blacksmiths, watch deliveries arrive. Enables sabotage (intercept rival's shipments). Gives the game a sense of place. |
| Scars | Mostly cosmetic. Visual storytelling. Not a significant gameplay modifier. |
| Defensive combat | Guard, dodge, riposte, shove/bash. Defense is as important as offense. Shield fighters are walls, light fighters dodge, skilled fighters counter. |
| Combat identity | Weapon type drives tactical identity via reach, speed, and available actions - not a class system. Spear = reach + zone control, dual wield = fast + flanking, net = entangle + control, shield = guard + bash. |

## Tech Stack
- **Engine**: Godot 4 (C#)
- **Rendering**: 2.5D (sprites in 3D world, square grid combat)
- **Architecture**: BodySim lib -> ProjectLanista.Core (pure C#) -> Godot (rendering/input only)
- **Data**: JSON for saves, JSON for game data definitions (data-driven)
- **Tests**: xUnit

## Architecture

```
BodySim/                      (existing lib - pure biology engine, no game concepts)
ProjectLanista.Core/          (pure C# game logic, testable without Godot)
  ├── Combat/                 (initiative queue, grid, action resolution, weaving)
  ├── Management/             (ludus, staff, economy, scheduling)
  ├── Social/                 (persona, traits, morale, relationships)
  ├── World/                  (arenas, factions, politics)
  ├── Crowd/                  (spectacle tracking, appetite, rewards)
  └── Data/                   (JSON loaders, game data definitions)
ProjectLanista.Godot/         (Godot project - rendering, input, UI, audio)
  ├── Scenes/Combat/
  ├── Scenes/Ludus/
  ├── Scenes/Menus/
  └── UI/
ProjectLanista.Tests/         (xUnit tests for Core + BodySim integration)
Data/                         (JSON game data - moddable)
  ├── equipment.json
  ├── spells.json
  ├── traits.json
  ├── arenas.json
  └── ...
```

---

## Phase 0: Foundation (Weeks 1-2)
**Goal**: Solution structure, BodySim integration, data-driven skeleton, basic game loop.

### Project Setup
- [ ] Create solution: Core, Godot, Tests projects with BodySim reference
- [ ] Set up Godot 4 C# project
- [ ] JSON data loader system (deserialize game data files into Core models)
- [ ] Create initial data files: equipment.json, spells.json, traits.json, arenas.json

### Core Domain Models
- [ ] `Gladiator`: owns BodySim `Body` + `Persona` + `Equipment` + combat stats (initiative, skills)
- [ ] `Ludus`: owns roster, gold, facilities, staff, reputation
- [ ] `GameState`: current day/week, phase, event queue, save/load to JSON
- [ ] `Arena`: grid dimensions, terrain layout, tier, crowd appetite profile

### Game Loop
- [ ] State machine: `ManagementPhase -> MatchSetup -> CombatPhase -> PostCombat -> ManagementPhase`
- [ ] Stub Godot scenes: main menu, ludus screen, combat screen, post-combat report
- [ ] Scene transitions working

---

## Phase 1: Combat Core (Weeks 3-6)
**Goal**: Two teams fighting on a variable-size grid with BodySim resolving damage.
Gladiators visually react to injuries. No HP bars.

### Initiative Queue
- [ ] HoMM5-style turn queue: ordered by speed/initiative stat
- [ ] Queue display showing upcoming turns for both sides
- [ ] Variable action time costs (fast actions = sooner next turn, slow actions = later)
- [ ] Spell casting consumes multiple queue slots for Arena-speed spells

### Grid & Movement
- [ ] Variable-size grid generation (oval arenas, different sizes per arena definition)
- [ ] Tile types: sand, mud, stone, pit, pillar (defined in arena JSON)
- [ ] Movement with AP cost, pathfinding
- [ ] Zones of control: adjacent enemies = engaged
- [ ] Disengage action (costs AP, safe retreat)
- [ ] Reaction attacks when leaving engagement without disengage

### Team Combat
- [ ] Flexible team sizes (1v1 to NvN)
- [ ] Team setup: player assigns gladiators to starting positions
- [ ] Simple FSM AI for enemy team:
  - States: Advance / Engage / Retreat / Protect / Pressure Mage
  - Transitions based on health, distance, team situation

### Melee Resolution
- [ ] Default attack: weapon type + skill determines hit location probabilistically (fast turns)
- [ ] Precision strike: optional, costs extra AP or skill check. Target specific body part.
- [ ] Hit resolution: attacker weapon skill vs defender skill -> hit location + severity
- [ ] Damage routed through BodySim:
  - Integumentary absorbs first (skin + armor from equipment)
  - Skeletal: fractures, structural failure
  - Muscular: tears, strength loss
  - Circulatory: bleeding, blood pressure drop
  - Nervous: pain generation, shock accumulation
- [ ] Limb disabling: fractured leg = can't move, damaged arm = can't attack with it
- [ ] Equipment weight system: heavy gear drains stamina faster, movement penalty. Strong gladiators handle it, nimble ones suffer.

### Defensive Actions
- [ ] Guard/Block: reduce incoming damage, costs action. Shields make this much better.
- [ ] Dodge: chance to avoid entirely, better for light/unarmored fighters. Requires stamina.
- [ ] Riposte/Counter: defensive stance that punishes next attacker. High skill requirement.
- [ ] Shove/Bash: push enemy back 1 tile, break engagement, disrupt weaving. Shields excel.
- [ ] Intercept: reaction - guardian steps into attack aimed at adjacent ally (protect the mage mechanic).

### Weapon Identity (no classes - gear defines tactics)
- [ ] Sword & shield: guard, bash, steady. The anchor. Medium damage, high defense.
- [ ] Two-handed (axe/hammer): cleave (hit adjacent targets), slow, devastating. The threat.
- [ ] Spear/trident: reach 2 tiles, zone control, keep-away. The controller.
- [ ] Dual wield / dagger: multiple attacks per turn, flanking bonus, fast. The assassin.
- [ ] Net & trident: entangle (immobilize), pull, unique gladiator fantasy.
- [ ] Each weapon type has different available actions, not just different damage numbers.

### Yield & Death
- [ ] Yield conditions (auto): pain threshold exceeded, can't stand, unconscious (blood pressure)
- [ ] Yield conditions (manual): domina orders gladiator to yield
- [ ] Post-yield: magistrate/crowd thumbs up/down decision (based on crowd satisfaction + politics)
- [ ] Death only on thumbs down or rare accidental (critical hit to head/neck, bleed-out if medicus too slow)

### Combat Godot Layer
- [ ] 2.5D grid rendering: textured ground plane + sprite gladiators
- [ ] Fixed isometric camera with zoom
- [ ] Click-to-move, click-to-attack input
- [ ] Body part targeting: silhouette overlay on attack
- [ ] Initiative queue UI (portrait bar showing turn order)
- [ ] No HP bars. Visual injury feedback:
  - Sprites show blood, limping animations, stagger
  - Status icons on hover only (bleeding, fractured, stunned)
- [ ] Basic animations: idle, move, attack, hit reaction, stagger, fall, yield
- [ ] Terrain rendering: mud patches, pillars, pits visible on grid
- [ ] Placeholder audio from day one: hit sounds, block sounds, crowd murmur, footsteps, yield cry (even free assets - combat feel is 50% audio)

---

## Phase 2: Magic System (Weeks 7-9)
**Goal**: Mage gladiators with full weave/link/release loop.
Data-driven spell definitions.

### Spell Data
- [ ] Seed spells defined in spells.json:
  - Speed class (Reflex / Combat / Arena)
  - Base power, base heat, element, shape
  - Windup time (queue slots consumed)
  - Available links
- [ ] Link modifiers in data: Amplify, Sustain, Fork, Pierce, Anchor, Detonate
  - Each link declares: windup increase, sustain cost, interruption sensitivity change
- [ ] Wild magic templates in data: chain arc, ground burst, cone spray, heat bloom, shockwave, etc.

### Weaving Core
- [ ] Mana, Bandwidth, Heat, Focus as combat resources on mage gladiators
- [ ] Weave state machine: Conjure -> (Stabilize | Link | Release | Abort) per turn
- [ ] Exponential scaling: Power(n) = Base * r^n, Heat(n) = BaseHeat * (r^n)^a
- [ ] Conductivity stat from NervousSystem: higher = less heat per output
- [ ] Casting modes: Trickle (safe, weak), Channel (sustained), Burst (spike), Overload (catastrophic)

### Interruption & Failure
- [ ] Focus check on damage during conjure/channel (skill + hit severity check)
- [ ] Club to the face = interrupted (simple and intuitive)
- [ ] Failure spectrum weighted by heat zone + weave level:
  - Fizzle: mana spent, minor stagger
  - Spill: wrong target/shape
  - Backlash: centered on caster, nerve burn, conductivity loss
  - Arc Flash: uncontrolled AOE, arena damage
  - Overload: myth-tier catastrophe, lethal, political event
- [ ] BodySim integration: failure effects resolved as heat + nerve damage + pain through existing systems

### Reflex Kit (instant survival spells)
- [ ] Micro-shield (reduce next hit)
- [ ] Flare (accuracy penalty on target)
- [ ] Spark-jolt (minor stun)
- [ ] Step impulse (small pushback)

### Engagement vs Magic
- [ ] Focus penalty when engaged in melee
- [ ] Melee reaction attacks can trigger interruption checks
- [ ] Telegraphing on grid: projected spell shape, progress pips, risk tier

### Magic Godot Layer
- [ ] Weave UI: current level, heat bar, focus indicator, link options panel
- [ ] Spell shape preview on grid (projected AOE, updates as links added)
- [ ] Visual effects: casting glow, release flash, failure explosions
- [ ] Heat feedback on mage sprite: glow -> sparks -> smoke as heat rises

---

## Phase 3: Post-Combat & Recovery (Weeks 10-11)
**Goal**: Injuries persist between fights. Treatment is a real decision.

### Injury Report
- [ ] Post-combat body scan: full injury list from BodySim state
- [ ] Visual body diagram showing all damage
- [ ] Injury severity classification: bruise / fracture / nerve damage / organ failure
- [ ] Recovery time estimates based on severity + current body state

### Chronic BodySim Mode (new BodySim features)

#### Growth (the power fantasy)
- [ ] Muscle hypertrophy: training increases muscle mass, strength, force output
- [ ] Bone densification: load-bearing training increases density and fracture resistance
- [ ] Nerve conductivity training: practice improves signal speed, lowers heat per cast
- [ ] Pain tolerance: combat experience raises pain thresholds (not nerves dying - toughening up)
- [ ] Cardiovascular conditioning: stamina pool grows, recovery between rounds improves
- [ ] Immune hardening: surviving infections builds resistance
- [ ] A veteran gladiator should be measurably, visibly superior to a rookie in every system

#### Healing & Recovery
- [ ] Time-based healing: bones knit, wounds close, blood replenishes over days/weeks
- [ ] Infection risk from open wounds (ImmuneSystem fights it over time)
- [ ] Scarring: mostly cosmetic. Visual storytelling on the body diagram. Minimal gameplay impact.
- [ ] Nerve damage persistence: reduced conductivity (mage power loss - the real cost of overload)

#### Decay (only when relevant)
- [ ] Atrophy of disabled/unused limbs (not resting injured ones - grace period)
- [ ] The Rust: healthy idle gladiators lose conditioning. Injured ones are exempt.
- [ ] Overtraining injuries: pushing too hard -> muscle tears, stress fractures (risk scales with intensity)

### Treatment System
- [ ] Medicus assignment: assign doctor to injured gladiator
- [ ] Treatment choices (quality depends on Medicus skill):
  - Bandage: cheap, slow, infection risk
  - Set bone: medium cost, proper healing
  - Surgery: expensive, fast, complication risk
  - Amputation: cheap, permanent loss, problem gone
- [ ] Drug/potion system routed through MetabolicSystem:
  - Painkillers (suppress pain, mask injuries, addiction risk)
  - Stimulants (temporary boost, toxicity)
  - Healing salves (accelerate recovery)
  - Illegal stims (huge boost, snap severity increase, addiction)

---

## Phase 4: Ludus Management (Weeks 12-14)
**Goal**: Functional management between fights. Match scheduling drives the phase.

### Match Scheduling (the backbone of management)
- [ ] Match board: upcoming matches announced with details
  - Format (1v1, 2v2, 3v3, free-for-all), date, arena, rewards
  - Opponent ludus and known info about their fighters
- [ ] Player signs up for matches, assigns gladiator candidates
- [ ] Prep countdown: days until match drive all other decisions
  - "2v2 in 5 days, prize 500g vs Ludus Varro. Who do I send? Are they healthy? What gear?"
- [ ] Match frequency scales with tier (fewer early, more later)
- [ ] Player chooses when to fight - no forced schedule early game (avoids death spiral with small roster)
- [ ] Securing matches: prestigious bouts aren't just available - you lobby, bribe, and politic for them
  - Low tier: open sign-up, anyone can enter
  - Mid tier: need magistrate's favor or prestige threshold to qualify
  - High tier: invitation only - requires political connections, sponsor backing, or rival challenge
  - Securing a slot in a high-tier match IS the management goal, fighting in it is the payoff

### Economy
- [ ] Gold as primary currency
- [ ] Income: match purses (base + crowd bonus + sponsor bonus)
- [ ] Expenses: food, medicine, staff wages, facility upkeep, equipment, commissions
- [ ] Weekly budget cycle with summary
- [ ] Competing money sinks: match entry fees for higher-tier bouts, bribes, facility upgrades, commissions
- [ ] Debt system (skeleton): borrow from syndicate, interest compounds, they own you if you can't pay

### Facilities (level 1 only, upgrades later)
- [ ] Cells: rest quality -> nervous system recovery rate
- [ ] Mess Hall: food quality -> metabolic satisfaction
- [ ] Training Yard: required for training. Quality affects injury risk.
- [ ] Apothecary: required for drug/salve production
- [ ] Baths: stress reduction rate
- [ ] Shrine: prestige/honor -> willpower buffs
- [ ] Each facility defined in JSON (name, cost, effects, upgrade path for later)

### Staff
- [ ] Doctore: training speed, training injury risk reduction
- [ ] Medicus: healing speed, available treatment options
- [ ] Magister: food efficiency (gold -> nutrition ratio)
- [ ] Hire/fire from available pool, assign to gladiator or facility
- [ ] Staff skill levels affect outcomes

### Gladiator Management
- [ ] Roster screen: all gladiators with status at a glance
- [ ] Individual screen: body diagram, persona summary, equipment, combat record
- [ ] Weekly schedule: Train / Rest / Recover / Idle
- [ ] Training focus: strength, speed, endurance, weapon skill, magic
  - Routes through BodySim (muscle hypertrophy, nerve conductivity training)
  - Overtraining risk scales with intensity
- [ ] Needs system (skeleton): food, rest, basic morale tracking

### Equipment
- [ ] 6 slots: weapon, offhand/shield, head, chest, legs, accessory
- [ ] Equipment defined in JSON: name, slot, weight, protection per body part, stat modifiers
- [ ] Weight system: total equipment weight vs gladiator strength -> stamina drain, movement penalty
- [ ] Weapon types: sword, spear, axe, club, dagger, trident, net (each with unique combat actions)
- [ ] Armor: protection mapped to specific BodySim body parts
- [ ] Mage gear: radiator pauldrons (cooling), grounding spear (heat discharge), anchor talisman
- [ ] Buy from market or commission from craftsmen (see world map)

### World Map
- [ ] City/region map showing ludus, arenas, merchants, blacksmiths, slave market, rivals
- [ ] Order supplies: pick merchant, pick goods, pay, delivery takes time (courier travels on map)
- [ ] Commission equipment: order custom gear from blacksmith, specify requirements, wait for delivery
- [ ] Watch deliveries arrive (courier moves on map day by day)
- [ ] Spatial sabotage opportunities: intercept rival shipments, bribe their suppliers (post-MVP)
- [ ] Espionage: send scout to rival ludus, costs gold + time, returns intel on their fighters
- [ ] Delivery events: rare random events on courier routes (ambush, delay, damage, theft)
  - Not frequent enough to be annoying, impactful enough to be memorable
  - Creates emergent stories ("bandits stole our shipment, send someone to retrieve it")
- [ ] Gives the game a sense of place beyond menu screens

### Recruitment
- [ ] Slave market: rotating roster of randomly generated gladiators, price by quality
- [ ] Captured prisoners: offered after certain victories
- [ ] Patron gifts: political reward for prestige milestones
- [ ] Volunteers: rare, high morale but freedom expectations

### Ludus Godot Layer
- [ ] Ludus overview screen (facility list/map, minimal)
- [ ] Roster list UI
- [ ] Individual gladiator screen with body diagram
- [ ] Weekly schedule assignment UI
- [ ] Financial summary panel
- [ ] Staff management panel
- [ ] Equipment loadout screen (drag and drop)
- [ ] Market/recruitment screen

---

## Phase 5: Persona & Social (Weeks 15-16)
**Goal**: Gladiators feel like people, not stat blocks.

### Persona Core
- [ ] Traits (2-3 per gladiator, defined in traits.json):
  - Ambitious, Loyal, Cowardly, Bloodthirsty, Ascetic, Gluttonous, Proud, etc.
  - Each trait modifies behavior in combat and management
- [ ] Morale: derived from needs satisfaction + win/loss record + treatment quality
- [ ] Stress: accumulates from combat, pain, poor conditions
  - Vices reduce stress: wine (+ toxicity), baths, etc.
- [ ] Loyalty: affected by treatment, pay, promises, rival offers
- [ ] Low morale effects: lazy fighting, match throwing, training refusal
- [ ] Low loyalty effects: susceptible to poaching, escape attempts

### Relationships
- [ ] Rivalry: hatred of specific opponents -> Enraged in combat (+damage, -defense)
- [ ] Brotherhood: bond between gladiators -> sync bonus in team fights
- [ ] Mentor/Student: veteran boosts trainee XP
- [ ] Simple relationship grid (like / dislike / neutral)

### Needs Scaling (from GDD)
- [ ] Rookie: gruel + water
- [ ] Veteran: wine + bedding
- [ ] Champion: oil + massage + luxuries
- [ ] Failure to meet needs = loyalty loss, potential match throwing

---

## Phase 6: Crowd & Spectacle (Weeks 17-18)
**Goal**: Fights have an audience. Performance matters financially and politically.

### Crowd System
- [ ] Crowd appetite per arena (Awe / Blood / Honor / Fear weights in arena JSON)
- [ ] Spectacle scoring per combat action:
  - Big hit, close dodge, dramatic yield
  - Magic release (especially high-level weaves)
  - Clean kill vs messy kill
  - Underdog moments
- [ ] Crowd excitement meter during combat (affects purse multiplier)
- [ ] Post-match rewards: purse bonus, fame per gladiator, prestige for ludus

### Match Value & Ego
- [ ] Gladiators evaluate if a match is "worthy": (arena tier + opponent rank + prize) vs ego
- [ ] Insulted status: legend in a mud pit = morale drop, lazy fighting
- [ ] The Rust: not fighting causes physical degradation (fat gain, slow reflexes)

### Escalation (not anti-spam)
- [ ] Higher tier arenas expect more spectacle - Tier 1 crowd happy with basics
- [ ] Overuse of magic in high-tier arenas = censor pressure (political consequence, not crowd boredom)
- [ ] Excessive brutality = magistrate attention (political, not mechanical punishment)

---

## Phase 7: Career & Politics Skeleton (Weeks 19-20)
**Goal**: Progression and a reason to keep playing.

### Arena Tiers
- [ ] Tier 1: Home arena (mud pit, small, customizable terrain)
- [ ] Tier 2: Provincial (stone circle, larger, harder floor = more fractures)
- [ ] Tier 3: Grand Colosseum (sand, perfect traction, huge crowd, invitation only)
- [ ] Each tier defined in arenas.json with grid size, terrain, crowd appetites

### Career Progression
- [ ] Match scheduling: choose from available opponents (difficulty/reward vary)
- [ ] Prestige accumulation from wins, spectacle, crowd favorites
- [ ] Tier advancement: prestige threshold + magistrate fee
- [ ] Gladiator ranks: Rookie -> Veteran -> Champion -> Legend
- [ ] The Rudis: freedom event for high-prestige gladiators
  - Allow freedom: lose gladiator, gain massive prestige + recruitment bonus
  - Deny freedom: keep gladiator, massive loyalty drop + revolt risk

### Politics (skeleton - event cards)
- [ ] Rival Ludus: opponent source + occasional sabotage events
  - Poaching attempts on unhappy gladiators
  - Supply disruption
  - Lobbying for rule changes
- [ ] Magistrate: permits, inspections, tax audits
- [ ] Syndicate: match fixing offers, illegal stim supply, retaliation on refusal
- [ ] Events as choice cards: situation description -> 2-3 options -> consequence
- [ ] All events defined in JSON (data-driven, moddable)

---

## Phase 8: Polish & Integration (Weeks 21-22)
**Goal**: Playable, stable, demonstrable vertical slice.

### Balance
- [ ] Damage numbers tuning (weapons vs armor vs BodySim thresholds)
- [ ] Economy balance (income vs expenses, progression pacing)
- [ ] Magic balance (weave risk/reward curves, conductivity scaling)
- [ ] Crowd scoring calibration
- [ ] AI behavior tuning

### Save/Load (Ironman)
- [ ] Single save file per campaign (ironman by default)
- [ ] Auto-save on every phase transition and before/after combat
- [ ] Full game state serialization to JSON (human-readable = player can backup/copy for "easy mode")
- [ ] BodySim state serialization (all systems, all nodes)
- [ ] No manual save/load UI - the game just saves
- [ ] Save scumming is possible by copying the JSON file - this IS the easy mode, by design

### Quality of Life
- [ ] Tutorial: first fight guided, first management week guided
- [ ] Tooltips and contextual help (IBM guidelines - progressive disclosure)
- [ ] Combat log (text record of what happened for players who want detail)

### Audio & Polish
- [ ] Sound effects: combat hits, crowd reactions, UI feedback
- [ ] Music: combat track, ludus ambient track
- [ ] Screen transitions and UI polish

### Playtesting
- [ ] Core loop test: manage -> fight -> recover -> manage. Is it engaging?
- [ ] Does the BodySim create interesting variety? (same fight, different outcomes)
- [ ] Do management decisions feel meaningful?
- [ ] Is magic fun and scary to use?
- [ ] Is the AI a reasonable opponent?

---

## Remaining Open Questions

1. **Weapon skill system**: One skill per weapon type, or broader categories (light/heavy/polearm)?
2. **Line of sight**: Do pillars block vision/targeting, or just movement?
3. **Friendly fire**: Can spells hit your own team? (Would add tactical depth + risk)
4. **Match formats**: Just team deathmatch, or also capture objectives, last man standing, etc.?
5. **Between-fight time**: How many management "days" between scheduled fights?
6. **Gladiator generation**: How are random gladiators generated? (Stat ranges by tier? Templates?)
7. ~~**Permadeath save**~~: Ironman by default. JSON save = natural save scum easy mode.
