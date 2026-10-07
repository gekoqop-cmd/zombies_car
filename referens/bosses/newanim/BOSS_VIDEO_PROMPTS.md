# Промты для боссов — 2D billboard

Все промты используют соответствующий idle PNG из этой папки как единственный reference image.

## Общие ограничения для каждого ролика

```text
Use the supplied boss PNG as the only character reference. Preserve the exact boss identity, face, body proportions, colors, clothing or armor, weapon, facing direction and thick cartoon outline style. One boss only. Flat solid chroma-key green background #00FF00, no floor, no scenery, no text, no watermark. Fixed orthographic camera, fixed scale, full body always inside frame, no cuts, no zoom, no camera shake. Keep the boss centered for a 2D billboard sprite. Start and end in the requested pose unless the prompt explicitly describes a non-looping transition. Do not add extra limbs, weapons or characters. Effects are allowed only when the specific ability asks for them.
```

## Соответствие проекту

| Босс | Волна | Способности |
|---|---:|---|
| Матка | 10 | Призыв выводка, плевок кислотой |
| Таран | 20 | Замах перед рывком, рывок, оглушён после рывка, кислотный дождь, удар об землю |
| Жнец | 30 | Взмах косой, огненные трещины от удара косой в землю, телепорт |

---

# Матка — `boss1_mother_idle.png`

## Idle 1 — тяжёлое дыхание, loop

```text
Animate a seamless idle loop for the Mother boss. Keep both feet planted and the enormous body centered. Make the belly and shoulders rise and fall with slow heavy breathing, let a few lime-green pustules pulse softly, and add tiny natural claw and jaw movement. No walking, no attack, no ability, no extra effects. Return exactly to the first frame.
```

## Idle 2 — угрожающая стойка, loop

```text
Animate a seamless threatening idle loop for the Mother boss. Keep the same planted stance and silhouette. Very slowly shift the weight from one leg to the other, flex the clawed fingers once, slightly open the mouth and let toxic pustules throb. The boss must stay in place and return to the exact starting pose. No attack and no summoned creatures.
```

## Idle 3 — вызов выводка без активации способности

```text
Animate a subtle boss idle loop for the Mother: she breathes heavily, looks downward and makes a small warning growl, with a slight movement of both arms while keeping them near the body. Do not raise the arms fully and do not spawn anything. Keep the body centered and loop cleanly.
```

## Движение / run

```text
Animate the Mother boss lumbering toward screen-right in a seamless loop. Use slow heavy steps, strong body sway, bouncing green pustules and swinging clawed arms. Preserve the huge weight and short powerful stride. Keep the character fully visible and do not add minions or attack effects.
```

## Базовая атака

```text
Animate one simple Mother boss melee attack only. Keep both feet planted. She makes one short heavy claw swipe with the right hand, then immediately returns to the exact original idle pose. One hit, one arm, one motion. No step, no lunge, no second swing, no summoned creatures.
```

## Способность — призыв выводка, руки вверх

```text
Animate the Mother's summon ability. She raises both clawed arms upward in a clear calling gesture, opens her mouth in a loud roar, holds the pose briefly, and a small toxic-green summon ring appears beneath her feet with a few simple green spawn flashes. Then she lowers her arms and returns to the exact idle pose. Keep the boss centered. No visible minion characters, no extra bosses, no camera movement.
```

## Способность — призыв выводка, руки к земле

```text
Animate the Mother's summon ability, ground-calling variant. She bends forward and presses both clawed hands toward the ground, the toxic-green summon ring grows outward under her, and a few green spawn flashes rise from the ring. She then straightens and returns to the exact idle pose. One ability action only. No visible minion characters, no scenery, fixed camera.
```

## Способность — плевок кислотой

```text
Animate the Mother's acid spit ability. Keep her feet planted and body centered. She leans her head forward, opens her mouth and spits two short arcs of bright toxic-green acid toward screen-right, with a brief mouth recoil after each spit. Then she closes her mouth and returns to the exact idle pose. Only the acid projectiles are allowed as effects; no scenery, no extra characters, no camera movement.
```

## Смерть — дополнительно

```text
Animate the Mother boss death. She reacts with a heavy full-body shudder, loses balance, drops to her knees and collapses forward onto her side. The lime pustules stop pulsing and the body becomes still. Keep the whole body visible and intact. No dismemberment and no background change.
```

---

# Таран — `boss2_ram_idle.png`

## Idle 1 — дыхание, loop

```text
Animate a seamless idle loop for the Ram boss. Keep both heavy boots planted and the huge arm cannon lowered in its original position. Add slow armored breathing, a slight shoulder rise, small head movement and a subtle green glow inside the cannon. Preserve the exact armor and silhouette. No firing, no walking, no ability.
```

## Idle 2 — тяжёлая готовность, loop

```text
Animate a seamless combat-ready idle loop for the Ram boss. Keep him centered and stationary. Make the cannon shift by a few centimeters with his breathing, let the chains and purple cloth move subtly, and flex the free fist once. Return exactly to the first frame. No charge, no projectile, no ground effect.
```

## Idle 3 — короткая угроза, loop

```text
Animate a subtle Ram boss idle loop: he lowers his armored head slightly, breathes heavily, gives one small shoulder jerk and lets the cannon glow pulse once. Keep both feet planted and the weapon attached. No attack and no ability activation. Clean seamless loop.
```

## Движение / run

```text
Animate the Ram boss running heavily toward screen-right in a seamless loop. Use short powerful armored strides, strong shoulder and armor bounce, cannon swinging with the body, and heavy boot impacts. Preserve the hulking weight. Keep the full character in frame and do not turn him into a different creature.
```

## Базовая атака

```text
Animate one simple Ram boss melee attack only. Keep both boots planted. He makes one short heavy strike with the free right fist, then returns directly to the exact original idle pose. One hit, one arm, one motion. The arm cannon remains attached and inactive. No step, no charge, no second punch.
```

## Способность — замах перед рывком

```text
Animate the Ram boss charge wind-up only. Keep him centered in place. He digs both boots into the ground, leans the armored torso forward, lowers the head, pulls the cannon and shoulders back, and holds a tense ready-to-charge pose. Add a small amount of dust under the boots, but do not move forward. End in the ready pose for the following charge animation. No dash yet, no camera movement.
```

## Способность — рывок, loop

```text
Animate a seamless loop of the Ram boss charging in place. Keep the boss centered for a 2D billboard: legs run rapidly, shoulders and armor pump, the head stays aimed forward, and the cannon bounces with each stride. Do not translate the character across the frame. No attack impact, no jump, no camera movement. The first and last frames must match.
```

## Состояние — оглушён после рывка

```text
Animate the Ram boss stunned after a charge. Keep both feet in roughly the same place. The heavy body sways left and right, knees wobble, the head droops, the cannon hangs low and the green visor flickers weakly. The boss looks dizzy and vulnerable. End in a stable dazed pose. No walking, no attack, no projectile.
```

## Способность — плевок кислотным дождём

```text
Animate the Ram boss acid-rain ability. Keep him centered and stationary. He raises and aims the arm cannon upward, the cannon glows green, and it fires a rapid volley of bright toxic-green acid globules that arc upward and fall toward screen-right like acid rain. After the volley, the cannon lowers and the boss returns to the exact idle pose. Keep only the acid projectiles as effects; no scenery and no camera movement.
```

## Способность — удар об землю

```text
Animate the Ram boss ground-slam ability. From the idle stance he crouches, jumps straight up a short distance, raises the armored body, then lands heavily with both boots and one powerful downward body impact. On landing, show one clean circular shockwave ring expanding along the bottom of the frame. Then return to the exact idle pose. No forward movement, no extra attacks, no camera shake.
```

## Смерть — дополнительно

```text
Animate the Ram boss death. His armor jolts from impact, the cannon drops, the knees buckle, and the huge armored body falls heavily onto its side. The green visor fades and the character becomes still. Keep armor, cannon and limbs intact. No dismemberment and no background change.
```

---

# Жнец — `boss3_reaper_idle.png`

## Idle 1 — дыхание и огонь, loop

```text
Animate a seamless idle loop for the Reaper boss. Keep both feet planted and the huge curved scythe held low in the same position. Add slow breathing, subtle movement of the ragged cloak, a gentle pulse of the orange lava cracks and a small ember glow in the face. No attack, no teleport, no ground effects. Return exactly to the first frame.
```

## Idle 2 — угрожающая стойка, loop

```text
Animate a seamless threatening idle loop for the Reaper. Keep the body centered and the scythe low. The head tilts slightly, the free fist tightens once, cloak edges sway, and the lava cracks brighten and dim slowly. Keep both feet planted. No scythe swing and no fire explosion.
```

## Idle 3 — перед боем, loop

```text
Animate a subtle Reaper combat idle loop. He makes a tiny weight shift, drags the scythe tip a few centimeters without attacking, exhales smoke once, and returns to the exact starting pose. Keep the scythe fully visible, character centered, no teleport and no cracks on the ground.
```

## Движение / run

```text
Animate the Reaper boss running toward screen-right in a seamless loop. Use long heavy strides, the ragged cloak trailing and the curved scythe held safely low, with orange lava cracks pulsing during the movement. Preserve the exact horns, body and weapon. Keep the full body visible and do not add fire trails.
```

## Базовая атака

```text
Animate one simple Reaper boss melee attack only. Keep both feet planted. He makes one short diagonal scythe cut in front of the body, then returns directly to the exact original idle pose. One swing, one motion. No step, no teleport, no second swing, no large ground effect.
```

## Способность — широкий взмах косой

```text
Animate the Reaper's wide scythe sweep ability. Keep the boss centered and both feet planted. He raises the huge curved scythe, holds a brief wind-up, then makes one wide horizontal sweep through the space in front of him. Show one clean translucent orange danger arc during the swing, then return to the exact idle pose. One sweep only, no teleport, no cracks, no camera movement.
```

## Способность — огненные трещины

```text
Animate the Reaper's fire-cracks ability. He raises the scythe and strikes its tip firmly into the ground in front of him. From the impact point, several bright orange-red cracks spread outward in a fan toward screen-right, glow briefly and burn with small flames. The Reaper holds the scythe in the ground, then pulls it out and returns to the exact idle pose. Keep the cracks as isolated effects over the green background; no scenery and no extra characters.
```

## Способность — телепорт

```text
Animate the Reaper's teleport ability. He becomes darker and fades into a small swirl of black ash and orange embers, leaving the green background empty for a brief moment. Then he reappears fully formed in a new position slightly in front of the player, already facing the same direction, with one short ember flash. Keep the boss intact, centered after reappearing, and do not add a second Reaper. No camera movement.
```

## Смерть — дополнительно

```text
Animate the Reaper boss death. He drops the scythe forward, staggers, falls heavily to one knee and collapses onto his side. The orange lava cracks dim, the cloak settles and the body becomes still. Keep horns, scythe and limbs intact. No dismemberment and no background change.
```

