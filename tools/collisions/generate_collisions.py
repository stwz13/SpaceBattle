#!/usr/bin/env python3
"""Generate collision vectors for the SpaceBattle collision decision tree.

For two shapes A and B the script writes every vector (dx, dy, dvx, dvy) such that
the objects touch during one tick, where

    dx,  dy  = B.position - A.position
    dvx, dvy = B.velocity - A.velocity

Algorithm: the Minkowski difference D = A (-) B of the two cell sets gives every offset at
which the shapes overlap (the primitive GJK is built on). A moving pair collides when the path
of the offset, d + v*t for t in [0, 1], enters D; the path is walked with a supercover line
(a Bresenham variant that also visits both neighbours when it crosses a cell corner), once per
integer relative velocity v in [-vmax, vmax]^2.

Shapes are text masks ('#' = occupied cell, '.' = empty, lines starting with ';' are
comments) or PNG sprites (requires Pillow; pixels with enough opacity are occupied).

Examples:
    python generate_collisions.py Ship.shape Ship.shape --vmax 3 -o Ship-Ship.collisions
    python generate_collisions.py ship.png torpedo.png --scale 8 --show
    python generate_collisions.py ship.png torpedo.png --vmax 4 --scale 8 --flip-y -o Ship-Torpedo.collisions
    python generate_collisions.py ship.png ship.png --scale 8 --all-rotations -o Collisions/Ship-Ship
    python generate_collisions.py --self-test
"""
import argparse
import itertools
import sys
from pathlib import Path

Cell = tuple[int, int]


# ---------------------------------------------------------------- shapes

def read_text_shape(path: Path) -> list[Cell]:
    rows = [line.rstrip("\n") for line in path.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.startswith(";")]
    return [(x, y) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch == "#"]


def read_image_shape(path: Path, scale: int, alpha_threshold: int, rotation: int) -> list[Cell]:
    try:
        from PIL import Image
    except ImportError:
        sys.exit("Reading PNG needs Pillow: pip install pillow")
    image = Image.open(path).convert("RGBA")
    if rotation % 8:
        # 45 degrees per step, counter-clockwise; NEAREST keeps hard alpha edges
        image = image.rotate(45 * (rotation % 8), resample=Image.Resampling.NEAREST, expand=True)
    width, height = image.size
    pixels = image.load()
    cells = set()
    for y in range(height):
        for x in range(width):
            if pixels[x, y][3] >= alpha_threshold:
                cells.add((x // scale, y // scale))
    return sorted(cells)


def rotate_cells_90(cells: list[Cell], quarter_turns: int) -> list[Cell]:
    """Exact rotation of a text mask by 90 degrees counter-clockwise per quarter turn (y axis down)."""
    result = cells
    for _ in range(quarter_turns % 4):
        result = [(y, -x) for x, y in result]
    return result


def read_shape(path: Path, scale: int, alpha_threshold: int, rotation: int = 0) -> list[Cell]:
    if path.suffix.lower() == ".png":
        cells = read_image_shape(path, scale, alpha_threshold, rotation)
    else:
        if rotation % 2:
            sys.exit(f"{path}: 45-degree rotation needs a PNG sprite; text masks rotate by 90 degrees only")
        cells = rotate_cells_90(read_text_shape(path), (rotation % 8) // 2)
    if not cells:
        sys.exit(f"Shape {path} has no occupied cells (check --alpha and --scale)")
    return cells


def flip_y(cells: list[Cell]) -> list[Cell]:
    """Images grow downwards; use this when the game's y axis points up."""
    return [(x, -y) for x, y in cells]


def render(cells: list[Cell]) -> str:
    """ASCII picture of an anchored mask; 'O' marks the anchor (object position)."""
    xs = [x for x, _ in cells] + [0]
    ys = [y for _, y in cells] + [0]
    occupied = set(cells)
    rows = []
    for y in range(min(ys), max(ys) + 1):
        row = ""
        for x in range(min(xs), max(xs) + 1):
            if (x, y) == (0, 0):
                row += "O" if (x, y) in occupied else "+"
            else:
                row += "#" if (x, y) in occupied else "."
        rows.append(row)
    return "\n".join(rows)


def to_text_mask(cells: list[Cell]) -> str:
    ox = min(x for x, _ in cells)
    oy = min(y for _, y in cells)
    occupied = {(x - ox, y - oy) for x, y in cells}
    width = max(x for x, _ in occupied) + 1
    height = max(y for _, y in occupied) + 1
    return "\n".join("".join("#" if (x, y) in occupied else "." for x in range(width)) for y in range(height)) + "\n"


def anchor(cells: list[Cell], mode: str) -> list[Cell]:
    """Move the shape so that its anchor (the object's position) is at (0, 0)."""
    if mode == "topleft":
        ox = min(x for x, _ in cells)
        oy = min(y for _, y in cells)
    else:  # center of the bounding box, rounded down
        ox = (min(x for x, _ in cells) + max(x for x, _ in cells)) // 2
        oy = (min(y for _, y in cells) + max(y for _, y in cells)) // 2
    return [(x - ox, y - oy) for x, y in cells]


# ---------------------------------------------------------------- geometry

def minkowski_difference(a: list[Cell], b: list[Cell]) -> set[Cell]:
    """All offsets d = B.pos - A.pos at which some cell of A meets some cell of B."""
    return {(ax - bx, ay - by) for ax, ay in a for bx, by in b}


def supercover(x0: int, y0: int, x1: int, y1: int) -> list[Cell]:
    """Every grid cell the segment between the two cell centres passes through.

    Unlike plain Bresenham, when the segment goes exactly through a cell corner
    both side neighbours are included, so no touch is missed.
    """
    nx, ny = abs(x1 - x0), abs(y1 - y0)
    sx = (x1 > x0) - (x1 < x0)
    sy = (y1 > y0) - (y1 < y0)
    x, y = x0, y0
    cells = [(x, y)]
    ix = iy = 0
    while ix < nx or iy < ny:
        decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx
        if decision == 0:
            cells.append((x + sx, y))
            cells.append((x, y + sy))
            x, y = x + sx, y + sy
            ix, iy = ix + 1, iy + 1
        elif decision < 0:
            x += sx
            ix += 1
        else:
            y += sy
            iy += 1
        cells.append((x, y))
    return cells


def collision_vectors(d: set[Cell], vmax: int) -> set[tuple[int, int, int, int]]:
    """Sweep the Minkowski difference backwards along every relative velocity.

    d + v*t hits D for some t in [0, 1]  <=>  d lies on the segment from p to p - v
    for some cell p of D.
    """
    result = set()
    speeds = range(-vmax, vmax + 1)
    for dvx, dvy in itertools.product(speeds, speeds):
        for px, py in d:
            for ox, oy in supercover(px, py, px - dvx, py - dvy):
                result.add((ox, oy, dvx, dvy))
    return result


def collides_directly(d: set[Cell], dx: int, dy: int, dvx: int, dvy: int) -> bool:
    """Reference check used by the self-test: walk the path and look it up in D."""
    return any(cell in d for cell in supercover(dx, dy, dx + dvx, dy + dvy))


# ---------------------------------------------------------------- output

def write_vectors(path: Path, vectors, header: list[str]) -> None:
    lines = [f"# {h}" for h in header]
    lines.append("# dx dy dvx dvy")
    lines += [f"{dx} {dy} {dvx} {dvy}" for dx, dy, dvx, dvy in sorted(vectors)]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ---------------------------------------------------------------- self-test

def self_test() -> None:
    # 1. Supercover includes both neighbours at an exact corner.
    assert supercover(0, 0, 3, 1) == [(0, 0), (1, 0), (2, 0), (1, 1), (2, 1), (3, 1)], supercover(0, 0, 3, 1)
    assert supercover(2, 2, 2, 2) == [(2, 2)]

    # 2. Single cells: only paths that pass through (0, 0) collide.
    d = minkowski_difference([(0, 0)], [(0, 0)])
    vectors = collision_vectors(d, 2)
    assert (0, 0, 0, 0) in vectors
    assert (1, 0, -1, 0) in vectors          # moving onto A
    assert (2, 0, 1, 0) not in vectors       # moving away
    assert (-1, 0, 2, 0) in vectors          # passing through A within the tick

    # 3. Sweep result equals the brute-force check for a non-trivial pair.
    ship = anchor([(1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (3, 1), (1, 2), (2, 2)], "center")
    torpedo = anchor([(0, 0), (1, 0)], "center")
    d = minkowski_difference(ship, torpedo)
    vmax = 3
    swept = collision_vectors(d, vmax)
    xs = range(min(x for x, _ in d) - vmax, max(x for x, _ in d) + vmax + 1)
    ys = range(min(y for _, y in d) - vmax, max(y for _, y in d) + vmax + 1)
    vs = range(-vmax, vmax + 1)
    brute = {(dx, dy, dvx, dvy) for dx, dy, dvx, dvy in itertools.product(xs, ys, vs, vs)
             if collides_directly(d, dx, dy, dvx, dvy)}
    assert swept == brute, (len(swept), len(brute))

    # 4. Same shapes: the set is symmetric under (d, v) -> (-d, -v).
    d = minkowski_difference(ship, ship)
    same = collision_vectors(d, 2)
    assert all((-dx, -dy, -dvx, -dvy) in same for dx, dy, dvx, dvy in same)

    print(f"self-test passed ({len(swept)} vectors in the ship/torpedo check)")


# ---------------------------------------------------------------- main

def load(path: Path, args, rotation: int) -> list[Cell]:
    cells = read_shape(path, args.scale, args.alpha, rotation)
    if args.flip_y:
        cells = flip_y(cells)
    return anchor(cells, args.anchor)


def generate(a: list[Cell], b: list[Cell], vmax: int, output: Path, header: list[str]) -> int:
    vectors = collision_vectors(minkowski_difference(a, b), vmax)
    write_vectors(output, vectors, header)
    return len(vectors)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("shape_a", nargs="?", type=Path, help="shape of object A (.shape text mask or .png sprite)")
    parser.add_argument("shape_b", nargs="?", type=Path, help="shape of object B")
    parser.add_argument("--vmax", type=int, default=3, help="max absolute relative speed per axis (default 3)")
    parser.add_argument("--anchor", choices=["center", "topleft"], default="center",
                        help="which point of a shape is the object's position (default center)")
    parser.add_argument("--scale", type=int, default=1, help="PNG pixels per game cell (default 1)")
    parser.add_argument("--alpha", type=int, default=128, help="PNG alpha threshold 0-255 (default 128)")
    parser.add_argument("--flip-y", action="store_true", help="game y axis points up (images grow down)")
    parser.add_argument("--rotate-a", type=int, default=0, help="rotate A by N*45 degrees counter-clockwise")
    parser.add_argument("--rotate-b", type=int, default=0, help="rotate B by N*45 degrees counter-clockwise")
    parser.add_argument("--all-rotations", action="store_true",
                        help="write one file per pair of directions (8x8) into the -o directory")
    parser.add_argument("--show", action="store_true", help="print the masks and stop (to check scale/alpha)")
    parser.add_argument("--export-shapes", type=Path, metavar="DIR",
                        help="also save the masks as editable .shape files into DIR")
    parser.add_argument("-o", "--output", type=Path, help="output file, or directory with --all-rotations")
    parser.add_argument("--self-test", action="store_true", help="run built-in checks and exit")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return
    if not args.shape_a or not args.shape_b:
        parser.error("two shapes are required (or use --self-test)")

    pairs = [(ra, rb) for ra in range(8) for rb in range(8)] if args.all_rotations \
        else [(args.rotate_a, args.rotate_b)]

    if args.show or args.export_shapes:
        for path, rotation in dict.fromkeys([(args.shape_a, pairs[0][0]), (args.shape_b, pairs[0][1])]):
            cells = load(path, args, rotation)
            if args.show:
                width = max(x for x, _ in cells) - min(x for x, _ in cells) + 1
                height = max(y for _, y in cells) - min(y for _, y in cells) + 1
                print(f"{path.name} (rotation {rotation}): {len(cells)} cells, {width}x{height}")
                print(render(cells))
                print()
            if args.export_shapes:
                args.export_shapes.mkdir(parents=True, exist_ok=True)
                target = args.export_shapes / f"{path.stem}_r{rotation}.shape"
                target.write_text(f"; from {path.name}, rotation {rotation}\n" + to_text_mask(cells), encoding="utf-8")
                print(f"mask -> {target}")
        if args.show:
            return

    base = f"A={args.shape_a.name} B={args.shape_b.name} vmax={args.vmax} anchor={args.anchor}" \
           f" scale={args.scale} alpha={args.alpha} flip_y={args.flip_y}"
    if args.all_rotations:
        directory = args.output or Path(f"{args.shape_a.stem}-{args.shape_b.stem}")
        total = sum(
            generate(load(args.shape_a, args, ra), load(args.shape_b, args, rb), args.vmax,
                     directory / f"{ra}-{rb}.collisions",
                     [base + f" rotate_a={ra} rotate_b={rb}", "generated by generate_collisions.py, do not edit by hand"])
            for ra, rb in pairs)
        print(f"{len(pairs)} files, {total} vectors -> {directory}")
        return

    output = args.output or Path(f"{args.shape_a.stem}-{args.shape_b.stem}.collisions")
    ra, rb = pairs[0]
    count = generate(load(args.shape_a, args, ra), load(args.shape_b, args, rb), args.vmax, output,
                     [base + f" rotate_a={ra} rotate_b={rb}", "generated by generate_collisions.py, do not edit by hand"])
    print(f"{count} vectors -> {output}")


if __name__ == "__main__":
    main()
