"""Sound effect recipes for Joly World. Each function returns a rendered Track."""

from synth import Track, saw, square, triangle


def pulse(duty):
    return lambda p: square(p, duty)


def coin():
    """A coin: the classic two-note bling, a fourth up."""
    t = Track(0.35)
    t.add_tone(0, 0.05, 987.77, pulse(0.25), 0.25, release=0.02)
    t.add_tone(0.05, 0.2, 1318.5, pulse(0.25), 0.25, release=0.1)
    return t


def gem():
    """A gem: a sparkling run up a major arpeggio with a shimmer on top."""
    t = Track(0.9)
    for i, freq in enumerate((783.99, 987.77, 1174.66, 1567.98, 1975.53)):
        t.add_tone(i * 0.05, 0.25, freq, triangle, 0.25, release=0.25, vibrato=0.01)
    t.add_tone(0.25, 0.35, 3135.96, pulse(0.5), 0.05, release=0.3, vibrato=0.02)
    return t


def item():
    """Picking up a potion or a key: a round bubbly blip."""
    t = Track(0.4)
    t.add_tone(0, 0.08, 440, triangle, 0.35, freq_end=880, release=0.04)
    t.add_tone(0.09, 0.12, 659.25, triangle, 0.3, freq_end=1046.5, release=0.08)
    return t


def jump():
    """A jump: a springy rising boing."""
    t = Track(0.25)
    t.add_tone(0, 0.12, 220, pulse(0.5), 0.18, freq_end=520, release=0.05)
    t.add_noise(0, 0.06, 0.08, smoothing=0.7, decay_power=2.0, seed=1)
    return t


def land():
    """Landing on grass: a soft thump."""
    t = Track(0.2)
    t.add_tone(0, 0.06, 120, triangle, 0.5, freq_end=60, attack=0.001, release=0.05)
    t.add_noise(0, 0.07, 0.15, smoothing=0.8, decay_power=2.0, seed=2)
    return t


def dash():
    """A dash: a quick whoosh of air."""
    t = Track(0.35)
    t.add_noise(0, 0.28, 0.45, smoothing=0.55, decay_power=1.2, seed=3)
    t.add_tone(0, 0.2, 300, saw, 0.06, freq_end=900, release=0.05)
    return t


def bolt():
    """Casting the magic spark: a bright twinkling zap."""
    t = Track(0.45)
    t.add_tone(0, 0.25, 1400, triangle, 0.25, freq_end=2400, release=0.12, vibrato=0.05)
    t.add_tone(0.02, 0.2, 700, pulse(0.25), 0.1, freq_end=1200, release=0.1)
    t.add_noise(0, 0.05, 0.1, smoothing=0.1, decay_power=2.0, seed=4)
    return t


def fizzle():
    """Not enough mana: a deflating little fizz."""
    t = Track(0.35)
    t.add_tone(0, 0.2, 500, pulse(0.5), 0.12, freq_end=180, release=0.06)
    t.add_noise(0, 0.15, 0.12, smoothing=0.2, decay_power=1.5, seed=5)
    return t


def pop():
    """A critter popped by the spark: a puff and a cheerful pop."""
    t = Track(0.4)
    t.add_tone(0, 0.04, 600, triangle, 0.4, freq_end=1600, release=0.02)
    t.add_noise(0.02, 0.18, 0.3, smoothing=0.5, decay_power=2.0, seed=6)
    t.add_tone(0.06, 0.12, 1046.5, pulse(0.25), 0.1, release=0.08)
    return t


def stomp():
    """Bouncing on a critter: a squishy boing."""
    t = Track(0.35)
    t.add_tone(0, 0.05, 160, triangle, 0.5, freq_end=90, attack=0.001, release=0.03)
    t.add_tone(0.04, 0.16, 330, pulse(0.5), 0.15, freq_end=880, release=0.06)
    return t


def hurt():
    """Getting hurt: an 'ouch' falling tone."""
    t = Track(0.35)
    t.add_tone(0, 0.2, 520, square, 0.2, freq_end=200, release=0.08)
    t.add_noise(0, 0.08, 0.15, smoothing=0.6, decay_power=2.0, seed=7)
    return t


def splash():
    """Falling into the river: a splash and bubbles."""
    t = Track(0.9)
    t.add_noise(0, 0.5, 0.6, smoothing=0.65, decay_power=1.5, seed=8)
    for i, freq in enumerate((400, 520, 460, 620)):
        t.add_tone(0.2 + i * 0.09, 0.05, freq, triangle, 0.12, freq_end=freq * 1.6, release=0.03)
    return t


def drink():
    """Drinking a potion: three glugs and a sparkle."""
    t = Track(0.7)
    for i in range(3):
        t.add_tone(i * 0.12, 0.07, 220 + i * 40, triangle, 0.35, freq_end=160 + i * 40, release=0.03)
    t.add_tone(0.4, 0.2, 1567.98, triangle, 0.15, freq_end=2093.0, release=0.1)
    return t


def gate():
    """A gate unlocked and swinging open: a click and a creak."""
    t = Track(0.9)
    t.add_noise(0, 0.03, 0.4, smoothing=0.1, decay_power=3.0, seed=9)
    t.add_tone(0, 0.03, 1800, pulse(0.25), 0.15, release=0.02)
    t.add_tone(0.12, 0.6, 180, saw, 0.1, freq_end=140, release=0.1, vibrato=0.06)
    return t


def locked():
    """A locked gate: a dull double knock."""
    t = Track(0.3)
    for at in (0.0, 0.12):
        t.add_tone(at, 0.05, 160, triangle, 0.5, freq_end=100, attack=0.001, release=0.04)
        t.add_noise(at, 0.04, 0.15, smoothing=0.7, decay_power=2.0, seed=10)
    return t


def checkpoint():
    """A checkpoint flag going up: a rising three-note chime."""
    t = Track(0.7)
    for i, freq in enumerate((659.25, 783.99, 1046.5)):
        t.add_tone(i * 0.09, 0.2, freq, pulse(0.25), 0.18, release=0.15)
    return t


def goal():
    """Reaching the goal: a happy fanfare."""
    t = Track(1.9)
    melody = ((523.25, 0.1), (659.25, 0.1), (783.99, 0.1), (1046.5, 0.25), (783.99, 0.1), (1046.5, 0.6))
    at = 0.0
    for freq, length in melody:
        t.add_tone(at, length, freq, pulse(0.25), 0.2, release=0.08)
        at += length + 0.03
    for freq in (261.63, 329.63, 392.0):
        t.add_tone(0.55, 0.9, freq, triangle, 0.15, release=0.3)
    return t


def knockout():
    """Knocked out: a wobbly falling slide."""
    t = Track(1.2)
    t.add_tone(0, 0.8, 700, triangle, 0.3, freq_end=120, release=0.2, vibrato=0.04)
    t.add_tone(0, 0.8, 350, pulse(0.5), 0.08, freq_end=60, release=0.2)
    return t


def move():
    """Menu cursor: a soft tick."""
    t = Track(0.08)
    t.add_tone(0, 0.025, 1100, pulse(0.25), 0.18, release=0.02)
    return t


def select():
    """Menu choice: two quick notes."""
    t = Track(0.2)
    t.add_tone(0, 0.04, 880, pulse(0.25), 0.2, release=0.02)
    t.add_tone(0.05, 0.06, 1318.5, pulse(0.25), 0.2, release=0.04)
    return t


def pause():
    """Pause: a low blip."""
    t = Track(0.2)
    t.add_tone(0, 0.08, 330, triangle, 0.35, freq_end=220, release=0.05)
    return t


EFFECTS = {
    "coin.ogg": coin,
    "gem.ogg": gem,
    "item.ogg": item,
    "jump.ogg": jump,
    "land.ogg": land,
    "dash.ogg": dash,
    "bolt.ogg": bolt,
    "fizzle.ogg": fizzle,
    "pop.ogg": pop,
    "stomp.ogg": stomp,
    "hurt.ogg": hurt,
    "splash.ogg": splash,
    "drink.ogg": drink,
    "gate.ogg": gate,
    "locked.ogg": locked,
    "checkpoint.ogg": checkpoint,
    "goal.ogg": goal,
    "knockout.ogg": knockout,
    "move.ogg": move,
    "select.ogg": select,
    "pause.ogg": pause,
}

# Normalise every effect so their loudness matches.
for _name, _recipe in list(EFFECTS.items()):
    def _normalised(recipe=_recipe):
        track = recipe()
        track.normalize(0.85)
        return track
    EFFECTS[_name] = _normalised
