"""Source videos of the green-screen takes: take key -> file (referens/newanim/...). Shared by process.py and sheets.py."""
import os

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
Z = os.path.join(ROOT, "referens", "newanim", "zombi")
B = os.path.join(ROOT, "referens", "newanim", "bosses", "animbos")
P = os.path.join(ROOT, "referens", "newanim", "player_axe")

TAKES = {
    "walker_run": (Z, "walker_run.mp4"), "walker_attack": (Z, "walker_attack.mp4"),
    "worker_run": (Z, "worker_run.mp4"), "worker_attack": (Z, "worker_attack.mp4"),
    "redcap_run": (Z, "redcap_run.mp4"), "redcap_attack": (Z, "redcap_attack.mp4"),
    "hoodie_run": (Z, "hodi_run.mp4"), "hoodie_attack": (Z, "hodi_attack.mp4"),
    "brute_run": (Z, "mutant_run.mp4"), "brute_attack": (Z, "mutant_attack.mp4"),
    "hound_run": (Z, "dog_run.mp4"), "hound_attack": (Z, "dog_attack.mp4"),
    "boss1_idle": (B, "matka_idle.mp4"), "boss1_run": (B, "matka_run.mp4"), "boss1_attack": (B, "matka_attack.mp4"),
    "boss1_dead": (B, "matka_dead(звук_убрать).mp4"), "boss1_summon": (B, "matka_вызов_зомби.mp4"),
    "boss2_idle": (B, "taran_idle.mp4"), "boss2_run": (B, "taran_run.mp4"), "boss2_attack": (B, "taran_attack.mp4"),
    "boss2_dead": (B, "taran_dead.mp4"), "boss2_stun": (B, "taran_stun(это-после-рывка).mp4"),
    "boss2_windup": (B, "taran_замах перед рывком.mp4"), "boss2_rain": (B, "taran_кислотный-дождь.mp4"),
    "boss2_charge": (B, "taran_рывок.mp4"),
    "boss3_idle": (B, "zhnec_ilde.mp4"), "boss3_run": (B, "zhnec_run.mp4"), "boss3_attack": (B, "zcnec_attack.mp4"),
    "boss3_dead": (B, "znec_dead.mp4"), "boss3_cracks": (B, "znec_способность-огненые-трещины.mp4"),
    "boss3_teleport": (B, "zcnec_способноть-телепорт(исзчезновение+появление).mp4"),
    "boss3_sink": (B, "znec_способность_телепорт(анимация-в-землю).mp4"),
    "axe_idle": (P, "axe_idle.mp4"), "axe_attack": (P, "axe_attack.mp4"), "axe_attack2": (P, "axe_attack2.mp4"),
    "axe_run": (P, "axe_run.mp4"), "axe_stun": (P, "stun_axe.mp4"),
}


def path(key):
    folder, name = TAKES[key]
    return os.path.join(folder, name)
