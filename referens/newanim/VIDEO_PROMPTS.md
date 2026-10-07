# Промты для видео-анимаций 2D-билбордов

Для каждого ролика загружай соответствующий PNG из этой папки как единственный character reference.

Общие ограничения, которые нужно добавлять к каждому промту:

```text
Use the supplied PNG as the only character reference. Preserve the exact character design, face, clothing, colors, proportions, outline style and facing direction. One character only. Flat solid chroma-key green background #00FF00, no floor, no shadow, no scenery, no text, no watermark. Fixed orthographic camera, fixed scale, full body always inside frame, no camera movement, no zoom, no cuts. Clean 2D cartoon game-sprite look suitable for extracting animation frames. Keep the green background perfectly uniform. Do not redesign the character or add/remove limbs, clothing or props.
```

## 1. Basic Walker Zombie — `basic_walker_idle.png`

### Движение / ходьба

```text
Animate the Basic Walker Zombie shuffling slowly toward screen-right in a loop. Hunched torso, uneven dragging steps, loose swinging arms, slight head bob, jaw and tongue moving subtly, natural weight transfer. End exactly where the loop can restart. Keep the same side-facing three-quarter view and the same green background.
```

### Атака — один укус

```text
Animate one simple Basic Walker Zombie bite attack only. Keep both feet planted and the body in place. The zombie opens its mouth, makes one short forward bite in place, then closes its mouth and returns directly to the exact original idle pose. One bite, one motion. No step, no lunge, no grab, no hand swing, no second bite, no camera movement.
```

### Смерть

```text
Animate the Basic Walker Zombie death: a brief hit reaction, arms flail, knees buckle, torso pitches forward, and the zombie collapses face-first onto the ground, ending motionless. Keep the whole body visible and preserve the character design. No dismemberment and no background change.
```

## 2. Construction Worker Zombie — `construction_worker_idle.png`

### Движение / ходьба

```text
Animate the Construction Worker Zombie walking heavily toward screen-right in a loop. Boots lift with weight, shoulders sway, orange safety vest and yellow hard hat bob naturally, arms hang forward with a slow zombie swing. Keep the same silhouette and readable full-body framing.
```

### Атака — один удар рукой

```text
Animate one simple Construction Worker Zombie arm attack only. Keep both boots planted. Raise the right arm and make one short downward claw strike with that single hand, then return directly to the exact original idle pose. One hit, one arm, one motion. No step, no lunge, no grab, no second swing, no tool or weapon, no camera movement.
```

### Смерть

```text
Animate the Construction Worker Zombie death: it staggers sideways, the hard hat wobbles, knees collapse, the body falls heavily onto its side, and it becomes motionless. Keep the helmet, vest and all clothing intact. No dismemberment, no extra effects, uniform green background.
```

## 3. Red-Cap Zombie — `redcap_zombie_idle.png`

### Движение / ходьба

```text
Animate the Red-Cap Zombie shambling toward screen-right in a loop. Uneven steps, red baseball cap bouncing slightly, arms loose and reaching forward, head and jaw making small idle movements. Maintain the same three-quarter side view and full-body framing.
```

### Атака — один укус

```text
Animate one simple Red-Cap Zombie bite attack only. Keep both feet planted and do not move the body forward. The zombie opens its mouth, makes one quick forward bite in place, then closes its mouth and returns directly to the exact original idle pose. One bite, one motion. No lunge, no grab, no hand swing, no second bite, no weapon, no camera movement.
```

### Смерть

```text
Animate the Red-Cap Zombie death: it receives a hit, stumbles backward, loses balance, drops to its knees and rolls onto its back, ending still. The red cap remains on the head. No gore burst, no dismemberment, no change to the green background.
```

## 4. Hoodie Zombie — `hoodie_zombie_idle.png`

### Движение / ходьба

```text
Animate the Hoodie Zombie moving toward screen-right in a loop. Hunched stealthy shuffle, hood sways with each step, red-gloved hands hang forward and flex, shoulders roll subtly. Preserve the hood opening, red gloves and dark clothing exactly.
```

### Атака — один удар рукой

```text
Animate one simple Hoodie Zombie claw attack only. Keep both feet planted and the body in place. The zombie makes one short claw swipe with its right red-gloved hand, then immediately returns to the exact original idle pose. One hit, one arm, one motion. No crouch, no step, no lunge, no bite, no second swing, no weapon, no camera movement.
```

### Смерть

```text
Animate the Hoodie Zombie death: it jerks from impact, clutches its chest with both red-gloved hands, sways, drops to one knee, then falls forward onto the ground and becomes still. Keep the hood and gloves intact. No dismemberment and no background change.
```

## 5. Brute Mutant — `brute_mutant_idle.png`

### Движение / ходьба

```text
Animate the Brute Mutant taking very heavy steps toward screen-right in a loop. Massive torso sways with inertia, enormous fists swing slightly, knees bend under the weight, glowing red pustules pulse subtly, and the head tracks forward. Keep the full body in frame and preserve the hulking proportions.
```

### Атака — один удар кулаком

```text
Animate one simple Brute Mutant fist attack only. Keep both feet planted. The mutant lifts its right enormous fist and makes one short heavy punch in place, then returns directly to the exact original idle pose. One hit, one fist, one motion. No stomp, no step, no lunge, no second punch, no bite, no camera shake, no extra objects.
```

### Смерть

```text
Animate the Brute Mutant death: it recoils from a strong hit, staggers with its huge upper body, drops to both knees, leans forward, and collapses onto its side, ending motionless. Preserve all pustules, fists, pants and silhouette. No dismemberment, no background change.
```

## 6. Mutant Hound — `mutant_hound_idle.png`

### Движение / бег

```text
Animate the Mutant Hound running on all four legs toward screen-right in a seamless loop. Use believable quadruped canine locomotion: alternating front and rear leg pairs, low shoulders, flexible spine, paws contacting the ground, tail following the body, mouth and glowing pustules moving subtly. Never stand upright or become bipedal. Keep all four paws visible whenever possible.
```

### Атака — один укус

```text
Animate one simple Mutant Hound bite attack only. Keep all four paws planted and keep the body in place. The quadruped quickly opens and closes its jaws once in a short forward bite, then returns to the exact original idle pose. One bite, one motion. No running, no leap, no lunge, no second bite, no standing upright, no extra legs.
```

### Смерть

```text
Animate the Mutant Hound death while staying strictly quadrupedal: it yelps and recoils, front legs weaken, hind legs buckle, the body drops onto its side, the tail settles, and it becomes motionless. Keep all four legs anatomically attached. No bipedal pose, no extra limbs, no dismemberment, no background change.
```

## 7. Player Survivor — `player_idle.png`

### Движение / бег

```text
Animate the Player Survivor jogging toward screen-right in a seamless loop while holding the same assault rifle with both hands. Keep the rifle pointed forward, backpack and scarf bouncing subtly, hair moving with the run, believable leg and arm coordination. Preserve the exact face, red hoodie, scarf, backpack with medical cross, cargo pants, knee pad and boots.
```

### Атака

```text
Animate one simple Player Survivor rifle attack only. Keep both feet planted and the body in place. The player makes one single shot from the rifle with one small muzzle flash and one brief recoil, then immediately returns to the exact original ready pose. One shot, one recoil, one motion. No burst, no walking, no turning, no camera movement, no background change.
```

### Idle

```text
Animate a subtle seamless idle loop for the Player Survivor: gentle breathing, tiny weight shift between the boots, slight scarf and hair movement, minimal rifle movement while both hands keep a secure ready grip. No walking, no firing, no change of clothing or weapon. End on the same pose for a clean loop.
```

### Смерть

```text
Animate the Player Survivor death: he reacts to an impact, loses his rifle grip, the rifle drops downward, he stumbles, falls to his knees and then onto his side, ending still. Keep the body intact and preserve the exact hero design, backpack and clothing. No blood spray, no dismemberment, no camera movement, uniform green background.
```

## 8. Player Survivor with Axe — `player_axe_hold.png`

### Проверка удержания топора / idle

```text
Animate a subtle seamless idle loop for the Player Survivor holding the single firefighter axe with both gloved hands. Keep the upper hand and lower hand visibly wrapped around the wooden handle, maintain believable elbow and shoulder alignment, axe head fully visible and stable, gentle breathing and tiny weight shift only. Preserve the exact hero face, hair, red hoodie, scarf, backpack with medical cross, cargo pants, knee pad and boots. Do not replace the axe with a rifle and do not add a second weapon.
```

### Атака топором — запасной промт

```text
Animate one simple Player Survivor axe attack only. Keep both feet planted. From the existing two-handed hold, make one short controlled downward axe chop in place, then return directly to the exact original two-handed hold. One chop, one motion, no step, no lunge, no second swing. Keep both hands attached to the handle, keep the axe head fully visible, no rifle and no extra weapon.
```

