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
- [ ] Attack action: choose target body part (high / mid / low zones)
- [ ] Hit resolution: attacker weapon skill vs defender skill -> hit location
- [ ] Damage routed through BodySim:
  - Integumentary absorbs first (skin + armor from equipment)
  - Skeletal: fractures, structural failure
  - Muscular: tears, strength loss
  - Circulatory: bleeding, blood pressure drop
  - Nervous: pain generation, shock accumulation
- [ ] Limb disabling: fractured leg = can't move, damaged arm = can't attack with it
- [ ] Equipment matters: weapon type determines damage profile, armor protects specific body parts

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
- [ ] Time-based healing: bones knit, wounds close, blood replenishes over days/weeks
- [ ] Infection risk from open wounds (ImmuneSystem fights it over time)
- [ ] Atrophy of disabled/unused limbs
- [ ] Scarring: permanent skin integrity reduction (charisma modifier for crowd)
- [ ] Nerve damage persistence: reduced conductivity (mage power loss)
- [ ] Overtraining injuries: pushing training too hard -> muscle tears, stress fractures

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
**Goal**: Functional management between fights. Skeleton of every system, deep on none.

### Economy
- [ ] Gold as primary currency
- [ ] Income: match purses (base + crowd bonus + sponsor bonus)
- [ ] Expenses: food, medicine, staff wages, facility upkeep, equipment
- [ ] Weekly budget cycle with summary

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
- [ ] Weapon types: sword, spear, axe, club, dagger, trident (different damage profiles)
- [ ] Armor: protection mapped to specific BodySim body parts
- [ ] Mage gear: radiator pauldrons (cooling), grounding spear (heat discharge), anchor talisman
- [ ] Buy/sell at market

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

### Anti-Spam
- [ ] Overuse of magic = censor pressure
- [ ] Excessive brutality = magistrate attention
- [ ] Crowd gets bored of repeated strategies

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
