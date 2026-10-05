# K4RMA — Refined Game Specification

Status: Team-aligned design reference
Purpose: Source of truth for gameplay/system refinement
Final production direction: 2D
Current prototype direction: 2.5D technical/gameplay prototype

---

# 1. Purpose of This Document

This document defines the current agreed K4RMA game design.

It is the source of truth for:

- story premise
- terminology
- player techniques
- technique personalization
- guardian inheritance
- guardian archetypes
- stage progression
- UI requirements
- combat rules
- presentation direction
- prototype scope

When old prototype behavior or documentation conflicts with this document, this document takes priority.

The current Unity prototype uses 3D assets in a 2.5D side-view environment.

The FINAL production game, however, is planned as:

> 2D side-scrolling boss-rush action game

The current 2.5D project should therefore be treated as a technical prototype used to validate gameplay systems before the final 2D production implementation.

Do NOT convert the current prototype to full 2D as part of the prototype-refinement phase.

---

# 2. Core Game Identity

K4RMA is a boss-rush action game about a disciple who has mastered the techniques taught by his master but must learn to transform those techniques into a fighting style of his own.

The defining gameplay idea is:

> The player changes one of the master's original techniques into a personal technique, while future guardians inherit the original technique that the player left behind.

Therefore one choice changes BOTH:

1. the player's own fighting style
2. the fighting style of future guardians

This relationship is the primary differentiation of K4RMA.

The player does NOT simply sacrifice abilities and become weaker.

Instead:

Original master's technique
→ player understands its principle
→ original active form disappears
→ player gains a new personal application of the principle
→ future guardian inherits the original form

---

# 3. Final Production Format

Final production target:

- 2D
- side-scrolling
- boss-rush
- action combat
- single-player
- Japanese-fantasy training-ground aesthetic
- Windows PC primary submission target

Current prototype:

- Unity 6000.6.2f1
- 2.5D
- 3D placeholder/presentation assets
- side-view combat
- movement constrained to combat plane
- used only to validate mechanics and progression

The current 3D environment and models are prototype presentation assets, not necessarily final-game production assets.

---

# 4. Story

The protagonist is a disciple who has trained under a master for many years.

The master taught the disciple three sword techniques:

1. 검기
2. 상승베기
3. 방어

After long training, the disciple believes he has mastered all three.

The master gives one final trial.

The purpose of the trial is not to prove that the disciple can reproduce the master's techniques.

The disciple must understand the principle behind each technique and transform it into a form suited to his own body, judgment and fighting style.

The master challenges him:

> Using my techniques well does not mean your training is complete.
> Understand why they work.
> Change them into techniques that belong to you.
> Stop merely reproducing my swordsmanship and complete your own.

The disciple enters an old training ground where four guardians wait.

After defeating each of the first three guardians, the disciple must choose one remaining original technique and transform it into his own technique.

When a technique is personalized:

- the original version can no longer be directly used by the player
- a new personalized combat effect becomes available
- the training ground records the original technique
- later guardians inherit that original technique

Previously inherited original techniques remain accumulated.

Therefore the player determines:

- how their own combat evolves
- how later guardians evolve

After the third guardian, all three techniques have been personalized.

The final guardian possesses all three original techniques.

The disciple fights it using only the three techniques he has transformed into his own style.

After defeating the final guardian, the disciple passes the trial.

The master asks:

> "Whose techniques are they now?"

The disciple answers:

> "I learned them from you. But now I can use them in my own way."

The game ends with the idea that the teaching remains, but its expression now belongs to the disciple.

---

# 5. Final Terminology

Use consistently:

## Original technique
원형 기술

The version originally taught by the master.

## Personalization
고유화

The process of transforming a master's original technique into the disciple's own technique.

## Personalized technique
고유 기술

The resulting technique used by the disciple.

## Inherited technique
계승 기술

The original technique inherited by later guardians.

Avoid using the old central terminology in player-facing content:

- sacrifice
- sacrificed
- unconscious
- unconsciousization
- 의식
- 무의식
- 무의식화
- 희생

Legacy implementation identifiers may temporarily remain internally if changing serialized data would introduce unnecessary risk.

Player-facing terminology must use the current design language.

---

# 6. Code and Language Policy

Internal code should remain in English.

Examples:

- TechniqueId
- TechniqueDefinition
- PersonalizationOrder
- GuardianArchetype
- GuardianInheritanceState
- SwordWave
- RisingSlash
- Guard
- PiercingSlash
- AirHover
- Counter

Do NOT mix Korean identifiers into class/method/field names unless unavoidable.

Bad:

- 고유화Manager
- Guardian계승Controller

Good:

- PersonalizationManager
- GuardianInheritanceController

Player-facing text may be Korean or English depending on current prototype needs.

The final production architecture should eventually support localization.

Potential final languages:

- Korean
- English
- Uzbek

However:

- full localization is NOT required during this prototype refinement
- do not block gameplay work for localization
- avoid introducing new hardcoded UI strings where a central text/config structure is easy to use
- actual translations can be completed after game text stabilizes

---

# 7. Player Base Actions

The player has:

- left/right movement
- jump
- basic sword attack
- original active technique inputs
- defensive input
- interaction/confirm
- pause
- restart

Combat fundamentals:

- melee sword combat
- side-view positioning
- readable enemy telegraphs
- player must react to distance, timing and guardian behavior

Personalized techniques should modify these fundamental combat actions rather than simply giving stat bonuses.

Avoid designs such as:

- +10% damage
- +15% movement speed
- +20% defense

The personalized technique must meaningfully change gameplay decisions.

---

# 8. Original Technique 1 — 검기

## Original form

Name:
검기

Internal identifier suggestion:
SwordWave

Function:

- ranged sword-energy attack
- activated manually
- energy slash travels forward
- gives the player ranged pressure
- clearly separate from normal melee attack

This is one of the master's original techniques.

---

# 9. Personalized Technique 1 — 관통베기

검기 becomes:

관통베기

Internal identifier suggestion:
PiercingSlash

Concept:

The disciple no longer sends sword energy away from himself.

Instead, he applies the same forward-cutting principle through his own movement.

Gameplay:

- integrated into melee attack flow
- player passes through/crosses the guardian while attacking
- changes player/guardian relative position
- rewards spacing and timing

Desired result:

Before:
player attacks from distance using 검기

After:
player closes/crosses space with the sword attack itself

Prototype requirements:

- cannot leave arena bounds
- cannot pass through walls
- cannot permanently overlap guardian collider
- cannot create infinite invulnerability
- must have clear attack trail/VFX
- must end at a safe valid position
- must not break camera tracking

Exact activation condition may be tuned.

Potential prototype trigger:

- combo finisher + direction
- contextual follow-up
- dedicated temporary input during prototype

Final behavior should still communicate that the skill has become part of the disciple's melee style.

---

# 10. Original Technique 2 — 상승베기

## Original form

Name:
상승베기

Internal identifier:
RisingSlash

Function:

- upward sword attack
- causes player to rise vertically
- manually activated
- useful against aerial/vertical threats

This is one of the master's original techniques.

---

# 11. Personalized Technique 2 — 체공

상승베기 becomes:

체공

Internal identifier:
AirHover

Concept:

The disciple no longer uses upward force simply to launch himself.

He learns to control that force while already airborne.

Gameplay:

- while jumping, an aerial sword attack can temporarily delay falling
- lets the player adjust landing timing
- useful for avoiding guardian attacks
- creates aerial timing decisions

Must NOT become infinite flight.

Prototype rules:

- limited hover duration
- limited activation per jump OR resource/timing limit
- reset after landing
- knockback cancels/overrides hover when necessary
- death/restart fully clears hover state
- gravity must always restore correctly

Visual feedback:

- short aerial pause
- air/slash effect
- readable suspension

---

# 12. Original Technique 3 — 방어

## Original form

Name:
방어

Internal identifier:
Guard

Function:

- manually activated protection
- blocks/absorbs attacks
- gives player safer defensive option

This is one of the master's original techniques.

---

# 13. Personalized Technique 3 — 반격

방어 becomes:

반격

Internal identifier:
Counter

Concept:

The disciple stops spreading defensive force around the entire body.

Instead, the disciple concentrates that principle at the precise moment an attack arrives.

Gameplay:

- timed defensive response
- successful timing redirects/receives guardian attack
- guardian is staggered/interrupted
- gives player counterattack opportunity

Important:

The mechanic should be satisfying and demonstrable, not brutally difficult.

Prototype should prefer:

- clear enemy telegraph
- forgiving but meaningful timing window
- clear success effect
- sound feedback
- guardian stagger
- optional brief hit-stop

Prevent:

- infinite guardian stun-lock
- permanent invulnerability
- unclear timing

---

# 14. Technique State Model

At run start:

검기 = Original
상승베기 = Original
방어 = Original

After personalization:

Original technique becomes unavailable to player.

Corresponding personalized technique becomes available.

Suggested state:

enum PlayerTechniqueState
{
    Original,
    Personalized
}

Guardian inheritance is tracked separately.

Do NOT treat GuardianInherited as the same player state.

Suggested structures:

TechniqueDefinition

Fields:
- TechniqueId id
- localized/display name
- original description
- personalized name
- personalized description
- guardian archetype type
- UI icon reference
- gameplay metadata

RunProgressionState

Fields:
- currentStage
- technique states
- personalization order
- inherited originals
- main guardian archetype
- run complete flag

The personalization order MUST preserve order.

A set alone is insufficient.

---

# 15. Valid Personalization Orders

There are six valid orders.

1.
검기
→ 상승베기
→ 방어

2.
검기
→ 방어
→ 상승베기

3.
상승베기
→ 검기
→ 방어

4.
상승베기
→ 방어
→ 검기

5.
방어
→ 검기
→ 상승베기

6.
방어
→ 상승베기
→ 검기

All six must be structurally supported.

The game does NOT require six entirely unique final guardian implementations.

The first selected technique determines the main archetype.

Later inherited techniques add patterns.

---

# 16. Stage Progression

The game contains four guardian battles.

## Stage 1

Guardian:

- neutral/basic guardian
- no inherited original techniques

Player:

- 검기
- 상승베기
- 방어

After victory:

- player chooses one of three original techniques
- technique becomes personalized

---

## Stage 2

Guardian inherits:

- original technique #1

Player has:

- one personalized technique
- two original techniques

The first inherited technique determines the guardian's main archetype.

After victory:

- choose one of remaining two original techniques

---

## Stage 3

Guardian inherits:

- original #1
- original #2

Main guardian archetype remains determined by #1.

Technique #2 adds:

- new pattern
- possible pattern combination
- minor visual/VFX distinction

Player has:

- two personalized
- one original

After victory:

- final original technique becomes personalized

---

## Stage 4

Final guardian inherits:

- all three original techniques

Main guardian archetype:

- still determined by first personalization choice

Second/third techniques:

- add combat patterns
- modify combinations
- add minor visual traits

Player has:

- 관통베기
- 체공
- 반격

Final guardian must be beatable using those three personalized techniques.

After victory:

- ending/result
- trial completed

---

# 17. Guardian Main Archetypes

The FIRST technique personalized by the player determines later guardians' main type.

---

## 17.1 검기형 Guardian

Internal:
SwordWaveArchetype

Core identity:

- ranged pressure
- more distance-oriented combat
- signature sword-wave pattern

Potential behavior:

- retreat/reposition
- ranged slash
- punish unsafe approach

If later 상승베기 is inherited:

Possible combination:

- jump/rise
- fire sword wave at different height
- vertical-to-ranged sequence

If later 방어 is inherited:

Possible combination:

- brief guard
- follow with sword wave

Do not make guard permanent.

---

## 17.2 상승베기형 Guardian

Internal:
RisingSlashArchetype

Core identity:

- vertical pressure
- rising attacks
- jump/air threat

Potential behavior:

- aggressive upward slash
- punish jumping
- force vertical timing

If later 검기 is inherited:

Possible combination:

- rise/jump
- emit sword wave during/after vertical attack

If later 방어 is inherited:

Possible combination:

- guard
- retaliate using rising slash

---

## 17.3 방어형 Guardian

Internal:
GuardArchetype

Core identity:

- defensive/counter-oriented combat
- punishes careless attacks

Potential behavior:

- readable guard stance
- retaliation opportunity
- deliberate pacing

If later 검기 is inherited:

Possible combination:

- guard
- retaliate with sword wave

If later 상승베기 is inherited:

Possible combination:

- guard
- retaliate with rising slash

Rules:

- guard must have limited duration
- guard must have clear telegraph
- player must have counterplay
- guardian cannot remain invulnerable indefinitely

---

# 18. Guardian Pattern System

Avoid building one giant BossController containing all behavior.

Preferred conceptual architecture:

GuardianController
GuardianBrain
GuardianPatternSelector

Pattern abstraction:

IGuardianPattern
or
GuardianPattern

Potential patterns:

- BasicMeleePattern
- SwordWavePattern
- RisingSlashPattern
- GuardPattern
- RisingSwordWavePattern
- GuardToSwordWavePattern
- GuardToRisingSlashPattern

Pattern selection may consider:

- distance
- cooldown
- current stage
- inherited techniques
- guardian archetype
- previous action
- recovery state
- player position

Rules:

- one primary attack behavior at a time
- no unavoidable overlaps
- strong attacks require telegraph
- attacks require recovery windows
- avoid repeated same-pattern spam
- inherited patterns must be modular

Do not over-engineer.

Refactor only enough to support the six progression orders cleanly.

---

# 19. Guardian Visual Progression

The final game will use 2D art.

The current prototype may continue using its current 3D guardian.

Do NOT spend excessive time producing final 3D guardian variants.

Minimum prototype differentiation:

## 검기형

Possible:
- blade aura
- sword-energy color/accent
- ranged slash VFX

## 상승베기형

Possible:
- vertical energy/wind accent
- upward slash trail
- distinct core effect

## 방어형

Possible:
- defensive sigil
- armor/shield accent
- guard VFX

Second/third inherited techniques:

- minor VFX
- minor material accents
- optional modular part

Final guardian:

- may be larger/more visually imposing
- may use combined VFX
- should still reuse guardian gameplay architecture

Visual polish is secondary to combat functionality.

---

# 20. Japanese Visual Direction

Final visual identity:

Japanese fantasy training ground.

Possible motifs:

- dojo
- shrine
- torii
- lanterns
- wood architecture
- stone path
- training seals
- banners
- Japanese swordsmanship visual language

Current 3D Japanese-style arena may remain for the technical prototype.

Do NOT reinterpret the game into:

- exorcist story
- yokai possession story
- cursed-mask story
- village festival story

The Japanese direction is primarily:

- visual
- asset
- effect
- environment
- character-design

The master/disciple story remains unchanged.

---

# 21. Skill Personalization Transition

The existing altar/transition system may be reused.

However it should no longer represent:

- sacrifice
- ability loss ritual
- unconscious conversion

Instead it represents:

> completing/personalizing one technique

Possible presentation:

검기
↓
관통베기

Original form:
검기

Your technique:
관통베기

Next guardian inherits:
검기

Keep transition short.

The player should understand three consequences:

1. original player active is removed
2. personalized player effect is gained
3. original is inherited by next guardian

---

# 22. Personalization Selection UI

After Stage 1 and Stage 2:

Show remaining original techniques.

Example:

[검기]

원형:
원거리 검격

고유화:
관통베기

효과:
적을 가로지르며 위치 변경

---

[상승베기]

원형:
상승 공격

고유화:
체공

효과:
공중 공격 중 잠시 체공

---

[방어]

원형:
보호 기술

고유화:
반격

효과:
공격을 정확한 타이밍에 받아쳐 반격

Also indicate:

> 다음 수호자가 원형 기술을 계승합니다.

Allow:

- selection
- cancel before confirmation
- confirmation

After confirmation:

- cannot undo during current run

After Stage 3:

Only one technique remains.

Still show confirmation/transition, but do not present a fake meaningful choice.

---

# 23. HUD

HUD must remain compact.

Player area:

- HP
- stage
- 3 technique slots

Possible slot states:

Original:
검기
상승베기
방어

Personalized:
관통베기
체공
반격

Guardian:

- HP
- guardian name/type
- inherited technique indicators

Example:

검기형 수호자

계승:
검기
상승베기

Do not fill the combat screen with explanatory paragraphs.

Use short names/icons during combat.

Use detailed explanation only in transition screens.

---

# 24. Story Presentation

Do NOT create long cutscenes for the prototype.

Possible title option:

게임 스토리

Full story may be available there.

Actual gameplay should use concise text.

Opening:

스승의 마지막 시험

"배운 기술을 그대로 따라 쓰는 것을 넘어,
그 원리를 이해하고 너만의 검술을 완성하라."

Rules:

"수호자를 쓰러뜨릴 때마다
기술 하나를 자신만의 방식으로 완성할 수 있습니다."

"내려놓은 원형 기술은
다음 수호자가 계승합니다."

Keep gameplay explanation short.

---

# 25. Combat Readability

Every major guardian attack should have:

- telegraph animation
- readable VFX
- reaction time
- active attack window
- recovery

Personalized player feedback:

## 관통베기
- distinctive trail
- clear forward/cross-through motion

## 체공
- obvious aerial hold
- subtle air effect

## 반격
- spark/flash
- sound cue
- guardian stagger
- optional light hit-stop

Do not rely on debug text for readability.

---

# 26. Difficulty Philosophy

Goal:

- learnable
- demonstrable
- fun
- increasingly complex

NOT:

- extreme Souls-like difficulty
- giant HP inflation

Stage difficulty should grow through:

- additional inherited patterns
- pattern combinations
- slightly increased decision speed

Parry/counter:

- meaningful timing
- but not so tight that presentation/playtesting regularly fails

---

# 27. Restart and State Reset

Restart must fully reset:

- stage
- player HP
- guardian HP
- technique states
- personalization order
- guardian inheritance
- guardian archetype
- projectiles
- temporary VFX
- cooldowns
- selection state
- PiercingSlash transient state
- AirHover transient state
- Counter/parry transient state

No previous run state may leak into a new run.

---

# 28. Prototype Camera and Arena

Keep the current stable ArenaBounds/camera architecture.

Requirements:

- player remains inside arena
- guardian remains inside arena
- side-view composition remains readable
- final guardian still fits camera
- no camera regression
- no old limited camera-range bug

Do not rebuild camera architecture unless necessary.

---

# 29. Sound Priority

Core useful sounds:

- sword swing
- hit
- player damage
- guardian damage
- original technique
- successful personalization
- successful counter
- victory
- defeat

BGM:

optional prototype polish.

Sound must not block gameplay completion.

---

# 30. Localization Direction

Localization is future-facing.

Final potential languages:

- Korean
- English
- Uzbek

During prototype refinement:

- code stays English
- do not translate entire prototype
- avoid unnecessary new hardcoded player-facing strings
- localization infrastructure may be introduced later in final 2D production

Do NOT allow localization work to delay gameplay systems.

---

# 31. Optional Future Systems

These are NOT part of mandatory prototype or current game submission scope:

- multiplayer
- networking
- web version
- browser game
- homepage
- Google login
- Kakao login
- user accounts
- cloud backend
- database
- rankings

They may be explored later if the core game is complete.

---

# 32. Explicitly Out of Scope

Do NOT add during this refinement:

- final 2D conversion
- generic enemy mobs
- procedural generation
- inventory system
- currencies
- equipment
- skill tree
- dialogue system
- long cutscenes
- additional core skills
- online features
- six bespoke final bosses
- completely separate guardian implementation per order

---

# 33. Prototype Success Criteria

The refined prototype is successful when:

1. Project compiles cleanly.

2. Stage 1 → Stage 4 can be completed in one run.

3. Player begins with:
   - 검기
   - 상승베기
   - 방어

4. 검기 can become 관통베기.

5. 상승베기 can become 체공.

6. 방어 can become 반격.

7. Original skill becomes unavailable after personalization.

8. Personalized behavior becomes active.

9. Guardian inherits original skills cumulatively.

10. First personalization determines:
    - 검기형
    - 상승베기형
    - 방어형

11. Later inherited techniques do not replace the main archetype.

12. Stage 4 guardian owns all three original techniques.

13. Player enters Stage 4 with all three personalized techniques.

14. All six personalization orders are supported by progression logic.

15. Restart clears all state.

16. Player-facing obsolete sacrifice/unconscious terminology is removed.

17. Japanese-fantasy prototype presentation remains stable.

18. No nested Unity project is created inside Game/Assets.

19. Existing tests continue to pass.

20. New progression tests pass.

21. Windows build remains possible.

---

# 34. Final Principle

Do not optimize the prototype for visual perfection.

Its purpose is to prove:

- 고유화
- player combat transformation
- guardian inheritance
- first-choice guardian archetypes
- pattern accumulation
- four-stage progression
- final guardian composition

The final production game will later rebuild/adapt those proven systems into a 2D presentation.
